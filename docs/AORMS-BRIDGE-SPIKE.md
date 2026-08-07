# AQC → AORMS bridge spike (D2 tracker)

**Status:** Spec ready · implementation in [HolagundiWorks/AQC](https://github.com/HolagundiWorks/AQC)  
**Canon:** [PORTAL-SYNC-BRIDGE.md](PORTAL-SYNC-BRIDGE.md) · [HUB-API.md](HUB-API.md)  
**Updated:** 2026-08-07

AQC is missing DB management to connect to the AORMS portal. This spike is the
**reference implementation** of `aorms_bridge` + local SQLite. AStudio and
AConsulting consume the same bridge after fork.

**Licensing:** keep **open source** (AGPL community lineage). SaaS commercial
terms deferred.

## Checklist

- [ ] Add `docs/AORMS-BRIDGE.md` in AQC (copy/adapt PORTAL-SYNC-BRIDGE)
- [ ] Introduce SQLite firm DB + `.bbsproj` import
- [ ] Implement `Aorms.Bridge` (activate → syncToken → meta/artifact Flush)
- [ ] Smoke: activate against hub / colocated · `Flush` · portal sees published row
- [ ] Extract shared package / submodule for AStudio + AConsulting
- [ ] Tag AQC baseline commit for forks to pin

## Smoke sequence

1. Hub: migration `0227` applied; `ESTI_ROLE=hub`  
2. Desktop: set licence API URL + hub URL + product API key + `INSTALL_ID`  
3. Activate licence → persist `syncToken`  
4. Commit a progress % or READY drawing → outbox → Flush  
5. Firm portal project view shows the published artifact/meta  

## Non-goals for spike

- Full multi-seat WS catch-up (wave 2)  
- Reimplementing BBS in TypeScript  
- SaaS commercial SKU work  
