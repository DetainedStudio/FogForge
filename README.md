# FogForge

**A free pack manager for custom Dead by Daylight icon packs.**

FogForge lets you install, switch and remove icon packs for Dead by Daylight with a single click: portraits, perks, powers, items, add-ons, offerings and store backgrounds.

> FogForge is an unofficial fan tool. It is not affiliated with or endorsed by Behaviour Interactive.

---

## Features

- **One-click packs** – activate a pack, switch to another one or go back to the original icons at any time.
- **Built-in pack store** – browse and download packs directly in the app. New and updated packs are marked automatically.
- **Pack preview** – look at every icon of a pack before you install it, with search, Killer/Survivor filters and a before/after slider.
- **Mix packs** – take each category from a different pack, e.g. portraits from Cartoon and perks from Comic.
- **Pick single icons** – use individual icons from other packs, e.g. only your favourite killer.
- **Survives game updates** – if a Dead by Daylight update resets your icons, FogForge re-applies your pack with one click (or automatically).
- **Choose what to change** – apply only the categories you want.
- **Automatic backups** – your original icons are saved before anything is replaced.
- **Automatic updates** – FogForge tells you when a new version is available and updates itself.
- **10 languages** – English, German, Spanish, French, Italian, Portuguese, Polish, Russian, Turkish and Dutch.

## Download

Get the latest **FogForge Setup.exe** from the [Releases](../../releases/latest) page.

## Installation

1. Run **FogForge Setup.exe**.
2. If Windows shows *"Windows protected your PC"*, click **More info** → **Run anyway**.
   This happens with new programs that Windows doesn't know yet.
3. Follow the installer. It usually finds Dead by Daylight automatically.
4. Start FogForge, pick a pack on the left and click **Activate**.
   Keep Dead by Daylight closed while doing this.

To go back to the original icons, click **Unload** in FogForge.
To uninstall, open **Windows Settings › Apps › FogForge › Uninstall**. Your original icons are restored.

## How it works

Dead by Daylight loads its menu icons from loose image files in

```
Dead by Daylight\DeadByDaylight\Content\UI\Icons
```

FogForge only replaces these image files and keeps a backup of the originals.
It **never modifies the game's .pak files** or any other game data.

Some icons (for example the offerings on the loading screen) are stored inside the game's .pak files and therefore can't be changed by FogForge.

## Requirements

- Windows 10 or 11
- .NET Framework 4.5 or newer (already included in Windows)
- Dead by Daylight (Steam)

## Icon packs

The packs are hosted separately in [fog-forge-packs](../../../fog-forge-packs). You don't need to download them manually – FogForge does it for you.

## Building from source

FogForge is a C# WinForms app for .NET Framework 4.5. On Windows with the Windows SDK installed:

```powershell
./build.ps1          # builds out/FogForge.exe and out/FogForge Setup.exe
```

Releases are built automatically by GitHub Actions from this repository when a release is published (see [`.github/workflows/release.yml`](.github/workflows/release.yml)).

## Code signing policy

Free code signing provided by [SignPath.io](https://signpath.io), certificate by [SignPath Foundation](https://signpath.org).
Details, team roles and what is signed: [Code signing policy](CODE_SIGNING.md).

## Privacy

This program will not transfer any personal information to other networked systems. It only contacts GitHub to check for updates and to download icon packs. See [PRIVACY.md](PRIVACY.md).

## License

FogForge is open source under the [MIT License](LICENSE).

## Disclaimer

Dead by Daylight and all related names are trademarks of Behaviour Interactive Inc. FogForge does not include or distribute any original game files. The **Default** pack is created on your own PC from your own game installation.
All custom pack artwork is fan-made.
Use at your own risk.
