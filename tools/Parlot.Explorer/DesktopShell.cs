using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Parlot.Explorer;

internal static class DesktopShell
{
    internal static async Task<int> Run(string url, CancellationToken stopping)
    {
        var platform = OperatingSystem.IsWindows() ? "win" : OperatingSystem.IsMacOS() ? "osx" : "linux";
        var rid = platform + "-" + RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
        var directory = Path.Combine(AppContext.BaseDirectory, "desktop", rid);
        var executable = OperatingSystem.IsMacOS()
            ? Path.Combine(directory, "Parlot Explorer.app", "Contents", "MacOS", "parlot-explorer-desktop")
            : Path.Combine(directory, OperatingSystem.IsWindows() ? "parlot-explorer-desktop.exe" : "parlot-explorer-desktop");
        if (!File.Exists(executable))
            throw new InvalidOperationException($"The desktop shell for {rid} is missing. Install a complete Parlot.Explorer package, or use --browser explicitly.");
        // NuGet extraction does not preserve executable mode for packaged native assets.
        if (!OperatingSystem.IsWindows())
        {
            var mode = File.GetUnixFileMode(executable);
            if ((mode & UnixFileMode.UserExecute) == 0) File.SetUnixFileMode(executable, mode | UnixFileMode.UserExecute);
        }
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardInput = true };
        start.ArgumentList.Add(url);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start the desktop shell. Use --browser to open the browser UI.");
        try
        {
            await process.WaitForExitAsync(stopping);
            return process.ExitCode;
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested) { return 0; }
        finally
        {
            // EOF asks the shell to quit; it also receives EOF if this host is killed unexpectedly.
            process.StandardInput.Close();
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await process.WaitForExitAsync(deadline.Token); }
            catch (OperationCanceledException)
            {
                if (!process.HasExited) process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }
}
