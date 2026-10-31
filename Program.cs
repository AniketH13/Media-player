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
            if (args.Length > 0 && args[0] == "--test-subtitles")
            {
                RunSubtitleTests();
                return;
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
                    "Media Player Startup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Ignore secondary failure
            }
        }
    }

    private static void RunSubtitleTests()
    {
        var tempSrt = Path.GetTempFileName() + ".srt";
        var tempVtt = Path.GetTempFileName() + ".vtt";
        var tempAss = Path.GetTempFileName() + ".ass";

        try
        {
            File.WriteAllText(tempSrt,
                "1\r\n00:00:01,000 --> 00:00:03,500\r\nHello <i>world</i>!\r\n\r\n2\r\n00:00:04,200 --> 00:00:07,800\r\nSecond <b>line</b>\r\n");
            File.WriteAllText(tempVtt,
                "WEBVTT\r\n\r\n00:01.000 --> 00:03.500 align:middle\r\nHello WebVTT!\r\n\r\n00:04.200 --> 00:07.800\r\nSecond line\r\n");
            File.WriteAllText(tempAss,
                "[Events]\r\nFormat: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text\r\nDialogue: 0,0:00:01.00,0:00:03.50,Default,,0,0,0,,Hello ASS!\\NSecond line\r\n");

            var srtCues = SubtitleParser.Parse(tempSrt);
            var vttCues = SubtitleParser.Parse(tempVtt);
            var assCues = SubtitleParser.Parse(tempAss);

            if (srtCues.Count != 2) throw new Exception($"SRT count expected 2, got {srtCues.Count}");
            if (srtCues[0].Text != "Hello world!") throw new Exception($"SRT[0] text expected 'Hello world!', got '{srtCues[0].Text}'");
            if (srtCues[0].Start != TimeSpan.FromSeconds(1) || srtCues[0].End != TimeSpan.FromMilliseconds(3500))
                throw new Exception($"SRT[0] time mismatch: {srtCues[0].Start} - {srtCues[0].End}");

            if (vttCues.Count != 2) throw new Exception($"VTT count expected 2, got {vttCues.Count}");
            if (vttCues[0].Text != "Hello WebVTT!") throw new Exception($"VTT[0] text expected 'Hello WebVTT!', got '{vttCues[0].Text}'");

            if (assCues.Count != 1) throw new Exception($"ASS count expected 1, got {assCues.Count}");
            if (!assCues[0].Text.Contains("Hello ASS!")) throw new Exception($"ASS[0] text mismatch: '{assCues[0].Text}'");

            Console.WriteLine("SUBTITLE_TESTS_PASSED");
        }
        finally
        {
            if (File.Exists(tempSrt)) File.Delete(tempSrt);
            if (File.Exists(tempVtt)) File.Delete(tempVtt);
            if (File.Exists(tempAss)) File.Delete(tempAss);
        }
    }
}
