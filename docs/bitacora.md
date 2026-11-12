# Bitácora

Problemas encontrados y cómo se resolvieron, del más reciente al más antiguo.

| Fecha | Problema | Causa | Solución |
| --- | --- | --- | --- |
| 2026-11-12 | `git push` rechazado con "fetch first" | El repositorio de GitHub ya tenía un commit inicial creado al darlo de alta | `git pull origin main --allow-unrelated-histories --no-rebase` y conservar las versiones locales en los conflictos |
| 2026-11-12 | `git commit` falla con "unable to auto-detect email address" | Identidad de Git sin configurar en el equipo | `git config --global user.name` y `user.email` (email privado de GitHub para no exponer el real) |
