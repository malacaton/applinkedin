# ADR-0001: Stack tecnológico

- **Estado:** Aceptada
- **Fecha:** 2026-11-12

## Contexto

La herramienta tiene un núcleo local (catálogo, cadencias, huecos), un servidor MCP por stdio, una CLI y, en la v2, una interfaz web local. El autor trabaja habitualmente con .NET en backend y con Angular en frontend.

## Decisión

- **Backend, núcleo, CLI y servidor MCP:** .NET 10 (LTS) y C#.
- **Servidor MCP:** SDK oficial `ModelContextProtocol` para C#, transporte stdio.
- **Frontend (v2):** Angular 22, servido por una API local en ASP.NET Core que reutiliza el mismo núcleo.

## Alternativas consideradas

- **.NET 11:** publicado en noviembre de 2026 como versión STS (dos años de soporte). Se descarta para empezar porque .NET 10 es LTS y el SDK de MCP ya publica binarios para net10.0. Se podrá migrar más adelante sin cambios de diseño.
- **TypeScript para todo:** ecosistema MCP muy amplio, pero obligaría a mantener el núcleo fuera del stack habitual del autor.

## Consecuencias

- Una única solución .NET con proyectos separados: núcleo, CLI, MCP y pruebas; la API web y el proyecto Angular se añaden en la v2.
- La lógica de negocio vive solo en el núcleo; CLI, MCP y API son capas finas sobre él.
