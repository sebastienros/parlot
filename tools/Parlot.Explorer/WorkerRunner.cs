using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Parlot.Explorer;

internal static class WorkerRunner
{
    private static readonly SemaphoreSlim Gate = new(1);

    internal static string Resolve(string path)
    {
        path = Path.GetFullPath(path);
        if (!File.Exists(path) || !path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Select a local .dll assembly.");
        return path;
    }

    internal static string Revision(string path)
    {
        var directory = Path.GetDirectoryName(Resolve(path))!;
        var builder = new StringBuilder();
        foreach (var file in Files(directory).Order(StringComparer.Ordinal))
        {
            var info = new FileInfo(file);
            builder.Append(file).Append(info.Length).Append(info.LastWriteTimeUtc.Ticks);
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    private static IEnumerable<string> Files(string directory)
    {
        var count = 0;
        foreach (var path in Directory.EnumerateFiles(directory, "*", new EnumerationOptions
            { RecurseSubdirectories = true, AttributesToSkip = FileAttributes.ReparsePoint, IgnoreInaccessible = false }))
        {
            if (++count > 2000) throw new IOException("Assembly output directory exceeds 2,000 files. Use a dedicated build output folder.");
            yield return path;
        }
    }

    internal static async Task<JsonElement> Run(Request request, CancellationToken cancellationToken)
    {
        var path = Resolve(request.Path);
        if (request.Input.Length > 128_000) throw new InvalidOperationException("Input limit is 128,000 UTF-16 code units.");
        await Gate.WaitAsync(cancellationToken);
        var temporary = Path.Combine(Path.GetTempPath(), "parlot-explorer-" + Guid.NewGuid().ToString("N"));
        try
        {
            var before = Revision(path);
            var snapshot = Path.Combine(temporary, "assembly");
            Directory.CreateDirectory(snapshot);
            var directory = Path.GetDirectoryName(path)!;
            long total = 0;
            var count = 0;
            foreach (var file in Files(directory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (++count > 2000 || (total += new FileInfo(file).Length) > 512L * 1024 * 1024)
                    throw new InvalidOperationException("Assembly output directory exceeds the snapshot limit (2,000 files / 512 MB). Use a dedicated build output folder.");
                var destination = Path.Combine(snapshot, Path.GetRelativePath(directory, file));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(file, destination);
            }
            if (before != Revision(path)) throw new IOException("Assembly output changed while copying. Retry after the build finishes.");
            var response = Path.Combine(temporary, "response.json");
            var start = new ProcessStartInfo(Environment.ProcessPath!)
            {
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, WorkingDirectory = snapshot, CreateNoWindow = true
            };
            if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
                start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            start.ArgumentList.Add("--worker");
            start.ArgumentList.Add(response);
            using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start parser worker.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(10));
            using var kill = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
            var stdout = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
            var stderr = process.StandardError.BaseStream.CopyToAsync(Stream.Null, CancellationToken.None);
            try
            {
                await process.StandardInput.WriteAsync(JsonSerializer.Serialize(request with { Path = Path.Combine(snapshot, Path.GetFileName(path)) }, Protocol.Json).AsMemory(), timeout.Token);
                process.StandardInput.Close();
                await process.WaitForExitAsync(timeout.Token);
                await Task.WhenAll(stdout, stderr).WaitAsync(timeout.Token);
                if (!File.Exists(response)) throw new InvalidOperationException($"Parser worker exited without a capture (exit {process.ExitCode}).");
                using var document = JsonDocument.Parse(await File.ReadAllTextAsync(response, timeout.Token));
                if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("workerError", out var error)) throw new InvalidOperationException(error.GetString());
                return document.RootElement.Clone();
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { throw new TimeoutException("Parser worker exceeded 10 seconds and was stopped. The previous capture is preserved."); }
        }
        finally
        {
            try { Directory.Delete(temporary, recursive: true); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            Gate.Release();
        }
    }
}
