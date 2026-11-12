# Documentación

| Carpeta / fichero | Contenido |
| --- | --- |
| [decisiones/](decisiones/) | Registro de decisiones de arquitectura (ADR): qué se decidió, por qué y qué se descartó |
| [historias/](historias/) | Historias de usuario (HU) de la v1, punto de partida de cada ciclo SDD |
| [bitacora.md](bitacora.md) | Problemas encontrados durante el desarrollo y cómo se resolvieron |

## Proceso de trabajo (SDD)

Cada funcionalidad sigue el mismo ciclo, partiendo de una historia de usuario:

1. **Historia** (`historias/HU-NN-*.md`): qué se quiere y por qué, con criterios de aceptación.
2. **Especificación**: comportamiento detallado, casos límite y contrato (entradas, salidas, errores).
3. **Plan**: diseño técnico, ficheros afectados y pruebas necesarias.
4. **Tareas**: pasos pequeños y verificables.
5. **Implementación y verificación** contra los criterios de aceptación.

Las decisiones relevantes que surjan se registran como ADR y los problemas, en la bitácora.
