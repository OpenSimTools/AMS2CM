# Contributing

## Development

Install the .NET SDK selected by `global.json`.
The GUI uses Uno Skia Desktop with a Win32 host. Windows x64 is the only
supported runtime; Linux hosting and OS integrations are deferred.

```powershell
dotnet build AMS2CM.sln -c Release -p:Platform=x64
dotnet test AMS2CM.sln -c Release -p:Platform=x64 --no-build --no-restore
dotnet run --project src/GUI -p:Platform=x64 -- --Game:Path="C:\TestGame"
```

Use a disposable game directory when testing mod operations. The GUI preserves
the existing command-line configuration, including the beta game overrides.
The installer build publishes a framework-dependent, untrimmed GUI to its
intermediate `gui-publish` directory and packages that output in the MSI.
Users must install the .NET 11 runtime for Windows x64 separately. See
[installer validation](src/Installer/README.md) for Windows Sandbox checks.

Core targets plain .NET. Windows-only defaults for `ISteamLibraryLocator` and
`ISafeFileDelete` are registered behind an OS guard and can be overridden before
calling `AddCoreServices`. Future desktop hosts must supply their own platform
services. Shared UI code uses Uno APIs rather than Windows App SDK or WinUIEx.

Uno requires explicit XAML base types in the four code-behind classes. Rider
may report these declarations as redundant; retain them to avoid Uno's
`UXAML0002` warnings. This inspection exception is approved for this migration.

## Releasing

Assuming we want to release version 1.2.3

- Tag the release and push it to the main repo
  ```
  git tag -a v1.2.3 -m "Release 1.2.3"
  git push origin 1.2.3
  ```
- Create a [new release](https://github.com/OpenSimTools/AMS2CM/releases/new)
  with that tag.
  - Set title to "v1.2.3"
  - Press "Generate release notes" and filter only what is relevant from the
    "What's Changed" section
  - Upload artefacts from the tag build.
  - "Set as a pre-release" if applicable
  - "Publish release"
