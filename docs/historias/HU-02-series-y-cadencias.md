# HU-02: Definir series y cadencias

**Como** autor de contenido
**quiero** describir mis series y con qué cadencia se publica cada una
**para** que la herramienta sepa cuándo toca publicar y pueda proponerme fechas.

## Criterios de aceptación

- Las series se definen en un fichero de configuración legible y editable a mano (ADR-0002).
- Cada serie tiene un identificador, un nombre y una regla de cadencia: días de la semana, frecuencia (semanal, quincenal…) y hora orientativa.
- Una serie puede no tener cadencia (publicación libre).
- Una serie puede declarar que sus piezas van después de una pieza de otra serie (por ejemplo, "dos días después de su pieza relacionada").
- Una configuración inválida produce un error claro que indica qué serie y qué campo fallan.

## Fuera de alcance

- Festivos o vacaciones (posible mejora posterior).
