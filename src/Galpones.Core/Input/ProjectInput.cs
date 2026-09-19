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

    /// <summary>
    /// Sección opcional para el sitio (Galpones.Core.Site): implantación de la nave en el lote,
    /// estacionamiento de autos y muelles/circulación de transporte pesado. Es una simulación de
    /// prefactibilidad, no un plano de sitio normativo (ver SIT-01/SIT-02 en PLAN-DE-TRABAJO.md).
    /// </summary>
    public LogisticaInput Logistica { get; set; } = new();
}

public sealed class LoteInput
{
    public double Frente { get; set; }
    public double Fondo { get; set; }

    /// <summary>
    /// Código de zona según el Plano de Zonificación General vigente para el lote puntual
    /// (se consulta en el mapa de IDECOR, no es una etiqueta libre). Se valida contra
    /// reglas/&lt;jurisdiccion&gt;/zonificacion.yaml — ver Galpones.Core.Rules.
    /// </summary>
    public string Zona { get; set; } = string.Empty;
}

public sealed class NaveInput
{
    public double Largo { get; set; }
    public double Ancho { get; set; }

    [YamlMember(Alias = "altura_libre", ApplyNamingConventions = false)]
    public double AlturaLibre { get; set; }
}

public sealed class EstructuraInput
{
    public string Tipo { get; set; } = string.Empty;
    public double Modulacion { get; set; }

    /// <summary>Pendiente de cubierta en %. Opcional: si falta, el generador aplica su default.</summary>
    [YamlMember(Alias = "pendiente_cubierta_pct", ApplyNamingConventions = false)]
    public double? PendienteCubiertaPct { get; set; }

    /// <summary>Nombre de tipo de columna preferido en el documento Revit (opcional).</summary>
    [YamlMember(Alias = "perfil_columna", ApplyNamingConventions = false)]
    public string? PerfilColumna { get; set; }

    /// <summary>Nombre de tipo de viga preferido en el documento Revit (opcional).</summary>
    [YamlMember(Alias = "perfil_viga", ApplyNamingConventions = false)]
    public string? PerfilViga { get; set; }
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

/// <summary>
/// Todos los campos son opcionales: si faltan, Galpones.Core.Site.SiteLayoutGenerator aplica
/// sus valores por defecto (marcados aquí como constantes para que el input y el generador
/// no puedan desincronizarse).
/// </summary>
public sealed class LogisticaInput
{
    /// <summary>Franja entre el frente del lote y la nave: acceso, maniobra y estacionamiento de autos.</summary>
    public const double RetiroFrenteDefaultM = 8.0;

    /// <summary>Margen mínimo entre el fondo de la nave y el fondo del lote.</summary>
    public const double RetiroFondoDefaultM = 3.0;

    /// <summary>Margen mínimo a cada lado de la nave; el lado izquierdo se reserva como corredor de acceso pesado.</summary>
    public const double RetiroLateralDefaultM = 4.5;

    [YamlMember(Alias = "retiro_frente_m", ApplyNamingConventions = false)]
    public double? RetiroFrenteM { get; set; }

    [YamlMember(Alias = "retiro_fondo_m", ApplyNamingConventions = false)]
    public double? RetiroFondoM { get; set; }

    [YamlMember(Alias = "retiro_lateral_m", ApplyNamingConventions = false)]
    public double? RetiroLateralM { get; set; }

    /// <summary>Cantidad de espacios de auto a generar. Si falta, se completa la franja frontal disponible.</summary>
    [YamlMember(Alias = "autos_cantidad", ApplyNamingConventions = false)]
    public int? AutosCantidad { get; set; }

    /// <summary>Cantidad de muelles de carga a generar. Si falta, se completa el paño de fondo de la nave.</summary>
    [YamlMember(Alias = "camiones_cantidad", ApplyNamingConventions = false)]
    public int? CamionesCantidad { get; set; }
}
