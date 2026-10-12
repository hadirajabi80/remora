# Remora

A fast, tabbed **Remote Desktop (RDP) manager for Windows** with a saved, encrypted password list you pick from — built for environments that sit behind a **WALLIX Bastion** (two‑step login), but works for plain RDP servers too.

![Remora](RemoteHub/app.ico)

## Features

- **Tabbed sessions** — open many machines at once; drag a tab out into its **own window** to put it on another monitor (the live session is kept connected).
- **Saved login list** — press `Ctrl+Alt+K` on any login screen, pick a saved login, and it is filled on the current screen (clipboard paste, so special characters work). Assign a credential to a machine to have it filled **automatically** on connect.
- **WALLIX aware** — recognises the bastion login and the "Target Information Required" screen with the built‑in Windows OCR, and fills the right credential on each.
- **Quick connect** — `Ctrl+Alt+P`, type a name or **any IP/host** to open an ad‑hoc session.
- **Import** — paste a WALLIX target list and every `Interactive@host-ip:RDP` row becomes a machine, auto‑grouped by name.
- **Fullscreen** with an mstsc‑style connection bar: pin it to keep it, or let it auto‑hide and reveal at the top edge; shows the server name, exit‑fullscreen and close.
- **Security** — everything is stored in `%APPDATA%\RemoteHub\vault.dat`, encrypted with **AES‑256‑GCM**, the key protected by **Windows DPAPI** (useless on another PC/user). Optional master password (PBKDF2‑SHA256) and auto‑lock. The bastion's public key is pinned on first connect.

## Requirements

- Windows 10/11 (x64). The released `Remora.exe` is self‑contained — **no .NET install needed**.
- To build from source: .NET 10 SDK.

## Build

```powershell
dotnet publish RemoteHub/RemoteHub.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
```

A framework‑dependent build (smaller, needs the .NET 10 Desktop Runtime):

```powershell
dotnet publish RemoteHub/RemoteHub.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSingleFile=true -o dist
```

## First‑time setup

1. **Settings (gear)** — enter the bastion address, your bastion login + password, and the *Authorization* value from the WALLIX list.
2. **Credentials (key)** — add the login/password you use on the target machines.
3. **Import list** — paste your WALLIX targets, or double‑click a machine / press `Ctrl+Alt+P` and type an IP.

## Shortcuts

| Keys | Action |
|---|---|
| `Ctrl+Alt+P` | Quick connect / type any IP |
| `Ctrl+Alt+K` | Pick a login and fill it on the current screen |
| `Ctrl+Alt+Enter` / `F11` | Fullscreen |
| `Ctrl+Alt+PgDn` / `PgUp` | Next / previous tab |
| `Ctrl+Alt+1…9` | Jump to a tab |
| `Ctrl+Alt+R` | Reconnect · `Ctrl+Alt+W` close · `Ctrl+Alt+B` sidebar · `Ctrl+Alt+L` lock |

Drag a tab below the strip to open it in a separate window. Right‑click a tab or machine for more.

## Command line

```
Remora.exe --import <file>   add machines from a WALLIX target list (creates the vault if needed)
Remora.exe --set-host <ip>   set the bastion address
Remora.exe --dump            print vault contents (no secrets shown as plaintext)
```

## Notes

The repository intentionally does **not** include any server list or vault — those contain private infrastructure and credentials. Keep your `servers.txt` out of version control.

---

Built with C# / WinForms. Uses the Windows RDP ActiveX (`mstscax.dll`) and the built‑in Windows OCR.
