param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -Offline:$Offline
$app = Join-Path $root '.build\app'
$version = (Get-Item -LiteralPath "$app\PromptDraft.exe").VersionInfo.FileVersion -replace '\.0$', ''
$dist = Join-Path $root 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
# Stage only named distributable files. Never archive the app's data directory.
$stage = Join-Path $root ('.build\package-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $stage -Force | Out-Null
$names = @('PromptDraft.exe', 'LICENSE', 'THIRD_PARTY_NOTICES.txt', 'README.md', 'CHANGELOG.md')
$dependencies = Get-Content -LiteralPath (Join-Path $root 'dependencies.lock.json') -Raw | ConvertFrom-Json
$names += @($dependencies | ForEach-Object file)
foreach ($name in $names) { Copy-Item -LiteralPath (Join-Path $app $name) -Destination $stage }
$archive = Join-Path $dist "PromptDraft-v$version-windows.zip"
Compress-Archive -LiteralPath @(Get-ChildItem -LiteralPath $stage -File | ForEach-Object FullName) -DestinationPath $archive -Force
$hash = (Get-FileHash -LiteralPath $archive -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath "$archive.sha256" -Value "$hash  $([IO.Path]::GetFileName($archive))" -Encoding ascii
Write-Host "Packaged $archive"
