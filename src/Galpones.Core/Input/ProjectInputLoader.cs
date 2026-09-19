using YamlDotNet.Serialization;

namespace Galpones.Core.Input;

public sealed class ProjectInputValidationException(IReadOnlyList<string> errors)
    : Exception("El input del proyecto tiene errores: " + string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

/// <summary>
/// Lee y valida proyecto.yaml contra el esquema rígido definido en Input.ProjectInput.
/// No interpreta normativa: solo carga y valida forma/rangos básicos del input.
/// </summary>
public static class ProjectInputLoader
{
    private static readonly IDeserializer Deserializer = new DeserializerBuilder()
        .WithCaseInsensitivePropertyMatching()
        .Build();

    public static ProjectInput LoadFromFile(string path)
    {
        using var reader = new StreamReader(path);
        return LoadFromTextReader(reader);
    }

    public static ProjectInput LoadFromYaml(string yaml)
    {
        using var reader = new StringReader(yaml);
        return LoadFromTextReader(reader);
    }

    private static ProjectInput LoadFromTextReader(TextReader reader)
    {
        var input = Deserializer.Deserialize<ProjectInput>(reader) ?? new ProjectInput();
        Validate(input);
        return input;
    }

    public static void Validate(ProjectInput input)
    {
        var errors = new List<string>();

        if (input.Lote is null || input.Nave is null || input.Estructura is null || input.Piso is null || input.Electrico is null)
            throw new ProjectInputValidationException(["Las secciones lote, nave, estructura, piso y electrico no pueden estar vacías."]);

        if (string.IsNullOrWhiteSpace(input.Tipologia))
            errors.Add("tipologia es obligatoria");

        if (string.IsNullOrWhiteSpace(input.Jurisdiccion))
            errors.Add("jurisdiccion es obligatoria");

        if (!string.Equals(input.Tipologia, "nave_deposito", StringComparison.OrdinalIgnoreCase))
            errors.Add("La tipología disponible por ahora es nave_deposito.");
        if (!string.Equals(input.Estructura.Tipo, "porticos_metalicos", StringComparison.OrdinalIgnoreCase))
            errors.Add("El sistema disponible por ahora es porticos_metalicos.");

        var numbers = new Dictionary<string, double>
        {
            ["lote.frente"] = input.Lote.Frente, ["lote.fondo"] = input.Lote.Fondo,
            ["nave.largo"] = input.Nave.Largo, ["nave.ancho"] = input.Nave.Ancho,
            ["nave.altura_libre"] = input.Nave.AlturaLibre, ["estructura.modulacion"] = input.Estructura.Modulacion,
            ["piso.sobrecarga_kN_m2"] = input.Piso.SobrecargaKnM2, ["electrico.potencia_kVA"] = input.Electrico.PotenciaKva,
        };
        if (input.Estructura.PendienteCubiertaPct is { } pendiente) numbers["estructura.pendiente_cubierta_pct"] = pendiente;
        foreach (var (name, value) in numbers)
            if (!double.IsFinite(value)) errors.Add($"{name} debe ser un número finito.");

        if (input.Lote.Frente <= 0)
            errors.Add("lote.frente debe ser mayor a 0");
        if (input.Lote.Fondo <= 0)
            errors.Add("lote.fondo debe ser mayor a 0");

        if (input.Nave.Largo <= 0)
            errors.Add("nave.largo debe ser mayor a 0");
        if (input.Nave.Ancho <= 0)
            errors.Add("nave.ancho debe ser mayor a 0");
        if (input.Nave.AlturaLibre <= 0)
            errors.Add("nave.altura_libre debe ser mayor a 0");

        if (input.Nave.Largo > input.Lote.Fondo)
            errors.Add("nave.largo no puede exceder lote.fondo");
        if (input.Nave.Ancho > input.Lote.Frente)
            errors.Add("nave.ancho no puede exceder lote.frente");

        if (input.Estructura.Modulacion <= 0)
            errors.Add("estructura.modulacion debe ser mayor a 0");
        if (input.Estructura.Modulacion > input.Nave.Largo)
            errors.Add("estructura.modulacion no puede exceder nave.largo");
        if (input.Estructura.Modulacion > 0 && input.Nave.Largo / input.Estructura.Modulacion > 2000)
            errors.Add("Esta versión admite hasta 2.000 módulos por nave. Aumentá la modulación.");

        if (input.Estructura.PendienteCubiertaPct is <= 0 or > 100)
            errors.Add("estructura.pendiente_cubierta_pct debe estar entre 0 (excluido) y 100");

        if (input.Piso.SobrecargaKnM2 <= 0)
            errors.Add("piso.sobrecarga_kN_m2 debe ser mayor a 0");
        if (input.Electrico.PotenciaKva < 0)
            errors.Add("electrico.potencia_kVA no puede ser negativa");

        if (errors.Count > 0)
            throw new ProjectInputValidationException(errors);
    }
}
