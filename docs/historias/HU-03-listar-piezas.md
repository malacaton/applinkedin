# HU-03: Listar piezas con filtros

**Como** autor de contenido
**quiero** listar mis piezas filtrando por serie, estado y rango de fechas
**para** saber de un vistazo qué tengo pendiente y qué ya salió.

## Criterios de aceptación

- Desde la CLI se pueden listar todas las piezas o filtrarlas por serie, por estado y por rango de fechas, combinando filtros.
- Cada línea muestra al menos: fichero, serie, estado y fecha (prevista o publicada).
- El orden por defecto es cronológico; las piezas sin fecha van al final.
- Existe una salida en JSON para uso desde otras herramientas.
- Los avisos de HU-01 se muestran aparte, sin mezclarse con el listado.
