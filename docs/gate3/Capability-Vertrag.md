# Capability-Vertrag – Gate-3-Pilot

Status: ARBEITSSTAND

## Sicherheitsprinzip

Jeder Aufruf wird serverseitig fail-closed geprüft. Ein Client erhält ausschließlich explizit gewährte Capability + Environment + ResourceScope. Modelltext, Promptinhalt oder Clientbehauptungen sind keine Autorisierungsquelle.

## V1-Pilot-Capabilities

### knowledge.search:v1

Wirkung: READ

Eingabe:
- query: nichtleer, begrenzte Länge
- resourceScope: explizit
- maxResults: serverseitig begrenzt

Ausgabe je Treffer:
- documentId
- documentVersionId
- logicalName
- minimierter snippet
- contentHash
- dataClassification
- aiPolicy
- freshnessTimestamp

Vor Ausgabe zwingend:
1. Client ACTIVE
2. Capability aktiv
3. Policy ALLOW
4. aktiver, zeitlich gültiger Grant
5. Environment stimmt
6. ResourceScope stimmt
7. Dokumentversion ist freigegeben
8. Datenklasse und AI-Policy erlauben den konkreten Ausgabepfad
9. AuditEvent mit correlationId

### knowledge.read:v1

Wirkung: READ

Eingabe:
- documentVersionId
- maxChars mit serverseitigem Maximum

Zusätzliche Invariante: Eine neuere Dokumentversion darf niemals automatisch die Freigabe einer älteren Version erben.

## Standard-Fehlercodes

- AUTHENTICATION_REQUIRED
- CLIENT_REVOKED
- CLIENT_INACTIVE
- CAPABILITY_DENIED
- ENVIRONMENT_DENIED
- RESOURCE_SCOPE_DENIED
- POLICY_DENIED
- APPROVAL_REQUIRED
- RESOURCE_NOT_FOUND
- RESOURCE_STALE
- DEPENDENCY_DEGRADED
- RATE_LIMITED
- INVALID_REQUEST
- INTERNAL_ERROR

## Pilotgrenze

Der erste reale Client ist lokaler Codex über internen HTTPS/MCP-Zugang. Kein öffentlicher Endpoint und kein ERP-Zugriff sind Voraussetzung für diesen Architekturbeweis.
