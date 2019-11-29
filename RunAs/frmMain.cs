using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RunAs.Helper;
using static RunAs.Impersonation;

namespace RunAs
{
    public partial class frmMain : Form
    {
        SimpleSecurity ss = new SimpleSecurity();
        //Examples 
        //SimpleSecurity ss = new SimpleSecurity();
        //txtEncryptedText.Text = ss.Encrypt(txtTextToEncrypt.Text);
        //SimpleSecurity ss = new SimpleSecurity();
        //txtOriginalText.Text = ss.Decrypt(txtEncryptedText.Text);

        public frmMain()
        {
            InitializeComponent();
            labelCurrentUser.Text = String.Format("Current user: {0} " +
                    "\nDefault Behavior: {1} " +
                    "\nIs Elevated: {2}" +
                    "\nIs Administrator: {3}" +
                    "\nIs Desktop Owner: {4}" +
                    "\nProcess Owner: {5}" +
                    "\nDesktop Owner: {6}",
                    Environment.UserName + " - " + WindowsIdentity.GetCurrent().Name,
                    UACHelper.UACHelper.GetExpectedRunLevel(Assembly.GetExecutingAssembly().Location).ToString(),
                    UACHelper.UACHelper.IsElevated.ToString(),
                    UACHelper.UACHelper.IsAdministrator.ToString(),
                    UACHelper.UACHelper.IsDesktopOwner.ToString(),
                    WindowsIdentity.GetCurrent().Name ?? "SYSTEM",
                    UACHelper.UACHelper.DesktopOwner.ToString());

            SetDataSource(comboBoxDomain, GetAllDomains().ToArray());
            SetDataSource(comboBoxUsername, GetAllUsers().ToArray());

            Placeholder(comboBoxDomain, "Domain");
            Placeholder(comboBoxUsername, "Username");
            Placeholder(textBoxPassword, "Password");
            buttonStart.Focus();

            if (File.Exists(credentialsPath))
            {
                try
                {
                    JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                    comboBoxDomain.Text = getCredentials.SelectToken("domain").ToString();
                    comboBoxUsername.Text = getCredentials.SelectToken("username").ToString();
                    textBoxPassword.Text = ss.Decrypt(getCredentials.SelectToken("password").ToString());
                }
                catch (Exception)
                {
                    comboBoxDomain.Text = String.Empty;
                    comboBoxUsername.Text = String.Empty;
                    textBoxPassword.Text = String.Empty;
                    if (File.Exists(credentialsPath))
                    {
                        File.Delete(credentialsPath);
                    }
                }
            }
            //if (UACHelper.UACHelper.IsAdministrator)
            //{
            //    buttonStart.Enabled = false;
            //    comboBoxDomain.Enabled = false;
            //    comboBoxUsername.Enabled = false;
            //    textBoxPassword.Enabled = false;
            //    buttonRestartWithAdminRights.Enabled = true;
            //}
            if (!UACHelper.UACHelper.IsAdministrator)
            {
                buttonRestartWithAdminRights.Enabled = false;
                UACHelper.WinForm.ShieldifyButton(buttonRestartWithAdminRights);
            }

            SetForegroundWindow(Handle.ToInt32());
        }

        public string credentialsPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\Credentials.json";
        public string executableFile = Assembly.GetExecutingAssembly().Location;
        public string username;
        public string password;
        public string domain;

        private void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                this.UseWaitCursor = true;
                buttonStart.Enabled = false;
                comboBoxDomain.Enabled = false;
                comboBoxUsername.Enabled = false;
                textBoxPassword.Enabled = false;


                JObject setCredentials = new JObject(
                    new JProperty("domain", comboBoxDomain.Text),
                    new JProperty("username", comboBoxUsername.Text),
                    new JProperty("password", ss.Encrypt(textBoxPassword.Text)));

                File.WriteAllText(credentialsPath, setCredentials.ToString());

                JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                domain = getCredentials.SelectToken("domain").ToString();
                username = getCredentials.SelectToken("username").ToString();
                password = ss.Decrypt(getCredentials.SelectToken("password").ToString());

                using (LogonUser(domain, username, password, LogonType.Service))
                {
                    using (WindowsIdentity.GetCurrent().Impersonate())
                    {
                        if (String.IsNullOrWhiteSpace(comboBoxDomain.Text) || String.IsNullOrWhiteSpace(comboBoxUsername.Text) || String.IsNullOrWhiteSpace(textBoxPassword.Text))
                        {
                            throw new ArgumentNullException();
                        }

                        Process p = new Process();

                        ProcessStartInfo ps = new ProcessStartInfo();

                        ps.FileName = executableFile;
                        ps.Domain = domain;
                        ps.UserName = username;
                        ps.Password = GetSecureString(password);
                        ps.LoadUserProfile = true;
                        ps.CreateNoWindow = true;
                        ps.UseShellExecute = false;

                        p.StartInfo = ps;
                        if (p.Start())
                        {
                            Application.Exit();
                        }
                    }
                }

            }
            catch (ArgumentNullException nullex)
            {
                var emptyControlsType = new List<string>();
                var emptyControlsName = new List<string>();
                var emptyControlsResult = new List<string>();
                var controls = new List<Control> { comboBoxDomain, comboBoxUsername, textBoxPassword };
                foreach (Control control in controls)
                {
                    if (String.IsNullOrEmpty(control.Text.Trim()))
                    {
                        string typ = control.GetType().Name.ToLower();
                        emptyControlsType.Add(UppercaseFirst(typ));
                        string name = control.Name.ToLower();
                        string result = name.Replace(typ, "");
                        emptyControlsName.Add(UppercaseFirst(result));
                        emptyControlsResult.Add(UppercaseFirst(typ) + ": " + UppercaseFirst(result));
                    }

                }
                MessageBox.Show(String.Format("{0}\n\n{1}", nullex.Message, string.Join("\n", emptyControlsResult)), nullex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Win32Exception win32ex)
            {
                MessageBox.Show(win32ex.Message, win32ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {

                MessageBox.Show(ex.GetHashCode().ToString() + "\n\n" + ex.HResult + "\n\n" + "Message: \n" + ex.Message + "\n\n" + "Source: \n" + ex.Source + "\n\n" + "Stack: \n" + ex.StackTrace + "\n\n" + "Data: \n" + ex.Data + "\n\n" + "InnerException: \n" + ex.InnerException, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.UseWaitCursor = false;
                buttonStart.Enabled = true;
                comboBoxDomain.Enabled = true;
                comboBoxUsername.Enabled = true;
                textBoxPassword.Enabled = true;
            }
        }

        private void buttonRestartWithAdminRights_Click(object sender, EventArgs e)
        {

            try
            {
                JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                domain = getCredentials.SelectToken("domain").ToString();
                username = getCredentials.SelectToken("username").ToString();
                password = ss.Decrypt(getCredentials.SelectToken("password").ToString());

                string path = string.Empty;

                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.Filter = "Application (*.exe)|*.exe";
                if (fileDialog.ShowDialog() == DialogResult.OK)
                {
                    path = fileDialog.FileName;

                    Task.Factory.StartNew(() =>
                    {
                        using (LogonUser(domain, username, password, LogonType.Service))
                        {
                            UACHelper.UACHelper.StartElevated(new ProcessStartInfo(path));
                        }
                    });
                }
                else
                {

                }
            }
            catch (Exception)
            {

            }
        }
    }
}
