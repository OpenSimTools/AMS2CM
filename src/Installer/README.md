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
