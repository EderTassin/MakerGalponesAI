using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Galpones.Revit;

/// <summary>
/// Revit 2025 carga todos los add-ins en el mismo AssemblyLoadContext, que admite una sola versión
/// por DLL. Si otro add-in cargó antes otra versión de una de nuestras dependencias (p. ej. pyRevit
/// trae YamlDotNet 15 y nosotros usamos 16), el bind falla con FileLoadException 0x80131621.
/// Este resolver carga las dependencias de terceros de la carpeta del add-in en un contexto privado,
/// sin tocar las versiones que usan los demás add-ins.
/// </summary>
internal static class AddinDependencyResolver
{
    private static readonly AssemblyLoadContext Contexto = new("Galpones.Dependencias");
    private static readonly Dictionary<string, (Version? Version, string Ruta)> Dependencias =
        new(StringComparer.OrdinalIgnoreCase);

    public static void Register()
    {
        var carpeta = Path.GetDirectoryName(typeof(AddinDependencyResolver).Assembly.Location)!;
        foreach (var dll in Directory.EnumerateFiles(carpeta, "*.dll"))
        {
            AssemblyName nombre;
            try
            {
                nombre = AssemblyName.GetAssemblyName(dll);
            }
            catch (BadImageFormatException)
            {
                continue; // DLL nativa
            }

            // Nuestros ensamblados quedan en el contexto de Revit: el add-in comparte sus tipos.
            if (nombre.Name is null || nombre.Name.StartsWith("Galpones.", StringComparison.OrdinalIgnoreCase))
                continue;
            Dependencias[nombre.Name] = (nombre.Version, dll);
        }

        AssemblyLoadContext.Default.Resolving += Resolver;
    }

    public static void Unregister() => AssemblyLoadContext.Default.Resolving -= Resolver;

    private static Assembly? Resolver(AssemblyLoadContext contexto, AssemblyName pedido)
    {
        // Solo la versión exacta que trae el add-in: otro add-in que pida otra versión sigue su propio camino.
        if (pedido.Name is null || !Dependencias.TryGetValue(pedido.Name, out var local) || pedido.Version != local.Version)
            return null;

        lock (Contexto)
        {
            return Contexto.Assemblies.FirstOrDefault(a => string.Equals(a.GetName().Name, pedido.Name, StringComparison.OrdinalIgnoreCase))
                ?? Contexto.LoadFromAssemblyPath(local.Ruta);
        }
    }
}
