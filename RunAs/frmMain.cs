using Newtonsoft.Json.Linq;
using Onova;
using Onova.Services;
using SimpleImpersonation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using static RunAs.Helper;

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
            this.Text = this.Text + " - " + Assembly.GetExecutingAssembly().GetName().Version;
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


        // https://social.msdn.microsoft.com/Forums/vstudio/en-US/2af1d146-0f9c-4760-b8f0-812dace836dc/wait-for-long-running-operation-before-next-operation-without-ui-freeze?forum=wpf
        // long running operation
        private async void buttonStart_Click(object sender, EventArgs e)
        {
            try
            {
                ControlStateSwitch(true, false, buttonRestartWithAdminRights.Enabled, false, false, false);

                if (String.IsNullOrWhiteSpace(comboBoxDomain.Text) || String.IsNullOrWhiteSpace(comboBoxUsername.Text) || String.IsNullOrWhiteSpace(textBoxPassword.Text))
                {
                    throw new ArgumentNullException();
                }

                JObject setCredentials = new JObject(
                    new JProperty("domain", comboBoxDomain.Text),
                    new JProperty("username", comboBoxUsername.Text),
                    new JProperty("password", ss.Encrypt(textBoxPassword.Text)));

                await Task.Factory.StartNew(() =>
                {
                    File.WriteAllText(credentialsPath, setCredentials.ToString());
                    JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                    domain = getCredentials.SelectToken("domain").ToString();
                    username = getCredentials.SelectToken("username").ToString();
                    password = ss.Decrypt(getCredentials.SelectToken("password").ToString());

                });

                await Task.Factory.StartNew(() =>
                {
                    string userPath = GetUserDirectoryPath();
                    bool hasAccess = false;

                    var credentials = new UserCredentials(domain, username, password);
                    SimpleImpersonation.Impersonation.RunAsUser(credentials, SimpleImpersonation.LogonType.Interactive, () =>
                    {
                        using (WindowsIdentity.GetCurrent().Impersonate())
                        {
                            if (HasFolderRights(userPath, FileSystemRights.FullControl, WindowsIdentity.GetCurrent()))
                            {
                                hasAccess = true;
                            }
                            else
                            {
                                hasAccess = false;
                            }
                        }
                    });

                    if (!hasAccess)
                    {
                        AddDirectorySecurity(userPath, String.Format(@"{0}\{1}", domain, username), FileSystemRights.FullControl, AccessControlType.Allow);
                    }
                });

                await Task.Factory.StartNew(() =>
                {
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
                });
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
                MessageBox.Show(win32ex.Message + " Code: " + win32ex.NativeErrorCode, win32ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Message: \n" + ex.Message + "\n\n" + "Source: \n" + ex.Source + "\n\n" + "Stack: \n" + ex.StackTrace, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                ControlStateSwitch(false, true, buttonRestartWithAdminRights.Enabled, true, true, true);
            }
        }

        private void buttonRestartWithAdminRights_Click(object sender, EventArgs e)
        {
            try
            {
                ControlStateSwitch(true, false, false, false, false, false);

                JObject getCredentials = JObject.Parse(File.ReadAllText(credentialsPath));
                domain = getCredentials.SelectToken("domain").ToString();
                username = getCredentials.SelectToken("username").ToString();
                password = ss.Decrypt(getCredentials.SelectToken("password").ToString());

                string path = string.Empty;

                //ConfigureWindowsRegistry();
                //UpdateGroupPolicy();
                ///Mapped drives are not available from an elevated prompt 
                ///when UAC is configured to "Prompt for credentials" in Windows
                ///https://support.microsoft.com/en-us/help/3035277/mapped-drives-are-not-available-from-an-elevated-prompt-when-uac-is-co#detail%20to%20configure%20the%20registry%20entry
                ///https://stackoverflow.com/a/25908932/11189474
                OpenFileDialog fileDialog = new OpenFileDialog();
                fileDialog.Filter = "Application (*.exe)|*.exe";
                fileDialog.Title = "Select a application";
                fileDialog.DereferenceLinks = true;
                fileDialog.Multiselect = true;
                DialogResult result = fileDialog.ShowDialog();
                if (result == DialogResult.OK || result == DialogResult.Yes)
                {
                    path = fileDialog.FileName;

                    Task.Factory.StartNew(() =>
                    {
                        UACHelper.UACHelper.StartElevated(new ProcessStartInfo(path));

                        //Process p = new Process();

                        //ProcessStartInfo ps = new ProcessStartInfo();

                        //ps.FileName = path;
                        //ps.Domain = domain;
                        //ps.UserName = username;
                        //ps.Password = GetSecureString(password);
                        //ps.LoadUserProfile = true;
                        //ps.CreateNoWindow = true;
                        //ps.UseShellExecute = false;

                        //p.StartInfo = ps;
                        //p.Start();
                    });
                }
                else
                {

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                ControlStateSwitch(false, true, true, true, true, true);
            }
        }


        #region Added CtrlBackspaceSupport to textbox with password char 
        private void textBoxPassword_KeyDown(object sender, KeyEventArgs e)
        {
            TextBox box = (TextBox)sender;
            if (e.KeyData == (Keys.Back | Keys.Control))
            {
                if (!box.ReadOnly && box.SelectionLength == 0)
                {
                    RemoveWord(box);
                }
                e.SuppressKeyPress = true;
            }
        }

        private void RemoveWord(TextBox box)
        {
            string text = Regex.Replace(box.Text.Substring(0, box.SelectionStart), @"(^\W)?\w*\W*$", "");
            box.Text = text + box.Text.Substring(box.SelectionStart);
            box.SelectionStart = text.Length;
        }
        #endregion

        #region Controle state
        private void ControlStateSwitch(bool Use_Wait_Cursor_State, bool Button_Start_State, bool Button_RestartWithAdminRights_State, bool ComboBox_Domain_State, bool ComboBox_Username_State, bool TextBox_Password_State)
        {
            this.UseWaitCursor = Use_Wait_Cursor_State;
            buttonStart.Enabled = Button_Start_State;
            buttonRestartWithAdminRights.Enabled = Button_RestartWithAdminRights_State;
            comboBoxDomain.Enabled = ComboBox_Domain_State;
            comboBoxUsername.Enabled = ComboBox_Username_State;
            textBoxPassword.Enabled = TextBox_Password_State;
            this.Refresh();
        }
        #endregion

        private async void frmMain_Shown(object sender, EventArgs e)
        {
            string user = "Hendrik-Koelbel";
            string project = "RunAsAdmin";
            string assetName = "RunAs.zip";
            try
            {
                // Configure to look for packages in specified directory and treat them as zips
                using (var manager = new UpdateManager(new GithubPackageResolver(user, project, assetName),new ZipPackageExtractor()))
                {
                    // Check for updates
                    var result = await manager.CheckForUpdatesAsync();
                    if (result.CanUpdate)
                    {
                        DialogResult dialog = MessageBox.Show(String.Format("A new version is available.\nold version: {0}\nnew version: {1}\nDo you want to update the version?", Assembly.GetExecutingAssembly().GetName().Version, result.LastVersion), "New update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                        if (dialog == DialogResult.Yes)
                        {
                            // Prepare an update by downloading and extracting the package
                            // (supports progress reporting and cancellation)
                            await manager.PrepareUpdateAsync(result.LastVersion);

                            // Launch an executable that will apply the update
                            // (can be instructed to restart the application afterwards)
                            manager.LaunchUpdater(result.LastVersion);

                            // Terminate the running application so that the updater can overwrite files
                            Environment.Exit(0);
                        }
                        else
                        {

                        }
                    }
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
