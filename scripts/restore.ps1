param([switch]$Offline)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendor = Join-Path $root 'vendor'
$cache = Join-Path $root '.cache'
New-Item -ItemType Directory -Path $vendor, $cache -Force | Out-Null
Add-Type -AssemblyName System.IO.Compression.FileSystem
$dependencies = Get-Content -LiteralPath (Join-Path $root 'dependencies.lock.json') -Raw | ConvertFrom-Json
foreach ($dependency in $dependencies) {
    $target = Join-Path $vendor $dependency.file
    if ((Test-Path -LiteralPath $target) -and ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -eq $dependency.sha256)) { continue }
    if ($Offline) { throw "Missing or invalid cached dependency: $($dependency.file)" }
    $id = $dependency.id.ToLowerInvariant()
    $version = $dependency.version
    $archive = Join-Path $cache "$id.$version.nupkg"
    $url = "https://api.nuget.org/v3-flatcontainer/$id/$version/$id.$version.nupkg"
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri $url -OutFile $archive -UseBasicParsing
    $zip = [IO.Compression.ZipFile]::OpenRead($archive)
    $pending = "$target.pending"
    try {
        $entry = $zip.GetEntry($dependency.entry)
        if ($null -eq $entry) { throw "Package entry missing: $($dependency.entry)" }
        $inputStream = $entry.Open()
        try {
            $outputStream = [IO.File]::Create($pending)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
    } finally { $zip.Dispose() }
    if ((Get-FileHash -LiteralPath $pending -Algorithm SHA256).Hash -ne $dependency.sha256) { throw "Checksum mismatch: $($dependency.file)" }
    Move-Item -LiteralPath $pending -Destination $target -Force
}
Write-Host 'Dependencies verified.'
