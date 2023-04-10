# Automobilista 2 Content Manager

Simple tool to keep Automobilista 2 mods in sync automatically. It is currently very basic and doesn't have a graphical user interface.

## Installation

- Download the pre-built binaries from the "artifacts" section of the [latest build](
https://github.com/OpenSimTools/AMS2CM/actions/workflows/ci.yaml?query=event%3Apush).
- Extract the archive to a directory of your choice.

## Usage

Create a `Mods/Enabled` directory in your AMS2 installation directory and place in there all mod archives - don't extract them! For car and track mods, download and place the [bootfiles for the right AMS2 version](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) in the same directory. Finally run `AMS2CM.exe` to install all mods.

If there are no files in `Mods/Enabled`, all previously installed mods and bootfiles will be uninstalled. This is especially useful before upgrading to a new version of the game.

## Tested Mods

Cars:
- `Alpine Pack v1.4.5`
- `AMS2_INDYCAR_2023_V1.0.1`
- `Dodge Viper SRT10 10 AMS2 (V1.45)`
- `Ferrari 250 GTO v1.4.5`
- `Ferrari 430 Scuderia AMS2 (2 cars 1.45)`
- `Ferrari 458 Italia AMS2 (V1.45.1)`
- `Ford GT 2006_AMS2 (V2.45 2 cars)`
- `Ford Shelby GT500 2020 AMS2 (2 cars 2.45)`
- `Jaguar XE SV Project8 (street 2.45)`
- `Lamborghini SCV12 v1.4.5.2`
- `Lexus LFA 10 AMS2 (2 cars 2.45)`
- `Mazda RX7 Rocket Bunny Road AMS2 (1.45)`
- `Nissan Silvia S15 Rocket Bunny (V1.45)`
- `Porsche 911 GT3 2018 AMS2 (V2.45) 4 Cars`
- `Porsche 935 2019 v1.4.5`
- `Porsche 992 cup v1.4.5`
- `Porsche 992 GT3 2022 AMS2 (V.1.45)`

Tracks:
- `azure`
- `Cali_Highway1` + `Cali_Highway2`
- `Mojave_Track_Pack`
- `Sugo`

Liveries:
- `[AMS2]F1_1988_Season`
- `AMS2_F12022`
