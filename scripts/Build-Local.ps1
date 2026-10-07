param(
    [string]$CompilerPath = "$PSScriptRoot\..\.tools\compiler\tasks\net472\csc.exe",
    [string]$FrameworkReferencePath = "$PSScriptRoot\..\.tools\net48\build\.NETFramework\v4.8",
    [string]$VbeInteropPath = 'C:\Windows\assembly\GAC_MSIL\Microsoft.Vbe.Interop\15.0.0.0__71e9bce111e9429c\Microsoft.Vbe.Interop.dll',
    [string]$ExtensibilityPath = 'C:\Windows\assembly\GAC\Extensibility\7.0.3300.0__b03f5f7f11d50a3a\extensibility.dll'
)

$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path "$PSScriptRoot\..").Path
$compiler = (Resolve-Path -LiteralPath $CompilerPath).Path
$vbeInterop = (Resolve-Path -LiteralPath $VbeInteropPath).Path
$extensibility = (Resolve-Path -LiteralPath $ExtensibilityPath).Path
$framework = (Resolve-Path -LiteralPath $FrameworkReferencePath).Path
$project = [xml](Get-Content -LiteralPath (Join-Path $projectRoot 'VbeLineNumbers.csproj'))
$sources = @($project.Project.ItemGroup.Compile.Include | Where-Object { $_ } | ForEach-Object { Join-Path $projectRoot $_ })
$references = @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Drawing.dll', 'System.Windows.Forms.dll') |
    ForEach-Object { '/r:' + (Resolve-Path -LiteralPath (Join-Path $framework $_)).Path }
$generatedDirectory = Join-Path $projectRoot 'obj\LocalBuild'
New-Item -ItemType Directory -Force -Path $generatedDirectory | Out-Null
$frameworkAttribute = Join-Path $generatedDirectory 'TargetFramework.cs'
Set-Content -LiteralPath $frameworkAttribute -Encoding UTF8 -Value '[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]'
$sources += $frameworkAttribute

foreach ($architecture in @('x64', 'x86')) {
    $buildDirectory = Join-Path $projectRoot "bin\$architecture\Release"
    New-Item -ItemType Directory -Force -Path $buildDirectory | Out-Null
    $dllPath = Join-Path $buildDirectory 'VbeLineNumbers.dll'
    $arguments = @(
        '/nologo', '/target:library', "/platform:$architecture", '/optimize+', '/warnaserror+',
        "/out:$dllPath", '/nostdlib+', "/link:$vbeInterop", "/link:$extensibility"
    ) + $references + $sources
    & $compiler @arguments
    if ($LASTEXITCODE -ne 0) { throw "Build failed: $architecture" }
    Copy-Item -LiteralPath (Join-Path $projectRoot 'VbeLineNumbers.ini') -Destination $buildDirectory
    Write-Output "Build succeeded: $architecture -> $dllPath"
}
