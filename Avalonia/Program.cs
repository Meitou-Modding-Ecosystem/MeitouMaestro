using System.Runtime.CompilerServices;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Maestro.Core;

namespace Maestro.CrossPlatform;

internal sealed class MaestroApp : Application
{
    [STAThread]
    public static int Main(string[] args)
    {
        // WSLg and some Linux VMs expose working OpenGL through Mesa's CPU renderer.
        // A Skia-only framebuffer cannot host the fitting control.
        var x11 = new X11PlatformOptions { RenderingMode = [X11RenderingMode.Glx, X11RenderingMode.Egl, X11RenderingMode.Software] };
        x11.GlxRendererBlacklist = x11.GlxRendererBlacklist.Where(name => name != "llvmpipe").ToArray();
        return AppBuilder.Configure<MaestroApp>().UsePlatformDetect().With(x11).StartWithClassicDesktopLifetime(args);
    }

    public override void Initialize()
    {
        Styles.Add(new FluentTheme());
        RequestedThemeVariant = ThemeVariant.Dark;
    }
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new OpenProject(desktop, desktop.Args is ["--ui-trial", var file] ? file : null);
        base.OnFrameworkInitializationCompleted();
    }
}

internal static class Ui
{
    public static TextBlock Label(string caption, double size = 13) => new() {
        Text = caption, FontSize = size, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 2) };
    public static Button Button(string caption, Action run)
    {
        var button = new Button { Content = caption, HorizontalContentAlignment = HorizontalAlignment.Center };
        button.Click += (_, _) => run();
        return button;
    }
    public static void Cell(Grid grid, Control child, int row = 0, int col = 0)
    {
        Grid.SetRow(child, row); Grid.SetColumn(child, col); grid.Children.Add(child);
    }
    public static Grid PathRow(TextBox box, Button browse)
    {
        var row = new Grid { ColumnDefinitions = new("*,Auto"), ColumnSpacing = 6 };
        Cell(row, box); Cell(row, browse, col: 1); return row;
    }
}

internal sealed class OpenProject : Window
{
    readonly IClassicDesktopStyleApplicationLifetime desktop;
    readonly TextBox[] folders = [new(), new(), new()];
    readonly TextBlock status = Ui.Label("");
    readonly string? trial;
    readonly Button open;
    readonly string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MeitouMaestro", "avalonia-paths.json");

    public OpenProject(IClassicDesktopStyleApplicationLifetime desktop, string? trial)
    {
        this.desktop = desktop; this.trial = trial;
        Title = "Meitou Maestro · Open project"; Width = 700; Height = 485;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var panel = new StackPanel { Margin = new Thickness(32, 20), Spacing = 7 };
        Content = new ScrollViewer { Content = panel };
        panel.Children.Add(Ui.Label("Open or create a mod", 26));
        string[] initial = [];
        try
        {
            if (trial != null) initial = UiTrial.Read(trial).Folders;
            else if (File.Exists(settings)) initial = JsonSerializer.Deserialize<string[]>(File.ReadAllBytes(settings)) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException) { status.Text = ex.Message; }
        string[] labels = ["Kenshi installation", $"MeitouClient v{EngineReaders.SupportedVersion} game folder", "Writable mod folder"];
        for (int i = 0; i < folders.Length; i++)
        {
            var box = folders[i]; box.Text = initial.ElementAtOrDefault(i) ?? "";
            var label = labels[i]; panel.Children.Add(Ui.Label(label));
            var browse = new Button { Content = "Browse…" };
            browse.Click += async (_, _) =>
            {
                try
                {
                    var chosen = await StorageProvider.OpenFolderPickerAsync(new() { Title = label });
                    if (chosen.FirstOrDefault()?.TryGetLocalPath() is string path) box.Text = path;
                }
                catch (Exception ex) { status.Text = ex.Message; }
            };
            panel.Children.Add(Ui.PathRow(box, browse));
        }
        open = Ui.Button("Open project", Open); panel.Children.Add(open); panel.Children.Add(status);
        if (trial != null) Opened += (_, _) => UiTrial.Click(open);
    }

    async void Open()
    {
        open.IsEnabled = false; status.Text = "Reading records and framework descriptors…";
        try
        {
            var paths = folders.Select(b => string.IsNullOrWhiteSpace(b.Text) ? throw new IOException("Choose all three folders.") : Path.GetFullPath(b.Text.Trim())).ToArray();
            if (!File.Exists(Path.Combine(paths[0], "data", "gamedata.base"))) throw new IOException("Choose the Kenshi folder containing data/gamedata.base.");
            EngineReaders.Select(paths[1]);
            Directory.CreateDirectory(paths[2]);
            var library = await Task.Run(() => ReadLibrary(paths[0], paths[2]));
            var workbench = new Workbench(library);
            if (trial == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settings)!);
                File.WriteAllBytes(settings, JsonSerializer.SerializeToUtf8Bytes(paths));
            }
            desktop.MainWindow = workbench;
            workbench.Show(); Close();
            if (trial != null) await UiTrial.Run(desktop, workbench, trial);
        }
        catch (Exception ex)
        {
            status.Text = ex.GetBaseException().Message; open.IsEnabled = true;
            if (trial != null) { File.WriteAllText(trial + ".failure", ex.ToString()); desktop.Shutdown(1); }
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    internal static Library ReadLibrary(string install, string project) => new(install, project);
}
