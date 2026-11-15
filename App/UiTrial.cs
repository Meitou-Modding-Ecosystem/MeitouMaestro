using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Maestro.Core;

namespace Maestro.Desktop;

internal sealed record TrialRecipe(string[] Folders, string Action, string[] Records, Dictionary<string, string> Inputs, string Export, string Screenshot);
internal static class UiTrial
{
    internal static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };
    internal static void Fail(string file, Exception ex) { File.WriteAllText(file + ".failure", ex.ToString()); Application.Current.Shutdown(1); }
    internal static async void Run(Workbench w, string file)
    {
        try
        {
            var r = JsonSerializer.Deserialize<TrialRecipe>(File.ReadAllText(file), Options)!;
            await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            w.Tools.SelectedItem = w.Tools.Items.Cast<Tool>().Single(t => t.Action.Id == r.Action);
            foreach (var id in r.Records)
            {
                w.Search.Text = id; w.Results.SelectedItem = w.Results.Items.Cast<RecordItem>().Single(i => i.Id == id);
                w.Add.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            }
            foreach (var field in r.Inputs) w.Set(field.Key, field.Value);
            w.Review.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); if (!w.Save.IsEnabled) throw new InvalidOperationException(w.Status);
            w.UpdateLayout();
            await w.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            var image = new RenderTargetBitmap((int)w.ActualWidth, (int)w.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            image.Render(w); var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = File.Create(r.Screenshot)) encoder.Save(stream);
            w.Save.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); if (!w.Status.StartsWith("Saved ")) throw new InvalidOperationException(w.Status);
            w.TrialExport = r.Export; w.Export.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            if (!w.Status.StartsWith("Exported ")) throw new InvalidOperationException(w.Status);
            Application.Current.Shutdown();
        }
        catch (Exception ex) { Fail(file, ex); }
    }
}
