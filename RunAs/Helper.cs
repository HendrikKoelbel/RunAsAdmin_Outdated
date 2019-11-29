using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.DirectoryServices.ActiveDirectory;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RunAs
{
    public static class Helper
    {
        #region Window to front
        [DllImport("User32.dll")]
        public static extern int SetForegroundWindow(int hWnd);
        #endregion

        #region  Bind textbox custom source
        public static void SetDataSource(ComboBox comboBox, params string[] stringArray)
        {
            if (stringArray != null)
            {
                Array.Sort(stringArray);
                AutoCompleteStringCollection col = new AutoCompleteStringCollection();
                foreach (var item in stringArray)
                {
                    col.Add(item);
                }
                comboBox.DataSource = col;
                comboBox.AutoCompleteCustomSource = col;
            }
            else
            {
                return;
            }
        }
        #endregion

        #region Get all Domains as string list
        public static List<string> GetAllDomains()
        {
            using (var forest = Forest.GetCurrentForest())
            {
                var domainList = new List<string>();
                domainList.Add(Environment.MachineName);
                foreach (Domain domain in forest.Domains)
                {
                    domainList.Add(domain.Name);
                    domain.Dispose();
                }

                return domainList;
            }
        }
        #endregion

        #region Placeholder
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SendMessage(IntPtr hWnd, int msg, int wParam, [MarshalAs(UnmanagedType.LPWStr)] string lParam);

        [DllImport("user32.dll")]
        private static extern bool GetComboBoxInfo(IntPtr hwnd, ref COMBOBOXINFO pcbi);
        [StructLayout(LayoutKind.Sequential)]

        private struct COMBOBOXINFO
        {
            public int cbSize;
            public RECT rcItem;
            public RECT rcButton;
            public UInt32 stateButton;
            public IntPtr hwndCombo;
            public IntPtr hwndItem;
            public IntPtr hwndList;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        public static void Placeholder(Control control, string placeholder)
        {
            if (control is ComboBox)
            {
                COMBOBOXINFO info = GetComboBoxInfo(control);
                SendMessage(info.hwndItem, EM_SETCUEBANNER, 0, placeholder);
            }
            else
            {
                SendMessage(control.Handle, EM_SETCUEBANNER, 0, placeholder);
            }
        }

        private static COMBOBOXINFO GetComboBoxInfo(Control control)
        {
            COMBOBOXINFO info = new COMBOBOXINFO();
            //a combobox is made up of three controls, a button, a list and textbox;
            //we want the textbox
            info.cbSize = Marshal.SizeOf(info);
            GetComboBoxInfo(control.Handle, ref info);
            return info;
        }
        #endregion

        #region Get all local users as string list
        private const int UF_ACCOUNTDISABLE = 0x0002;
        public static List<string> GetLocalUsers()
        {
            var path = string.Format("WinNT://{0},computer", Environment.MachineName);
            using (var computerEntry = new DirectoryEntry(path))
            {
                var users = new List<string>();
                foreach (DirectoryEntry childEntry in computerEntry.Children)
                {
                    if (childEntry.SchemaClassName == "User")// filter all users
                    {
                        if (((int)childEntry.Properties["UserFlags"].Value & UF_ACCOUNTDISABLE) != UF_ACCOUNTDISABLE)// only if accounts are enabled
                        {
                            users.Add(childEntry.Name); // add active user to list
                        }
                    }
                }
                return users;
            }
        }
        #endregion

        #region Get all ad users as string list
        public static List<string> GetADUsers()
        {
            using (var forest = Forest.GetCurrentForest())
            {
                var domainList = new List<string>();
                var ADUsers = new List<string>();
                foreach (Domain domain in forest.Domains)
                {
                    using (var context = new PrincipalContext(ContextType.Domain, domain.Name))
                    {
                        using (var searcher = new PrincipalSearcher(new UserPrincipal(context)))
                        {
                            foreach (var result in searcher.FindAll())
                            {
                                DirectoryEntry de = result.GetUnderlyingObject() as DirectoryEntry;
                                ADUsers.Add(de.Properties["samAccountName"].Value.ToString());
                                //Console.WriteLine("First Name: " + de.Properties["givenName"].Value);
                                //Console.WriteLine("Last Name : " + de.Properties["sn"].Value);
                                //Console.WriteLine("SAM account name   : " + de.Properties["samAccountName"].Value);
                                //Console.WriteLine("User principal name: " + de.Properties["userPrincipalName"].Value);
                            }
                        }
                    }
                    domain.Dispose();
                }
                return ADUsers;
            }
        } 
        #endregion

        #region Get all users (AD + Local)
        public static List<string> GetAllUsers()
        {
            var allUsers = new List<string>();
            try
            {
                foreach (var user in GetLocalUsers())
                {
                    allUsers.Add(user);
                }
                foreach (var user in GetADUsers())
                {
                    allUsers.Add(user);
                }
                return allUsers;
            }
            catch (Exception)
            {
                return allUsers;
            }
        } 
        #endregion

        #region Uppercase first letter
        public static string UppercaseFirst(string str)
        {
            if (string.IsNullOrEmpty(str))
                return string.Empty;
            return char.ToUpper(str[0]) + str.Substring(1).ToLower();
        }
        #endregion
    }
}
