param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'restore.ps1') -Offline:$Offline
$output = Join-Path $root '.build\app'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'The Windows .NET Framework C# compiler was not found.' }
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$compilerArgs = @('/nologo', '/target:winexe', '/optimize+', "/out:$output\PromptDraft.exe", "/win32icon:$root\assets\App.ico", "/resource:$root\assets\App.ico,PromptDraft.App.ico", "/resource:$root\assets\App.png,PromptDraft.App.png", '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', "/reference:$root\vendor\Markdig.dll")
& $compiler @compilerArgs @sources
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
$dependencies = Get-Content -LiteralPath (Join-Path $root 'dependencies.lock.json') -Raw | ConvertFrom-Json
foreach ($dependency in $dependencies) { Copy-Item -LiteralPath (Join-Path "$root\vendor" $dependency.file) -Destination $output -Force }
foreach ($name in @('LICENSE', 'THIRD_PARTY_NOTICES.txt', 'README.md', 'CHANGELOG.md')) { Copy-Item -LiteralPath (Join-Path $root $name) -Destination $output -Force }
Write-Host "Built $output\PromptDraft.exe"
