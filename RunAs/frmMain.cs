using Newtonsoft.Json.Linq;
using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;
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
            labelCurrentUser.Text = String.Format("Current user: {0}", Environment.UserName + " - " + WindowsIdentity.GetCurrent().Name);
            if (File.Exists(credentialsPath))
            {
                try
                {
                    JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                    textBoxDomain.Text = getCredentials.SelectToken("domain").ToString();
                    textBoxUsername.Text = getCredentials.SelectToken("username").ToString();
                    textBoxPassword.Text = ss.Decrypt(getCredentials.SelectToken("password").ToString());
                }
                catch (Exception)
                {
                    textBoxDomain.Text = String.Empty;
                    textBoxUsername.Text = String.Empty;
                    textBoxPassword.Text = String.Empty;
                    File.Delete(credentialsPath);
                }
            }
            if (!UACHelper.UACHelper.IsElevated)
            {
                buttonRestartWithAdminRights.Enabled = false;
                //WinForm.ShieldifyButton(buttonRestartWithAdminRights);
            }
            if (Environment.UserName == "Clientadmin")
            {
                buttonStart.Enabled = false;
                textBoxDomain.Enabled = false;
                textBoxUsername.Enabled = false;
                textBoxPassword.Enabled = false;
                buttonRestartWithAdminRights.Enabled = true;
            }
        }

        public string credentialsPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments) + @"\Credentials.json";
        public string username;
        public string password;
        public string domain;

        private void buttonStart_Click(object sender, EventArgs e)
        {
            this.UseWaitCursor = true;
            buttonStart.Enabled = false;
            textBoxDomain.Enabled = false;
            textBoxUsername.Enabled = false;
            textBoxPassword.Enabled = false;


            JObject setCredentials = new JObject(
                new JProperty("domain", textBoxDomain.Text),
                new JProperty("username", textBoxUsername.Text),
                new JProperty("password", ss.Encrypt(textBoxPassword.Text)));

            File.WriteAllText(credentialsPath, setCredentials.ToString());

            JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
            domain = getCredentials.SelectToken("domain").ToString();
            username = getCredentials.SelectToken("username").ToString();
            password = ss.Decrypt(getCredentials.SelectToken("password").ToString());

            Task.Factory.StartNew(() =>
            {
                using (LogonUser(domain, username, password, LogonType.Service))
                {

                    Process p = new Process();

                    ProcessStartInfo ps = new ProcessStartInfo();

                    ps.FileName = Application.ExecutablePath;
                    ps.Domain = domain;
                    ps.UserName = username;
                    ps.Password = GetSecureString(password);
                    ps.LoadUserProfile = true;
                    ps.UseShellExecute = false;
                    p.StartInfo = ps;
                    if (p.Start())
                    {
                        Application.Exit();
                    }
                }
            });
        }

        private void buttonRestartWithAdminRights_Click(object sender, EventArgs e)
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
                Process p = new Process();
                ProcessStartInfo ps = new ProcessStartInfo();
                ps.Domain = domain;
                ps.UserName = username;
                ps.Password = GetSecureString(password);
                ps.FileName = path;
                ps.LoadUserProfile = true;
                ps.UseShellExecute = false;
                p.StartInfo = ps;
                p.Start();
            }
            else
            {

            }
        }
    }
}
