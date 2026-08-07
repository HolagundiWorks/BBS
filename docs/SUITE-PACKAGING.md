# AQC three-app packaging (suite)

**Status:** Scaffold · **Updated:** 2026-08-07  
**Canon:** esti `docs/esti/AORMS-SUITE.md`

| Installer | Project | Role |
| --- | --- | --- |
| AQC Estimation | `BBSDesktop/AQC.Estimation` | BOQ / rate books / measurement |
| AQC BBS | `BBSDesktop/AQC.BBS` | Bar bending / steel recon |
| AQC Project Management | `BBSDesktop/AQC.PM` | Programme / packages / RA (AProc) |

All ProjectReference `Aorms.Bridge` and will share `bbs_engine` (same as BBSApp).
`BBSApp` remains the reference UI until domain screens are split into these shells.

```bat
dotnet run --project BBSDesktop\AQC.Estimation -c Release
dotnet run --project BBSDesktop\AQC.BBS -c Release
dotnet run --project BBSDesktop\AQC.PM -c Release
```

MSIX identities and WinUI forks are follow-on work (D6 / suite S3).
