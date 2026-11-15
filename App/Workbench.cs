using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Microsoft.Win32;
using Maestro.Core;

namespace Maestro.Desktop;

internal sealed class Workbench : Window
{
    readonly Library lib;
    Editing editor;
    internal readonly ComboBox Tools = new(), Existing = new(), PreviewAnimal = new();
    internal readonly TextBox Search = StyleKit.Box("RecordFilter");
    internal readonly ListBox Results = new(), Picks = new();
    internal readonly Dictionary<string, Control> Fields = new();
    internal Button Add = null!, Review = null!, Save = null!, Export = null!;
    internal string? TrialExport;
    internal string Status => status.Text;
    readonly TextBlock status = StyleKit.Caption(""), hint = StyleKit.Caption("");
    readonly TextBox change = StyleKit.Box("ChangeReview");
    readonly StackPanel controls = new();
    readonly FitView view = new();
    readonly List<RecordItem> selection = [];
    string? lastMesh; Surface[]? saddle;
    Tool? tool => Tools.SelectedItem as Tool;
    bool updating;
    public Workbench(string install, string project)
    {
        lib = new Library(install, project); editor = new Editing(lib);
        Title = "Meitou Maestro · " + lib.ModName; Width = 1320; Height = 840; MinWidth = 900; MinHeight = 620;
        WindowStartupLocation = WindowStartupLocation.CenterScreen; StyleKit.Setup(this);
        var shell = new DockPanel { Margin = new Thickness(16) }; Content = shell;
        var head = new DockPanel { Margin = new Thickness(0, 0, 0, 12) }; DockPanel.SetDock(head, Dock.Top); shell.Children.Add(head);
        var name = StyleKit.Title("Meitou Maestro", 25); DockPanel.SetDock(name, Dock.Left); head.Children.Add(name);
        var file = StyleKit.Caption(lib.ModName, 15); file.HorizontalAlignment = HorizontalAlignment.Right; head.Children.Add(file);
        var foot = new DockPanel { Margin = new Thickness(0, 8, 0, 0) };
        DockPanel.SetDock(foot, Dock.Bottom); shell.Children.Add(foot);
        Export = StyleKit.Button("Export ZIP", () => Guard(ExportZip)); Save = StyleKit.Button("Save reviewed changes", () => Guard(Commit));
        Review = StyleKit.Button("Review changes", () => Guard(Prepare));
        foreach (var b in new[] { Export, Save, Review }) { DockPanel.SetDock(b, Dock.Right); foot.Children.Add(b); }
        foot.Children.Add(status);
        var columns = new Grid();
        foreach (var width in new[] { new GridLength(270), new GridLength(1, GridUnitType.Star), new GridLength(300) })
            columns.ColumnDefinitions.Add(new ColumnDefinition { Width = width });
        shell.Children.Add(columns);
        var list = new DockPanel { Margin = new Thickness(0, 0, 10, 0) }; columns.Children.Add(list);
        var top = new StackPanel(); DockPanel.SetDock(top, Dock.Top); list.Children.Add(top);
        top.Children.Add(StyleKit.Title("Actions", 17)); top.Children.Add(Tools);
        top.Children.Add(StyleKit.Caption("Find records by name or FCS ID")); top.Children.Add(Search);
        var bottom = new StackPanel(); DockPanel.SetDock(bottom, Dock.Bottom); list.Children.Add(bottom);
        Add = StyleKit.Button("Add selected record", () => Guard(AddRecord)); bottom.Children.Add(Add);
        bottom.Children.Add(StyleKit.Caption("Applied to")); Picks.Height = 75; bottom.Children.Add(Picks);
        bottom.Children.Add(StyleKit.Button("Remove selected record", () =>
        {
            if (Picks.SelectedItem is RecordItem r) { selection.Remove(r); RefreshSelection(); Changed(); }
        }));
        list.Children.Add(Results);
        var middle = new Grid(); Grid.SetColumn(middle, 1); columns.Children.Add(middle);
        middle.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        middle.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        middle.RowDefinitions.Add(new RowDefinition { Height = new GridLength(150) });
        middle.Children.Add(view); Grid.SetRow(hint, 1); middle.Children.Add(hint);
        change.IsReadOnly = true; change.VerticalContentAlignment = VerticalAlignment.Top;
        change.TextWrapping = TextWrapping.Wrap; change.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        Grid.SetRow(change, 2); middle.Children.Add(change);
        var right = new ScrollViewer { Content = controls, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(12, 0, 0, 0) };
        Grid.SetColumn(right, 2); columns.Children.Add(right);
        ToolTipService.SetToolTip(Results, "Record name, FCS ID and defining mod");
        Results.ItemTemplate = RowTemplate();
        Results.MouseDoubleClick += (_, _) => Guard(AddRecord);
        Search.TextChanged += (_, _) => Filter();
        Tools.SelectionChanged += (_, _) => Guard(ChooseAction);
        PreviewAnimal.SelectionChanged += (_, _) => { if (!updating) Redraw(); };
        Existing.SelectionChanged += (_, _) => { if (!updating && Existing.SelectedItem is string id) Guard(() => LoadFit(id)); };
        Tools.ItemsSource = lib.Actions;
        Tools.DisplayMemberPath = ""; Tools.SelectedIndex = lib.Actions.Count > 0 ? 0 : -1;
        if (lib.Diagnostics.Count > 0) change.Text = string.Join("\n", lib.Diagnostics);
        if (lib.Actions.Count == 0) status.Text = "Install a framework containing maestro.json, then reopen the project.";
        Save.IsEnabled = false;
    }
    static DataTemplate RowTemplate()
    {
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        foreach (var field in new[] { "Name", "Id", "Source" })
        {
            var text = new FrameworkElementFactory(typeof(TextBlock));
            text.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(field));
            text.SetValue(TextBlock.FontSizeProperty, field == "Name" ? 14d : 11d); stack.AppendChild(text);
        }
        return new DataTemplate { VisualTree = stack };
    }
    void Guard(Action action)
    {
        try { action(); } catch (Exception e) { status.Text = e.GetBaseException().Message; editor.Invalidate(); Save.IsEnabled = false; }
    }
    void ChooseAction()
    {
        updating = true; selection.Clear(); Fields.Clear(); controls.Children.Clear(); Existing.ItemsSource = null;
        lastMesh = null; saddle = null;
        if (tool == null) { updating = false; return; }
        controls.Children.Add(StyleKit.Title(tool.Action.Label, 17)); controls.Children.Add(StyleKit.Caption(tool.Name + " · " + tool.Source));
        if (tool.Action.Preview != null)
        {
            controls.Children.Add(StyleKit.Caption("Reopen saved fit")); controls.Children.Add(Existing);
            controls.Children.Add(StyleKit.Caption("Preview animal")); controls.Children.Add(PreviewAnimal);
        }
        foreach (var i in tool.Action.Inputs)
        {
            if (i.Kind == "selection") continue;
            controls.Children.Add(StyleKit.Caption(i.Label)); Control f;
            if (i.Kind == "boolean")
            {
                var box = new CheckBox { IsChecked = (bool?)i.Default ?? false }; box.Checked += (_, _) => Changed(); box.Unchecked += (_, _) => Changed(); f = box; controls.Children.Add(f);
            }
            else if (i.Kind == "bone")
            {
                var box = new ComboBox(); box.SelectionChanged += (_, _) => Changed(); f = box; controls.Children.Add(f);
            }
            else
            {
                var box = StyleKit.Box(i.Key, Display(i.Default)); box.TextChanged += (_, _) => Changed(); f = box;
                if (i.Kind == "asset")
                {
                    var row = new DockPanel(); var browse = StyleKit.Button("…", () => { var dlg = new OpenFileDialog { Title = i.Label }; if (dlg.ShowDialog() == true) box.Text = dlg.FileName; });
                    DockPanel.SetDock(browse, Dock.Right); row.Children.Add(browse); row.Children.Add(box); controls.Children.Add(row);
                }
                else controls.Children.Add(f);
            }
            AutomationProperties.SetAutomationId(f, i.Key); Fields[i.Key] = f;
        }
        if (tool.Action.Edits.FirstOrDefault(e => e.Str("op") == "jsonEntry") is { } edit)
        {
            if (editor.Owner(edit) is { } pack) Set(edit.Str("owner"), pack);
            Existing.ItemsSource = editor.Entries(edit).Select(e => (string)e[edit.Str("idField")]!).ToArray();
        }
        view.Visibility = tool.Action.Preview == null ? Visibility.Collapsed : Visibility.Visible;
        var area = (Grid)view.Parent;
        area.RowDefinitions[0].Height = tool.Action.Preview == null ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        area.RowDefinitions[2].Height = tool.Action.Preview == null ? new GridLength(1, GridUnitType.Star) : new GridLength(150);
        updating = false; Filter(); RefreshSelection(); Changed();
        if (lib.Diagnostics.Count > 0) change.Text = string.Join("\n", lib.Diagnostics);
    }
    void Filter()
    {
        var term = Search.Text.Trim();
        Results.ItemsSource = lib.Records.Where(r => r.Type == tool?.Action.RecordType &&
            (r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Id.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Source.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    void AddRecord()
    {
        if (Results.SelectedItem is not RecordItem r) return;
        if (tool?.Action.Inputs.Any(i => i.Kind == "selection") != true) selection.Clear();
        if (selection.All(s => s.Id != r.Id)) selection.Add(r);
        RefreshSelection(); Changed();
        if (tool != null && editor.ExistingReference(tool, r.Id) is bool b)
            Set(tool.Action.Edits.First(e => e.Str("op") == "reference").Str("value"), b.ToString());
    }
    void RefreshSelection()
    {
        updating = true;
        try
        {
            Picks.ItemsSource = selection.ToArray(); PreviewAnimal.ItemsSource = selection.ToArray(); PreviewAnimal.SelectedIndex = selection.Count - 1;
            var bone = tool?.Action.Inputs.FirstOrDefault(i => i.Kind == "bone");
            if (bone != null && Fields[bone.Key] is ComboBox box)
            {
                var prev = box.SelectedItem as string;
                IEnumerable<string> names = selection.Count == 0 ? [] : lib.Animal(selection[0].Id).Bones.Keys;
                foreach (var s in selection.Skip(1)) names = names.Intersect(lib.Animal(s.Id).Bones.Keys, StringComparer.Ordinal);
                var common = names.Order(StringComparer.Ordinal).ToArray(); box.ItemsSource = common; box.SelectedItem = prev;
            }
        }
        finally { updating = false; }
    }
    void LoadFit(string id)
    {
        if (tool == null) return;
        var edit = tool.Action.Edits.First(e => e.Str("op") == "jsonEntry");
        var entry = editor.Entries(edit).Single(o => (string?)o[edit.Str("idField")] == id);
        selection.Clear(); var mapped = edit["fields"]!.AsObject();
        if (edit["uniqueSelection"] is { } k && entry[k.GetValue<string>()] is JsonArray ids)
            foreach (var val in ids) selection.Add(lib.Records.Single(r => r.Id == (string?)val));
        RefreshSelection(); updating = true;
        foreach (var (field, bind) in mapped)
            if (Fields.ContainsKey(bind!.GetValue<string>())) Set(bind.GetValue<string>(), Display(entry[field]));
        updating = false; Changed();
    }
    static string Display(JsonNode? n) => n is JsonArray a ? string.Join(" ", a.Select(x => x!.ToJsonString())) : n?.ToString() ?? "";
    internal void Set(string k, string v)
    {
        switch (Fields[k]) { case TextBox b: b.Text = v; break; case ComboBox b: b.SelectedItem = v; break; case CheckBox b: b.IsChecked = bool.Parse(v); break; }
    }
    string Get(string k) => Fields[k] switch { TextBox b => b.Text, ComboBox b => b.SelectedItem as string ?? "", CheckBox b => (b.IsChecked == true).ToString(), _ => "" };
    void Changed() { if (updating) return; editor.Invalidate(); Save.IsEnabled = false; change.Clear(); Redraw(); }
    void Redraw()
    {
        var p = tool?.Action.Preview;
        if (p == null || PreviewAnimal.SelectedItem is not RecordItem r)
        {
            view.Draw(null, null, null, default, default, default, 0);
            hint.Text = p == null ? (selection.Count == 0 ? "Select a record to edit." : selection[0].Name) : "Select an animal to inspect its rig.";
            return;
        }
        try
        {
            Vector3 V(string key) { var v = Editing.Vector(Get(p.Str(key))); return new((float)v[0]!, (float)v[1]!, (float)v[2]!); }
            var file = Get(p.Str("mesh")); if (file != lastMesh)
            {
                saddle = string.IsNullOrWhiteSpace(file) ? null : Library.Geometry(Path.IsPathFullyQualified(file) ? file : SafeFiles.Target(lib.Project, file));
                lastMesh = file;
            }
            view.Draw(lib.Animal(r.Id), Get(p.Str("anchor")), saddle, V("offset"), V("rotation"), V("seat"), Editing.Number(Get(p.Str("facing"))));
            hint.Text = "Drag to orbit · Wheel to zoom · Blue bone / gold seat and facing"; status.Text = r.Name + " · rest pose";
        }
        catch (Exception ex) { status.Text = ex.GetBaseException().Message; }
    }
    void Prepare() { if (tool == null) return; change.Text = editor.Review(tool, selection, Fields.Keys.ToDictionary(k => k, Get)); Save.IsEnabled = true; status.Text = "Review the changes before saving."; }
    void Commit() { editor.Save(); editor = new Editing(lib); ChooseAction(); status.Text = "Saved " + lib.ModName; }
    void ExportZip()
    {
        string? dest = TrialExport;
        if (dest == null) { var dlg = new SaveFileDialog { FileName = Path.ChangeExtension(lib.ModName, "zip"), Filter = "ZIP archive|*.zip" }; if (dlg.ShowDialog() != true) return; dest = dlg.FileName; }
        ProjectFiles.Export(lib.Project, dest, lib.Actions); status.Text = "Exported " + Path.GetFileName(dest);
    }
}
