using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;

namespace Maestro.Desktop;

internal static class StyleKit
{
    internal static Brush Ink => Brush("#233238");
    internal static Brush Paper => Brush("#f3f1e9");
    internal static Brush Accent => Brush("#17645c");
    internal static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    internal static void Setup(Window w)
    {
        w.FontFamily = new FontFamily("Inter, Segoe UI");
        w.FontSize = 13; w.Background = Paper; w.Foreground = Ink;
    }
    internal static TextBlock Title(string t, double size) => new() { Text = t, FontSize = size, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 6) };
    internal static TextBlock Caption(string t, double size = 13) => new() { Text = t, FontSize = size, Margin = new Thickness(0, 9, 0, 5), TextWrapping = TextWrapping.Wrap };
    internal static TextBox Box(string id, string text = "")
    {
        var b = new TextBox { Text = text, Padding = new Thickness(7, 5, 7, 5), VerticalContentAlignment = VerticalAlignment.Center, BorderBrush = Brush("#a3aaa3") };
        AutomationProperties.SetAutomationId(b, id); return b;
    }
    internal static Button Button(string label, Action onClick)
    {
        var b = new Button { Content = label, Background = Accent, Foreground = Brushes.White, BorderThickness = new Thickness(0), Padding = new Thickness(12, 6, 12, 6), Margin = new Thickness(2, 2, 2, 2) };
        b.Click += (_, _) => onClick(); return b;
    }
}
