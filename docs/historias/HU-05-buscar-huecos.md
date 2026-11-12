# HU-05: Buscar huecos libres

**Como** autor de contenido
**quiero** que la herramienta me proponga las próximas fechas libres de una serie
**para** planificar sin revisar el calendario a mano.

## Criterios de aceptación

- Dada una serie y una fecha de inicio, se devuelven las N próximas fechas que cumplen su cadencia y no tienen ya una pieza asignada.
- Se tienen en cuenta piezas en estado `listo`, `programado` y `publicado` con fecha.
- Si la serie no tiene cadencia, se informa de que no se pueden calcular huecos.
- El resultado es el mismo para los mismos datos de entrada (determinista y probado con tests).
