Remora — Remote Desktop
=======================

HOW TO INSTALL (on your friend's PC)
------------------------------------
1. Copy this whole folder to the PC (USB, network share, etc.). It must contain:
      Remora.exe
      servers.txt
      install-remora.bat
      install-remora.ps1
2. Double-click  install-remora.bat
3. If Windows SmartScreen shows a blue warning ("Windows protected your PC"):
      click "More info"  ->  "Run anyway".   (It only appears because the app isn't code-signed.)
4. Remora installs to  %LOCALAPPDATA%\Programs\Remora , adds the 37 servers,
   sets the bastion address, makes a Desktop + Start-Menu shortcut, and opens.

No .NET install is needed — everything is bundled in Remora.exe (55 MB).

FIRST-TIME SETUP INSIDE REMORA
------------------------------
Each person uses their OWN login (passwords are never copied between PCs — they are
encrypted per Windows user).
1. Click  Settings (gear)  ->  enter the bastion address + your bastion login + password,
   and the Authorization value from the WALLIX list.
2. Click  Credentials (key)  ->  New  ->  enter the machine login + password you use
   on the "Target Information Required" screen.  Make it the default (star).
3. Double-click a server to connect.  On any login screen press  Ctrl+Alt+K  and pick
   the login, OR assign a credential to a machine (right-click -> Credentials) so it
   fills automatically every time.

UPDATING THE SERVER LIST LATER
------------------------------
Replace servers.txt (paste the WALLIX list rows), then inside Remora click
"Import list" and paste — or re-run install-remora.bat.

SHORTCUTS
---------
Ctrl+Alt+P  quick connect / type any IP      Ctrl+Alt+K  fill a login
Ctrl+Alt+Enter or F11  fullscreen            drag a tab out  -> separate window (other monitor)
