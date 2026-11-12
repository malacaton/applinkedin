# HU-07: Crear una pieza nueva

**Como** autor de contenido
**quiero** crear el fichero de una pieza nueva con el nombre y los metadatos correctos
**para** no tener que recordar la convención de nombres ni el formato del bloque YAML.

## Criterios de aceptación

- Dada una serie y un título, se crea el `.md` con el nombre según la convención configurada y un bloque YAML con `serie` y `estado: idea`.
- La numeración sigue a la última pieza de la serie.
- Si ya existe un fichero con ese nombre, no se sobrescribe y se informa del conflicto.
- Opcionalmente se puede asignar la primera fecha libre de la serie (HU-05).
