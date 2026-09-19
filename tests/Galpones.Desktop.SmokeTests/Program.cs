using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Galpones.Core.Input;
using Galpones.Desktop;
using Galpones.Desktop.Controls;

namespace Galpones.Desktop.SmokeTests;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        var output = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/qa");
        Directory.CreateDirectory(output);
        try
        {
            var app = new App(); app.InitializeComponent();
            var window = new MainWindow();
            var editor = window.Editor;
            Check(editor.Model?.Columnas.Count == 22, "Modelo inicial");
            Check(editor.ReviewItems.Any(i => i.Titulo == "zona no reconocida"), "Zona no confirmada señalada");
            Capture(window, Path.Combine(output, "desktop-diseno.png"));

            editor.Fields["largo"].Value = "62";
            editor.Fields["pendiente"].Value = "12,5";
            editor.Fields["columna"].Value = "Acero: Perfil 300";
            Check(editor.IsDirty && editor.IsStale, "Edición invalida la vista anterior");
            Check(editor.Generate(), "Generación con coma decimal");
            Check(editor.Model!.Columnas.Max(c => c.X) == 62, "Pórtico extremo");
            Check(editor.Model.PendienteCubiertaPct == 12.5, "Pendiente aplicada");
            Check(editor.ReviewItems.Any(i => i.Titulo == "Último módulo ajustado"), "Módulo residual informado");
            var project = Path.Combine(output, "smoke-proyecto.yaml");
            ProjectInputWriter.Save(project, editor.BuildInput());
            editor.MarkSaved(project);
            var loaded = ProjectInputLoader.LoadFromFile(project);
            editor.LoadInput(loaded, project);
            Check(editor.Fields["columna"].Value == "Acero: Perfil 300", "Familia conservada tras guardar y abrir");
            Check(editor.Input!.Nave.Largo == 62 && !editor.IsDirty, "Proyecto reabierto sin cambios pendientes");
            File.WriteAllText(Path.Combine(output, "smoke-revision.md"), editor.BuildReviewMarkdown());
            var preview = (StructurePreview)window.FindName("Preview");
            preview.IsPlan = true;
            ((RadioButton)window.FindName("PlanButton")).IsChecked = true;
            Capture(window, Path.Combine(output, "desktop-planta.png"));

            ((RadioButton)window.FindName("SiteButton")).IsChecked = true;
            Check(editor.SiteLayout is { EstacionamientoAutos.Count: > 0, Muelles.Count: > 0 }, "Simulación de sitio generada");
            Capture(window, Path.Combine(output, "desktop-sitio.png"));
            editor.SetImplantacion(10, 10);
            Check(editor.SiteLayout!.Nave.X == 10 && editor.SiteLayout.Nave.Y == 10, "Arrastre interactivo mueve la nave");
            Check(editor.SiteLayout.Metricas.CoberturaPct > 0 && editor.SiteLayout.Metricas.SuperficieEstacionamientoM2 > 0, "Métricas de sitio calculadas");
            Capture(window, Path.Combine(output, "desktop-sitio-movido.png"));
            ((RadioButton)window.FindName("AxonButton")).IsChecked = true;

            editor.Fields["ancho"].Value = "inválido";
            Check(!editor.Generate() && editor.Model is null, "Datos inválidos limpian la vista");
            Check(editor.ReviewItems.Any(i => i.Estado == "CORREGIR"), "Error de datos visible");
            var tabs = (TabControl)window.FindName("WorkspaceTabs");
            tabs.SelectedIndex = 1;
            Capture(window, Path.Combine(output, "desktop-error.png"));
            editor.Fields["ancho"].Value = "25";
            Check(editor.Generate(), "Recuperación tras corregir datos");
            Capture(window, Path.Combine(output, "desktop-revision.png"));
            tabs.SelectedIndex = 2;
            Capture(window, Path.Combine(output, "desktop-entrega.png"));
            tabs.SelectedIndex = 0;
            Capture(window, Path.Combine(output, "desktop-compacto.png"), 1064, 661);
            Check(File.Exists(Path.Combine(AppContext.BaseDirectory, "docs", "PLAN-DE-TRABAJO.md")), "Documento de trabajo distribuido");
            editor.MarkSaved(project);
            window.Close();
            File.WriteAllText(Path.Combine(output, "resultado.txt"), "PASS: edición, generación, validación, reglas, guardar/reabrir, exportación y seis capturas WPF.\nRevit no ejecutado.\n");
            Console.WriteLine("PASS: desktop smoke checks; screenshots in " + output);
            return 0;
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(output, "resultado.txt"), ex.ToString());
            Console.Error.WriteLine(ex);
            return 1;
        }
    }

    private static void Check(bool passed, string name)
    {
        if (!passed) throw new InvalidOperationException("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    private static void Capture(MainWindow window, string path, int width = 1320, int height = 820)
    {
        // Renderiza únicamente el árbol visual de la aplicación, sin capturar la pantalla del usuario.
        var root = (FrameworkElement)window.Content;
        root.Width = width; root.Height = height;
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
