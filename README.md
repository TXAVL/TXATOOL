# TXATOOL - WIN

An all-in-one Windows toolkit for IT support: system tweaks and optimization, cleanup, hardware info, downloads (apps, ISOs, Office, WinPE), license backup and network tools. Light and dark themes (follows Windows), Vietnamese and English.

* **Version:** 1.1.0
* **Author:** [TXA](https://fb.com/vlog.txa.2311)

## Build

Requires the .NET 9 SDK on Windows.

```
dotnet build TXATOOL.csproj
```

Installer: `compile_setup.bat` builds a self-contained win-x64 publish and an NSIS 3 installer (`TXATOOL.nsi`) into `Output`.
