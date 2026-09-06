# ADR-002 – Portable Runtime, Setup und integrierte Containerplattform

**Datum:** 06.09.2026  
**Status:** `ARBEITSSTAND / durch ENT-028 richtungsgebend`

## Kontext

Die Soulier-Zentrale soll intern einfach installierbar und später auf Ersatzhardware bzw. andere geeignete Unternehmensserver migrierbar sein, ohne den Fachkern an einen konkreten physischen Server oder Hypervisor zu koppeln. Gleichzeitig soll V1 kein generischer Server-, Docker- oder Netzwerkmanager werden.

## Entscheidung

Für die betriebsinterne Soulier-V1 wird eine integrierte Runtime auf Basis des bestehenden Referenzprofils **Ubuntu Server 24.04 LTS + Docker Engine + Docker Compose** verfolgt.

- Die Soulier-Zentrale bleibt die sichtbare Gesamtanwendung.
- Ein separater `Setup/Runtime Manager` darf ausschließlich definierte Soulier-Betriebsaktionen ausführen.
- Web/API und n8n erhalten keinen allgemeinen Docker-Socket und keine freie Host-Shell.
- PostgreSQL, authentik, n8n und freigegebene Worker/Adapter dürfen containerisiert unter der Runtime laufen.
- Portabilität wird über einen `DeploymentPlatformContract`, Deploymentprofile und Backup/Restore erreicht.
- Hardware, Serverhersteller und Hypervisor sind keine Domainabhängigkeit.
- DHCP, DNS, Firewall und freie Netzwerkverwaltung werden nicht autonom von der Zentrale verändert.
- Alternative Runtimes/Deploymentprofile sind nicht V1-Pflicht.
- Productization/Verkauf und Drittanbieter-Redistribution werden erst bei realem Vermarktungsziel neu geprüft.

## Sicherheitsgrenze

Der Runtime Manager ist eine separate privilegierte Betriebsgrenze. Er verwendet allowlist-basierte Operationen, versionierte Manifeste, Audit und Recovery. Keine Capability wie `shell.execute_any`, `filesystem.read_any` oder `docker.execute_any` wird eingeführt.

## Konsequenzen für Gate 3

ENT-028 eröffnet neuen Scope:

1. Platform Contract;
2. Setup-/Preflight-Prototyp;
3. secret-freies Environment Manifest;
4. Runtime-Manager-Vertrag/Privilege-Modell;
5. Docker-/Compose-Referenzprofil;
6. Operations-/Infrastrukturstatusmodell;
7. Update-/Rollbackmodell;
8. Migrations-/Restorepfad;
9. Runtime-Securitytests;
10. realer IT-Abgleich.

Der vorherige grüne Code-/DB-/Restore-Stand bleibt Evidenz für den bisherigen Scope; Gate 3 ist wieder `ARBEITSSTAND`.