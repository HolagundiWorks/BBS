# AQC → AORMS bridge spike (D2 tracker)

**Status:** Docs ✅ · Code scaffold 🚧 · Open source (SaaS licensing deferred)

## Checklist

- [x] Add `docs/AORMS-BRIDGE.md`
- [x] Scaffold `BBSDesktop/Aorms.Bridge` (FirmDb + Activate + Flush)
- [x] Wire `ProjectReference` from BBSApp + `AormsBridgeHost`
- [ ] Migrate / dual-write `.bbsproj` ↔ firm outbox on publish actions
- [ ] Smoke: activate → syncToken → Flush against hub
- [ ] Extract package for AStudio / AConsulting
- [ ] Tag baseline commit for forks

## Build

```bat
cd BBSDesktop
dotnet build Aorms.Bridge\Aorms.Bridge.csproj -c Release
dotnet build BBSApp\BBSApp.csproj -c Release -p:Platform=x64
```

Env for smoke: `ESTI_LICENSE_API_URL`, `ESTI_HUB_URL`, `ESTI_PRODUCT_API_KEY`, `INSTALL_ID`.
