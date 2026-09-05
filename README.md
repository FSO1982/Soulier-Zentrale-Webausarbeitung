# Soulier-Zentrale Webausarbeitung

Interne Projekt- und Implementierungsablage der Soulier-Zentrale.

## Status

Gate 3: ARBEITSSTAND. Dieses Repository enthält zunächst einen reversiblen Architektur-Skeleton/PoC. Produktivdeployment, IN-FORM-Schreibzugriff und Gate-3-Freigabe sind ausdrücklich nicht enthalten.

## Verbindliche Leitplanken

- .NET 10 / ASP.NET Core Backend
- React + TypeScript Admin-Frontend (späterer Ausbau)
- PostgreSQL 18
- Single-Tenant, modularer Monolith, Hexagonal / Ports & Adapters
- REST/JSON + OpenAPI als allgemeine API
- MCP als zusätzlicher AI-Client-Transport
- IN-FORM bleibt ERP-Source-of-Truth und V1 read-only
- n8n erhält keinen direkten DB- oder IN-FORM-Zugriff
- serverseitige Capability-, Scope- und Policy-Prüfung; Fail Closed

Die Projektakte und freigegebenen Baselines liegen im gemeinsamen Google-Drive-Arbeitsordner.
