# AQC → AORMS bridge spike (D2 tracker)

**Status:** ✅ Code + hub activate→Flush smoke  
**Updated:** 2026-08-07  
**Baseline tag:** `aorms-bridge-d2` (pin for AStudio / AConsulting)

## Checklist

- [x] `docs/AORMS-BRIDGE.md` ([PR #4](https://github.com/HolagundiWorks/AQC/pull/4))  
- [x] `BBSDesktop/Aorms.Bridge` FirmDb · Activate · Flush ([PR #5](https://github.com/HolagundiWorks/AQC/pull/5))  
- [x] Wire envelopes = `MetaEventBody` / `SyncIngestBody`  
- [x] Local outbox smoke (Flush skips with `missing_sync_token` without activate)  
- [x] Hub smoke with real licence → `syncToken` → Flush meta accepted  
- [ ] Package for AStudio / AConsulting (ENGINE-PIN consume)  
- [x] Tag AQC baseline for forks (`aorms-bridge-d2`)  

## Local smoke

```bat
cd BBSDesktop
REM local outbox only (no token):
dotnet run --project Aorms.Bridge.Smoke -c Release

REM full hub activate → Flush (colocated esti hub):
set ESTI_HUB_URL=http://127.0.0.1:4000
set ESTI_LICENSE_API_URL=http://127.0.0.1:4000/platform
set ESTI_PRODUCT_API_KEY=hlp_sk_...
set ESTI_LICENSE_KEY=HLP-....
set INSTALL_ID=smoke-device-1
dotnet run --project Aorms.Bridge.Smoke -c Release
```

Expect `OK hub activate → meta Flush` when key + API key are set.
