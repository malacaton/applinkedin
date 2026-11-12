# ADR-0003: Límites de la integración con LinkedIn

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

LinkedIn ofrece gratis un programador de publicaciones. Su API para un desarrollador individual es limitada: publicar en el propio nombre (`w_member_social`) es autoservicio, pero no se puede programar a fecha futura, crear artículos largos ni carruseles orgánicos, y leer las publicaciones propias está restringido. El acuerdo de usuario prohíbe automatizar la actividad en la web con bots o extensiones.

## Decisión

- **v1:** ninguna llamada a LinkedIn.
- **v2:** publicar ahora y editar el texto de un post propio por la API oficial; abrir una pieza en LinkedIn con un enlace en el navegador predeterminado.
- **Nunca:** controlar un navegador sobre LinkedIn, extraer datos de la web ni sincronizar desde LinkedIn.

## Consecuencias

- Programar sigue haciéndose con el programador nativo de LinkedIn; el estado "programado" lo marca el usuario.
- La URL de lo publicado manualmente se registra a mano una vez.
