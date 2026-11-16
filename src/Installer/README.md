# Installer validation

Windows Sandbox must be installed and enabled on the host. Follow Microsoft's
[Windows Sandbox installation instructions](https://learn.microsoft.com/en-us/windows/security/application-security/application-isolation/windows-sandbox/windows-sandbox-install).
Complete any required restart before continuing.

After building, open the **generated** configuration in `src\Installer\bin\{Platform}\{Configuration}\Sandbox.wsb`
(e.g. for a Release build, open `x64\Release\Sandbox.wsb`).

Do not open the source `src\Installer\Sandbox.wsb` directly: its `HostFolder` is empty. The build copies it to the
output folder and fills in that folder's absolute path. Rebuild after moving the checkout or output directory.

The generated configuration maps the build output to `C:\Installer` inside the sandbox and starts the MSI installer
automatically. Installation logs are recorded in `C:\Installer\install.log`.

Install the .NET 11 runtime for Windows x64 inside the sandbox before launching
the application. The MSI packages a framework-dependent GUI and does not bundle
or install .NET.

## Expected validation warnings

Uno's fonts are installed privately alongside the application. MSI validation
reports ICE60 because these versioned fonts have no language ID and are not
registered in the system Font table. Keep their original metadata: Microsoft
[specifies that fonts should have no language ID](https://learn.microsoft.com/en-us/windows/win32/msi/file-table).
These warnings remain visible with approval for this migration. Repair may
unnecessarily recopy the affected fonts.

ICE61 also reports the existing `AllowSameVersionUpgrades` policy, which permits
reinstalling a build with the same MSI version. This migration preserves that
upgrade policy.
