# Gate 3 – Infrastruktur-Abnahmeplan 0.1

Status: `ARBEITSSTAND`  
Zweck: Ausführungs- und Evidenzplan für die Gate-3-Punkte, die erst auf der realen Soulier-Zielinfrastruktur nachgewiesen werden können.

## 1. Grundsatz

Dieser Plan ist **keine Go-live-Freigabe**. Er definiert, welche Nachweise auf der realen Zielumgebung erbracht werden müssen, bevor Nodon Gate 3 auf `ABNAHMEBEREIT` setzen darf.

V1 bleibt intern erreichbar. Ein öffentlicher Internet-Ingress ist nicht Teil dieses Plans.

## 2. Zielumgebung – nachzuweisende Basis

Vor Installation dokumentieren:

- verantwortlicher Host/Hypervisor und VM;
- Ubuntu Server 24.04 LTS oder ausdrücklich freigegebene Abweichung;
- Docker Engine/Compose oder ausdrücklich freigegebene Abweichung;
- CPU/RAM/Disk-Zuteilung;
- persistente Volumes/Pfade für PostgreSQL, Content Store, authentik und Backup-Staging;
- interne DNS-Namen;
- Netzwerksegment/VLAN;
- erlaubte ein- und ausgehende Verbindungen;
- Patch-/Updateverantwortung;
- Monitoring-/Log-Ziel;
- Backup-Verantwortung und Offsite-Schicht.

UNKNOWNs werden nicht durch Annahmen ersetzt.

## 3. Netzwerk- und Firewall-Abnahme

### Pflicht

- Soulier-Zentrale ist aus dem vorgesehenen internen Netz erreichbar.
- Kein direkter öffentlicher Internetzugriff auf App, PostgreSQL, authentik, n8n oder Content Store.
- PostgreSQL ist ausschließlich für die notwendigen internen Container/Hosts erreichbar.
- Verwaltungsports sind nicht allgemein im LAN freigegeben.
- Remote-Zugriff erfolgt nur über den von der IT freigegebenen sicheren Zugang/VPN.
- Ausgehender Zugriff wird auf die tatsächlich benötigten Ziele begrenzt.

### Negativtests

- Zugriff auf PostgreSQL von einem nicht autorisierten Netz/Client schlägt fehl.
- direkter Zugriff auf interne Verwaltungsendpunkte von nicht vorgesehenen Quellen schlägt fehl.
- Gate-3-Testendpunkte `/internal/*` und Pilot-MCP sind in Production nicht verfügbar.

## 4. Internes TLS / Zertifikate

### Pflicht

- App und authentik werden intern über HTTPS angesprochen.
- Zertifikate stammen aus einer durch die Soulier-IT akzeptierten internen oder öffentlichen Vertrauenskette.
- Hostnamen stimmen mit Zertifikat-SANs überein.
- abgelaufene, falsche oder nicht vertrauenswürdige Zertifikate dürfen nicht still akzeptiert werden.
- private Schlüssel liegen nicht im Repository, Drive oder Anwendungslog.

### Evidenz

- Browser-/Client-Verbindung ohne Zertifikatswarnung;
- OIDC-Discovery/Tokenvalidierung über HTTPS;
- negativer Test mit falschem/unvertrauenswürdigem Zertifikat;
- dokumentierter Erneuerungsweg und Verantwortlicher.

## 5. authentik-PoC

### Konfiguration

- eigener OIDC/OAuth2-Provider für die Soulier-Zentrale;
- eindeutige Audience/Client-Konfiguration;
- Frank als einziger regulärer menschlicher V1-Benutzer;
- Soulier-Zentrale verwendet `sub` als stabile externe Identitätsbindung;
- Rollen des IdP erteilen **keine** Soulier-Capabilities automatisch.

### Pflichtnachweise

1. Frank kann sich erfolgreich anmelden.
2. gültiges Token mit korrektem Issuer/Audience/Signatur/Lifetime wird akzeptiert.
3. fehlendes Token wird abgewiesen.
4. falsche Audience wird abgewiesen.
5. abgelaufenes Token wird abgewiesen.
6. gültiges Token einer nicht in der Soulier-Zentrale angelegten Person wird abgewiesen.
7. deaktivierter interner HumanPrincipal wird trotz gültigem OIDC-Token abgewiesen.
8. Passkey/WebAuthn/Windows-Hello-Pfad wird auf Franks Arbeitsgerät praktisch getestet, soweit von der freigegebenen Identity-Infrastruktur unterstützt.
9. Logout und Revocation werden praktisch getestet.
10. Recovery/Break-glass ist dokumentiert und getestet, ohne einen permanenten zweiten normalen V1-Benutzer einzuführen.
11. authentik-Konfiguration/Daten sind Teil eines dokumentierten Backup-/Restorewegs.

## 6. Persistente Benutzer-/Rollenverwaltung

Die Anwendung hat intern getrennte Objekte für:

- HumanPrincipal;
- RoleDefinition;
- RoleCapability;
- HumanRoleAssignment mit ResourceScope, Environment und Gültigkeitszeitraum.

Abnahme:

- Frank ist als aktiver HumanPrincipal an seinen realen OIDC-`sub` gebunden;
- Rolle/Capabilities/Scopes werden datengetrieben verwaltet, nicht im Code hart verdrahtet;
- Deaktivierung wirkt ohne Codeänderung;
- spätere neue Benutzer können ohne Neu-Build der Anwendung angelegt werden;
- jede Erweiterung des Nutzerkreises bleibt eine Entscheidung Franks.

## 7. Secret Backend

Vor produktiver Aktivierung wird genau ein Backend als V1-Standard festgelegt und dokumentiert.

Bereits implementierte, nicht automatisch produktiv aktivierte Adapter:

- allowlist-basierte Umgebungsvariablenauflösung;
- allowlist-basierter Secret-File-Mount unter festem Root, ohne freie Pfade oder Verzeichnisenumeration.

Pflichtkriterien des gewählten Backends:

- Secrets nicht in Git, Drive, App-Konfiguration im Klartext oder Logs;
- Least Privilege auf Datei-/Container-/Prozesszugriff;
- Rotation ohne Codeänderung;
- fehlendes Secret führt fail-closed zu Fehler statt leerem Fallback;
- Backup-/Recovery-Verhalten geklärt;
- Secret-Werte werden in Tests/Logs nicht ausgegeben.

Die Aktivierung eines konkreten produktiven Backends bleibt vor Abnahme explizit zu entscheiden.

## 8. PostgreSQL und Migrationen

- `ConnectionStrings:Soulier` ist in Production Pflicht.
- Production startet nicht ohne OIDC-Konfiguration und Datenbankkonfiguration.
- `/health/ready` liefert nur `ready`, wenn die Datenbank erreichbar ist und keine Migration aussteht.
- Migrationen werden kontrolliert vor dem App-Start/Deployment ausgeführt; kein blindes automatisches Schema-Upgrade im Produktivstart.
- vor schemaändernden Deployments existiert ein überprüfbarer Backup-/Rollbackweg.

## 9. Content Store

- fester On-Prem-Rootpfad;
- Pfad liegt auf vorgesehenem persistentem Storage;
- App-Prozess erhält nur notwendige Rechte;
- Content wird SHA-256-adressiert;
- freie Pfade, Traversal und Überschreiben sind nicht Bestandteil der Schnittstelle;
- Hashprüfung wird vor released Knowledge Reads durchgeführt;
- Content Store ist Bestandteil des realen Restoretests.

## 10. Codex-MCP-Praxistest

Nach Bereitstellung der internen Test-/Pilotinstanz:

1. Codex auf Franks Arbeitsrechner verbindet sich zum internen MCP-Endpunkt.
2. Nur freigegebene Tools sind sichtbar.
3. `knowledge_search` findet ausschließlich freigegebenen Scope.
4. `knowledge_read` liest nur die konkret freigegebene Version im erlaubten Scope.
5. fremder Scope wird abgewiesen.
6. Prompt-Injection-Versuch erweitert keine Rechte.
7. Secret-/SQL-/Filesystem-/IN-FORM-Werkzeuge sind nicht frei exponiert.
8. Correlation-/Audit-Nachweis für ALLOW und DENY wird kontrolliert.

Der Test gilt erst als bestanden, wenn er **vom realen lokalen Codex** ausgeführt wurde. Ein CI-MCP-Test ersetzt diesen Nachweis nicht.

## 11. Echter Backup-/Restoretest

Auf der Zielinfrastruktur oder einer von der IT freigegebenen repräsentativen Restore-Zielumgebung:

- PostgreSQL sichern;
- Content Store sichern;
- Identity/authentik-relevanten Zustand sichern;
- Backup vom Primärsystem getrennt verfügbar machen;
- Testbestand gezielt unbrauchbar machen oder neue leere Restore-Zielumgebung verwenden;
- Datenbank und Dateien wiederherstellen;
- Migration History prüfen;
- Knowledge Release + Hash prüfen;
- HumanPrincipal/Rollen/Assignments prüfen;
- Audit-Append-only-Schutz prüfen;
- Content-Hash prüfen;
- Login und minimalen Wissenszugriff nach Restore prüfen;
- tatsächliche Restorezeit dokumentieren und gegen RTO 8 Geschäfts-Stunden bewerten;
- Sicherungsintervall gegen RPO 4 Stunden bewerten.

## 12. IT-Freigabe

Für die technische Betriebsfreigabe erhält die Soulier-IT mindestens:

- Architektur-/Datenflussübersicht;
- Ports/Firewallmatrix;
- Container-/Diensteliste;
- Persistenzpfade;
- Secret- und Zertifikatskonzept;
- Patch-/Updateweg;
- Backup-/Restoreweg;
- Monitoring/Logging;
- bekannte Restrisiken;
- Rückfall-/Abschaltweg.

Abweichungen von freigegebenen Architekturleitplanken werden als Entscheidung/ADR dokumentiert und nicht still übernommen.

## 13. Gate-3-Abschlusskriterium

Gate 3 kann erst auf `ABNAHMEBEREIT` gesetzt werden, wenn:

- alle automatisierten Code-/DB-/Recovery-Gates grün sind;
- die realen Nachweise aus authentik, TLS, Codex, Netzwerk/IT und Restore vorliegen;
- keine offene HIGH/CRITICAL Security-Finding verbleibt;
- offene Restrisiken dokumentiert sind;
- der PR-/Artefaktstand exakt dem geprüften Stand entspricht.

Nur Frank kann anschließend `FREIGEGEBEN` setzen.
