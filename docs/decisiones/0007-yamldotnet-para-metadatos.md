# ADR-0007: YamlDotNet para leer los metadatos

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

Los metadatos de cada pieza van en un bloque YAML al inicio del fichero (ADR-0002). El núcleo necesita interpretarlo con precisión: tipos de nodo (texto, lista, mapa), claves duplicadas y la posición de los errores, para dar avisos útiles. Hasta ahora el núcleo no tenía dependencias externas, y ADR-0006 pide mantenerlas al mínimo.

## Decisión

Usar **YamlDotNet** en `Ali.Core`, con su modelo de representación (`YamlStream`) en lugar de deserializar a clases, para validar cada campo y avisar por separado.

## Alternativas consideradas

- **Parser propio para `clave: valor`:** sin dependencias, pero no cubre listas, comillas, comentarios ni bloques multilínea, y daría avisos falsos con YAML válido.
- **Deserializar a una clase con YamlDotNet:** menos código, pero un solo campo con tipo incorrecto hace fallar todo el bloque, y la historia pide avisar campo a campo.

## Consecuencias

- Una dependencia de licencia MIT y sin dependencias transitivas. Es la biblioteca YAML más extendida en .NET.
- La escritura del bloque (HU-04) podrá usar la misma biblioteca o escribir el YAML a mano. Se decidirá en esa historia, con la prioridad de no alterar el cuerpo del fichero.
