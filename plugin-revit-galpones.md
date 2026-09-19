# Plugin Revit: generador de proyectos de galpones

Análisis de factibilidad, arquitectura propuesta y plan de piloto.

## 1. Requerimiento

Construir un plugin para Revit que, a partir de un input de datos, genere un proyecto. Ejemplo: un galpón de depósito ubicado en un lote determinado, con dimensiones, instalaciones eléctricas y capacidad de carga del piso definidas, que respete la normativa de edificación de Córdoba, Argentina.

Acuerdos posteriores:

- El trabajo se hace junto con arquitectos.
- La auditoría y el cumplimiento normativo quedan a cargo del arquitecto.
- El input puede tener un esquema rígido; no hace falta lenguaje natural.
- Objetivo inicial: que la herramienta resuelva al menos el 50% del proyecto y el especialista lo termine. A futuro, llegar al 80–90%.

## 2. Factibilidad

| Aspecto | Factible | Comentario |
|---|---|---|
| Generar el modelo desde un input | Sí | La Revit API es C#/.NET (Revit 2025+ sobre .NET 8). Un galpón es una tipología repetitiva, lo que simplifica. |
| Input estructurado (YAML/Excel) | Sí | Parte simple del problema. |
| Cumplimiento normativo automático | Parcial | Requiere traducir normativa a reglas ejecutables con un experto de dominio. Queda fuera del piloto. |
| Cálculo estructural (carga del piso) | No con Revit solo | Revit modela, no dimensiona. Hace falta un motor propio de predimensionado o integrarse con software de cálculo (p. ej. Robot Structural Analysis). |
| Documentación aprobable | No | Lo que genera la herramienta es un anteproyecto. La documentación para aprobar siempre lleva firma de un profesional matriculado. |

### Normativa involucrada (referencia)

- Código de edificación y zonificación del municipio correspondiente (cada municipio de Córdoba tiene el suyo: retiros, FOS/FOT, alturas).
- CIRSOC: estructura y cargas.
- AEA 90364: instalaciones eléctricas.
- Ley 19.587 / Decreto 351/79: higiene, seguridad e incendio en establecimientos.

## 3. Principio de diseño clave

**La herramienta no interpreta reglamentos crudos (PDFs) en tiempo de ejecución.** Los lineamientos se traducen una sola vez a reglas estructuradas y versionadas, que los arquitectos mantienen. Interpretar documentos libres en cada corrida no es reproducible ni auditable.

Se puede usar un LLM offline para proponer borradores de reglas a partir de la normativa, siempre con revisión y aprobación del arquitecto antes de incorporarlas.

Conviene separar en capas distintas:

- **Normativa general** (código municipal, CIRSOC, AEA): packs de reglas reutilizables.
- **Especificaciones del proyecto** (lo que pide cada cliente): input del proyecto.

## 4. Estructura de carpeta propuesta

```
proyecto-galpon-X/
├── proyecto.yaml          # input del proyecto (esquema rígido)
├── reglas/
│   ├── cordoba-capital/   # pack por jurisdicción, versionado
│   │   ├── zonificacion.yaml
│   │   ├── incendio.yaml
│   │   └── estructura.yaml
│   └── comunes/           # CIRSOC, AEA, Ley 19.587
├── familias/              # .rfa de perfiles, aberturas, etc.
└── plantilla.rte
```

### Ejemplo de input

```yaml
# proyecto.yaml
tipologia: nave_deposito
jurisdiccion: cordoba-capital
lote: { frente: 40, fondo: 80, zona: "industrial-2" }
nave: { largo: 60, ancho: 25, altura_libre: 8 }
estructura: { tipo: porticos_metalicos, modulacion: 6 }
piso: { sobrecarga_kN_m2: 30 }
electrico: { potencia_kVA: 150, tension: trifasica }
```

Alternativa: Excel como input, probablemente más amigable para los arquitectos.

### Tipos de regla

Cada regla lleva referencia al artículo de origen, para trazabilidad.

- **Derivación:** calcula valores (p. ej. cantidad y distancia máxima de salidas según superficie y ocupación).
- **Restricción:** valida y marca incumplimientos (retiros, altura máxima, FOS).
- **Default:** completa lo que el input no especifica.

## 5. Arquitectura

1. **Core en .NET puro:** lee el input, valida el esquema, aplica reglas y genera un modelo intermedio (ejes, pórticos, muros, aberturas, en coordenadas).
2. **Adaptador Revit (add-in delgado):** traduce el modelo intermedio a elementos de Revit.
3. **Reporte:** reglas aplicadas, incumplimientos y artículo de referencia.

**Por qué separar el core de Revit:** la Revit API solo corre dentro del proceso de Revit y es difícil de testear. Con el core aislado, la mayor parte de la lógica tiene tests unitarios normales, y se puede apuntar a otros destinos (IFC, o Autodesk Platform Services Design Automation para ejecución headless en la nube) sin reescribir.

## 6. Plan del piloto

### Definición de éxito

El "50%" se mide en **horas de modelado ahorradas**, no en cantidad de elementos generados.

Criterios:

- ≥50% de horas ahorradas en al menos 2 proyectos reales.
- Cero retrabajo por errores del generador (es preferible que no modele algo a que lo modele mal).
- Lista priorizada de brechas para la fase 2.

### Paso 0: línea base (1 semana)

Tomar 2–3 galpones ya hechos por los arquitectos y registrar cuántas horas llevó modelarlos y en qué se fueron: estructura, envolvente, aberturas, vistas, láminas, instalaciones. Ese desglose define qué automatizar primero.

### Alcance del piloto (8–10 semanas)

**Incluye:**

- Una sola tipología: nave de depósito con pórticos metálicos, cubierta a dos aguas, planta rectangular.
- Input YAML o Excel con esquema rígido.
- Output: niveles, grillas, pórticos, correas, cerramientos, cubierta, piso, portones y aberturas, más vistas y planos básicos.
- Arquitectura core .NET + add-in separada desde el inicio.

**Excluye:**

- Reglas normativas automáticas (los parámetros los define el arquitecto en el input).
- Instalaciones.
- Cálculo estructural.

### Instrumentación

Al cerrar cada proyecto, comparar el modelo generado con el modelo entregado: qué elementos modificó, borró o agregó el arquitecto. Revit expone IDs de elemento, así que el diff es automatizable. Ese diff es el backlog priorizado con datos reales para las fases siguientes.

## 7. Roadmap

| Fase | Objetivo | Contenido |
|---|---|---|
| Piloto | ≥50% | Generador paramétrico de nave, vistas y planos básicos |
| Fase 2 | ~70% | Láminas completas, cómputo métrico, MEP básico (luminarias por grilla, tableros), reglas de derivación simples |
| Fase 3 | 80–90% | Packs de reglas por jurisdicción, predimensionado estructural, variantes de tipología (entrepisos, oficinas anexas, naves múltiples) |

Cada 10% adicional cuesta más que el anterior: el último tramo son casos especiales, criterio de diseño y detalles que varían por proyecto. Puede que el óptimo costo/beneficio esté alrededor del 75%. La instrumentación del piloto es lo que permite decidirlo con números.

## 8. Estimaciones gruesas

Supuesto: 1 desarrollador con C#, sin experiencia previa en Revit API.

| Componente | Esfuerzo |
|---|---|
| Generador paramétrico (input → modelo), incluida curva de aprendizaje de la API | 6–10 semanas |
| Primer pack de reglas de una jurisdicción | 2–4 semanas (depende de los arquitectos) |
| Motor de validación para un municipio y subconjunto de reglas | 8–12 semanas, con experto de dominio part-time |
| Predimensionado estructural y eléctrico por tablas | 8+ semanas, con validación profesional |

## 9. Riesgos

- Dependencia de familias de Revit (perfiles, aberturas) que debe proveer el cliente o el equipo de arquitectura.
- Cambios en la normativa, que obligan a versionar los packs de reglas.
- Variabilidad entre municipios.
- Expectativas mal calibradas si el alcance no se acota por escrito desde el inicio.
- Responsabilidad profesional: dejar documentado que la salida es un anteproyecto sujeto a revisión y firma de un matriculado.

## 10. Preguntas abiertas

- ¿La salida esperada es un anteproyecto para cotizar o documentación lista para presentar en el municipio?
- ¿Qué municipio o municipios cubre el primer pack de reglas?
- ¿Input en YAML o en Excel?
- ¿Qué versión de Revit usan los arquitectos?
- ¿Quién provee y mantiene la biblioteca de familias?
