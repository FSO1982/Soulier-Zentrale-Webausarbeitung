# ERP-Adapter-Skeleton 0.1

Status: `ARBEITSSTAND` – V1 ohne produktive ERP-/IN-FORM-Verbindung.

## Ziel

Die Soulier-Zentrale erhält eine herstellerneutrale Read-Port-Grenze (`IErpReader`), ohne eine zweite ERP-Wahrheit aufzubauen und ohne unbekannte IN-FORM-Schnittstellen zu erfinden.

## V1-Umsetzung

- `IErpReader` besitzt nur definierte Read-Operationen für Customer/Order-Readmodels.
- `MockErpReader` liefert ausschließlich explizit gesetzte Testdaten.
- `InformReadOnlyAdapterSkeleton` enthält **keine** ODBC-, SQL-, API-, Credential- oder Netzwerkkonfiguration.
- Ein Aufruf des IN-FORM-Skeletons schlägt fail-closed mit `INFORM_NOT_CONFIGURED` fehl.
- Es gibt keinen Schreibvertrag und keine freie Query-Schnittstelle.
- MCP, n8n und KI erhalten keinen direkten ERP-/DB-Zugriff.

## Bewusst nicht erfunden

Bis zur Hersteller-/Schnittstellenklärung sind unbekannt bzw. nicht produktiv bestätigt:

- ODBC-/API-Lizenz und zulässiger Nutzungsumfang;
- 32-/64-Bit-Anforderungen;
- konkrete Tabellen, Views oder API-Ressourcen;
- technisch erzwingbare Read-only-Berechtigung;
- Support-/Updatevertrag;
- produktive Credentials/Service Identity;
- Netzwerkpfad/Connector-Host.

Diese Punkte werden nicht durch angenommene SQL-Tabellen oder hypothetische Endpoints ersetzt.

## Aktivierungsregel

Eine reale IN-FORM-Anbindung ist eine spätere eigene Aktivierung. Vorher sind mindestens Herstellerklärung, Security-/IT-Prüfung, explizite Read-only-Grenze, Capability-/Scope-Vertrag, Audit und Frank-Freigabe erforderlich.
