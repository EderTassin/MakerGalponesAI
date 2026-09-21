using System.Reflection;
using Autodesk.Revit.UI;

namespace Galpones.Revit;

public sealed class App : IExternalApplication
{
    private const string TabName = "Galpones";

    public Result OnStartup(UIControlledApplication application)
    {
        // Antes de que cualquier comando toque Galpones.Core (y por lo tanto YamlDotNet).
        AddinDependencyResolver.Register();

        application.CreateRibbonTab(TabName);
        var panel = application.CreateRibbonPanel(TabName, "Generador");

        var assemblyPath = Assembly.GetExecutingAssembly().Location;
        panel.AddItem(new PushButtonData(
            "ImportarFamilias",
            "Importar\nfamilias",
            assemblyPath,
            typeof(Commands.ImportarFamiliasCommand).FullName));

        panel.AddItem(new PushButtonData(
            "GenerarGalpon",
            "Generar\ngalpón",
            assemblyPath,
            typeof(Commands.GenerarGalponCommand).FullName));

        return Result.Succeeded;
    }

    public Result OnShutdown(UIControlledApplication application)
    {
        AddinDependencyResolver.Unregister();
        return Result.Succeeded;
    }
}
