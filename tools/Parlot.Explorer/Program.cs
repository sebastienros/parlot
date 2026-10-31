using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Parlot.Explorer;

if (args is ["--worker", var responsePath])
{
    var input = await Console.In.ReadToEndAsync();
    Console.SetOut(TextWriter.Null);
    Console.SetError(TextWriter.Null);
    object response;
    try { response = Worker.Execute(JsonSerializer.Deserialize<Request>(input, Protocol.Json)!); }
    catch (Exception exception) { response = new { workerError = exception.ToString() }; }
    await File.WriteAllTextAsync(responsePath, JsonSerializer.Serialize(response, Protocol.Json));
    return;
}
if (args.Contains("--help"))
{
    Console.WriteLine("parlot-explorer [assembly.dll] [--browser | --no-browser]\nOpens a standalone desktop window. --browser opens your browser; --no-browser only starts the server. Enable Diagnostics = true on GenerateParser factories.\nOnly load assemblies you trust: parser code executes with your local user permissions.");
    return;
}
if (args.Contains("--browser") && args.Contains("--no-browser"))
{
    Console.Error.WriteLine("Choose either --browser or --no-browser.");
    Environment.ExitCode = 1;
    return;
}
var initialPath = args.FirstOrDefault(static argument => !argument.StartsWith("--", StringComparison.Ordinal));
if (initialPath is not null) initialPath = WorkerRunner.Resolve(initialPath);
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], WebRootPath = Path.Combine(AppContext.BaseDirectory, "wwwroot") });
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.WebHost.ConfigureKestrel(static server => server.Listen(IPAddress.Loopback, 0));
builder.Services.ConfigureHttpJsonOptions(static options => options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);
var app = builder.Build();
var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
app.Use(async (context, next) =>
{
    // Reject foreign Host/Origin values as well as unauthenticated API calls (including DNS rebinding).
    if (context.Request.Host.Host != "127.0.0.1" ||
        (context.Request.Headers.Origin.Count > 0 && context.Request.Headers.Origin != $"http://{context.Request.Host}"))
    { context.Response.StatusCode = 403; return; }
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; worker-src 'self' blob:; style-src 'self' 'unsafe-inline'; font-src 'self' data:; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'";
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Headers["X-Parlot-Token"] != token)
    { context.Response.StatusCode = 401; return; }
    try { await next(context); }
    catch (Exception exception) when (exception is not OperationCanceledException)
    {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = exception.Message });
    }
});
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapGet("/api/session", () => new { initialPath, directory = initialPath is null ? Environment.CurrentDirectory : Path.GetDirectoryName(initialPath) });
async Task<JsonElement> RunWorker(Request request, CancellationToken cancellation)
{
    using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, app.Lifetime.ApplicationStopping);
    return await WorkerRunner.Run(request, linked.Token);
}
app.MapPost("/api/catalog", (Request request, CancellationToken cancellation) => RunWorker(request with { Parser = null }, cancellation));
app.MapPost("/api/capture", (Request request, CancellationToken cancellation) => RunWorker(request, cancellation));
app.MapPost("/api/revision", (Request request) => new { revision = WorkerRunner.Revision(request.Path) });
app.MapGet("/api/files", (string? path) =>
{
    var directory = Path.GetFullPath(path ?? Environment.CurrentDirectory);
    var options = new EnumerationOptions { IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint };
    return new
    {
        directory, parent = Directory.GetParent(directory)?.FullName,
        entries = Directory.EnumerateFileSystemEntries(directory, "*", options)
            .Where(static item => Directory.Exists(item) || item.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(Directory.Exists).ThenBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Take(500)
            .Select(static item => new { path = item, name = Path.GetFileName(item), directory = Directory.Exists(item) }).ToArray()
    };
});
await app.StartAsync();
var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
var url = address + "/#" + token;
Console.WriteLine($"\nParlot Explorer: {url}\nPress Ctrl+C to stop.\n");
try
{
    if (args.Contains("--no-browser")) await app.WaitForShutdownAsync();
    else if (args.Contains("--browser"))
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        await app.WaitForShutdownAsync();
    }
    else
    {
        Environment.ExitCode = await DesktopShell.Run(url, app.Lifetime.ApplicationStopping);
        if (Environment.ExitCode != 0) Console.Error.WriteLine("The desktop window could not stay open. Check the platform webview requirements, or use --browser.");
    }
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    Environment.ExitCode = 1;
}
finally
{
    app.Lifetime.StopApplication();
    await app.StopAsync();
    await app.DisposeAsync();
}
