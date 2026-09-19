using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Galpones.Core.Input;
using Galpones.Desktop.ViewModels;
using Microsoft.Win32;

namespace Galpones.Desktop;

public partial class MainWindow : Window
{
    public ProjectEditorViewModel Editor { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = Editor;
    }

    private void Design_Click(object sender, RoutedEventArgs e) => WorkspaceTabs.SelectedIndex = 0;
    private void Review_Click(object sender, RoutedEventArgs e)
    {
        if (Editor.IsStale) Editor.Generate();
        WorkspaceTabs.SelectedIndex = 1;
    }
    private void Delivery_Click(object sender, RoutedEventArgs e) => WorkspaceTabs.SelectedIndex = 2;
    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (!Editor.Generate()) WorkspaceTabs.SelectedIndex = 1;
    }
    private void ViewMode_Changed(object sender, RoutedEventArgs e)
    {
        if (Preview is not null) Preview.IsPlan = PlanButton?.IsChecked == true;
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsaved()) return;
        Editor.NewProject();
        WorkspaceTabs.SelectedIndex = 0;
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        if (!ConfirmUnsaved()) return;
        var dialog = new OpenFileDialog { Title = "Abrir proyecto MakerGalpones", Filter = "Proyecto (*.yaml;*.yml)|*.yaml;*.yml", InitialDirectory = Editor.CurrentPath is null ? Path.Combine(AppContext.BaseDirectory, "samples") : Path.GetDirectoryName(Editor.CurrentPath) };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var input = ProjectInputLoader.LoadFromFile(dialog.FileName);
            Editor.LoadInput(input, dialog.FileName);
            WorkspaceTabs.SelectedIndex = 0;
        }
        catch (Exception ex) { ShowError("No se pudo abrir el proyecto", ex); }
    }

    private void Save_Click(object sender, RoutedEventArgs e) => SaveProject();
    private void SaveAs_Click(object sender, RoutedEventArgs e) => SaveProject(saveAs: true);
    private bool SaveProject(bool saveAs = false)
    {
        if (!Editor.Generate()) { WorkspaceTabs.SelectedIndex = 1; return false; }
        var path = Editor.CurrentPath;
        if (path is null || saveAs)
        {
            var dialog = new SaveFileDialog { Title = "Guardar proyecto", Filter = "Proyecto YAML (*.yaml)|*.yaml", DefaultExt = ".yaml", FileName = path is null ? "proyecto-galpon.yaml" : Path.GetFileName(path), InitialDirectory = path is null ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) : Path.GetDirectoryName(path) };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }
        try
        {
            ProjectInputWriter.Save(path, Editor.Input!);
            Editor.MarkSaved(path);
            return true;
        }
        catch (Exception ex) { ShowError("No se pudo guardar el proyecto", ex); return false; }
    }

    private void Prepare_Click(object sender, RoutedEventArgs e)
    {
        if (SaveProject()) WorkspaceTabs.SelectedIndex = 2;
    }

    private void ExportReview_Click(object sender, RoutedEventArgs e)
        => Export("Exportar revisión", "Documento Markdown (*.md)|*.md", "revision.md", () => Editor.BuildReviewMarkdown());

    private void ExportModel_Click(object sender, RoutedEventArgs e)
        => Export("Exportar modelo intermedio", "Modelo JSON (*.json)|*.json", "modelo.json", () => JsonSerializer.Serialize(new { schemaVersion = 1, units = "meters", state = "preliminary", project = Editor.Input, model = Editor.Model }, new JsonSerializerOptions { WriteIndented = true }));

    private void Export(string title, string filter, string fileName, Func<string> content)
    {
        if (!Editor.Generate()) { WorkspaceTabs.SelectedIndex = 1; return; }
        var dialog = new SaveFileDialog { Title = title, Filter = filter, FileName = fileName };
        if (dialog.ShowDialog(this) != true) return;
        try { File.WriteAllText(dialog.FileName, content(), new UTF8Encoding(false)); }
        catch (Exception ex) { ShowError("No se pudo exportar el archivo", ex); }
    }

    private void WorkPlan_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var document = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "docs", "PLAN-DE-TRABAJO.md"));
            new Window
            {
                Title = "MakerGalpones · Trabajos pendientes", Owner = this, Width = 900, Height = 720,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new TextBox { Text = document, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(28), BorderThickness = new Thickness(0), FontSize = 14 }
            }.Show();
        }
        catch (Exception ex) { ShowError("No se pudo abrir el documento", ex); }
    }

    private bool ConfirmUnsaved()
    {
        if (!Editor.IsDirty) return true;
        var choice = MessageBox.Show(this, "Tenés cambios sin guardar. ¿Querés guardarlos antes de continuar?", "MakerGalpones", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        return choice == MessageBoxResult.No || choice == MessageBoxResult.Yes && SaveProject();
    }

    private void Window_Closing(object? sender, CancelEventArgs e) => e.Cancel = !ConfirmUnsaved();
    private void ShowError(string title, Exception ex) => MessageBox.Show(this, ex.Message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
}
