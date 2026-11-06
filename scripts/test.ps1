param([switch]$Ui, [switch]$Offline)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'build.ps1') -Offline:$Offline
$output = Join-Path $root '.build\tests'
New-Item -ItemType Directory -Path $output -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $root 'vendor') -Filter '*.dll' | Copy-Item -Destination $output -Force
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs' | ForEach-Object FullName)
$suites = @('StorageTests', 'CoreTests')
if ($Ui) { $suites += @('FeatureTests', 'SidebarPaintTests') }
foreach ($suite in $suites) {
    $compilerArgs = @('/nologo', '/target:exe', "/main:$suite", "/out:$output\$suite.exe", "/resource:$root\assets\App.ico,PromptDraft.App.ico", "/resource:$root\assets\App.png,PromptDraft.App.png", '/reference:System.Windows.Forms.dll', '/reference:System.Drawing.dll', '/reference:System.Web.Extensions.dll', "/reference:$root\vendor\Markdig.dll")
    & $compiler @compilerArgs @sources "$root\tests\$suite.cs"
    if ($LASTEXITCODE -ne 0) { throw "Test compilation failed: $suite" }
    & "$output\$suite.exe" "$output\$suite-data"
    if ($LASTEXITCODE -ne 0) { throw "Test suite failed: $suite" }
}
Write-Host 'All requested tests passed.'
