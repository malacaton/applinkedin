# HU-00: Esqueleto de la solución

**Como** desarrollador del proyecto
**quiero** una solución .NET 10 con la estructura de proyectos acordada y pruebas automáticas
**para** que cada historia siguiente tenga dónde encajar sin rehacer la base.

## Criterios de aceptación

- La solución contiene proyectos separados para núcleo, CLI, servidor MCP y pruebas (ADR-0001).
- `dotnet build` y `dotnet test` funcionan desde la raíz sin pasos adicionales.
- Existe un test de ejemplo que pasa.
- El `.gitignore` excluye las salidas de compilación de .NET.
- Un flujo de integración continua en GitHub Actions compila y ejecuta las pruebas en cada push.

## Fuera de alcance

- Cualquier funcionalidad de negocio.
- Proyectos de la v2 (API web, Angular).
