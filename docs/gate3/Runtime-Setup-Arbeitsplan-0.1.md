# Gate 3 – Runtime-/Setup-Arbeitsplan 0.1

**Stand:** 06.09.2026  
**Status:** `ARBEITSSTAND`

## Ziel

ENT-028 in einen überprüfbaren V1-Implementierungspfad übersetzen, ohne den Soulier-Core an Docker-spezifische Domainlogik zu koppeln.

## Arbeitspakete

### WP-RUN-01 Platform Contract
- maschinenlesbarer Vertrag für unterstütztes V1-Referenzprofil;
- Mindest-/Kompatibilitätskriterien für OS, Architektur, Runtime, Storage, Netzwerk, TLS und Ressourcen;
- keine Bindung an Serverhersteller/Hypervisor.

### WP-RUN-02 Setup/Preflight
- read-only Erkennung/Validierung von OS, Architektur, CPU/RAM, Storage, Docker/Compose, benötigten Hostports, DNS/TLS-Grundlage;
- keine freie Netzwerk-/Portscannerfunktion;
- keine Behauptung organisatorischer IP-/DNS-Freiheit.

### WP-RUN-03 Environment Manifest
- secret-freies, versioniertes Manifest;
- Host-/Runtime-Metadaten, Deploymentprofil, Ressourcen, konfigurierte Adresse, TLS-/Backupstatus;
- auditierbare Konfigurationsänderungen.

### WP-RUN-04 Runtime Manager
- separate privilegierte Betriebsgrenze;
- allowlist-basierte Operationen für definierte Soulier-Komponenten;
- kein allgemeiner Docker-Socket in Web/API/n8n;
- keine freie Shell/Dateisystem-/Docker-API.

### WP-RUN-05 Referenzdeployment
- Ubuntu Server 24.04 LTS + Docker Engine + Compose;
- soulier-web, soulier-api, PostgreSQL, authentik, n8n, notwendige Worker/Adapter;
- interne Netze, minimale Portpublikation, persistente Volumes, Secrets außerhalb Images/Repo.

### WP-RUN-06 Operations-Modell
- Health/Readiness/Version/Storage/TLS/Backup/Dependency-Status;
- Page Contract `Administration -> System & Infrastruktur`;
- keine Secrets oder unnötigen Hostdetails.

### WP-RUN-07 Update/Rollback
- versionierte Deploymentmanifeste;
- kontrollierte Migrationen;
- Backup vor relevanten Änderungen;
- Health-Prüfung und Recoveryweg.

### WP-RUN-08 Migration/Restore
- Neuinstallation auf Ersatzserver;
- Restore von PostgreSQL, Content, Identity, Konfiguration und Secret-Recovery;
- Hash-/Audit-/Login-/MCP-Minimaltests;
- RPO/RTO-Nachweis.

### WP-RUN-09 Security
- Privilege Escalation Web/API -> Runtime;
- manipulierte Manifeste/Images;
- Information Leakage;
- Update ohne Recovery;
- Missbrauch Runtime Manager als freie Shell;
- inkonsistenter Restore.

### WP-RUN-10 Reale Infrastruktur
- IT-Abgleich mit tatsächlicher Soulier-Zielumgebung;
- TLS/DNS/Firewall/Hardening/Backup/Secret Backend/authentik/Codex;
- realer Restore und IT-/Security-Abnahme.

## Nicht in diesem Scope

- Multi-Tenant/SaaS;
- OEM/White-Label;
- mehrere Container-Runtimes;
- generischer Docker-/App-Store;
- autonome DHCP-/DNS-/Firewallverwaltung;
- produktive ERP-Verbindung.

## Gate

Der vorherige Head `8e9307cf17a02e81cc3116ebc4ecd63dceb4e671` bleibt grüne Evidenz für den früheren Scope. Dieser Arbeitsplan erweitert Gate 3; `ABNAHMEBEREIT` erst nach Umsetzung/Tests plus realer Infrastruktur-Evidenz. `FREIGEGEBEN` ausschließlich durch Frank.