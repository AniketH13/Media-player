using System;
using System.IO;
using System.Windows.Forms;

namespace Frame;

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
            // Stage 1: Initialize native FFmpeg engine foundation
            try
            {
                Unosquare.FFME.Library.FFmpegDirectory = AppContext.BaseDirectory;
                Unosquare.FFME.Library.LoadFFmpeg();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FFmpeg initialization notice: {ex.Message}");
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new Form1(args));
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText("crash.log", ex.ToString());
                MessageBox.Show($"Application encountered an error on startup:\n\n{ex.Message}\n\nFull details written to crash.log",
                    "Frame Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Ignore secondary failure
            }
        }
    }
}
