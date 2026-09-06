# build.ps1 - Compile the mod against your local 7 Days To Die install.
# Usage:  pwsh ./build.ps1            (Release, default)
#         pwsh ./build.ps1 -Configuration Debug

param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$proj = Get-ChildItem (Join-Path $root "Source") -Filter "*.csproj" | Select-Object -First 1
if (-not $proj) {
    Write-Error "No .csproj found under $root\Source"
    exit 1
}

Write-Host "Building $($proj.Name) [$Configuration] against your GameDir..." -ForegroundColor Cyan

$xml = [xml](Get-Content $proj.FullName -Raw)
$isSdk = [bool]$xml.Project.Sdk

if ($isSdk) {
    # SDK-style project -> dotnet build
    dotnet build $proj.FullName -c $Configuration
} else {
    # Legacy (non-SDK) csproj, e.g. FicusHUD -> needs MSBuild
    $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
    $msbuild = if (Test-Path $vswhere) {
        & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" |
            Select-Object -First 1
    }
    if (-not $msbuild) {
        Write-Error "MSBuild not found. Install the Visual Studio Build Tools."
        exit 1
    }
    & $msbuild $proj.FullName /p:Configuration=$Configuration /v:minimal
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$dll = Join-Path $root ($proj.BaseName + ".dll")
if (Test-Path $dll) {
    Write-Host "Build OK -> $dll" -ForegroundColor Green
    Write-Host ""
    Write-Host "Next steps (commit + release):" -ForegroundColor Cyan
    Write-Host "  git add -A"
    Write-Host "  git commit -m \"Bump to x.y.z\""
    Write-Host "  git push"
    Write-Host "  git tag vx.y.z"
    Write-Host "  git push --tags"
} else {
    Write-Warning "Build finished, but expected DLL not found at $dll. Check OutputPath in the csproj."
}
