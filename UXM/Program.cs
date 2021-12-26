using System;
using System.Windows.Forms;

namespace UXM
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            var settings = Properties.Settings.Default;
            if (settings.UpgradeRequired)
            {
                settings.Upgrade();
                settings.UpgradeRequired = false;
                settings.Save();
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FormMain());

            settings.Save();
        }
    }
}
