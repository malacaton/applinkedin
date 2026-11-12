# HU-04: Cambiar el estado de una pieza

**Como** autor de contenido
**quiero** marcar una pieza como lista, programada o publicada, con su fecha y su URL
**para** que el catálogo refleje lo que ya he hecho en LinkedIn.

## Criterios de aceptación

- Se puede cambiar `estado`, `fecha_prevista`, `fecha_publicada` y `url` de una pieza identificada por su fichero.
- Solo se modifica el bloque YAML; el cuerpo del fichero queda idéntico byte a byte.
- Si el fichero no tenía bloque YAML, se crea al inicio sin alterar el resto.
- Marcar como `publicado` exige `fecha_publicada`; la `url` es opcional.
- Un estado o una fecha inválidos se rechazan sin modificar el fichero.
