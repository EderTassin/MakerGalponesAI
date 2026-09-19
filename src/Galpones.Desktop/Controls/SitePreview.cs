using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Galpones.Core.Site;

namespace Galpones.Desktop.Controls;

/// <summary>
/// Vista de sitio interactiva estilo TestFit: la nave se arrastra con el mouse dentro del lote
/// (con imán a los retiros) y se rota con doble clic; el solver regenera estacionamiento, patio
/// de maniobra y accesos en vivo. Es geometría orientativa, no un plano de sitio aprobado.
/// </summary>
public sealed class SitePreview : FrameworkElement
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(SiteLayoutModel), typeof(SitePreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public SiteLayoutModel? Model { get => (SiteLayoutModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }

    /// <summary>El usuario soltó/arrastró la nave a una nueva posición (metros, coords del lote).</summary>
    public event Action<double, double>? PlacementChanged;

    /// <summary>El usuario pidió rotar la nave (doble clic o botón).</summary>
    public event Action? RotateRequested;

    private const double SnapToleranciaM = 0.6;

    private double _scale = 1, _offsetX, _offsetY;
    private bool _dragging;
    private Point _agarreLote; // offset del mouse respecto del origen de la nave, en metros

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (Model is not { LoteFrenteM: > 0, LoteFondoM: > 0 } site) return;
        var lot = ToLot(e.GetPosition(this));
        if (!DentroDeNave(site, lot)) return;
        if (e.ClickCount == 2)
        {
            RotateRequested?.Invoke();
            return;
        }
        _dragging = true;
        _agarreLote = new Point(lot.X - site.Nave.X, lot.Y - site.Nave.Y);
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (Model is not { LoteFrenteM: > 0, LoteFondoM: > 0 } site) return;
        var lot = ToLot(e.GetPosition(this));
        if (!_dragging)
        {
            Cursor = DentroDeNave(site, lot) ? Cursors.SizeAll : Cursors.Arrow;
            return;
        }
        var x = Clamp(lot.X - _agarreLote.X, 0, Math.Max(0, site.LoteFrenteM - site.Nave.Ancho));
        var y = Clamp(lot.Y - _agarreLote.Y, 0, Math.Max(0, site.LoteFondoM - site.Nave.Profundidad));
        x = Snap(x, site.GuiasSnapX);
        y = Snap(y, site.GuiasSnapY);
        if (Math.Abs(x - site.Nave.X) < 0.001 && Math.Abs(y - site.Nave.Y) < 0.001) return;
        PlacementChanged?.Invoke(x, y);
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        if (!_dragging) return;
        _dragging = false;
        ReleaseMouseCapture();
    }

    private bool DentroDeNave(SiteLayoutModel site, Point lot)
        => lot.X >= site.Nave.X && lot.X <= site.Nave.X + site.Nave.Ancho
           && lot.Y >= site.Nave.Y && lot.Y <= site.Nave.Y + site.Nave.Profundidad;

    private Point ToLot(Point screen) => new((screen.X - _offsetX) / _scale, (screen.Y - _offsetY) / _scale);
    private static double Clamp(double v, double min, double max) => Math.Min(Math.Max(v, min), max);
    private static double Snap(double v, IReadOnlyList<double> guias)
    {
        foreach (var g in guias)
            if (Math.Abs(v - g) <= SnapToleranciaM)
                return g;
        return v;
    }

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

        var topReserved = height < 180 ? 14 : 56;
        var bottomReserved = height < 180 ? 6 : 44;
        _scale = Math.Min((width - 40) / site.LoteFrenteM, (height - topReserved - bottomReserved) / site.LoteFondoM);
        _offsetX = (width - site.LoteFrenteM * _scale) / 2;
        _offsetY = topReserved + (height - topReserved - bottomReserved - site.LoteFondoM * _scale) / 2;

        Rect Project(RectDef r) => new(_offsetX + r.X * _scale, _offsetY + r.Y * _scale, r.Ancho * _scale, r.Profundidad * _scale);
        Point ProjectPoint(double x, double y) => new(_offsetX + x * _scale, _offsetY + y * _scale);

        // Lote y guías de retiro
        var lote = new Rect(_offsetX, _offsetY, site.LoteFrenteM * _scale, site.LoteFondoM * _scale);
        dc.DrawRectangle(Brush("#EFF4F1"), new Pen(Brush("#B9CCC5"), 1) { DashStyle = DashStyles.Dash }, lote);
        var guiaPen = new Pen(Brush("#BFD8CE"), 1) { DashStyle = new DashStyle(new[] { 4.0, 4.0 }, 0) };
        foreach (var gx in site.GuiasSnapX)
        {
            var p1 = ProjectPoint(gx, 0); var p2 = ProjectPoint(gx, site.LoteFondoM);
            dc.DrawLine(guiaPen, p1, p2);
        }
        foreach (var gy in site.GuiasSnapY)
        {
            var p1 = ProjectPoint(0, gy); var p2 = ProjectPoint(site.LoteFrenteM, gy);
            dc.DrawLine(guiaPen, p1, p2);
        }

        // Calle de acceso pesado y patio de maniobra
        if (site.CorredorAccesoPesado is { } corredor)
            dc.DrawRectangle(Brush("#FDEBD3"), new Pen(Brush("#E4C491"), 1) { DashStyle = DashStyles.Dash }, Project(corredor));
        if (site.PatioManiobraPesada is { } patio)
        {
            dc.DrawRectangle(Brush("#FCE3C4"), new Pen(Brush("#E0AE68"), 1.2) { DashStyle = DashStyles.Dash }, Project(patio));
            var pr = Project(patio);
            if (pr.Width > 90 && pr.Height > 26)
                Text(dc, $"PATIO DE MANIOBRA · {site.Metricas.ProfundidadPatioM:0.#} m",
                    new Point(pr.Left + 6, pr.Top + 6), site.Metricas.ProfundidadPatioM < 30 ? "#B04A17" : "#9A6A2F", 10);
        }

        // Estacionamiento de autos (plazas rotadas según el ángulo configurado)
        foreach (var plaza in site.EstacionamientoAutos)
            DibujarPlaza(dc, plaza);

        // Nave (arrastrable)
        var naveRect = Project(site.Nave);
        dc.DrawRectangle(Brush("#D8ECE5"), new Pen(Brush("#129783"), 1.8), naveRect);
        var gridPen = new Pen(Brush("#8FC2B6"), 0.8) { DashStyle = DashStyles.Dash };
        var rotada = site.NaveRotacionGrados is 90 or 270;
        foreach (var eje in site.EjesEstructuralesM)
        {
            if (rotada)
            {
                var x = _offsetX + (site.Nave.X + eje) * _scale;
                dc.DrawLine(gridPen, new Point(x, naveRect.Top), new Point(x, naveRect.Bottom));
            }
            else
            {
                var y = _offsetY + (site.Nave.Y + eje) * _scale;
                dc.DrawLine(gridPen, new Point(naveRect.Left, y), new Point(naveRect.Right, y));
            }
        }
        Text(dc, "NAVE", new Point(naveRect.Left + 6, naveRect.Top + 6), "#0E6E5E", 11);

        // Cotas de implantación respecto del frente
        if (height >= 180)
        {
            var origenFrente = ProjectPoint(0, 0);
            var naveOrigen = ProjectPoint(site.Nave.X, site.Nave.Y);
            dc.DrawLine(new Pen(Brush("#7C9A92"), 0.8) { DashStyle = DashStyles.Dash },
                new Point(naveOrigen.X, origenFrente.Y - 4), new Point(naveOrigen.X, naveOrigen.Y));
            Text(dc, $"x {site.Nave.X:0.##} m", new Point(naveOrigen.X + 4, naveOrigen.Y - 16), "#52716A", 10);
            Text(dc, $"y {site.Nave.Y:0.##} m", new Point(naveRect.Right - 44, naveOrigen.Y + 12), "#52716A", 10);
        }

        // Muelles sobre la cara activa
        var muellePen = new Pen(Brush("#294958"), 2.4);
        var pasoMuellesPx = site.Muelles.Count > 1
            ? Math.Sqrt(Math.Pow(site.Muelles[1].X - site.Muelles[0].X, 2) + Math.Pow(site.Muelles[1].Y - site.Muelles[0].Y, 2)) * _scale
            : double.MaxValue;
        foreach (var muelle in site.Muelles)
        {
            var punto = ProjectPoint(muelle.X, muelle.Y);
            var (dx, dy) = site.CaraDeMuelles is CaraMuelles.Frente or CaraMuelles.Fondo ? (5.0, 0.0) : (0.0, 5.0);
            dc.DrawLine(muellePen, new Point(punto.X - dx, punto.Y - dy), new Point(punto.X + dx, punto.Y + dy));
            if (height >= 220 && pasoMuellesPx >= 26) Text(dc, muelle.Nombre, new Point(punto.X - 8, punto.Y + 7), "#52716A", 9);
        }

        if (height >= 180)
        {
            Text(dc, "SIMULACIÓN DE SITIO · PLANTA", new Point(15, 12), "#708782", 10);
            Text(dc, "Arrastrá la nave · doble clic para rotar · orientativo, no normativo", new Point(15, 28), "#8A5916", 10);
            DibujarLeyenda(dc, width);
            DibujarMetricas(dc, site, height);
        }
    }

    private void DibujarPlaza(DrawingContext dc, PlazaDef plaza)
    {
        var rect = new Rect(_offsetX + plaza.X * _scale, _offsetY + plaza.Y * _scale, plaza.Ancho * _scale, plaza.Profundidad * _scale);
        if (plaza.Angulo is >= 89 and <= 91)
        {
            dc.DrawRectangle(Brush("#DCE8F5"), new Pen(Brush("#7FA6CC"), 1), rect);
            return;
        }
        // Celda de hilera tenue + huella del vehículo rotada según el ángulo.
        dc.DrawRectangle(Brush("#EAF1F8"), null, rect);
        var filasEnX = plaza.Ancho <= plaza.Profundidad;
        var centro = new Point(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);
        var giro = (90 - plaza.Angulo) * (filasEnX ? 1 : -1);
        dc.PushTransform(new RotateTransform(giro, centro.X, centro.Y));
        var vehiculo = new Rect(centro.X - 1.25 * _scale, centro.Y - 2.5 * _scale, 2.5 * _scale, 5.0 * _scale);
        dc.DrawRectangle(Brush("#DCE8F5"), new Pen(Brush("#7FA6CC"), 1), vehiculo);
        dc.Pop();
    }

    private void DibujarLeyenda(DrawingContext dc, double width)
    {
        var x = width - 210; var y = 10.0;
        LeyendaItem(dc, ref x, y, "#D8ECE5", "Nave");
        LeyendaItem(dc, ref x, y, "#DCE8F5", "Autos");
        LeyendaItem(dc, ref x, y, "#FCE3C4", "Pesado");
    }

    private void LeyendaItem(DrawingContext dc, ref double x, double y, string color, string texto)
    {
        dc.DrawRectangle(Brush(color), new Pen(Brush("#9FB3AE"), 0.7), new Rect(x, y + 2, 10, 10));
        x += 14;
        Text(dc, texto, new Point(x, y), "#52716A", 10);
        x += texto.Length * 6.5 + 14;
    }

    private void DibujarMetricas(DrawingContext dc, SiteLayoutModel site, double height)
    {
        var m = site.Metricas;
        var autos = m.AutosSolicitados is { } s ? $"{m.AutosColocados}/{s} autos" : $"{m.AutosColocados} autos";
        var patioColor = m.ProfundidadPatioM is > 0 and < 30 ? "#B04A17" : "#52716A";
        var y = height - 40;
        dc.DrawRectangle(Brush("#FFFFFF"), new Pen(Brush("#DAE3E5"), 1), new Rect(12, y - 6, Math.Min(ActualWidth - 24, 640), 34));
        Text(dc, $"Cobertura {m.CoberturaPct:0.#}%", new Point(24, y + 3), "#152F39", 11);
        Text(dc, autos, new Point(150, y + 3), "#152F39", 11);
        Text(dc, $"{m.MuellesColocados} muelles ({site.CaraDeMuelles.ToString().ToLowerInvariant()})", new Point(245, y + 3), "#152F39", 11);
        Text(dc, $"patio {m.ProfundidadPatioM:0.#} m", new Point(420, y + 3), patioColor, 11);
        Text(dc, $"lote {site.LoteFrenteM:0.##} × {site.LoteFondoM:0.##} m", new Point(505, y + 3), "#52716A", 11);
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private void Text(DrawingContext dc, string value, Point point, string color, double size)
        => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
}
