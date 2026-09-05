# MCP-Codex-Pilot 0.1

Status: ARBEITSSTAND

## Ziel

Codex erhält über einen internen MCP-Transport ausschließlich die bereits freigegebenen Soulier-Zentrale-Capabilities. MCP ist Transport/Toolbeschreibung, nicht Autorisierungsquelle.

## V1-Pilottools

### `soulier_knowledge_search`

- Capability: `knowledge.search:v1`
- Eingabe: `query`, `resourceScope`, optional `maxResults`
- Server erzwingt Clientstatus, Grant, Environment, Scope, Daten-/AI-Policy und KnowledgeRelease.
- Ausgabe enthält nur freigegebene Metadaten/Ergebnis-Snippets und Versionsreferenzen.

### `soulier_knowledge_read`

- Capability: `knowledge.read:v1`
- Eingabe: `documentVersionId`, `resourceScope`, `maxChars`
- Zugriff ausschließlich auf exakt freigegebene Version.
- Eine neue Dokumentversion erbt keine alte Release-Freigabe.

### `soulier_client_status`

- Capability: `client.self.status:v1`
- Nur Status des aufrufenden Clients; keine fremden Clientdaten.

## Trust Boundary

`Codex -> internes HTTPS/MCP -> Soulier API -> Capability/Policy Enforcement -> Knowledge Port`

Nicht erlaubt:

- direkter Datenbankzugriff
- freier Dateisystemzugriff
- freie HTTP-Weiterleitung
- freie SQL-/ODBC-Abfragen
- Secrets im Klartext
- IN-FORM-Schreibzugriff
- Autorisierung allein aufgrund eines MCP-Toolnamens

## Fehlersemantik

- `AUTHENTICATION_REQUIRED`
- `CLIENT_REVOKED`
- `CAPABILITY_DENIED`
- `ENVIRONMENT_DENIED`
- `RESOURCE_SCOPE_DENIED`
- `POLICY_DENIED`
- `APPROVAL_REQUIRED`
- `RESOURCE_NOT_FOUND`
- `RESOURCE_STALE`
- `DEPENDENCY_DEGRADED`
- `RATE_LIMITED`
- `INVALID_REQUEST`
- `INTERNAL_ERROR`

## Negative Pflichtfälle

1. unbekannter Client -> DENY
2. widerrufener Client -> DENY
3. fehlende Capability -> DENY
4. fremde Umgebung -> DENY
5. fremder Scope -> DENY
6. nicht freigegebene Dokumentversion -> DENY
7. `AI_FORBIDDEN` -> DENY vor Modellaufruf
8. `LOCAL_ONLY` -> kein externer Fallback
9. Prompt Injection -> keine Rechteausweitung
10. Secret-Anfrage -> kein Klartextsecret
11. degradierte Wissensquelle -> keine erfundene Aktualität

## Noch nicht festgelegt

- konkrete MCP-.NET-Bibliothek und deren Version
- finale interne TLS-/PKI-Konfiguration
- konkrete Client-Credential-/Token-Bindung

Diese Punkte werden erst nach aktueller Hersteller-/SDK-Prüfung und IT-Rahmenentscheidung implementiert.
