using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

// A windowless test process, NOT Unity or a distributable game. No OS input automation.
static class RestartProbe
{
    [STAThread]
    static int Main()
    {
        string executable = Process.GetCurrentProcess().MainModule.FileName;
        string signal = Environment.GetEnvironmentVariable("FC_RESTART_PROBE_SIGNAL");
        if (string.IsNullOrEmpty(signal)) return 2;
        if (executable.IndexOf("versions", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            File.WriteAllText(signal + ".launched", executable);
            return 0;
        }
        DateTime deadline = DateTime.UtcNow.AddMinutes(3);
        while (!File.Exists(signal + ".exit") && DateTime.UtcNow < deadline) Thread.Sleep(50);
        return 0;
    }
}
