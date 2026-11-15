# Bitácora

Problemas encontrados y cómo se resolvieron, del más reciente al más antiguo.

| Fecha | Problema | Causa | Solución |
| --- | --- | --- | --- |
| 2026-11-15 | Claude Desktop rechaza el `.zip` de la skill: "Zip file contains path with invalid characters" | `Compress-Archive` de Windows PowerShell 5.1 guarda las rutas internas con `\`, y el formato zip exige `/` | En el manual, crear el `.zip` con el `tar.exe` de Windows (`tar -a -c -f ...`), que usa `/` |
| 2026-11-14 | `ali estado casos/02.MD` renombraba el fichero `02.md` a `02.MD` | En Windows, `File.Move` con sobrescritura crea el destino con las mayúsculas de la ruta recibida, aunque el sistema de ficheros no las distinga | Resolver cada segmento de la ruta con su nombre real en disco antes de escribir; test específico para Windows |
| 2026-11-12 | `dotnet test` falla con "Testing with VSTest target is no longer supported by Microsoft.Testing.Platform on .NET 10 SDK" | xUnit v3 4.x usa Microsoft.Testing.Platform, y el SDK de .NET 10 exige activarlo explícitamente para `dotnet test` | Añadir `"test": { "runner": "Microsoft.Testing.Platform" }` a `global.json` y quitar `Microsoft.NET.Test.Sdk` y `xunit.runner.visualstudio`, que ya no hacen falta |
| 2026-11-12 | `git push` rechazado con "fetch first" | El repositorio de GitHub ya tenía un commit inicial creado al darlo de alta | `git pull origin main --allow-unrelated-histories --no-rebase` y conservar las versiones locales en los conflictos |
| 2026-11-12 | `git commit` falla con "unable to auto-detect email address" | Identidad de Git sin configurar en el equipo | `git config --global user.name` y `user.email` (email privado de GitHub para no exponer el real) |
