# VbeLineNumbers

VbeLineNumbers is a COM add-in that displays line numbers beside the VBA editor code pane.

Current target:

- Microsoft Access 2021 64-bit
- Microsoft Access 2021 32-bit
- VBE / VBA editor
- .NET Framework 4.8

## 64-bit Installation

Download the 64-bit release zip from GitHub Releases and extract it to a local folder.

The zip contains:

- `VbeLineNumbers.dll`
- `VbeLineNumbers.ini`

Open PowerShell as Administrator and run:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" `
  "C:\Path\To\VbeLineNumbers.dll" `
  /codebase
```

Replace `C:\Path\To\VbeLineNumbers.dll` with the extracted DLL path.

After registration, start Access and open the VBE. The add-in is registered under:

```text
HKCU\Software\Microsoft\VBA\VBE\6.0\Addins64\VbeLineNumbers.Connect
```

## 64-bit Uninstall

Open PowerShell as Administrator and run:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe" `
  "C:\Path\To\VbeLineNumbers.dll" `
  /unregister
```

## 32-bit Installation

Download the 32-bit release zip from GitHub Releases and extract it to a local folder.

The zip contains:

- `VbeLineNumbers.dll`
- `VbeLineNumbers.ini`

Open PowerShell as Administrator and run:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" `
  "C:\Path\To\VbeLineNumbers.dll" `
  /codebase
```

Replace `C:\Path\To\VbeLineNumbers.dll` with the extracted DLL path.

After registration, start Access and open the VBE. The 32-bit add-in is registered under:

```text
HKCU\Software\Microsoft\VBA\VBE\6.0\Addins\VbeLineNumbers.Connect
```

## 32-bit Uninstall

Open PowerShell as Administrator and run:

```powershell
& "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\RegAsm.exe" `
  "C:\Path\To\VbeLineNumbers.dll" `
  /unregister
```

## Settings

`VbeLineNumbers.ini` must be placed next to `VbeLineNumbers.dll`.

```ini
BackgroundColor=Gainsboro
```

`BackgroundColor` accepts named colors such as `Gainsboro` or HTML-style values such as `#F0F0F0`.

## Development Build

Build the 64-bit Debug configuration:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  VbeLineNumbers.csproj `
  /p:Configuration=Debug `
  /p:Platform=x64
```

The Debug output is:

```text
bin\x64\Debug\VbeLineNumbers.dll
```

Build the 64-bit Release configuration:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\amd64\MSBuild.exe" `
  VbeLineNumbers.csproj `
  /p:Configuration=Release `
  /p:Platform=x64
```

The Release output is:

```text
bin\x64\Release\VbeLineNumbers.dll
```

Build the 32-bit Release configuration:

```powershell
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" `
  VbeLineNumbers.csproj `
  /p:Configuration=Release `
  /p:Platform=x86
```

The 32-bit Release output is:

```text
bin\x86\Release\VbeLineNumbers.dll
```

## Notes

- Keep `Guid` and `ProgId` unchanged for COM compatibility.
- 64-bit VBE registration uses the `Addins64` registry path.
- 32-bit VBE registration uses the `Addins` registry path.
- Build outputs such as `bin/`, `obj/`, DLLs, PDBs, and local test databases are not committed to Git.
