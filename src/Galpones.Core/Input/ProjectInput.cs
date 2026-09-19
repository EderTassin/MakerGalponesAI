using YamlDotNet.Serialization;

namespace Galpones.Core.Input;

/// <summary>
/// Representa el esquema rígido de proyecto.yaml (ver plugin-revit-galpones.md, sección 4).
/// </summary>
public sealed class ProjectInput
{
    public string Tipologia { get; set; } = string.Empty;
    public string Jurisdiccion { get; set; } = string.Empty;
    public LoteInput Lote { get; set; } = new();
    public NaveInput Nave { get; set; } = new();
    public EstructuraInput Estructura { get; set; } = new();
    public PisoInput Piso { get; set; } = new();
    public ElectricoInput Electrico { get; set; } = new();
}

public sealed class LoteInput
{
    public double Frente { get; set; }
    public double Fondo { get; set; }
    public string Zona { get; set; } = string.Empty;
}

public sealed class NaveInput
{
    public double Largo { get; set; }
    public double Ancho { get; set; }

    [YamlMember(Alias = "altura_libre")]
    public double AlturaLibre { get; set; }
}

public sealed class EstructuraInput
{
    public string Tipo { get; set; } = string.Empty;
    public double Modulacion { get; set; }
}

public sealed class PisoInput
{
    [YamlMember(Alias = "sobrecarga_kN_m2", ApplyNamingConventions = false)]
    public double SobrecargaKnM2 { get; set; }
}

public sealed class ElectricoInput
{
    [YamlMember(Alias = "potencia_kVA", ApplyNamingConventions = false)]
    public double PotenciaKva { get; set; }
    public string Tension { get; set; } = string.Empty;
}
