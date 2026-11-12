# Bitácora

Problemas encontrados y cómo se resolvieron, del más reciente al más antiguo.

| Fecha | Problema | Causa | Solución |
| --- | --- | --- | --- |
| 2026-11-12 | `dotnet test` falla con "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK" | xUnit v3 4.x usa Microsoft.Testing.Platform, y el SDK de .NET 10 exige activarlo explícitamente para `dotnet test` | Añadir `"test": { "runner": "Microsoft.Testing.Platform" }` a `global.json` y quitar `Microsoft.NET.Test.Sdk` y `xunit.runner.visualstudio`, que ya no hacen falta |
| 2026-11-12 | `git push` rechazado con "fetch first" | El repositorio de GitHub ya tenía un commit inicial creado al darlo de alta | `git pull origin main --allow-unrelated-histories --no-rebase` y conservar las versiones locales en los conflictos |
| 2026-11-12 | `git commit` falla con "unable to auto-detect email address" | Identidad de Git sin configurar en el equipo | `git config --global user.name` y `user.email` (email privado de GitHub para no exponer el real) |
