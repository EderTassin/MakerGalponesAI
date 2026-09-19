# MakerGalpones — trabajos por hacer

## Objetivo

Transformar requisitos en un proyecto desarrollado y documentado que el arquitecto pueda revisar, corregir y completar. La meta final es llegar a planos de construcción con las disciplinas y verificaciones necesarias resueltas. El piloto debe automatizar la mayor parte del trabajo repetitivo de una tipología acordada; el ahorro se medirá incluyendo revisión y retrabajo.

El arquitecto define criterios, valida resultados y corrige excepciones. Cada corrección debe conservarse para un proyecto o convertirse, después de revisarla, en una mejora del estándar. Regenerar no debe borrar esas correcciones.

## Base disponible en esta entrega

- Aplicación Windows independiente: `Galpones.Desktop`, C# / WPF / .NET 8.
- Formulario para terreno, nave, modulación, pendiente, tipos de familias y requisitos técnicos.
- Apertura y guardado YAML compatibles con el add-in existente.
- Motor `Galpones.Core` compartido por desktop y Revit.
- Vista de planta y axonometría del modelo intermedio: niveles, ejes, columnas y vigas.
- Vista de sitio (planta) interactiva, estilo TestFit: la nave se arrastra dentro del lote rectangular (con imán a los retiros) y se rota con doble clic; el solver regenera en vivo estacionamiento de autos (ángulo 45/60/90° configurable), cara de muelles (auto u override), patio de maniobra pesada y calle de acceso, con métricas de tabulación (cobertura, autos, muelles, profundidad de patio). La posición/orientación elegida se guarda en el YAML (`implantacion`) para alimentar la generación en Revit. Es una simulación de prefactibilidad orientativa (adelanto de SIT-01/SIT-02), no un plano de sitio ni una comprobación normativa: no calcula radios de giro reales ni retiros por ordenanza.
- Revisión preliminar del pack de zonificación. Se muestran las comprobaciones faltantes; no equivale a aprobación normativa.
- Exportación de revisión Markdown y modelo JSON en metros.
- Reutilización del selector de familias de Revit y de la importación desde `familias/` junto al YAML.
- Cierre del último pórtico cuando el largo no es múltiplo de la modulación.
- Protección en el nuevo conector contra una segunda generación en el mismo documento; todavía no es sincronización.

La conexión inicial es por archivo: guardar en desktop y seleccionar ese YAML en Revit. No hay comunicación en vivo ni recuperación de cambios del RVT. Las cargas y la potencia se conservan como requisitos: no dimensionan estructura, piso ni instalaciones.

## Backlog priorizado

| ID | Prioridad | Trabajo | Dependencia | Criterio de aceptación | Responsable funcional |
|---|---|---|---|---|---|
| DEF-01 | P0 | Seleccionar una tipología y dos proyectos de referencia de ML. | Modelos, planos y profesionales. | Inventario acordado de elementos, vistas, planillas y láminas que reproduce el piloto. | Arquitecto + ML |
| DEF-02 | P0 | Medir el proceso manual por tarea. | DEF-01 | Horas de modelado, documentación, revisión y corrección con una calidad comparable. | Arquitecto + producto |
| CAT-01 | P0 | Aprobar catálogo de familias, parámetros y plantilla Revit. | DEF-01 | Tipos inequívocos; unidades, hospedaje y parámetros verificados. Nada depende de elegir un tipo arbitrario. | BIM + ingeniería |
| GEO-01 | P0 | Precisar altura útil, niveles de apoyo, modulación y restricciones geométricas. | CAT-01 | Casos límite aprobados; altura útil descuenta elementos que ocupan el espacio. | Arquitecto + ingeniería |
| REV-01 | P0 | Probar desktop → YAML → Revit con familias reales. | CAT-01 | Coinciden dimensiones y cantidades; los fallos no dejan modelos parciales; resultado revisado dentro de Revit 2025. | Desarrollo + BIM |
| MOD-01 | P0 | Generar cubierta física, cerramientos, piso y aberturas. | CAT-01, GEO-01 | Objetos nativos editables con ubicación, hospedaje y parámetros correctos. | Desarrollo + BIM |
| DOC-01 | P0 | Crear plantas, cortes, fachadas y vistas de cubierta. | MOD-01 | Vistas acordadas con rangos, recortes, escalas y visibilidad correctos. | Desarrollo + arquitecto |
| DOC-02 | P0 | Automatizar cotas, etiquetas, planillas y láminas. | DOC-01 | Láminas legibles, sin superposiciones, y cantidades consistentes. | Desarrollo + arquitecto |
| SYN-01 | P0 | Identidades estables, revisiones y registro de elementos administrados. | REV-01 | Repetir una operación no duplica elementos; se distingue lo manual de lo generado. | Desarrollo |
| SYN-02 | P0 | Actualizar por diferencias y preservar correcciones. | SYN-01 | Cambiar dimensiones conserva ajustes protegidos y detecta conflictos antes de modificar. | Desarrollo + BIM |
| SYN-03 | P1 | Canal local desktop–Revit y cola de comandos. | SYN-01 | Destino explícito, respuestas verificadas, espera cuando Revit está ocupado y recuperación tras desconexión. | Desarrollo |
| SYN-04 | P1 | Recuperar cambios admitidos desde Revit. | SYN-02, SYN-03 | Contrato por propiedad; conflictos resueltos sin bucles. Pruebas de deshacer, rehacer, borrar y guardar como. | Desarrollo + BIM |
| NOR-01 | P0 | Aprobar reglas por jurisdicción. | DEF-01 | Fuentes y vigencia verificadas; uso, retiros, FOS, FOT y altura definidos. Distinguir aprobado, incumplido y sin información. | Profesional de la jurisdicción |
| CAL-01 | P1 | Integrar predimensionado y cálculo estructural. | CAT-01, GEO-01 | Hipótesis y cargas trazables; contraste con casos de ingeniería y revisión responsable. | Ingeniería + desarrollo |
| SIT-01 | P1 | Terreno poligonal y coordenadas compartidas. | GEO-01 | Unidades, origen, norte y georreferencia consistentes en un caso de control. | Topografía + BIM |
| SIT-02 | P2 | Comparar emplazamientos y circulación logística. | SIT-01, NOR-01 | Restricciones y métricas comparables; vehículos y maniobras definidos. | Operaciones ML + arquitecto |
| ALT-01 | P1 | Comparar alternativas paramétricas. | GEO-01, CAT-01 | Variantes reproducibles del mismo programa con cantidades y supuestos explícitos. | Desarrollo + arquitecto |
| IA-01 | P2 | Asistente para proponer cambios y explicar resultados. | Reglas y contratos estables. | Propuestas estructuradas, validadas y reversibles. Feedback registrado, sin modificar reglas automáticamente. | Desarrollo + arquitecto |
| OPS-01 | P1 | Empaquetar, instalar y actualizar desktop y conector. | REV-01 | Instalación repetible, compatibilidad por versión de Revit, logs y recuperación de errores. | Desarrollo + IT |
| VAL-01 | P0 | Medir ahorro sobre proyectos representativos. | MOD-01, DOC-02, SYN-02 | Tiempo total hasta la entrega revisada menor que la línea base, con igual calidad y alcance. | ML + arquitecto |

P0 indica lo necesario para demostrar ahorro significativo, no lo implementado hoy. P1 y P2 se ajustarán al tiempo que consume cada tarea. El mapa avanzado puede adelantarse si la factibilidad del terreno resulta ser el principal cuello de botella.

## Hitos

1. **Base de escritorio — entrega actual.** Abrir, editar, validar, guardar y visualizar el mismo proyecto que recibe Revit. Cerrar REV-01 con el profesional antes de considerar verificada la integración.
2. **Edificio revisable.** Una tipología con estructura, envolvente, piso y aberturas; catálogo aprobado y verificaciones pendientes explícitas.
3. **Paquete documental.** Vistas, cotas, etiquetas, planillas y láminas de los proyectos de referencia.
4. **Iteración confiable.** Actualizar sin duplicados ni pérdida silenciosa de correcciones. Probar las identidades desde el comienzo, aunque el producto completo llegue después.
5. **Validación de ahorro.** Dos o más proyectos, tiempos comparables y registro de retrabajo. El porcentaje objetivo se acuerda con la línea base; no se presenta como resultado medido por anticipado.
6. **Ampliación técnica.** Cálculo, georreferencia, sitio, instalaciones y asistente según prioridad validada.

## Contrato de correcciones

- **Excepción de proyecto:** conservar y mostrar como protegida al regenerar.
- **Cambio de parámetro:** recalcular las dependencias afectadas.
- **Cambio de estándar:** revisar y versionar antes de aplicarlo a otros proyectos.
- **Conflicto:** mostrar valor anterior, edición de Revit y propuesta de MakerGalpones. No elegir silenciosamente el último cambio.

Revit administra la documentación; MakerGalpones administra la intención de diseño. Los datos editables en ambos requieren una política por propiedad. Preservar un elemento no garantiza preservar sus referencias geométricas: cotas, etiquetas y hospedajes deben probarse.

## Comprobaciones de entrega

- Apertura y guardado sin alterar unidades, familias ni requisitos.
- Rechazo de datos inválidos antes de generar o exportar; vistas marcadas como pendientes cuando se edita un dato.
- Pórticos extremos y módulos residuales, además de múltiplos exactos.
- Ausencia de reglas o datos marcada como pendiente; no inferir cumplimiento.
- En Revit: plantilla vacía, niveles compatibles, familias faltantes, parámetros incompatibles y cancelación.
- Sincronización: cambios simultáneos, tipos, borrados, undo/redo, cierre inesperado y trabajo compartido.
- Documentación: revisión visual de cada lámina y comparación de cantidades con el modelo.

## Decisiones pendientes

Confirmar versión de Revit del cliente, familias, plantilla documental, tipología inicial, jurisdicciones, colaboración y política de datos. Comparar TestFit para emplazamiento e Informed Design para catálogos industriales antes de construir capacidades equivalentes. Sus funcionalidades anunciadas deben ensayarse sobre un caso real de ML.

Referencias de investigación:

- [TestFit Industrial](https://www.testfit.io/typologies/industrial)
- [Speckle Revit: conversiones y limitaciones](https://docs.speckle.systems/connectors/revit/revit)
- [Autodesk Informed Design](https://www.autodesk.com/learn/ondemand/curated/product-definition-with-informed-design/1RYDmqudKHCOgU7VFMuUO6)

## Arquitectura actual

`Galpones.Desktop` → `Galpones.Core` → archivo YAML → `Galpones.Revit` → elementos nativos.

El desktop funciona sin Revit abierto. El add-in requiere Revit para generar. La vista representa el modelo intermedio, no una lectura del RVT. La evolución incorporará transporte local, identidades y resolución de conflictos sin trasladar reglas de diseño al proceso de Revit.
