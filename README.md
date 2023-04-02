# Automobilista 2 Content Manager

Simple tool to keep Automobilista 2 mods in sync automatically. It is currently very basic and doesn't have a graphical user interface.

## Installation

- Download the pre-built binaries from the "artifacts" section of the [latest build](
https://github.com/OpenSimTools/AMS2CM/actions/workflows/ci.yaml?query=event%3Apush).
- Extract the archive to a directory of your choice.

## Usage

Create a `Mods/Enabled` directory in your AMS2 installation directory and place in there all mod archives - don't extract them! For car and track mods, download and place the [bootfiles for the right AMS2 version](https://projectcarsmoddingteam.weebly.com/downloads---automobilista-2.html) in the same directory. Finally run `AMS2CM.exe` to install all mods.

If there are no files in `Mods/Enabled`, all previously installed mods and bootfiles will be uninstalled. This is especially useful before upgrading to a new version of the game.
