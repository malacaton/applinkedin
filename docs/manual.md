# Manual de instalación y uso

Este manual explica cómo instalar appLinkedIn y cómo preparar tus ficheros. También cuenta cómo conectarlo a Claude Desktop (o a otro cliente MCP) y cómo probarlo con la carpeta de ejemplo del repositorio. Al final se explica la skill de ejemplo para el asistente.

## 1. Requisitos

- **.NET 10:** el SDK, para compilar. Para ejecutar basta el runtime, pero lo normal es instalar el SDK: [dotnet.microsoft.com/download](https://dotnet.microsoft.com/download).
- **Git**, para clonar el repositorio.
- **Un cliente MCP:** Claude Desktop (Windows o macOS), Claude Code u otro cliente compatible con MCP por stdio.
- **En Linux:** funciona la CLI y cualquier cliente MCP por stdio. Claude Desktop no tiene versión para Linux.

Comprueba la versión con `dotnet --version`. Debe empezar por `10.`.

## 2. Compilar

```bash
git clone https://github.com/malacaton/applinkedin.git
cd applinkedin
dotnet publish back/src/Ali.Mcp -c Release -o publicado/mcp
dotnet publish back/src/Ali.Cli -c Release -o publicado/cli
```

Quedan dos programas, con rutas estables que no cambian al recompilar:

| Programa | Fichero | Para qué |
| --- | --- | --- |
| Servidor MCP | `publicado/mcp/ali-mcp.dll` | Lo lanza tu cliente de IA |
| Línea de comandos | `publicado/cli/ali.dll` | Para usarlo desde la terminal |

Se ejecutan con `dotnet <fichero.dll>`. La carpeta `publicado/` está excluida de Git.

## 3. Tu carpeta de contenido

appLinkedIn trabaja sobre una carpeta con tus piezas en Markdown (`.md`), en tantas subcarpetas como quieras. Cada pieza lleva sus metadatos en un bloque YAML al principio:

```markdown
---
serie: casos-reales
estado: programado
fecha_prevista: 2026-11-17
formato: articulo
---
# La memoria de los agentes
```

| Campo | Valores |
| --- | --- |
| `serie` | `id` de una serie del fichero de series. Sin serie, la pieza queda "sin clasificar" |
| `estado` | `idea`, `borrador`, `listo`, `programado` o `publicado` |
| `fecha_prevista` / `fecha_publicada` | `AAAA-MM-DD` |
| `url` | Enlace a la pieza publicada |
| `depende_de` | Ruta de otra pieza (con o sin `.md`), o una lista de rutas |
| `formato` | Texto libre: `articulo`, `post`, `carrusel`... |

**Cómo se leen tus ficheros:**

- **Solo el bloque YAML:** la herramienta únicamente lee y cambia ese bloque; el texto de la pieza no se toca nunca.
- **Ficheros ignorados:** los ocultos, es decir, los que empiezan por `.` (como `.git` u `.obsidian`).
- **Campos desconocidos:** se respetan.
- **Valores no válidos:** generan un aviso, pero no detienen nada.

**Para empezar,** copia la carpeta [`ejemplo/`](../ejemplo/) del repositorio y adáptala.

## 4. El fichero de series

Las series y sus reglas de publicación van en `series.yaml`, en la raíz de tu carpeta de contenido:

```yaml
series:
  - id: casos-reales             # minúsculas, dígitos y guiones; es lo que ponen las piezas en 'serie'
    nombre: Casos reales
    carpeta: casos               # dónde crea 'ali crear' las piezas nuevas (por defecto, el id)
    fichero: "{nn}-{titulo}"     # nombre de las piezas nuevas (por defecto, este)
    cadencia:
      dias: martes               # un día o una lista: [lunes, jueves]
      frecuencia: semanal        # semanal o quincenal
      hora: "09:00"              # orientativa

  - id: fundamentos
    nombre: Fundamentos
    cadencia:
      dias: [lunes, jueves]
      frecuencia: quincenal
      desde: 2026-11-02          # obligatoria en quincenal: una semana "de publicación"

  - id: resumen-caso
    nombre: Resumen de un caso
    despues_de:                  # cada pieza va N días después de su pieza relacionada
      serie: casos-reales
      dias: 2

  - id: notas
    nombre: Notas sueltas        # sin cadencia: publicación libre
```

**Marcadores de `fichero`:**

| Marcador | Significado |
| --- | --- |
| `{n}`, `{nn}`, `{nnn}` | Número de orden, sin relleno o con relleno a 2 o 3 cifras |
| `{titulo}` | El título en minúsculas, sin tildes y con guiones. Es obligatorio |
| `{fecha}` | La fecha prevista |

**Otras reglas:**

- **Errores:** si el fichero tiene errores, se informa de todos a la vez, indicando la serie y el campo. Un nombre de campo mal escrito también es un error, para que una errata no pase inadvertida.
- **Otra ubicación:** puedes guardar el fichero en otro sitio e indicarlo con `--series` o con la variable `ALI_SERIES`.

## 5. Conectar con Claude Desktop

Abre el fichero de configuración de Claude Desktop. Si no existe, créalo.

| Sistema | Ruta |
| --- | --- |
| Windows | `%APPDATA%\Claude\claude_desktop_config.json` |
| macOS | `~/Library/Application Support/Claude/claude_desktop_config.json` |

También puedes llegar a él desde Claude Desktop: **Configuración → Desarrollador → Editar configuración**.

Añade `applinkedin` dentro de `mcpServers`, con tus rutas.

**Windows.** En JSON, cada `\` se escribe `\\`:

```json
{
  "mcpServers": {
    "applinkedin": {
      "command": "dotnet",
      "args": [
        "C:\\ruta\\a\\applinkedin\\publicado\\mcp\\ali-mcp.dll",
        "--contenido", "C:\\ruta\\a\\tus\\articulos"
      ]
    }
  }
}
```

**macOS:**

```json
{
  "mcpServers": {
    "applinkedin": {
      "command": "dotnet",
      "args": [
        "/Users/tu-usuario/applinkedin/publicado/mcp/ali-mcp.dll",
        "--contenido", "/Users/tu-usuario/articulos"
      ]
    }
  }
}
```

**Opciones del servidor:**

- **Fichero de series en otro sitio:** añade `"--series", "<ruta>"` a `args`.
- **Carpeta de contenido:** es obligatoria. El servidor no usa la carpeta actual, porque Claude Desktop lo arranca desde una carpeta imprevisible.

## 6. Comprobar que funciona

1. Cierra Claude Desktop del todo y vuelve a abrirlo.
2. En el menú de herramientas de la conversación debe aparecer **applinkedin** con cinco herramientas: `listar_piezas`, `buscar_huecos`, `validar_calendario`, `cambiar_estado` y `crear_pieza`.
3. Prueba a preguntar, por ejemplo:
   - "¿Qué piezas tengo programadas o listas?"
   - "Propón las tres próximas fechas libres de casos-reales desde el 15 de noviembre de 2026."
   - "Valida mi calendario del 15 al 30 de noviembre de 2026."
   - "Crea un caso real titulado «Evaluar agentes con trazas» en la primera fecha libre."

Las fechas de `ejemplo/` son fijas, de noviembre y diciembre de 2026. Para verlas como futuras, pide las búsquedas "desde el 15 de noviembre de 2026", o cambia las fechas de las piezas. La sección 8 muestra lo que debe salir con el ejemplo.

**Antes de escribir,** Claude Desktop pide tu permiso para usar las herramientas `cambiar_estado` y `crear_pieza`. Ninguna borra ni sobrescribe ficheros.

## 7. Otros clientes

- **Claude Code:**

  ```bash
  claude mcp add applinkedin -- dotnet /ruta/a/applinkedin/publicado/mcp/ali-mcp.dll --contenido /ruta/a/tus/articulos
  ```

- **Cualquier cliente MCP por stdio:** el comando es `dotnet` y los argumentos son `<ruta>/ali-mcp.dll --contenido <carpeta> [--series <fichero>]`. También puedes indicar la carpeta con la variable `ALI_CONTENIDO`.

## 8. Usar la CLI

La CLI hace lo mismo que las herramientas, desde la terminal:

```bash
dotnet publicado/cli/ali.dll <comando> [opciones]
```

Indica la carpeta con `-c <carpeta>` o con la variable `ALI_CONTENIDO`. Si no indicas ninguna, se usa la carpeta actual. `--help` muestra la ayuda de cada comando. Los ejemplos de esta sección se ejecutan sobre `ejemplo/`.

**Listar piezas** (`ali listar`, con filtros `-s`, `-e`, `--sin-clasificar`, `--desde`, `--hasta` y `--json`):

```
FECHA       ESTADO      SERIE         FICHERO
2026-11-10  publicado   casos-reales  casos/01-agentes-en-produccion.md
2026-11-16  listo       fundamentos   fundamentos/01-que-es-un-token.md
2026-11-17  programado  casos-reales  casos/02-memoria-de-los-agentes.md
—           borrador    casos-reales  casos/03-evaluar-un-rag.md
—           idea        —             notas/ideas-sueltas.md
—           idea        resumen-caso  resumenes/01-agentes-en-produccion.md
—           idea        resumen-caso  resumenes/03-evaluar-un-rag.md

7 piezas
```

`ali listar -e programado -e listo`:

```
FECHA       ESTADO      SERIE         FICHERO
2026-11-16  listo       fundamentos   fundamentos/01-que-es-un-token.md
2026-11-17  programado  casos-reales  casos/02-memoria-de-los-agentes.md

2 piezas
```

**Buscar huecos libres** (`ali huecos <serie>`, con `--desde`, `-n` y `--json`). Una fecha está ocupada cuando la serie ya tiene ese día una pieza `listo`, `programado` o `publicado`.

`ali huecos casos-reales --desde 2026-11-15 -n 3`:

```
Huecos de casos-reales (martes, semanal) desde 2026-11-15:

FECHA       DÍA     COINCIDE CON
2026-11-24  martes  —
2026-12-01  martes  —
2026-12-08  martes  —
```

`ali huecos resumen-caso --desde 2026-11-15` (serie con `despues_de`):

```
Huecos de resumen-caso (2 días después de casos-reales) desde 2026-11-15:

FECHA       DÍA     TRAS                                COINCIDE CON
2026-11-19  jueves  casos/02-memoria-de-los-agentes.md  —
```

**Validar el calendario** (`ali validar`, con `--desde`, `--hasta` —por defecto, 28 días—, `-s` y `--json`).

`ali validar --desde 2026-11-15 --hasta 2026-11-30`:

```
Calendario del 2026-11-15 al 2026-11-30: 5 problemas

FECHA       TIPO       SERIE         PIEZAS                                                      DETALLE
2026-11-19  hueco      fundamentos   —                                                           Toca publicar y no hay pieza.
2026-11-19  hueco      resumen-caso  casos/02-memoria-de-los-agentes.md                          Falta la pieza de resumen-caso que va 2 días después de casos/02-memoria-de-los-agentes.md.
2026-11-24  hueco      casos-reales  —                                                           Toca publicar y no hay pieza.
2026-11-30  hueco      fundamentos   —                                                           Toca publicar y no hay pieza.
—           bloqueada  resumen-caso  resumenes/03-evaluar-un-rag.md, casos/03-evaluar-un-rag.md  Depende de casos/03-evaluar-un-rag.md, que está en borrador.
```

**Tipos de problema:**

| Tipo | Qué significa |
| --- | --- |
| `choque` | Dos piezas de una serie el mismo día |
| `hueco` | Un día de publicación sin pieza |
| `fuera_de_cadencia` | Una pieza en un día que no es de su cadencia |
| `desfase` | Distancia distinta de la que pide `despues_de` |
| `orden` | Una pieza va antes que aquella de la que depende |
| `bloqueada` | Depende de una pieza que aún no está lista |
| `dependencia_rota` | `depende_de` apunta a algo que no existe |
| `serie_desconocida` | La `serie` de la pieza no está en el fichero de series |

**Crear una pieza** (`ali crear <serie> "<título>"`, con `--fecha`, `--fecha-libre`, `--desde`, `--formato` y `--depende-de`).

`ali crear casos-reales "Evaluar agentes con trazas" --fecha-libre --desde 2026-11-15`:

```
Creada casos/04-evaluar-agentes-con-trazas.md
  serie: casos-reales
  estado: idea
  fecha_prevista: 2026-11-24
```

**Cambiar el estado** (`ali estado <fichero>`, con `-e`, `--fecha-prevista`, `--fecha-publicada` y `--url`). Las fechas admiten `hoy`.

`ali estado casos/02-memoria-de-los-agentes -e publicado --fecha-publicada 2026-11-17 --url https://www.linkedin.com/pulse/ejemplo-memoria`:

```
Actualizado casos/02-memoria-de-los-agentes.md:
  estado: programado → publicado
  fecha_publicada: — → 2026-11-17
  url: — → https://www.linkedin.com/pulse/ejemplo-memoria
```

## 9. Problemas frecuentes

| Problema | Causa y solución |
| --- | --- |
| Las herramientas no aparecen en Claude Desktop | El JSON de configuración no es válido, o la ruta del `.dll` es incorrecta. Revisa el registro del servidor: `%APPDATA%\Claude\logs\mcp-server-applinkedin.log` en Windows, `~/Library/Logs/Claude/mcp-server-applinkedin.log` en macOS |
| `error: No existe la carpeta de contenido` | Ruta mal escrita o, en Windows, con `\` sin duplicar en el JSON |
| `El fichero de series ... No existe el fichero de series` | Falta `series.yaml` en tu carpeta de contenido, o la ruta de `--series` es incorrecta. `listar_piezas` funciona sin él |
| `dotnet` no se encuentra | El SDK o el runtime de .NET 10 no está instalado o no está en el `PATH`. En `command` puedes poner la ruta completa a `dotnet` (por ejemplo, `C:\\Program Files\\dotnet\\dotnet.exe`) |
| No veo un cambio que hice a mano | Las piezas y las series se leen en cada llamada, así que no hace falta reiniciar. Solo hay que reiniciar el cliente si cambias su configuración |

## 10. Privacidad

- **Todo es local.** El servidor y la CLI solo leen y escriben en tu carpeta de contenido. No se conectan a Internet ni a LinkedIn (ADR-0003 y ADR-0005).
- **Lo que llega al modelo de IA:** tu cliente de IA sí envía al modelo lo que lee con las herramientas (metadatos y rutas de tus piezas), según sus propias condiciones de uso.

## 11. Skill de ejemplo

La carpeta [`skill/planificar-publicaciones/`](../skill/planificar-publicaciones/) contiene una skill para tu asistente. Le explica cuándo usar cada herramienta y qué criterios editoriales seguir, para que no tengas que repetirlo en cada conversación. Tiene dos ficheros:

| Fichero | Contenido |
| --- | --- |
| `SKILL.md` | Qué es appLinkedIn, cuándo usar cada herramienta, reglas (no inventar fechas, confirmar antes de escribir…) y flujos típicos |
| `criterios-editoriales.md` | Criterios de ejemplo: longitudes por formato, tono, tus series, preferencias de calendario y lista de revisión. **Personalízalo** |

### Instalar en Claude Desktop

1. Comprime la carpeta `planificar-publicaciones` (la carpeta entera, no solo su contenido):

   - Windows (PowerShell):

     ```powershell
     Compress-Archive -Path skill\planificar-publicaciones -DestinationPath planificar-publicaciones.zip
     ```

   - macOS:

     ```bash
     cd skill && zip -r ../planificar-publicaciones.zip planificar-publicaciones
     ```

2. En Claude Desktop, ve a **Configuración → Capacidades → Skills**, sube el `.zip` y actívala.

### Instalar en Claude Code

Copia la carpeta a tus skills personales, o a las del proyecto donde trabajes:

```bash
cp -r skill/planificar-publicaciones ~/.claude/skills/
```

### Adaptarla

- **Tus criterios:** edita `criterios-editoriales.md` con tus longitudes, tu tono, la descripción de **tus** series y tus preferencias de calendario. `SKILL.md` solo hay que tocarlo si cambias cómo quieres que use las herramientas.
- **Tras cada cambio:** en Claude Desktop, vuelve a comprimirla y subirla. En Claude Code, basta con guardar el fichero.

### Comprobar que se activa

Pide, por ejemplo: "Planifica mis próximas dos semanas de LinkedIn". Con la skill activa, el asistente empieza revisando el calendario (`validar_calendario`) y buscando huecos (`buscar_huecos`). Antes de crear o cambiar ninguna pieza, te enseña el plan y pide confirmación.
