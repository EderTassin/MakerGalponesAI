using System.Reflection;
using Autodesk.Revit.UI;

namespace Galpones.Revit;

public sealed class App : IExternalApplication
{
    private const string TabName = "Galpones";

    public Result OnStartup(UIControlledApplication application)
    {
        application.CreateRibbonTab(TabName);
        var panel = application.CreateRibbonPanel(TabName, "Generador");

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        var buttonData = new PushButtonData(
            "GenerarGalpon",
            "Generar\ngalpón",
            assemblyPath,
            typeof(Commands.GenerarGalponCommand).FullName);

        panel.AddItem(buttonData);

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application) => Result.Succeeded;
}
