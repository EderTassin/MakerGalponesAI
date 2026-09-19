using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using Autodesk.Revit.DB;
using Galpones.Revit.Families;

namespace Galpones.Revit.UI;

/// <summary>
/// Ventana modal para que el arquitecto elija el tipo de familia de cada slot
/// (columna, viga) antes de generar. No persiste nada: la preselección viene del YAML.
/// Agregar un slot nuevo es una línea en <see cref="SlotDefinitions"/>.
/// </summary>
public partial class FamilyPickerWindow : Window
{
    public const string SinFamiliasItem = "(sin familias — importá desde familias/ o usá la plantilla estructural)";

    private readonly Document _doc;
    private readonly string _carpetaFamilias;

    public ObservableCollection<SlotViewModel> Slots { get; } = [];

    /// <summary>Tipo elegido por slot ("Familia: Tipo"), solo válido si DialogResult == true.</summary>
    public IReadOnlyDictionary<string, string> Selecciones =>
        Slots.ToDictionary(s => s.Titulo, s => s.Seleccion!);

    public FamilyPickerWindow(Document doc, string carpetaFamilias,
        string? perfilColumnaYaml, string? perfilVigaYaml)
    {
        InitializeComponent();
        _doc = doc;
        _carpetaFamilias = carpetaFamilias;

        Slots.Add(new SlotViewModel("Columna", BuiltInCategory.OST_StructuralColumns, perfilColumnaYaml));
        Slots.Add(new SlotViewModel("Viga", BuiltInCategory.OST_StructuralFraming, perfilVigaYaml));
        foreach (var slot in Slots)
            slot.PropertyChanged += (_, _) => ActualizarEstadoGenerar();

        DataContext = this;
        RefrescarCombos();
    }

    /// <summary>Muestra la ventana como diálogo modal hijo de la ventana principal de Revit.</summary>
    public bool? ShowDialogOwned()
    {
        new WindowInteropHelper(this).Owner = Process.GetCurrentProcess().MainWindowHandle;
        return ShowDialog();
    }

    private void RefrescarCombos()
    {
        foreach (var slot in Slots)
        {
            var seleccionAnterior = slot.Seleccion;
            slot.Items.Clear();
            foreach (var disponible in FamilySymbolResolver.ListAvailableSymbols(_doc, slot.Categoria))
                slot.Items.Add(disponible);
            if (slot.Items.Count == 0)
                slot.Items.Add(SinFamiliasItem);

            slot.Seleccion =
                slot.Items.FirstOrDefault(i => i == seleccionAnterior)
                ?? slot.Items.FirstOrDefault(i => CoincideConPreseleccion(i, slot.PreseleccionYaml))
                ?? slot.Items[0];
        }
        ActualizarEstadoGenerar();
    }

    private static bool CoincideConPreseleccion(string item, string? preseleccionYaml)
        => !string.IsNullOrWhiteSpace(preseleccionYaml)
           && (item.Equals(preseleccionYaml, StringComparison.OrdinalIgnoreCase)
               || item.EndsWith(": " + preseleccionYaml, StringComparison.OrdinalIgnoreCase));

    private void ActualizarEstadoGenerar()
        => GenerarButton.IsEnabled = Slots.All(s =>
            s.Seleccion is not null && s.Seleccion != SinFamiliasItem);

    private void Importar_Click(object sender, RoutedEventArgs e)
    {
        FamilyImporter.ImportResult resultado;
        using (var tx = new Transaction(_doc, "Importar familias"))
        {
            tx.Start();
            resultado = FamilyImporter.ImportarDesdeCarpeta(_doc, _carpetaFamilias);
            if (resultado.HuboCambios)
                tx.Commit();
            else
                tx.RollBack();
        }

        EstadoImportacion.Text = resultado.Resumen(_carpetaFamilias);
        RefrescarCombos();
    }

    private void Generar_Click(object sender, RoutedEventArgs e)
        => DialogResult = true;

    public sealed class SlotViewModel(string titulo, BuiltInCategory categoria, string? preseleccionYaml)
        : INotifyPropertyChanged
    {
        public string Titulo { get; } = titulo;
        public BuiltInCategory Categoria { get; } = categoria;
        public string? PreseleccionYaml { get; } = preseleccionYaml;
        public ObservableCollection<string> Items { get; } = [];

        private string? _seleccion;
        public string? Seleccion
        {
            get => _seleccion;
            set
            {
                if (_seleccion == value) return;
                _seleccion = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Seleccion)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
