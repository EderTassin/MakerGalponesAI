# Verificación de la base desktop

Fecha: 19 de septiembre de 2026.

## Resultado

- Solución completa compilada en Release: 0 errores, 0 advertencias.
- 36 pruebas del motor aprobadas.
- Comprobación WPF aprobada: modelo inicial, zona no confirmada, edición que marca vista pendiente, coma decimal, pórtico extremo, módulo residual, familias conservadas al guardar y reabrir, errores visibles y recuperación después de corregirlos.
- Documento de trabajos pendientes incluido en la salida de la aplicación.
- Seis capturas del árbol visual WPF generadas en `artifacts/qa`: diseño, planta, error, revisión, entrega y ventana compacta. Se inspeccionaron visualmente; se corrigió un recorte de la vista en ventanas pequeñas.

## Regresiones corregidas

- Largo 62 m con modulación 6 m: ahora el pórtico extremo está en 62 m y el último tramo mide 2 m.
- Cocientes decimales: no generan un módulo casi nulo por redondeo de coma flotante.
- Serialización YAML: se conservan los nombres `altura_libre`, `pendiente_cubierta_pct`, `perfil_columna` y `perfil_viga` que espera el add-in.
- Datos no finitos, secciones nulas y cantidades desmesuradas de módulos se rechazan antes de generar.
- Los campos YAML desconocidos producen error en vez de perderse silenciosamente al guardar.

## Límites de esta verificación

El conector se compiló contra las DLL instaladas de Revit 2025. No se ejecutó la generación dentro de Revit ni se verificaron familias reales, cotas, hospedajes o trabajo compartido. Las capturas corresponden a la aplicación WPF, no a Revit.

La comunicación actual es por archivo. No hay sincronización bidireccional, cálculo estructural ni generación de planos de construcción. El siguiente control de integración está definido como REV-01 en el plan de trabajo.

## Repetir

```powershell
dotnet build GalponesRevit.slnx -c Release
dotnet test tests/Galpones.Core.Tests/Galpones.Core.Tests.csproj -c Release --no-build
dotnet run --project tests/Galpones.Desktop.SmokeTests/Galpones.Desktop.SmokeTests.csproj -c Release --no-build -- artifacts/qa
```
