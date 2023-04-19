# Automobilista 2 Content Manager

**One-click installation of AMS2 mods**. No more reading instructions or editing files!

It is currently a command line tool, but a graphical user interface is coming soon. More features will be coming in the new few weeks and months.

Any feedback is very welcome.

## Installation

- Download the pre-built binaries from the "artifacts" section of the [latest build](
https://github.com/OpenSimTools/AMS2CM/actions/workflows/ci.yaml?query=event%3Apush).
- Extract the archive to a directory of your choice.

## Prerequisites
- .NET 6 Desktop Runtime (download from [Microsoft](https://dotnet.microsoft.com/en-us/download/dotnet/6.0)).
- Make sure that the game is in its original state, without any mod installed (neither by JSGME nor manually).

## Usage

Create a `Mods/Enabled` directory in your AMS2 installation directory and place in there all mod archives - don't
extract them! For car and track mods, download and place the bootfiles for the right AMS2 version in the same directory
(they can be found in the [Reiza Forum](https://forum.reizastudios.com/threads/permanent-link-to-bootfiles.30553/) or
on [Project Cars Modding Team's website](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html));
be careful to keep the original name starting with the double underscore. Finally run `AMS2CM.exe` to install all mods.

This will:
- restore the original state (before the previous run)
- extract all mods into a temporary directory
- move all relevant files to the game directory
- fill `vehiclelist.lst` with `crd` files
- fill `tracklist.lst` with `trd` files
- fill `driveline.rg` with record blocks extracted from the installation instructions

If there are no files in `Mods/Enabled`, all previously installed mods and bootfiles will be uninstalled. This is
especially useful before upgrading to a new version of the game.

To update a mod, simply replace the old archive with the new version in the `Mods/Enabled` directory and run AMS2CM
again.

[![Instruction video for v0.1.1](https://img.youtube.com/vi/4tB210UT_rs/hqdefault.jpg)](https://youtu.be/4tB210UT_rs)

### Limitations

- Every time AMS2CM is run, it will do a complete reinstall (uninstall followed by install as describe above).
  Optimisations to update only files that have changed will come in later releases.
- It will try and register all crd and trd files. For mods where this is not correct, the unwanted files will have to
  be manually blacklisted and will not work unless explicitly implemented (e.g. the Dallara IR18 2023 had two such
  files). A future version will make blacklisting configurable. I will also try to work with modders to provide an
  even smoother experience.

## Tested Mods

The following mods were tested and work correctly.

### Cars

- [Alpine A110 Pack](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5
- [Dallara IR18 2023](https://www.racedepartment.com/downloads/dallara-ir18-2023-mod-road-version.59081/) 1.0.1, 1.0.3 (without custom team)
- [Dodge Viper SRT-10 2010](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45
- [Ferrari 250 GTO](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5
- [Ferrari 430 Scuderia](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45
- [Ferrari 458 Italia](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45.1
- [Ferrari FXX-K](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5
- [Ford GT 2006](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 2.45
- [Ford Shelby GT500 2020](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 2.45
- [Jaguar XE SV Project8 2019](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 2.45
- [Lamborghini Essenza SC12](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5.2
- [Lexus LFA 10](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 2.45
- [Mazda RX7 Rocket Bunny Road](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45
- [Nissan Silvia S15 Rocket Bunny](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45
- [Porsche 911 GT3 2018](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 2.45
- [Porsche 935 2019](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5
- [Porsche 992 GT3 Cup](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.4.5
- [Porsche 992 GT3 2022](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) 1.45

### Tracks

- [Azure Coast](https://projectcarsmoddingteam.weebly.com/ams2-tracks.html)
- [California Highway](https://projectcarsmoddingteam.weebly.com/ams2-tracks.html)
- [Mojave](https://projectcarsmoddingteam.weebly.com/ams2-tracks.html)
- [Sugo](https://projectcarsmoddingteam.weebly.com/ams2-tracks.html)

### Skins

- [F1 1988](https://www.racedepartment.com/downloads/ams2-f1-1988-season.54981/) 1.0
- [F1 2022](https://www.racedepartment.com/downloads/f1-2022-skinpack-for-the-f-ultimate-gen-2.50129/) FINAL
