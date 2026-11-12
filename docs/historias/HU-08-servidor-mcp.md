# HU-08: Servidor MCP

**Como** autor de contenido
**quiero** usar el catálogo, los huecos y los cambios de estado desde mi asistente de IA
**para** planificar y redactar conversando, con mis datos reales.

## Criterios de aceptación

- Un servidor MCP local por stdio expone las herramientas: `listar_piezas`, `buscar_huecos`, `validar_calendario`, `cambiar_estado` y `crear_pieza`.
- Cada herramienta reutiliza el núcleo sin duplicar lógica (ADR-0001).
- La carpeta de contenido y el fichero de series se indican al arrancar el servidor.
- Las herramientas de escritura cumplen las mismas reglas que la CLI (HU-04, HU-07).
- Cada herramienta tiene una descripción clara de qué hace y de sus parámetros.

## Fuera de alcance

- Servidor MCP remoto (HTTP).
