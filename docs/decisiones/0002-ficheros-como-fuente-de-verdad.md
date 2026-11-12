# ADR-0002: Los ficheros Markdown son la fuente de verdad

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

El contenido ya existe como ficheros `.md` en una carpeta del usuario. Mantener una base de datos paralela obligaría a sincronizar dos fuentes.

## Decisión

- Cada pieza es un fichero `.md`. Sus metadatos (serie, estado, fechas, URL, dependencias) van en un bloque YAML al inicio del fichero.
- Las series y sus reglas de cadencia se definen en un fichero de configuración legible y editable a mano.
- La herramienta **solo modifica el bloque de metadatos o crea ficheros nuevos**. Nunca reescribe el cuerpo de un artículo.

## Consecuencias

- Sin base de datos ni servidor. El historial lo da Git si el usuario versiona su carpeta.
- Los ficheros sin bloque de metadatos se tratan como piezas "sin clasificar", sin error.
