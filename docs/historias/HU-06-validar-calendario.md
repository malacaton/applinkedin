# HU-06: Validar el calendario

**Como** autor de contenido
**quiero** detectar choques, huecos sin cubrir y dependencias rotas
**para** corregirlos antes de que afecten a la publicación.

## Criterios de aceptación

- Se detectan dos piezas de la misma serie en la misma fecha.
- Se detectan fechas de cadencia sin pieza asignada dentro de un rango dado.
- Se detectan piezas cuya fecha es anterior a la de la pieza de la que dependen.
- Se detectan piezas bloqueadas: dependen de otra que aún no está lista.
- Cada problema indica tipo, piezas afectadas y fecha.
