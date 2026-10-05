using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Parlot.Explorer.Windows;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        if (args.Length != 1 || !Uri.TryCreate(args[0], UriKind.Absolute, out var uri) || uri.Scheme != "http" || uri.Host != "127.0.0.1") return;
        ApplicationConfiguration.Initialize();
        using var form = new Form { Text = "Parlot Explorer", Width = 1440, Height = 960, MinimumSize = new Size(850, 650), StartPosition = FormStartPosition.CenterScreen };
        using var web = new WebView2 { Dock = DockStyle.Fill };
        form.Controls.Add(web);
        form.Shown += async (_, _) =>
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Parlot", "Explorer", "WebView2");
                var environment = await CoreWebView2Environment.CreateAsync(userDataFolder: folder);
                await web.EnsureCoreWebView2Async(environment);
                web.CoreWebView2.NavigationStarting += (_, navigation) =>
                {
                    if (!Uri.TryCreate(navigation.Uri, UriKind.Absolute, out var destination) || destination.GetLeftPart(UriPartial.Authority) != uri.GetLeftPart(UriPartial.Authority)) navigation.Cancel = true;
                };
                web.CoreWebView2.NewWindowRequested += (_, request) => request.Handled = true;
                web.CoreWebView2.Navigate(uri.AbsoluteUri);
            }
            catch (Exception exception)
            {
                MessageBox.Show(form, "WebView2 could not start. Open the explorer URL in your browser.\n\n" + exception.Message, "Parlot Explorer");
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
                form.Close();
            }
        };
        Application.Run(form);
    }
}
