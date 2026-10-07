# Build the native layer and drop the artifact into native/out/<rid>/
# Usage: ./build.ps1                    build for the current platform
# Usage: ./build.ps1 -Target <triple>   build for a target that is installed through rustup
# Other platforms are produced by the release workflow on their own runners

param(
    [string]$Target = ""
)

$ErrorActionPreference = "Stop"

$Root = $PSScriptRoot
$Output = Join-Path $Root "out"

# Without an explicit target the host triple is read straight off rustc rather than guessed
if ([string]::IsNullOrWhiteSpace($Target)) {
    $Target = (& rustc -vV | Select-String "^host:").ToString().Split(":")[1].Trim()
    if ([string]::IsNullOrWhiteSpace($Target)) {
        throw "could not read the host target from rustc -vV"
    }
}

# Triple to nuget rid and artifact name
# A cdylib has no prefix on Windows, and lib* on Linux and macOS
$Known = @{
    "x86_64-pc-windows-msvc"    = @{ Rid = "win-x64";   File = "netcraft_native.dll" }
    "x86_64-unknown-linux-gnu"  = @{ Rid = "linux-x64"; File = "libnetcraft_native.so" }
    "x86_64-apple-darwin"       = @{ Rid = "osx-x64";   File = "libnetcraft_native.dylib" }
    "aarch64-unknown-linux-gnu" = @{ Rid = "linux-arm64"; File = "libnetcraft_native.so" }
    "aarch64-apple-darwin"      = @{ Rid = "osx-arm64"; File = "libnetcraft_native.dylib" }
}

if (-not $Known.ContainsKey($Target)) {
    throw "no rid mapping for target $Target, add one to this script"
}

$Info = $Known[$Target]
Write-Host "==> cargo build --release --target $Target"

Push-Location $Root
try {
    & cargo build --release --target $Target
    if ($LASTEXITCODE -ne 0) {
        throw "cargo build failed with exit code $LASTEXITCODE"
    }
}
finally {
    Pop-Location
}

$Built = Join-Path $Root "target\$Target\release\$($Info.File)"
if (-not (Test-Path $Built)) {
    throw "cargo reported success but $Built is missing"
}

$Destination = Join-Path $Output $Info.Rid
New-Item -ItemType Directory -Force -Path $Destination | Out-Null
Copy-Item $Built (Join-Path $Destination $Info.File) -Force

Write-Host "==> $($Info.Rid) -> $(Join-Path $Destination $Info.File)"
