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
        .IgnoreUnmatchedProperties()
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

    private static void Validate(ProjectInput input)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(input.Tipologia))
            errors.Add("tipologia es obligatoria");

        if (string.IsNullOrWhiteSpace(input.Jurisdiccion))
            errors.Add("jurisdiccion es obligatoria");

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

        if (input.Piso.SobrecargaKnM2 <= 0)
            errors.Add("piso.sobrecarga_kN_m2 debe ser mayor a 0");

        if (errors.Count > 0)
            throw new ProjectInputValidationException(errors);
    }
}
