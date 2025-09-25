# Soft2-LargeSystemsDev-OLA4

## Group

- Oskar (Ossi-1337, cph-oo221)
- Peter (Peter537, cph-pa153)
- Yusuf (StylizedAce, cph-ya56)

## Log Examples

System log kan ses i [system_example_log.txt](system_example_log.txt)

Audit log kan ses i [audit_example_log.txt](audit_example_log.txt)

## How to Run

Programmet kan køres i [CopenhagenCityBikes/](./CopenhagenCityBikes/) via `dotnet run` efter `dotnet build` har været kørt.

Programmet kører 30 "falske" API kald med minimum af 1 af hvert endpoint.

## What we log

### System log

- Application startup/shutdown og bootstrap‑fejl - i `CopenhagenCityBikes/Program.cs`.
- Request‑sammendrag: HTTP method, path, status og elapsed_ms (målt i middleware).
- Simulerede eksterne kald og latency (INFO/WARN) - i `CopenhagenCityBikes/Helpers/TimeIt.cs`.
- Advarsler ved lavt lager eller nægtede operationer (WARN) - i `CopenhagenCityBikes/Services/BikeService.cs`.
- Fejl/undtagelser med stacktrace (ERROR) - logges i middleware og bootstrap.
- correlation_id inkluderes i system‑logs via MDLC.
- Rotation: daglig arkivering; system‑filer beholdes 14 dage (se `CopenhagenCityBikes/nlog.config`).

### Audit log

- Separate JSON‑linjer fra `AUDIT`‑loggeren som bliver skrevet af `CopenhagenCityBikes/Helpers/AuditHelper.cs`.
- Events: `LOGIN_SUCCESS`/`LOGIN_FAILURE`, `RESERVATION_CREATE`, `RENTAL_START`, `RENTAL_END`, `ADMIN_INVENTORY_UPDATE`.
- Hver audit‑post indeholder `user_id`, `action`, relevante resource‑felter (f.eks. `bike_id`, `rental_id`), `ip` og `correlation_id`.
- Audit beholdes længere (90 dage), som er konfigureret i `CopenhagenCityBikes/nlog.config`.
- Privacy‑aware: ingen CPR, navne, emails, tokens eller passwords logges.

## Rotation / Retention

Rotation og retention bliver sat i NLog's configurations fil: [CopenhagenCityBikes/nlog.config](./CopenhagenCityBikes/nlog.config)

Rotation i filen er styret af attributten `archiveEvery` (og relaterede arkiv indstillinger), så det vil sige hvornår en ny arkivfil oprettes, hvilket for os er dagligt via `archiveEvery="Day"`.

Retention styres af `maxArchiveFiles`, altså hvor mange dages daglige arkiver der beholdes. I vores konfiguration betyder `maxArchiveFiles="14"` for `systemFile` at system‑logfiler beholdes 14 dage, og `maxArchiveFiles="90"` for `auditFile` at audit‑logfiler beholdes 90 dage.
