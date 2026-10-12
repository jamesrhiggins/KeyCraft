KeyCraft 0.2 - independent Windows keyboard utility

Run KeyCraft.exe on Windows x64 with .NET Framework 4.8.
Keep the assets folder beside the executable; it contains your keyboard graphic.

ACTIVATION
Saved mappings activate when Windows detects an Apple keyboard (vendor 05AC).
Green circle: Apple keyboard detected. Gray circle: none detected.
Detection refreshes every two seconds. Disconnecting pauses mappings; reconnecting
reactivates them unless manually paused. Settings save and apply immediately.
Live mappings affect ALL keyboards while an Apple keyboard is connected.
Closing hides the app in the tray. Right-click the tray icon to exit.
Ctrl+Alt+Pause stops mappings. No auto-start or system registry mappings.

FUNCTION ROW
Utility actions are the default; uncheck the option for normal F1-F12.
F1/F2: Built-in display brightness via WMI, or external display brightness via DDC/CI. Supported external monitors are adjusted together; unsupported monitors remain unchanged. Brightness availability varies by monitor and connection.
F3: Task View. F4: Search. F5: File Explorer. F6: Show desktop.
F7/F8/F9: Previous track, play/pause, next track.
F10/F11/F12: Mute, volume down, volume up.
Native Fn reading is attempted via exposed Apple HID reports. Some A1843
connection does not advertise a dedicated Fn control in its accessible raw HID
collections. Physical Fn behavior is not verified.
Right Control is an additional Fn key by default: hold it for F1-F12.
Disable that checkbox to restore Right Control's configured modifier mapping.

USB STORAGE
The Eject USB drives button and F13 request safe removal of connected USB disks.
Native Apple Eject is handled if Windows exposes its HID Consumer Eject usage.
That usage is not advertised in some accessible consumer collections;
use F13 or the on-screen/tray button until hardware support is verified.
Only USB storage is targeted. Keyboards, mice and hubs are not ejected.
System, boot-partition and active page-file disks are excluded, including disks
sharing a USB enclosure with a protected disk. Windows can veto busy devices.
Failed requests show the Windows code and veto; close files/apps and retry.
No forced removal, device disabling, or administrator escalation is used.
Only unplug a USB drive after Windows reports successful safe removal.

NUMERIC KEYPAD
The physical Clear key erases the current line with Home, Shift+End, Backspace. Modified Clear is left alone; keypad 5 with Num Lock off is preserved. In an empty multiline line, Backspace can join it with the preceding line.
The on-screen Num Lock button also toggles it and displays ON/OFF.
Keep Num Lock off is a persistent setting: it automatically saves and restores
on restart, and forces Num Lock back off while KeyCraft is active. It overrides
attempts to enable Num Lock and disables the on-screen toggle until unchecked.
The policy is suspended when mappings are paused or no Apple keyboard is detected.
It only runs while KeyCraft is running; it does not change Windows startup policy.
Keypad equals emits a literal = when its OEM key code/scan code is exposed.

VISUALS
Your keyboard image with its complete shadow and a matching app/tray icon, fully drawn rounded controls, translucent-looking cards, animated buttons and
breathing green detection indicator. Inspired by Apple materials; this uses
Windows rendering rather than Apple's Liquid Glass runtime.

Settings: %LOCALAPPDATA%\KeyCraft\profile.txt.
Elevated applications and Windows-reserved shortcuts may bypass mappings.
Build: powershell -ExecutionPolicy Bypass -File .\build.ps1
Source code and build instructions are included.
Battery reporting and a signed Apple HID filter driver are not implemented.

PERFORMANCE AND WINDOW SIZE
Keyboard hooks run on a dedicated input thread. Ordinary numpad keys pass through.
The keyboard graphic is cached after scaling. Visible animations use 15 ms timers.
The content area stays at 1020 x 980 (or the available size on smaller displays).
Maximizing centers the content with background margins instead of stretching it.
