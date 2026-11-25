using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Maestro.Desktop;

internal static class Program
{
    internal static string Settings => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeitouMaestro", "paths.json");
    [STAThread]
    public static int Main(string[] args)
    {
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        return app.Run(new Welcome(args));
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static void Enter(string game, string install, string project, string[] args)
    {
        Maestro.Core.EngineReaders.Select(game);
        OpenEditor(install, project, args);
    }
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void OpenEditor(string install, string project, string[] args)
    {
        var next = new Workbench(install, project);
        var previous = Application.Current.MainWindow;
        Application.Current.MainWindow = next;
        next.Show(); previous.Close();
        if (args is ["--ui-trial", var recipe]) UiTrial.Run(next, recipe);
    }
}

internal sealed class Welcome : Window
{
    public Welcome(string[] args)
    {
        Title = "Meitou Maestro · Open project"; Width = 680; Height = 510; WindowStartupLocation = WindowStartupLocation.CenterScreen;
        StyleKit.Setup(this);
        var page = new StackPanel { Margin = new Thickness(32, 22, 32, 22) };
        Content = page;
        page.Children.Add(StyleKit.Title("Meitou Maestro", 30));
        page.Children.Add(StyleKit.Caption("Open or create a mod", 18));
        string[] input = ["", "", ""];
        try { if (File.Exists(Program.Settings)) input = JsonSerializer.Deserialize<string[]>(File.ReadAllText(Program.Settings)) ?? input; } catch (IOException) { } catch (JsonException) { }
        string? script = args is ["--ui-trial", var path] ? path : null;
        if (script != null) input = JsonDocument.Parse(File.ReadAllBytes(script)).RootElement.GetProperty("folders").EnumerateArray().Select(v => v.GetString()!).ToArray();
        var labels = new[] { "Kenshi installation", $"MeitouClient v{Maestro.Core.EngineReaders.SupportedVersion} directory", "Writable mod folder" };
        var boxes = new List<TextBox>();
        for (int i = 0; i < labels.Length; i++)
        {
            page.Children.Add(StyleKit.Caption(labels[i]));
            var row = new DockPanel(); page.Children.Add(row);
            var box = StyleKit.Box("Folder" + i, i < input.Length ? input[i] : ""); boxes.Add(box);
            var browse = StyleKit.Button("Browse", () => { var dialog = new OpenFolderDialog { Title = "Choose a directory" }; if (dialog.ShowDialog() == true) box.Text = dialog.FolderName; });
            DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); row.Children.Add(box);
        }
        var error = StyleKit.Caption(""); error.TextWrapping = TextWrapping.Wrap;
        var open = StyleKit.Button("Open project", () =>
        {
            try
            {
                var paths = boxes.Select(b => Path.GetFullPath(b.Text)).ToArray();
                if (!File.Exists(Path.Combine(paths[0], "data", "gamedata.base"))) throw new IOException("Choose the Kenshi installation containing data/gamedata.base.");
                if (!Directory.Exists(paths[2])) Directory.CreateDirectory(paths[2]);
                if (script == null) { Directory.CreateDirectory(Path.GetDirectoryName(Program.Settings)!); File.WriteAllText(Program.Settings, JsonSerializer.Serialize(paths)); }
                Program.Enter(paths[1], paths[0], paths[2], args);
            }
            catch (Exception ex) { error.Text = ex.Message; if (script != null) UiTrial.Fail(script, ex); }
        });
        open.Margin = new Thickness(0, 18, 0, 2); page.Children.Add(open); page.Children.Add(error);
        if (script != null) Loaded += (_, _) => Dispatcher.BeginInvoke(() => open.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent)));
    }
}
