# MakerGalpones

Aplicación Windows para configurar galpones, revisar una estructura preliminar y continuar en Revit. El objetivo es producir un proyecto documentado que el arquitecto pueda corregir y validar.

- [Trabajos pendientes y criterios de aceptación](docs/PLAN-DE-TRABAJO.md)
- [Análisis inicial del prototipo](plugin-revit-galpones.md) — histórico; el plan vigente es el anterior.

## Abrir la aplicación

Ejecutar `Abrir-MakerGalpones.cmd`. Usa el ejecutable de `artifacts/desktop` si está disponible; de lo contrario ejecuta el proyecto desde el código fuente.

```powershell
dotnet run --project src/Galpones.Desktop/Galpones.Desktop.csproj
```

Requisitos: Windows y .NET 8 Desktop Runtime para el ejecutable. Para compilar: SDK compatible con `net8.0-windows` (la solución `.slnx` requiere SDK reciente). El desktop no necesita Revit instalado.

## Funcionalidades

Formulario, apertura y guardado YAML, niveles/ejes/pórticos, vistas de planta y axonometría, revisión preliminar y exportación Markdown/JSON. Acepta coma o punto decimal, sin separadores de miles. Una zona vacía o desconocida se muestra como pendiente de revisión.

Las cargas todavía no dimensionan elementos. El pack normativo es un borrador. No se generan cerramientos, cubierta física, instalaciones ni láminas. La vista representa líneas estructurales y no refleja familias ni modificaciones del RVT.

## Continuar en Revit 2025

1. Guardar el proyecto en desktop o elegir **Preparar para Revit**.
2. Con Revit cerrado, instalar el conector actualizado:

```powershell
dotnet build src/Galpones.Revit/Galpones.Revit.csproj -c Release -p:DeployRevitAddin=true
```

3. Abrir un proyecto con plantilla estructural y usar **Galpones → Generar galpón**.
4. Seleccionar el YAML y elegir familias. Se pueden cargar `.rfa` desde `familias/` junto al YAML.

La instalación escribe en `%APPDATA%/Autodesk/Revit/Addins/2025`. Una compilación normal no instala nada. Para otra ruta usar `-p:RevitInstallDir=...`; otras versiones requieren validación específica.

El nuevo conector marca documentos generados y bloquea una segunda generación para evitar duplicaciones. No actualiza modelos ni sincroniza cambios. Abrir otro documento para cada alternativa. Un modelo creado con el conector anterior no tiene esa marca.

## Compilar y probar

```powershell
dotnet build GalponesRevit.slnx -c Release
dotnet test tests/Galpones.Core.Tests/Galpones.Core.Tests.csproj -c Release
dotnet run --project tests/Galpones.Desktop.SmokeTests/Galpones.Desktop.SmokeTests.csproj -c Release -- artifacts/qa
dotnet publish src/Galpones.Desktop/Galpones.Desktop.csproj -c Release -o artifacts/desktop
```

La solución completa necesita DLL de Revit 2025; el desktop y sus pruebas compilan por separado. Las pruebas de escritorio generan capturas de su propio árbol visual WPF y verifican edición, errores, exportación y reapertura. No sustituyen las pruebas dentro de Revit con familias reales.

## Organización

- `src/Galpones.Core`: datos, validación, generación, YAML y revisión.
- `src/Galpones.Desktop`: aplicación WPF independiente.
- `src/Galpones.Revit`: elementos nativos, selección e importación de familias.
- `reglas`: packs en revisión.
- `docs`: alcance y backlog.
- `tests`: pruebas del motor y del desktop.
