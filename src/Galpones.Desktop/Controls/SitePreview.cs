using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Galpones.Core.Site;

namespace Galpones.Desktop.Controls;

/// <summary>
/// Vista en planta de la simulación de sitio: lote, nave a escala, estacionamiento de autos y
/// circulación/muelles de transporte pesado. Igual que StructurePreview, no depende de Revit ni
/// de un navegador; es geometría orientativa, no un plano de sitio aprobado.
/// </summary>
public sealed class SitePreview : FrameworkElement
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(SiteLayoutModel), typeof(SitePreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public SiteLayoutModel? Model { get => (SiteLayoutModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth; var height = ActualHeight;
        if (width < 100 || height < 35) return;
        dc.DrawRoundedRectangle(Brush("#F8FBFA"), null, new Rect(0, 0, width, height), 8, 8);
        if (Model is not { LoteFrenteM: > 0, LoteFondoM: > 0 } site)
        {
            Text(dc, "Sin simulación de sitio · revisá los datos de entrada", new Point(24, height / 2), "#61767F", 13);
            return;
        }

        var deltaX = site.LoteFrenteM; var deltaY = site.LoteFondoM;
        var topReserved = height < 180 ? 14 : 56;
        var bottomReserved = height < 180 ? 6 : 26;
        var scale = Math.Min((width - 40) / deltaX, (height - topReserved - bottomReserved) / deltaY);
        var offsetX = (width - deltaX * scale) / 2; var offsetY = topReserved + (height - topReserved - bottomReserved - deltaY * scale) / 2;
        Rect Project(RectDef r) => new(offsetX + r.X * scale, offsetY + r.Y * scale, r.Ancho * scale, r.Profundidad * scale);
        Point ProjectPoint(double x, double y) => new(offsetX + x * scale, offsetY + y * scale);

        var lote = new Rect(offsetX, offsetY, deltaX * scale, deltaY * scale);
        dc.DrawRectangle(Brush("#EFF4F1"), new Pen(Brush("#B9CCC5"), 1) { DashStyle = DashStyles.Dash }, lote);

        if (site.CorredorAccesoPesado is { } corredor)
            dc.DrawRectangle(Brush("#FCE9D2"), null, Project(corredor));
        if (site.PatioManiobraPesada is { } patio)
            dc.DrawRectangle(Brush("#FCE9D2"), new Pen(Brush("#E0AE68"), 1) { DashStyle = DashStyles.Dash }, Project(patio));

        foreach (var espacio in site.EstacionamientoAutos)
            dc.DrawRectangle(Brush("#DCE8F5"), new Pen(Brush("#7FA6CC"), 1), Project(espacio));

        var naveRect = Project(site.Nave);
        dc.DrawRectangle(Brush("#D8ECE5"), new Pen(Brush("#129783"), 1.6), naveRect);
        var gridPen = new Pen(Brush("#8FC2B6"), 0.8) { DashStyle = DashStyles.Dash };
        foreach (var eje in site.EjesEstructuralesM)
        {
            var y = offsetY + (site.Nave.Y + eje) * scale;
            dc.DrawLine(gridPen, new Point(naveRect.Left, y), new Point(naveRect.Right, y));
        }

        var muellePen = new Pen(Brush("#294958"), 2.2);
        var pasoMuellesPx = site.Muelles.Count > 1 ? (site.Muelles[1].X - site.Muelles[0].X) * scale : double.MaxValue;
        foreach (var muelle in site.Muelles)
        {
            var punto = ProjectPoint(muelle.X, muelle.Y);
            dc.DrawLine(muellePen, new Point(punto.X, punto.Y - 5), new Point(punto.X, punto.Y + 5));
            if (height >= 220 && pasoMuellesPx >= 26) Text(dc, muelle.Nombre, new Point(punto.X - 8, punto.Y + 7), "#52716A", 9);
        }

        if (height >= 180)
        {
            Text(dc, "SIMULACIÓN DE SITIO · PLANTA", new Point(15, 12), "#708782", 10);
            Text(dc, "Retiros orientativos. No es un plano de sitio ni una aprobación normativa.", new Point(15, 28), "#8A5916", 10);
            Text(dc, $"Lote {site.LoteFrenteM:0.##} × {site.LoteFondoM:0.##} m   /   {site.EstacionamientoAutos.Count} autos   /   {site.Muelles.Count} muelles", new Point(15, height - 22), "#52716A", 11);
        }
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private void Text(DrawingContext dc, string value, Point point, string color, double size)
        => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
}
