# EasyControl

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-0078D4)
![.NET](https://img.shields.io/badge/.NET-10-512BD4)
![License](https://img.shields.io/badge/license-not%20specified-lightgrey)

Turn almost any controller into an **Xbox 360 controller** on Windows.

EasyControl reads your DirectInput gamepad, re-emits it as a virtual **XInput** pad, and gives you a simple
**press-to-assign** mapper plus one-tap shortcuts such as screenshots and the Xbox Game Bar.

---

## Features

- **DirectInput to XInput** — makes any gamepad work in games that only understand Xbox controllers.
- **Press-to-assign mapping** — tap a control on screen, then press the matching button on your controller.
- **Map everything** — buttons, triggers, sticks and D-pad/hats.
- **Invert stick axes** — flip any stick axis with one toggle.
- **Screenshot shortcut** — bind a button or key to `Win + PrtScn`.
- **Xbox Game Bar shortcut** — bind the controller's Guide/Home button to `Win + G`.
- **Runs in the tray** — start with Windows, optionally minimise to the notification area.
- **Optional hiding** — hide the physical controller while mapping so games only see the virtual one.
- **No extra downloads** — the ViGEmBus and HidHide drivers are bundled right in.

---

## Install

1. Download `EasyControl-1.0.0-win-x64.zip` and extract it anywhere.
2. Run **`EasyControl.exe`**.
3. On first launch you'll be asked to install the required drivers — click **Install** and approve the
   administrator prompt **once**.

> The package is self-contained, so you **do not** need to install .NET separately.

---

## Quick start

1. Connect your controller.
2. Open the **Devices** tab, pick your controller and activate it — this creates the virtual Xbox 360 pad.
3. Open the **Profile** tab and map your controls:
   - Tap a row (for example *Button A*), then press the button you want on your controller.
   - Do the same for triggers, sticks and the D-pad.
   - Use the **Invert** chip on the stick rows if an axis feels backwards.
4. Optionally bind the **Screenshot** and **Xbox Game Bar** cards.
5. Launch your game. It now sees a normal Xbox 360 controller.

---

## Mapping controls

| Group        | What you can map                                            |
| ------------ | ----------------------------------------------------------- |
| **Buttons**  | A, B, X, Y, bumpers, stick clicks, Back, Start, D-pad        |
| **Triggers** | Left and right trigger                                        |
| **Sticks**   | Left and right stick, each with an optional axis invert      |

- **Screenshot button**: press a controller button *or any keyboard key* to bind `Win + PrtScn`.
- **Xbox Game Bar**: bind the controller's **Guide/Home** button (or any key) to open `Win + G` instead of
  its browser/OS default.

---

## Settings

- **Run when Windows starts** — launch EasyControl when you sign in.
- **Don't show in the notification tray** — closing the window exits the app instead of staying in the tray.
- **Start minimised** — launch hidden and go straight to the tray.
- **Hide physical controller while mapping** — uses HidHide so games only see the virtual pad.
- **Virtual controller position** — choose which XInput player slot the virtual pad uses.
- **Components** — shows driver status and can install anything missing.

---

## Notes & troubleshooting

- **Administrator access** is only needed to install the drivers or to hide the physical controller.
- **Windows SmartScreen** may warn about an unrecognised app the first time — choose *More info* then
  *Run anyway*.
- The Bluetooth/USB controller must be connected *before* activating it in the app.
- If a game doesn't see the controller, make sure ViGEmBus is installed (see **Settings > Components**) and
  press **Refresh** on the **Devices** tab.

---

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone https://github.com/harrysof/Easycontrol.git
cd Easycontrol
dotnet build -c Release
```

To produce a self-contained build you can copy to another PC:

```powershell
dotnet publish EasyControl.csproj -c Release -r win-x64 --self-contained true -o dist\EasyControl
```
