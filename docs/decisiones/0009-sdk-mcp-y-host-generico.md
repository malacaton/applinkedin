# ADR-0009: SDK oficial de MCP y host genérico de .NET para el servidor

- **Estado:** Aceptada
- **Fecha:** 2026-11-15

## Contexto

HU-08 expone el núcleo como servidor MCP local por stdio. ADR-0001 ya eligió el SDK oficial de MCP para C#. Falta fijar la versión y lo que ese SDK necesita para el transporte stdio.

## Decisión

- **`ModelContextProtocol` 2.4.0:** el SDK oficial, con licencia MIT y binarios para `net10.0`. Se usa solo en `Ali.Mcp`.
- **`Microsoft.Extensions.Hosting` 10.0.x:** el host genérico de .NET, al que el SDK se integra para el transporte stdio. También se usa solo en `Ali.Mcp`.
- **Contrato compartido con la CLI:** el formato JSON de salida y la lectura de fechas con `hoy` pasan al núcleo (`Ali.Core.Output` y `Ali.Core.Input`). Así las dos capas devuelven exactamente lo mismo sin duplicar código. Solo usan `System.Text.Json`, que viene con .NET.

## Alternativas consideradas

- **Implementar el protocolo a mano** (JSON-RPC sobre stdio): sin dependencias, pero obligaría a seguir la evolución de la especificación de MCP por nuestra cuenta.
- **SDK 2.3.0** (octubre de 2026): algo más rodada, pero sin ventajas concretas frente a la 2.4.0.
- **Proyecto compartido aparte** (`Ali.Shared`) para el contrato JSON: más estructura para una pieza pequeña.

## Consecuencias

- **Encaje con ADR-0006:** el host genérico es la infraestructura estándar de .NET para procesos de larga duración, no un framework de aplicación. Por eso no contradice esa decisión.
- **Núcleo y CLI:** no reciben dependencias nuevas.
- **Código de presentación en el núcleo:** el núcleo contiene ahora los documentos JSON de salida. Son registros simples, sin lógica de negocio.
