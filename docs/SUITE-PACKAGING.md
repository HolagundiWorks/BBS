# AQC three-app packaging (suite)

**Status:** Scaffold · **Updated:** 2026-08-08  
**Canon:** esti `docs/esti/AORMS-SUITE.md` · `docs/esti/DESKTOP-REPOS.md`

| Installer | In-tree (AQC SoT) | Product repo |
| --- | --- | --- |
| AQC Estimation | `BBSDesktop/AQC.Estimation` | [AQC-Estimation](https://github.com/HolagundiWorks/AQC-Estimation) |
| AQC BBS | `BBSDesktop/AQC.BBS` | [AQC-BBS](https://github.com/HolagundiWorks/AQC-BBS) |
| AQC Project Management | `BBSDesktop/AQC.PM` | [AQC-PM](https://github.com/HolagundiWorks/AQC-PM) |

**This repo (AQC)** remains the engine SoT: `bbs_engine`, `Aorms.Bridge`, reference `BBSApp`.
Product repos pin AQC as a submodule and own installer identity / domain UI.

```bat
dotnet run --project BBSDesktop\AQC.Estimation -c Release
dotnet run --project BBSDesktop\AQC.BBS -c Release
dotnet run --project BBSDesktop\AQC.PM -c Release
```

MSIX identities and WinUI domain screens are follow-on work.
