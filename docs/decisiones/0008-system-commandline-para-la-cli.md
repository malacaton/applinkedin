# ADR-0008: System.CommandLine para la CLI

- **Estado:** Aceptada
- **Fecha:** 2026-11-14

## Contexto

La CLI tendrá varios subcomandos (`listar`, `estado`, `huecos`, `validar`, `crear`), con opciones que se repiten (`--serie`, `--estado`), alias cortos y ayuda. Interpretar todo eso a mano es código repetitivo y propenso a errores.

## Decisión

Usar **System.CommandLine** 3.0 en `Ali.Cli`. Es la biblioteca oficial de Microsoft para interpretar argumentos (licencia MIT, sin dependencias transitivas).

- Las opciones se reciben como texto. La CLI valida estados y fechas para dar los mismos mensajes en español que el núcleo.
- La salida y los errores se escriben en `TextWriter` inyectados, para poder probar los comandos en el mismo proceso.

## Alternativas consideradas

- **Interpretación manual:** sin dependencias, pero habría que escribir y mantener ayuda, alias, repetición de opciones y mensajes de error de sintaxis.
- **Spectre.Console.Cli:** más vistosa, pero añade una dependencia mayor que no hace falta para una salida de texto simple.

## Consecuencias

- Una dependencia más, solo en la CLI; el núcleo y el servidor MCP no la usan.
- La ayuda y los errores de sintaxis salen en el idioma del sistema, gracias a las traducciones que trae la biblioteca. Los mensajes propios de la herramienta están siempre en español.
