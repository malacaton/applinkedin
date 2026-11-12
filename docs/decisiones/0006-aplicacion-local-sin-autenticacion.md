# ADR-0006: Aplicación solo local, sin autenticación y sin frameworks de aplicación

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

La herramienta la usa una sola persona en su propio equipo. La interfaz de la v2 se sirve en `localhost` y nunca se expone a la red.

## Decisión

- La API local y la interfaz escuchan solo en `localhost`.
- No hay gestión de usuarios, inicio de sesión ni autorización.
- No se usan plantillas ni frameworks de aplicación completos (por ejemplo, ABP). La solución se construye con proyectos .NET y Angular estándar, con las dependencias mínimas.

## Consecuencias

- Menos código y menos dependencias que mantener; el repositorio se entiende rápido.
- Si algún día se quisiera exponer en red, habría que revisar esta decisión antes (autenticación, CORS, HTTPS).
- Los únicos secretos posibles son los de la v2 (token de LinkedIn): se guardan en configuración local excluida de Git.
