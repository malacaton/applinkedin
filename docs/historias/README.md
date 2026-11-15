# Historias de usuario (v1)

Orden recomendado de desarrollo. Cada historia es la entrada de un ciclo SDD (ver [docs/README.md](../README.md)).

| HU | Título | Depende de | Estado |
| --- | --- | --- | --- |
| [HU-00](HU-00-esqueleto-solucion.md) | Esqueleto de la solución | — | Hecha |
| [HU-01](HU-01-catalogo.md) | Leer el catálogo de piezas | HU-00 | En curso |
| [HU-02](HU-02-series-y-cadencias.md) | Definir series y cadencias | HU-00 | En curso |
| [HU-03](HU-03-listar-piezas.md) | Listar piezas con filtros | HU-01, HU-02 | En curso |
| [HU-04](HU-04-cambiar-estado.md) | Cambiar el estado de una pieza | HU-01 | En curso |
| [HU-05](HU-05-buscar-huecos.md) | Buscar huecos libres | HU-02, HU-03 | En curso |
| [HU-06](HU-06-validar-calendario.md) | Validar el calendario | HU-05 | En curso |
| [HU-07](HU-07-crear-pieza.md) | Crear una pieza nueva | HU-01, HU-02 | En curso |
| [HU-08](HU-08-servidor-mcp.md) | Servidor MCP | HU-03 a HU-07 | En curso |
| [HU-09](HU-09-instalacion.md) | Instalación y uso con Claude Desktop | HU-08 | En curso |
| [HU-10](HU-10-skill-ejemplo.md) | Skill de ejemplo | HU-08 | En curso |

Los criterios de aceptación son el punto de partida: la especificación de cada historia puede refinarlos.
