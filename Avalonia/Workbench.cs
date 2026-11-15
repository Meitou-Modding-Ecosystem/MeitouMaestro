using System.Numerics;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Maestro.Core;

namespace Maestro.CrossPlatform;

internal sealed class Workbench : Window
{
    internal readonly Library Library;
    Editing editor;
    internal readonly ComboBox Actions = new(), Fits = new(), Animals = new();
    internal readonly ListBox Records = new(), Picks = new();
    internal readonly TextBox Search = new();
    internal readonly FitView View = new();
    internal readonly Dictionary<string, Control> Fields = [];
    internal readonly Button Add, Review, Save, Export;
    internal string? TrialExport;
    internal string Status => status.Text ?? "";
    readonly TextBlock status = Ui.Label(""), hint = Ui.Label("");
    readonly TextBox diff = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap };
    readonly StackPanel inputs = new() { Spacing = 3 };
    readonly List<RecordItem> selected = [];
    readonly Grid middle;
    Tool? Tool => Actions.SelectedItem as Tool;
    bool updating;
    string? meshPath;
    Surface[]? mesh;

    internal Workbench(Library library)
    {
        Library = library; editor = new(library);
        Title = "Meitou Maestro · " + Library.ModName;
        Width = 1230; Height = 775; MinWidth = 850; MinHeight = 550;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var page = new Grid { Margin = new Thickness(16, 10, 16, 12), RowDefinitions = new("Auto,*,Auto"), RowSpacing = 10 };
        Content = page;
        var heading = new Grid { ColumnDefinitions = new("*,Auto") };
        Ui.Cell(page, heading); Ui.Cell(heading, Ui.Label("Meitou Maestro", 24));
        Ui.Cell(heading, Ui.Label(Library.ModName, 17), col: 1);
        var body = new Grid { ColumnDefinitions = new("244,*,278"), ColumnSpacing = 14 };
        Ui.Cell(page, body, 1);
        var left = new Grid { RowDefinitions = new("Auto,Auto,Auto,Auto,*,Auto,Auto,76,Auto"), RowSpacing = 3 };
        Ui.Cell(body, left);
        Ui.Cell(left, Ui.Label("Framework action")); Actions.HorizontalAlignment = HorizontalAlignment.Stretch;
        Ui.Cell(left, Actions, 1);
        Ui.Cell(left, Ui.Label("Find a record"), 2); Search.PlaceholderText = "Name, FCS ID or source mod"; Ui.Cell(left, Search, 3);
        Records.ItemTemplate = new FuncDataTemplate<RecordItem>((record, _) => {
            var row = new StackPanel();
            if (record == null) return row;
            row.Children.Add(new TextBlock { Text = record.Name, TextWrapping = TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = record.Id, FontSize = 11, Opacity = .75 });
            row.Children.Add(new TextBlock { Text = record.Source, FontSize = 11, Opacity = .75 });
            return row;
        });
        Ui.Cell(left, Records, 4); Add = Ui.Button("Add selected record", () => Guard(AddSelected)); Ui.Cell(left, Add, 5);
        Ui.Cell(left, Ui.Label("Applied to"), 6); Ui.Cell(left, Picks, 7);
        Ui.Cell(left, Ui.Button("Remove selected record", () => Guard(() => {
            if (Picks.SelectedItem is RecordItem item) { selected.Remove(item); UpdateSelection(); Changed(); }
        })), 8);
        middle = new Grid { RowDefinitions = new("*,Auto,155"), RowSpacing = 5 };
        Ui.Cell(body, middle, col: 1); Ui.Cell(middle, View.WithPointerSurface()); Ui.Cell(middle, hint, 1); Ui.Cell(middle, diff, 2);
        Ui.Cell(body, new ScrollViewer { Content = inputs, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, col: 2);
        var footer = new Grid { ColumnDefinitions = new("*,Auto,Auto,Auto"), ColumnSpacing = 8 };
        Ui.Cell(page, footer, 2); Ui.Cell(footer, status);
        Review = Ui.Button("Review changes", () => Guard(Prepare));
        Save = Ui.Button("Save reviewed changes", () => Guard(Commit));
        Export = Ui.Button("Export ZIP", ExportZip);
        Ui.Cell(footer, Review, col: 1); Ui.Cell(footer, Save, col: 2); Ui.Cell(footer, Export, col: 3);
        Records.DoubleTapped += (_, _) => Guard(AddSelected);
        Search.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Filter(); };
        Actions.SelectionChanged += (_, _) => Guard(ChooseAction);
        Animals.SelectionChanged += (_, _) => { if (!updating) Redraw(); };
        Fits.SelectionChanged += (_, _) => { if (!updating && Fits.SelectedItem is string id) Guard(() => LoadFit(id)); };
        View.Error += message => status.Text = message;
        Actions.ItemsSource = Library.Actions;
        Actions.SelectedIndex = Library.Actions.Count > 0 ? 0 : -1;
        if (Library.Actions.Count == 0) status.Text = "Install a framework containing maestro.json and reopen the project.";
        if (Library.Diagnostics.Count > 0) diff.Text = string.Join("\n", Library.Diagnostics);
        Save.IsEnabled = false;
        Export.IsEnabled = File.Exists(Library.FcsPath);
    }

    void Guard(Action action)
    {
        try { action(); }
        catch (Exception ex) { status.Text = ex.GetBaseException().Message; editor.Invalidate(); Save.IsEnabled = false; }
    }

    void ChooseAction()
    {
        if (Tool is not { } tool) return;
        updating = true;
        try
        {
            selected.Clear(); Fields.Clear(); inputs.Children.Clear(); Fits.ItemsSource = null; meshPath = null; mesh = null;
            inputs.Children.Add(Ui.Label(tool.Action.Label, 19));
            inputs.Children.Add(Ui.Label(tool.Source));
            var show = tool.Action.Preview != null;
            View.IsVisible = show;
            middle.RowDefinitions[0].Height = show ? GridLength.Star : new GridLength(0);
            middle.RowDefinitions[2].Height = show ? new GridLength(155) : GridLength.Star;
            if (show)
            {
                inputs.Children.Add(Ui.Label("Reopen saved fit")); inputs.Children.Add(Fits);
                inputs.Children.Add(Ui.Label("Preview animal")); inputs.Children.Add(Animals);
                Fits.HorizontalAlignment = Animals.HorizontalAlignment = HorizontalAlignment.Stretch;
            }
            foreach (var field in tool.Action.Inputs.Where(i => i.Kind != "selection"))
            {
                inputs.Children.Add(Ui.Label(field.Label));
                if (field.Kind == "boolean")
                {
                    var check = new CheckBox { IsChecked = (bool?)field.Default ?? false };
                    check.IsCheckedChanged += (_, _) => Changed();
                    Fields.Add(field.Key, check); inputs.Children.Add(check);
                }
                else if (field.Kind == "bone")
                {
                    var box = new ComboBox { HorizontalAlignment = HorizontalAlignment.Stretch };
                    box.SelectionChanged += (_, _) => Changed();
                    Fields.Add(field.Key, box); inputs.Children.Add(box);
                }
                else
                {
                    var box = new TextBox { Text = Display(field.Default), MinHeight = 29 };
                    Fields.Add(field.Key, box);
                    box.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Changed(); };
                    if (field.Kind != "asset") { inputs.Children.Add(box); continue; }
                    var browse = new Button { Content = "…" };
                    browse.Click += async (_, _) => {
                        try
                        {
                            var chosen = await StorageProvider.OpenFilePickerAsync(new() { Title = field.Label });
                            if (chosen.FirstOrDefault()?.TryGetLocalPath() is string file) box.Text = file;
                        }
                        catch (Exception ex) { status.Text = ex.Message; }
                    };
                    inputs.Children.Add(Ui.PathRow(box, browse));
                }
            }
            if (tool.Action.Edits.FirstOrDefault(e => e.Str("op") == "jsonEntry") is { } edit)
            {
                if (editor.Owner(edit) is { } owner) Set(edit.Str("owner"), owner);
                Fits.ItemsSource = editor.Entries(edit).Select(e => (string)e[edit.Str("idField")]!).ToArray();
            }
        }
        finally { updating = false; }
        Filter(); UpdateSelection(); Changed();
    }
    void Filter()
    {
        string term = Search.Text?.Trim() ?? "";
        Records.ItemsSource = Library.Records.Where(r => r.Type == Tool?.Action.RecordType &&
            (r.Name.Contains(term, StringComparison.OrdinalIgnoreCase) || r.Id.Contains(term, StringComparison.OrdinalIgnoreCase)
            || r.Source.Contains(term, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    void AddSelected()
    {
        if (Tool is not { } tool || Records.SelectedItem is not RecordItem item) return;
        if (!tool.Action.Inputs.Any(i => i.Kind == "selection")) selected.Clear();
        if (!selected.Contains(item)) selected.Add(item);
        UpdateSelection(); Changed();
        if (editor.ExistingReference(tool, item.Id) is bool value)
            Set(tool.Action.Edits.First(e => e.Str("op") == "reference").Str("value"), value.ToString());
    }
    void UpdateSelection()
    {
        updating = true;
        try
        {
            var rows = selected.ToArray();
            Picks.ItemsSource = rows;
            Animals.ItemsSource = rows; Animals.SelectedIndex = rows.Length - 1;
            if (Tool?.Action.Inputs.FirstOrDefault(i => i.Kind == "bone") is { } field && Fields[field.Key] is ComboBox box)
            {
                var previous = box.SelectedItem;
                IEnumerable<string> bones = rows.Length == 0 ? [] : Library.Animal(rows[0].Id).Bones.Keys;
                foreach (var r in rows.Skip(1)) bones = bones.Intersect(Library.Animal(r.Id).Bones.Keys, StringComparer.Ordinal);
                box.ItemsSource = bones.Order(StringComparer.Ordinal).ToArray();
                box.SelectedItem = previous;
            }
        }
        finally { updating = false; }
    }
    void LoadFit(string id)
    {
        var edit = Tool!.Action.Edits.First(e => e.Str("op") == "jsonEntry");
        var entry = editor.Entries(edit).Single(e => (string?)e[edit.Str("idField")] == id);
        selected.Clear();
        if (edit["uniqueSelection"] is { } key && entry[(string)key!] is JsonArray ids)
            foreach (var v in ids) selected.Add(Library.Records.Single(r => r.Id == (string?)v));
        UpdateSelection();
        updating = true;
        try
        {
            foreach (var (name, binding) in edit["fields"]!.AsObject())
                if (Fields.ContainsKey((string)binding!)) Set((string)binding!, Display(entry[name]));
        }
        finally { updating = false; }
        Changed();
    }
    static string Display(JsonNode? node) => node is JsonArray a ? string.Join(" ", a.Select(v => v!.ToJsonString())) : node?.ToString() ?? "";
    internal void Set(string key, string value)
    {
        switch (Fields[key])
        {
            case TextBox box: box.Text = value; break;
            case ComboBox box: box.SelectedItem = value; break;
            case CheckBox box: box.IsChecked = bool.Parse(value); break;
        }
    }
    internal string Get(string key) => Fields[key] switch {
        TextBox t => t.Text ?? "", ComboBox c => c.SelectedItem as string ?? "", CheckBox b => (b.IsChecked == true).ToString(), _ => "" };

    void Changed()
    {
        if (updating) return;
        editor.Invalidate(); Save.IsEnabled = false; diff.Text = ""; Redraw();
    }
    void Redraw()
    {
        if (Tool?.Action.Preview is not { } p || Animals.SelectedItem is not RecordItem r)
        {
            View.Draw(null, null, null, default, default, default, 0);
            hint.Text = ""; status.Text = selected.Count == 0 ? "Select a record." : selected[0].Name; return;
        }
        try
        {
            Vector3 Vector(string key) { var a = Editing.Vector(Get(p.Str(key))); return new((float)a[0]!, (float)a[1]!, (float)a[2]!); }
            var file = Get(p.Str("mesh"));
            if (file != meshPath)
            {
                mesh = string.IsNullOrWhiteSpace(file) ? null : Library.Geometry(Path.IsPathFullyQualified(file) ? file : SafeFiles.Target(Library.Project, file));
                meshPath = file;
            }
            View.Draw(Library.Animal(r.Id), Get(p.Str("anchor")), mesh, Vector("offset"), Vector("rotation"), Vector("seat"), Editing.Number(Get(p.Str("facing"))));
            hint.Text = "Drag to orbit · Wheel or +/− to zoom · Blue bone · Gold seat and facing"; status.Text = r.Name + " · rest pose";
        }
        catch (Exception ex) { status.Text = ex.GetBaseException().Message; }
    }
    void Prepare()
    {
        if (Tool is not { } tool) return;
        diff.Text = editor.Review(tool, selected, Fields.Keys.ToDictionary(k => k, Get));
        Save.IsEnabled = true; status.Text = "Review the changes before saving.";
    }
    void Commit()
    {
        editor.Save(); editor = new(Library); ChooseAction(); Export.IsEnabled = true;
        status.Text = "Saved " + Library.ModName;
    }
    async void ExportZip()
    {
        try
        {
            string? path = TrialExport;
            if (path == null)
            {
                var chosen = await StorageProvider.SaveFilePickerAsync(new() { Title = "Export saved mod", SuggestedFileName = Path.ChangeExtension(Library.ModName, ".zip"), DefaultExtension = "zip" });
                path = chosen?.TryGetLocalPath();
            }
            if (path == null) return;
            ProjectFiles.Export(Library.Project, path, Library.Actions); status.Text = "Exported " + Path.GetFileName(path);
        }
        catch (Exception ex) { status.Text = ex.GetBaseException().Message; }
    }
}
