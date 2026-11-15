using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Maestro.Core;

namespace Maestro.CrossPlatform;

// Opt-in replay of the same window and controls an author uses. The Linux runner drives the pointer and captures the X11 window.
internal sealed record Trial(string[] Folders, string Action, string[] Records, Dictionary<string,string> Inputs, string? Export, bool Capture = false);
internal static class UiTrial
{
    internal static Trial Read(string path) => JsonSerializer.Deserialize<Trial>(File.ReadAllBytes(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidDataException("Empty UI trial.");
    internal static void Click(Button button)
    {
        if (!button.IsEnabled) throw new InvalidOperationException($"{button.Content} is disabled.");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    static async Task Wait(Func<bool> ready, string step, int seconds = 30)
    {
        var timeout = System.Diagnostics.Stopwatch.StartNew();
        while (!ready())
        {
            if (timeout.Elapsed.TotalSeconds > seconds) throw new TimeoutException(step);
            await Task.Delay(100);
        }
    }
    internal static async Task Run(IClassicDesktopStyleApplicationLifetime desktop, Workbench w, string path)
    {
        var t = Read(path);
        static void Select(Workbench window, string id) =>
            window.Actions.SelectedItem = window.Actions.Items.Cast<Tool>().Single(tool => tool.Action.Id == id);
        Select(w, t.Action);
        foreach (var id in t.Records)
        {
            w.Search.Text = id;
            w.Records.SelectedItem = w.Records.Items.Cast<RecordItem>().Single(r => r.Id == id);
            Click(w.Add);
        }
        foreach (var (key, value) in t.Inputs) w.Set(key, value);
        if (t.Export == null)
        {
            Click(w.Review);
            if (!w.Save.IsEnabled) throw new InvalidDataException(w.Status);
            File.WriteAllText(path + ".pass", $"Opened {w.Actions.SelectedItem} and reviewed {t.Records.Length} record(s).");
            desktop.Shutdown(); return;
        }
        foreach (var item in w.Animals.Items.Cast<RecordItem>().Reverse().ToArray())
        {
            int before = w.View.Frames;
            w.Animals.SelectedItem = item; w.View.RequestNextFrameRendering();
            await Wait(() => w.View.Frames > before, $"No OpenGL frame for {item.Name}. {w.Status}");
        }
        Click(w.Review);
        if (!w.Save.IsEnabled) throw new InvalidDataException(w.Status);
        if (t.Capture)
        {
            var mid = w.View.TranslatePoint(new Point(w.View.Bounds.Width / 2, w.View.Bounds.Height / 2), w)!.Value;
            File.WriteAllText(path + ".ready", $"{(int)(mid.X * w.RenderScaling)} {(int)(mid.Y * w.RenderScaling)}");
            await Wait(() => File.Exists(path + ".continue"), "Waiting for window capture and orbit.", 150);
            if (w.View.Orbits < 1 || w.View.WheelZooms + w.View.KeyZooms < 1) throw new InvalidOperationException($"Viewport received {w.View.Orbits} orbit events, {w.View.WheelZooms} wheel zooms and {w.View.KeyZooms} key zooms.");
        }
        string graphics = $"{w.View.Renderer}; {w.View.Frames} fit frames; {w.View.Orbits} orbit events, {w.View.WheelZooms} wheel zooms and {w.View.KeyZooms} key zooms";
        Click(w.Save);
        if (!w.Status.StartsWith("Saved ")) throw new InvalidDataException(w.Status);
        var lib = await Task.Run(() => OpenProject.ReadLibrary(t.Folders[0], t.Folders[2]));
        var reopened = new Workbench(lib); desktop.MainWindow = reopened; reopened.Show(); w.Close();
        Select(reopened, t.Action);
        reopened.Fits.SelectedItem = reopened.Fits.Items.Cast<string>().Single();
        if (!reopened.Picks.Items.Cast<RecordItem>().Select(r => r.Id).SequenceEqual(t.Records))
            throw new InvalidDataException("Reopened targets changed.");
        Click(reopened.Review);
        if (!reopened.Save.IsEnabled) throw new InvalidDataException("Reopened fit: " + reopened.Status);
        reopened.TrialExport = t.Export; Click(reopened.Export);
        if (!reopened.Status.StartsWith("Exported ")) throw new InvalidDataException(reopened.Status);
        File.WriteAllText(path + ".pass", $"Reviewed, saved, reopened and exported {reopened.Library.ModName}. {graphics}");
        desktop.Shutdown();
    }
}
