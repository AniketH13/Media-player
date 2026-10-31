using System;
using System.IO;
using System.Windows.Forms;

namespace MediaPlayer;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main(string[] args)
    {
        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(args));
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show($"Application encountered an error on startup:\n\n{ex.Message}\n\nFull details written to crash.log",
                    "Media Player Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Ignore secondary failure
            }
        }
    }
}
