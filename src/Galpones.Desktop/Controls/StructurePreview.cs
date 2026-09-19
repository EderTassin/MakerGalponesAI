using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Galpones.Core.IntermediateModel;

namespace Galpones.Desktop.Controls;

/// <summary>Vista ortográfica de la geometría del core; no depende de Revit ni de un navegador.</summary>
public sealed class StructurePreview : FrameworkElement
{
    public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(nameof(Model), typeof(GalponModel), typeof(StructurePreview), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsPlanProperty = DependencyProperty.Register(nameof(IsPlan), typeof(bool), typeof(StructurePreview), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public GalponModel? Model { get => (GalponModel?)GetValue(ModelProperty); set => SetValue(ModelProperty, value); }
    public bool IsPlan { get => (bool)GetValue(IsPlanProperty); set => SetValue(IsPlanProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var width = ActualWidth; var height = ActualHeight;
        if (width < 100 || height < 35) return;
        dc.DrawRoundedRectangle(Brush("#F8FBFA"), null, new Rect(0, 0, width, height), 8, 8);
        if (Model is not { Columnas.Count: > 0 } model)
        {
            Text(dc, "Sin modelo · revisá los datos de entrada", new Point(24, height / 2), "#61767F", 13);
            return;
        }

        var length = model.Columnas.Max(c => c.X); var span = model.Columnas.Max(c => c.Y);
        var eaves = model.Levels.Single(l => l.Nombre == "Cubierta").ElevacionMetros;
        var ridge = model.Levels.Single(l => l.Nombre == "Cumbrera").ElevacionMetros;
        var points = model.Vigas.SelectMany(v => new[] { v.Inicio, v.Fin })
            .Concat(model.Columnas.Select(c => new Punto3D(c.X, c.Y, 0))).ToArray();
        Point Raw(Punto3D p) => IsPlan ? new Point(p.X, -p.Y) : new Point((p.X - p.Y) * 0.82, (p.X + p.Y) * 0.30 - p.Z);
        var raw = points.Select(Raw).ToArray();
        var minX = raw.Min(p => p.X); var minY = raw.Min(p => p.Y);
        var deltaX = Math.Max(0.01, raw.Max(p => p.X) - minX); var deltaY = Math.Max(0.01, raw.Max(p => p.Y) - minY);
        var verticalPadding = height < 180 ? 28 : 100;
        var scale = Math.Min((width - 78) / deltaX, (height - verticalPadding) / deltaY);
        var offsetX = (width - deltaX * scale) / 2; var offsetY = (height - deltaY * scale) / 2 - 8;
        Point Project(Punto3D p) { var r = Raw(p); return new((r.X - minX) * scale + offsetX, (r.Y - minY) * scale + offsetY); }
        void Line(Punto3D a, Punto3D b, Pen pen) => dc.DrawLine(pen, Project(a), Project(b));

        var outline = new StreamGeometry();
        using (var context = outline.Open())
        {
            context.BeginFigure(Project(new(0, 0, 0)), true, true);
            context.PolyLineTo([Project(new(length, 0, 0)), Project(new(length, span, 0)), Project(new(0, span, 0))], true, false);
        }
        dc.DrawGeometry(Brush("#E9F2EF"), new Pen(Brush("#C8DDD5"), 1), outline);
        var gridPen = new Pen(Brush("#B8CBC7"), 0.8) { DashStyle = DashStyles.Dash };
        foreach (var grid in model.Grids)
        {
            if (grid.Direction == GridDirection.Y) Line(new(grid.PosicionMetros, 0, 0), new(grid.PosicionMetros, grid.LongitudMetros, 0), gridPen);
            else Line(new(0, grid.PosicionMetros, 0), new(grid.LongitudMetros, grid.PosicionMetros, 0), gridPen);
        }
        var columnPen = new Pen(Brush("#129783"), 2.2);
        var beamPen = new Pen(Brush("#294958"), 2);
        foreach (var column in model.Columnas.OrderBy(c => c.X + c.Y))
        {
            var foot = Project(new(column.X, column.Y, 0));
            if (!IsPlan) Line(new(column.X, column.Y, 0), new(column.X, column.Y, eaves), columnPen);
            dc.DrawEllipse(Brush("#138D7B"), null, foot, IsPlan ? 3.5 : 2.3, IsPlan ? 3.5 : 2.3);
        }
        foreach (var beam in model.Vigas) Line(beam.Inicio, beam.Fin, beamPen);
        if (!IsPlan)
        {
            var referencePen = new Pen(Brush("#80B2A7"), 1) { DashStyle = DashStyles.Dash };
            Line(new(0, 0, eaves), new(length, 0, eaves), referencePen);
            Line(new(0, span, eaves), new(length, span, eaves), referencePen);
            Line(new(0, span / 2, ridge), new(length, span / 2, ridge), referencePen);
        }
        if (height >= 180)
        {
            Text(dc, IsPlan ? "PLANTA ESTRUCTURAL" : "AXONOMETRÍA ESTRUCTURAL", new Point(15, 12), "#708782", 10);
            Text(dc, $"L {length:0.##} m   /   A {span:0.##} m   /   Cumbrera {ridge:0.##} m", new Point(15, height - 28), "#52716A", 11);
        }
    }

    private static SolidColorBrush Brush(string hex) => new((Color)ColorConverter.ConvertFromString(hex));
    private void Text(DrawingContext dc, string value, Point point, string color, double size)
        => dc.DrawText(new FormattedText(value, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("Segoe UI"), size, Brush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip), point);
}
