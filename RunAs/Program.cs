using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RunAs
{
    static class Program
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Ereignis - Handler für UI-Threads:
			Application.ThreadException +=
                new System.Threading.ThreadExceptionEventHandler(
                    Application_ThreadException);

            // Alle unbehandelten WinForms-Fehler durch diesen Ereignis-Handler 
            // zwingen (unabhängig von config-Einstellungen):
            Application.SetUnhandledExceptionMode(
                UnhandledExceptionMode.CatchException);

            // Ereignis-Hanlder für nicht UI-Threads:
            AppDomain.CurrentDomain.UnhandledException +=
                new UnhandledExceptionEventHandler(
                    CurrentDomain_UnhandledException);

            // Dieser try-catch-Block ist im Prinzip nicht nötig.
            // Hier ist er aber trotzdem damit die beiden Ereignisse
            // an diesen weiterleiten können und der anschießende
            // finally-Block zur Speicherbereinigung ausgeführt
            // werden kann
            try
            {
                // Anwenung ausführen:
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new frmMain());
            }
            catch (Exception ex)
            {
                MessageBox.Show("Message: \n" + ex.Message /* + "\n\n" + "Source: \n" + ex.Source + "\n\n" + "Stack: \n" + ex.StackTrace + "\n\n" + "Data: \n" + ex.Data*/, ex.GetType().Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                // Da es statische Ereignisse sind wird zur Speicherschonung
                // der Handler entfernt:
                AppDomain.CurrentDomain.UnhandledException -=
                    CurrentDomain_UnhandledException;
                Application.ThreadException -=
                    Application_ThreadException;
            }
        }
        //---------------------------------------------------------------------
        /// <summary>
        /// Ereignisbehandlung für Thread-Exceptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            // Weiterleiten der Exception:
            throw e.Exception;
        }
        //---------------------------------------------------------------------
        /// <summary>
        /// Ereignisbehandlung für alle anderen unbehandelten Exceptions
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Weiterleiten der Exception:
            throw (Exception)e.ExceptionObject;
        }
    }
}
