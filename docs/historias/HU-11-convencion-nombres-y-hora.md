# HU-11: Convención de nombres propia y hora de publicación

**Como** autor de contenido que ya tiene una carpeta organizada a su manera
**quiero** que la herramienta cree y renombre las piezas siguiendo mi convención de nombres, y que registre a qué hora se publica cada una
**para** no adaptar mi carpeta a la herramienta y poder comparar resultados entre horarios.

## Criterios de aceptación

- La plantilla de nombres admite la fecha en formato corto (`AAMMDD`), el título tal como se escribió y la raíz de la carpeta de contenido como destino.
- La numeración de una pieza nueva sale de los nombres existentes según la plantilla de su serie, no solo de las cifras iniciales.
- Una serie puede tomar el número de la pieza de la que depende (por ejemplo, un post de jueves que acompaña al del martes).
- Cada pieza puede llevar su hora de publicación (`hora`). Se lee, se lista, se cambia y se indica al crear.
- Si cambia la fecha de una pieza cuyo nombre lleva la fecha, se renombra el fichero sin borrar ni sobrescribir nada.
