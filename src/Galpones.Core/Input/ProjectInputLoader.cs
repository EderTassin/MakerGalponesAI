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
        input.Logistica ??= new();

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

        var rotado = input.Implantacion.RotacionGrados is 90 or 270;
        var anchoEfectivoNave = rotado ? input.Nave.Largo : input.Nave.Ancho;
        var profundidadEfectivaNave = rotado ? input.Nave.Ancho : input.Nave.Largo;

        if (anchoEfectivoNave > input.Lote.Frente)
            errors.Add(rotado
                ? "nave.largo no puede exceder lote.frente con rotación de 90/270"
                : "nave.ancho no puede exceder lote.frente");
        if (profundidadEfectivaNave > input.Lote.Fondo)
            errors.Add(rotado
                ? "nave.ancho no puede exceder lote.fondo con rotación de 90/270"
                : "nave.largo no puede exceder lote.fondo");

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

        if (input.Logistica.RetiroFrenteM is <= 0)
            errors.Add("logistica.retiro_frente_m debe ser mayor a 0");
        if (input.Logistica.RetiroFondoM is <= 0)
            errors.Add("logistica.retiro_fondo_m debe ser mayor a 0");
        if (input.Logistica.RetiroLateralM is <= 0)
            errors.Add("logistica.retiro_lateral_m debe ser mayor a 0");
        if (input.Logistica.AutosCantidad is < 0)
            errors.Add("logistica.autos_cantidad no puede ser negativa");
        if (input.Logistica.CamionesCantidad is < 0)
            errors.Add("logistica.camiones_cantidad no puede ser negativa");
        if (input.Logistica.AnguloEstacionamiento is not (null or 45 or 60 or 90))
            errors.Add("logistica.angulo_estacionamiento debe ser 45, 60 o 90");
        if (input.Logistica.ProfundidadPatioM is <= 0)
            errors.Add("logistica.profundidad_patio_m debe ser mayor a 0");
        if (input.Logistica.AnchoCallePesadaM is <= 0)
            errors.Add("logistica.ancho_calle_pesada_m debe ser mayor a 0");
        if (input.Logistica.MuellesEn is { } cara &&
            !new[] { "auto", "frente", "fondo", "izquierda", "derecha" }.Contains(cara, StringComparer.OrdinalIgnoreCase))
            errors.Add("logistica.muelles_en debe ser auto, frente, fondo, izquierda o derecha");

        input.Implantacion ??= new();
        if (input.Implantacion.RotacionGrados is not (0 or 90 or 180 or 270))
            errors.Add("implantacion.rotacion_grados debe ser 0, 90, 180 o 270");
        if (input.Implantacion.XM is < 0)
            errors.Add("implantacion.x_m no puede ser negativa");
        if (input.Implantacion.YM is < 0)
            errors.Add("implantacion.y_m no puede ser negativa");
        if (input.Implantacion.XM is { } x && input.Nave is { } nave && input.Lote is { } lote && nave.Ancho > 0 && lote.Frente > 0)
        {
            if (x + anchoEfectivoNave > lote.Frente)
                errors.Add($"implantacion.x_m + ancho efectivo de la nave ({x + anchoEfectivoNave:0.##} m) no puede exceder lote.frente ({lote.Frente:0.##} m)");
            if (input.Implantacion.YM is { } y && y + profundidadEfectivaNave > lote.Fondo)
                errors.Add($"implantacion.y_m + profundidad efectiva de la nave ({y + profundidadEfectivaNave:0.##} m) no puede exceder lote.fondo ({lote.Fondo:0.##} m)");
        }

        // El sitio (Galpones.Core.Site) implanta la nave dentro del lote con estos retiros; si no
        // entra, mejor rechazarlo acá con un mensaje claro que dejar que el generador de sitio falle.
        if (anchoEfectivoNave > 0 && input.Lote.Frente > 0)
        {
            var retiroLateral = input.Logistica.RetiroLateralM ?? LogisticaInput.RetiroLateralDefaultM;
            var anchoRequerido = anchoEfectivoNave + 2 * retiroLateral;
            if (anchoRequerido > input.Lote.Frente)
                errors.Add($"ancho efectivo de la nave + 2×logistica.retiro_lateral_m ({anchoRequerido:0.##} m) no puede exceder lote.frente ({input.Lote.Frente:0.##} m)");
        }
        if (profundidadEfectivaNave > 0 && input.Lote.Fondo > 0)
        {
            var retiroFrente = input.Logistica.RetiroFrenteM ?? LogisticaInput.RetiroFrenteDefaultM;
            var retiroFondo = input.Logistica.RetiroFondoM ?? LogisticaInput.RetiroFondoDefaultM;
            var fondoRequerido = profundidadEfectivaNave + retiroFrente + retiroFondo;
            if (fondoRequerido > input.Lote.Fondo)
                errors.Add($"profundidad efectiva de la nave + logistica.retiro_frente_m + logistica.retiro_fondo_m ({fondoRequerido:0.##} m) no puede exceder lote.fondo ({input.Lote.Fondo:0.##} m)");
        }

        if (errors.Count > 0)
            throw new ProjectInputValidationException(errors);
    }
}
