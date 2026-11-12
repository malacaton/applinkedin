# ADR-0005: La IA vive en el cliente, no en el núcleo

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Decisión

El núcleo no llama a ningún modelo de IA. Es determinista y se prueba con tests. Las capacidades se exponen por MCP y es el cliente (Claude u otro) quien aporta la IA. El criterio editorial se entrega como una skill de ejemplo.

## Consecuencias

- Usar la herramienta no tiene coste; quien quiera una IA de pago la conecta por su cuenta.
- No hay agentes propios en la v1: el cliente decide qué herramienta llamar.
