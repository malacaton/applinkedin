# HU-01: Leer el catálogo de piezas

**Como** autor de contenido
**quiero** que la herramienta lea mi carpeta de artículos en Markdown
**para** tener todas mis piezas y su estado en un solo sitio sin mantener otra base de datos.

## Criterios de aceptación

- Dada una carpeta, se leen todos los `.md`, incluidas subcarpetas.
- Si un fichero empieza por un bloque YAML, se leen al menos: `serie`, `estado`, `fecha_prevista`, `fecha_publicada`, `url`, `depende_de`, `formato`.
- Un fichero sin bloque YAML aparece como pieza "sin clasificar", sin error.
- Un bloque YAML mal formado no detiene la lectura: la pieza se marca con un aviso que indica fichero y motivo.
- Los estados válidos son `idea`, `borrador`, `listo`, `programado` y `publicado`; cualquier otro valor genera un aviso.
- La lectura nunca modifica ningún fichero.

## Fuera de alcance

- Escribir metadatos (HU-04).
- Detección de carruseles por nombre de imagen (v2).
