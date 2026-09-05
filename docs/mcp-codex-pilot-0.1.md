# MCP-Codex-Pilot 0.1

Status: `ARBEITSSTAND` – technischer MCP-Transportnachweis grün; realer Aufruf aus Franks lokalem Codex noch offen.

## Ziel

Codex erhält über einen internen MCP-Transport ausschließlich die bereits freigegebenen Soulier-Zentrale-Capabilities. MCP ist Transport/Toolbeschreibung, nicht Autorisierungsquelle.

## Verifizierter technischer Stand – 06.09.2026

- offizielles C# MCP SDK `ModelContextProtocol.AspNetCore` **v2.2.0**;
- Transport: **Streamable HTTP**, stateless;
- Endpoint: `/mcp`;
- Gate-3-Pilot nur in `Testing` oder ausdrücklich aktiviertem `Development`;
- `Production` mappt den Pilot-MCP-Endpunkt nicht;
- Bearer-Token wird vor der MCP-Verarbeitung verlangt;
- echter SDK-MCP-Clienttest erfolgreich: Connect → Tools listen → Tool aufrufen;
- Standard-CI Run `33999126762`: success;
- PostgreSQL-Integration Run `33999126663`: success;
- der reale Codex-Aufruf auf Franks Arbeitsrechner ist **noch kein nachgewiesener Test**.

Aktuelle offizielle Referenzen:

- OpenAI Codex MCP: `https://learn.chatgpt.com/docs/extend/mcp`
- Codex config reference: `https://learn.chatgpt.com/docs/config-file/config-reference`
- offizielles MCP C# SDK: `https://github.com/modelcontextprotocol/csharp-sdk`
- geprüfter SDK-Release: `v2.2.0` vom 13.08.2026.

## V1-Pilottools – aktuell implementiert

### `knowledge_search`

- interne Capability: `knowledge.search:v1`;
- Eingabe: `query`, `resourceScope`;
- Gate-3-Testscope: `soulier:pilot`;
- Server erzwingt Clientstatus, Grant, Environment und Scope;
- Autorisierungsentscheidung wird über den auditierten Pfad geführt;
- Ausgabe enthält nur den definierten Test-Wissensbestand;
- produktiver Knowledge-Reader ist noch nicht angeschlossen.

### `knowledge_read`

- interne Capability: `knowledge.read:v1`;
- Eingabe: `documentVersionId`, `resourceScope`, `maxChars`;
- Server erzwingt Clientstatus, Grant, Environment und Scope;
- Autorisierungsentscheidung wird auditiert;
- im Gate-3-Transporttest ist nur die fest definierte Testversion lesbar;
- der produktive, persistierte Knowledge-/Storage-Pfad folgt separat.

### Noch nicht implementiert

`client.self.status` bleibt als mögliche spätere minimale Capability vorgesehen, ist aber für den aktuellen MCP-Transportbeweis nicht erforderlich.

## Trust Boundary

`Codex -> internes HTTPS/MCP -> Soulier API -> AuthN -> Capability/Policy Enforcement -> Knowledge Port -> minimiertes Ergebnis -> Audit`

Im aktuellen automatisierten Gate-3-Test wird statt produktivem OIDC ein **nicht produktiver Bearer-Pilotschutz** verwendet. Dieser ist ausschließlich Übergangstechnik bis zur authentik/OIDC-Integration.

Nicht erlaubt:

- direkter Datenbankzugriff;
- freier Dateisystemzugriff;
- freie HTTP-Weiterleitung;
- freie SQL-/ODBC-Abfragen;
- Secrets im Klartext;
- IN-FORM-Schreibzugriff;
- Autorisierung allein aufgrund eines MCP-Toolnamens.

## Codex-Konfiguration für den späteren lokalen Pilot

Codex unterstützt projektbezogene `.codex/config.toml`-Dateien für vertrauenswürdige Projekte und kann den Bearer-Token aus einer Umgebungsvariable beziehen.

Beispiel:

```toml
[mcp_servers.soulier_zentrale]
url = "http://127.0.0.1:5188/mcp"
bearer_token_env_var = "SOULIER_MCP_PILOT_TOKEN"
enabled_tools = ["knowledge_search", "knowledge_read"]
default_tools_approval_mode = "prompt"
enabled = true
```

Der Wert von `SOULIER_MCP_PILOT_TOKEN` gehört **nicht** in Git, Drive, `.codex/config.toml` oder Dokumentation.

Für den Development-Server muss derselbe Tokenwert serverseitig sicher als `Soulier:Mcp:PilotToken` bereitgestellt werden. Das ist eine Übergangslösung; die produktionsnahe Authentifizierung wird mit authentik/OIDC ersetzt.

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

Zusätzlich auf Knowledge-Release-Ebene:

- `RELEASE_VERSION_MISMATCH`
- `RELEASE_HASH_MISMATCH`
- `RELEASE_INACTIVE`
- `DOCUMENT_REVIEW_REQUIRED`

## Negative Pflichtfälle

Bereits automatisiert nachgewiesen:

1. fehlender MCP-Bearer-Token → HTTP 401 vor MCP-Verarbeitung;
2. Production → Pilot-MCP nicht gemappt;
3. Production → interner Authorization-Probe nicht gemappt;
4. widerrufener Client → DENY;
5. fehlende Capability → DENY;
6. fremde Umgebung → DENY;
7. fremder Scope → DENY;
8. Hashabweichung bei KnowledgeRelease → DENY.

Noch im Gesamt-Gate-3/Gate-4-Nachweis offen:

- produktives authentik/OIDC;
- `AI_FORBIDDEN` vor Modellaufruf;
- `LOCAL_ONLY` ohne externen Fallback;
- nicht freigegebener Provider;
- Prompt Injection ohne Rechteausweitung;
- Secret-Anfrage ohne Klartextsecret;
- degradierte Wissensquelle ohne erfundene Aktualität;
- realer Codex-Aufruf auf Franks Arbeitsrechner.

## Offene produktionsnahe Punkte

- finale interne TLS-/PKI-Konfiguration;
- authentik/OIDC-Token-/Claim-Bindung;
- produktiver Knowledge-/Storage-Reader;
- persistente Client-/Grant-Auflösung aus PostgreSQL statt Gate-3-Testprofil;
- lokaler Codex-Praxistest.
