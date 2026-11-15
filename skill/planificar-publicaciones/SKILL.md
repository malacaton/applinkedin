---
name: planificar-publicaciones
description: Planifica, revisa y registra publicaciones y artículos para LinkedIn organizados en series, usando las herramientas del servidor MCP applinkedin. Úsala cuando la persona quiera ver su calendario de contenido, encontrar fechas libres para una serie, crear una pieza nueva, revisar un borrador antes de darlo por listo o marcar una pieza como programada o publicada.
---

# Planificar publicaciones con appLinkedIn

## Qué es appLinkedIn

- **Un catálogo local:** cada pieza (publicación, artículo o carrusel) es un fichero `.md` en la carpeta de la persona, con sus metadatos en un bloque YAML al principio (`serie`, `estado`, `fecha_prevista`, `fecha_publicada`, `url`, `depende_de`, `formato`).
- **Los ficheros son la fuente de verdad:** no hay base de datos aparte.
- **Las series** tienen una regla de publicación: una cadencia semanal o quincenal, o "N días después" de otra serie.
- **No publica en LinkedIn.** La persona programa y publica con el programador nativo de LinkedIn. Esta skill ayuda a decidir **qué** publicar y **cuándo**, y a registrarlo.

Estados de una pieza, en orden: `idea` → `borrador` → `listo` → `programado` → `publicado`.

## Cuándo usar cada herramienta

| Herramienta | Úsala para |
| --- | --- |
| `listar_piezas` | Saber qué hay antes de proponer nada. Admite filtros por `serie`, `estado` y fechas (`desde`, `hasta`), y `sin_clasificar` |
| `buscar_huecos` | Proponer fechas para una `serie`. Devuelve las próximas fechas libres según su regla |
| `validar_calendario` | Revisar el plan antes y después de cambiarlo: choques, huecos, piezas bloqueadas y dependencias rotas |
| `cambiar_estado` | Registrar avances: `estado`, `fecha_prevista`, `fecha_publicada` y `url` de una pieza (`fichero`) |
| `crear_pieza` | Crear el fichero de una idea nueva en su `serie`, con su `titulo`. Con `fecha_libre`, le asigna la primera fecha libre |

Las fechas se escriben `AAAA-MM-DD` o `hoy`.

## Reglas

1. **No inventes fechas.** Propón solo las que devuelva `buscar_huecos`, o deja que `crear_pieza` asigne una con `fecha_libre`.
2. **Confirma antes de escribir.** Antes de `cambiar_estado` o `crear_pieza`, resume en una frase qué vas a cambiar y pide confirmación. No hace falta si la persona ya te lo ha pedido de forma explícita ("márcala como publicada con esta URL").
3. **Marcar como publicado exige `fecha_publicada`.** Si no sabes la URL, pídela; si no la tiene aún, se puede añadir después.
4. **Explica los problemas en lenguaje llano.** Por cada problema de `validar_calendario`, di qué pasa y propón un arreglo concreto, por ejemplo: "el martes 24 no tiene pieza: ¿muevo ahí el borrador X o creo una nueva?".
5. **Lee los errores.** Si una herramienta devuelve un error, explícaselo a la persona y corrige la petición; no repitas la misma llamada.
6. **No toques el texto de las piezas.** Las herramientas solo cambian el bloque YAML. Si te piden redactar o mejorar un texto, propónlo en la conversación y que la persona lo copie en su fichero.
7. **Sigue los criterios editoriales.** Al redactar o revisar, aplica [criterios-editoriales.md](criterios-editoriales.md). Si chocan con lo que pida la persona, manda la persona.

## Flujos típicos

### Planificar las próximas semanas

1. `validar_calendario` para ver el estado actual (por defecto, cuatro semanas).
2. Para cada serie con huecos, `buscar_huecos`.
3. `listar_piezas` con `estado` `borrador` y `listo`, para ver qué hay preparado.
4. Propón un plan: qué pieza va en cada hueco y qué ideas faltan. Respeta las preferencias de calendario de los criterios.
5. Con la confirmación de la persona:
   - `cambiar_estado` para fijar la `fecha_prevista` de las piezas existentes;
   - `crear_pieza` para las ideas nuevas.
6. `validar_calendario` otra vez para comprobar el resultado.

### Tengo una idea

1. Si no está clara, ayuda a elegir la serie: usa la descripción de las series de los criterios.
2. Propón un título breve.
3. Con la confirmación, `crear_pieza` con `fecha_libre` si quiere fecha, o sin fecha si es solo una idea.

### Ya lo he publicado

1. `cambiar_estado` con `estado` `publicado`, `fecha_publicada` (normalmente `hoy`) y `url`.
2. Si la pieza tiene series que dependen de ella (por ejemplo, un resumen dos días después), `buscar_huecos` en esa serie y ofrece crear la pieza relacionada.

### Revisar un borrador antes de darlo por listo

1. Pide el texto o léelo si te lo comparten.
2. Revísalo con la lista de [criterios-editoriales.md](criterios-editoriales.md) y propone los cambios.
3. Cuando la persona lo dé por bueno, `cambiar_estado` a `listo`.
