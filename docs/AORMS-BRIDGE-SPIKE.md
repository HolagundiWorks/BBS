# AQC → AORMS bridge spike (D2 tracker)

**Status:** Code ✅ on AQC `main` · Hub activate smoke 🚧  
**Updated:** 2026-08-07

## Checklist

- [x] `docs/AORMS-BRIDGE.md` ([PR #4](https://github.com/HolagundiWorks/AQC/pull/4))  
- [x] `BBSDesktop/Aorms.Bridge` FirmDb · Activate · Flush ([PR #5](https://github.com/HolagundiWorks/AQC/pull/5))  
- [x] Wire envelopes = `MetaEventBody` / `SyncIngestBody`  
- [x] Local outbox smoke (Flush skips with `missing_sync_token` without activate)  
- [ ] Hub smoke with real licence → `syncToken` → Flush meta accepted  
- [ ] Package for AStudio / AConsulting (see ENGINE-PIN)  
- [ ] Tag AQC baseline for forks  

## Local smoke

```bat
cd BBSDesktop
dotnet run --project Aorms.Bridge.Smoke -c Release
```

Expect `skipped=missing_sync_token` until activate.
