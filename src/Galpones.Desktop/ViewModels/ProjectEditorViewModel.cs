using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using Galpones.Core.Input;
using Galpones.Core.IntermediateModel;
using Galpones.Core.Workflow;

namespace Galpones.Desktop.ViewModels;

public sealed class InputField(string key, string label, string unit = "") : INotifyPropertyChanged
{
    public string Key { get; } = key;
    public string Label { get; } = label;
    public string Unit { get; } = unit;
    private string _value = "";
    public string Value
    {
        get => _value;
        set { if (_value == value) return; _value = value; PropertyChanged?.Invoke(this, new(nameof(Value))); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed record FieldGroup(string Title, string Description, IReadOnlyList<InputField> Fields);

public sealed class ProjectEditorViewModel : INotifyPropertyChanged
{
    public IReadOnlyList<FieldGroup> Groups { get; }
    public IReadOnlyDictionary<string, InputField> Fields { get; }
    public ObservableCollection<ReviewItem> ReviewItems { get; } = [];
    public string? CurrentPath { get; private set; }
    public ProjectInput? Input { get; private set; }
    public GalponModel? Model { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsStale { get; private set; }
    public string Status { get; private set; } = "Listo para configurar.";
    public string ProjectTitle => CurrentPath is null ? "Nueva nave de depósito" : Path.GetFileNameWithoutExtension(CurrentPath);
    public string FileLabel => (CurrentPath ?? "Proyecto sin guardar") + (IsDirty ? " · cambios sin guardar" : "");
    public string Area => Input is null ? "—" : $"{Input.Nave.Largo * Input.Nave.Ancho:N0}";
    public string Frames => Model is null ? "—" : (Model.Columnas.Count / 2).ToString();
    public string Columns => Model?.Columnas.Count.ToString() ?? "—";
    public string Beams => Model?.Vigas.Count.ToString() ?? "—";
    public string Ridge => Model is null ? "—" : $"{Model.Levels.Single(l => l.Nombre == "Cumbrera").ElevacionMetros:0.##} m";
    public string PreviewCaption => Input is null ? "Completá los datos para generar la vista." : $"{Input.Nave.Largo:0.##} × {Input.Nave.Ancho:0.##} m  ·  Altura de referencia {Input.Nave.AlturaLibre:0.##} m  ·  Pendiente {Model!.PendienteCubiertaPct:0.##}%";
    private bool _loading;
    private readonly string _rulesDirectory;

    public ProjectEditorViewModel(string? rulesDirectory = null)
    {
        _rulesDirectory = rulesDirectory ?? Path.Combine(AppContext.BaseDirectory, "reglas");
        Groups = [
            new("01 / Nave", "Nave rectangular con cubierta a dos aguas.", [
                new("largo", "Largo", "m"), new("ancho", "Ancho", "m"),
                new("altura", "Altura de referencia", "m"), new("modulacion", "Modulación máxima", "m"),
                new("pendiente", "Pendiente de cubierta", "%")]),
            new("02 / Terreno", "Dimensiones y ubicación normativa del lote.", [
                new("frente", "Frente", "m"), new("fondo", "Fondo", "m"),
                new("jurisdiccion", "Jurisdicción"), new("zona", "Código de zona")]),
            new("03 / Familias", "Opcional. Nombre exacto del tipo en Revit; puede incluir «Familia: Tipo».", [
                new("columna", "Tipo de columna"), new("viga", "Tipo de viga")]),
            new("04 / Requisitos técnicos", "Se guardan como requisitos. El cálculo y las instalaciones están pendientes.", [
                new("carga", "Sobrecarga del piso", "kN/m²"), new("potencia", "Potencia eléctrica", "kVA"),
                new("tension", "Tensión")])
        ];
        Fields = Groups.SelectMany(g => g.Fields).ToDictionary(f => f.Key);
        foreach (var field in Fields.Values) field.PropertyChanged += FieldChanged;
        NewProject();
    }

    public void NewProject() => LoadInput(new ProjectInput
    {
        Tipologia = "nave_deposito", Jurisdiccion = "cordoba-capital",
        Lote = new() { Frente = 40, Fondo = 80, Zona = "" },
        Nave = new() { Largo = 60, Ancho = 25, AlturaLibre = 8 },
        Estructura = new() { Tipo = "porticos_metalicos", Modulacion = 6, PendienteCubiertaPct = 10 },
        Piso = new() { SobrecargaKnM2 = 30 }, Electrico = new() { PotenciaKva = 150, Tension = "trifasica" }
    }, null);

    public void LoadInput(ProjectInput input, string? path)
    {
        ProjectInputLoader.Validate(input);
        _loading = true;
        void Set(string key, object? value) => Fields[key].Value = value is double number ? number.ToString("0.############", CultureInfo.InvariantCulture) : value?.ToString() ?? "";
        Set("frente", input.Lote.Frente); Set("fondo", input.Lote.Fondo); Set("zona", input.Lote.Zona);
        Set("jurisdiccion", input.Jurisdiccion); Set("largo", input.Nave.Largo); Set("ancho", input.Nave.Ancho);
        Set("altura", input.Nave.AlturaLibre); Set("modulacion", input.Estructura.Modulacion);
        Set("pendiente", input.Estructura.PendienteCubiertaPct ?? GalponModelGenerator.PendienteCubiertaDefaultPct);
        Set("columna", input.Estructura.PerfilColumna); Set("viga", input.Estructura.PerfilViga);
        Set("carga", input.Piso.SobrecargaKnM2); Set("potencia", input.Electrico.PotenciaKva); Set("tension", input.Electrico.Tension);
        CurrentPath = path;
        IsDirty = false;
        _loading = false;
        Generate();
        NotifyAll();
    }

    public ProjectInput BuildInput()
    {
        var errors = new List<string>();
        string Text(string key) => Fields[key].Value.Trim();
        double Number(string key)
        {
            if (double.TryParse(Text(key).Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)) return value;
            errors.Add($"{Fields[key].Label}: ingresá un número válido, sin separador de miles.");
            return 0;
        }
        var input = new ProjectInput
        {
            Tipologia = "nave_deposito", Jurisdiccion = Text("jurisdiccion"),
            Lote = new() { Frente = Number("frente"), Fondo = Number("fondo"), Zona = Text("zona") },
            Nave = new() { Largo = Number("largo"), Ancho = Number("ancho"), AlturaLibre = Number("altura") },
            Estructura = new()
            {
                Tipo = "porticos_metalicos", Modulacion = Number("modulacion"), PendienteCubiertaPct = Number("pendiente"),
                PerfilColumna = string.IsNullOrWhiteSpace(Text("columna")) ? null : Text("columna"),
                PerfilViga = string.IsNullOrWhiteSpace(Text("viga")) ? null : Text("viga")
            },
            Piso = new() { SobrecargaKnM2 = Number("carga") },
            Electrico = new() { PotenciaKva = Number("potencia"), Tension = Text("tension") }
        };
        if (errors.Count != 0) throw new ProjectInputValidationException(errors);
        ProjectInputLoader.Validate(input);
        return input;
    }

    public bool Generate()
    {
        ReviewItems.Clear();
        try
        {
            var input = BuildInput();
            var review = ProjectReviewService.Evaluate(input, _rulesDirectory);
            Input = input;
            Model = review.Model;
            foreach (var item in review.Items) ReviewItems.Add(item);
            IsStale = false;
            Status = "Vista actualizada · modelo preliminar para revisión";
            NotifyAll();
            return true;
        }
        catch (ProjectInputValidationException ex)
        {
            Input = null; Model = null; IsStale = true;
            foreach (var error in ex.Errors) ReviewItems.Add(new("CORREGIR", "Dato de proyecto", error));
            Status = "Hay datos por corregir. Consultá la pestaña Revisión.";
            NotifyAll();
            return false;
        }
    }

    public void MarkSaved(string path)
    {
        CurrentPath = path; IsDirty = false;
        Status = "Proyecto guardado. Disponible para abrir desde el add-in de Revit.";
        NotifyAll();
    }

    public string BuildReviewMarkdown()
    {
        if (Input is null || Model is null || IsStale) throw new InvalidOperationException("Actualizá el modelo antes de exportar la revisión.");
        return $"# Revisión preliminar — {ProjectTitle}\n\nGenerada: {DateTimeOffset.Now:yyyy-MM-dd HH:mm zzz}\n\n" +
            $"Nave: {PreviewCaption}\n\nSuperficie: {Area} m². Pórticos: {Frames}. Columnas: {Columns}. Vigas: {Beams}.\n\n" +
            "Estado: borrador. No constituye una aprobación normativa ni un cálculo estructural.\n\n" +
            string.Join("\n\n", ReviewItems.Select(i => $"## {i.Estado} · {i.Titulo}\n\n{i.Detalle}" + (i.Fuente.Length > 0 ? $"\n\nReferencia: {i.Fuente}" : ""))) + "\n";
    }

    private void FieldChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading) return;
        IsDirty = true; IsStale = true;
        Status = "Parámetros modificados · actualizá la vista para revisar el resultado";
        NotifyAll();
    }

    private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    public event PropertyChangedEventHandler? PropertyChanged;
}
