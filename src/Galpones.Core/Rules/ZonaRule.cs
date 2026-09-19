namespace Galpones.Core.Rules;

/// <summary>
/// Una regla de zonificación extraída de una ordenanza municipal (ej. Córdoba Capital,
/// Ordenanza 8256/86). Cada campo remite al artículo de origen para trazabilidad
/// (ver plugin-revit-galpones.md, sección 4, "Tipos de regla").
///
/// El F.O.S. de varias zonas depende del frente del lote (dos tramos: menor o mayor/igual
/// a 25 m). Cuando la ordenanza fija un único valor se usa <see cref="FosMax"/>; cuando es
/// por tramos se usan <see cref="FosMaxFrenteMenor25M"/> / <see cref="FosMaxFrenteMayorIgual25M"/>
/// y <see cref="FosMax"/> queda en null.
///
/// <see cref="FotMax"/> y <see cref="AlturaMaxMetros"/> quedan en null cuando la ordenanza
/// no fija un valor único (p. ej. "sin limitaciones", o remite a otro artículo). Los
/// retiros no se modelan como números sueltos porque casi siempre son condicionales
/// (dependen del frente del lote y/o de la altura de la edificación): se dejan como texto
/// descriptivo en <see cref="Retiros"/> para no inventar una precisión que la ordenanza no da.
/// </summary>
public sealed record ZonaRule(
    string Zona,
    string Caracter,
    double? FosMax,
    double? FosMaxFrenteMenor25M,
    double? FosMaxFrenteMayorIgual25M,
    double? FotMax,
    double? AlturaMaxMetros,
    string Retiros,
    string Articulo)
{
    public bool TienesFosPorTramoDeFrente => FosMaxFrenteMenor25M is not null;

    /// <summary>F.O.S. máximo aplicable a un lote con el frente dado, en metros.</summary>
    public double? FosMaxParaFrente(double frenteMetros) => TienesFosPorTramoDeFrente
        ? frenteMetros < 25.0 ? FosMaxFrenteMenor25M : FosMaxFrenteMayorIgual25M
        : FosMax;
}
