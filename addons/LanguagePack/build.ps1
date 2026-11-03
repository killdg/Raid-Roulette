param(
    [Parameter(Mandatory = $true)]
    [string]$SPTPath,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$package = Join-Path $root 'package'
$artifacts = Join-Path $root 'artifacts'

function Invoke-Dotnet {
    param([string[]]$Arguments)
    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet failed with exit code $LASTEXITCODE"
    }
}

# All references come from the explicitly supplied SPT installation. No SDK,
# game DLL, font, translator or other asset is downloaded by this build script.
Invoke-Dotnet @('format', 'whitespace', $root, '--folder', '--include', 'src', 'tests', 'native-checks', '--verify-no-changes', '--verbosity', 'quiet')
Invoke-Dotnet @('build', (Join-Path $root 'src/Client/LanguagePack.Client.csproj'), '-c', 'Release', "-p:SPTPath=$SPTPath", '--nologo')
Invoke-Dotnet @('build', (Join-Path $root 'src/Server/LanguagePack.csproj'), '-c', 'Release', "-p:SPTBinaries=$(Join-Path $SPTPath 'SPT_Runtime')", '--nologo')
Invoke-Dotnet @('run', '--project', (Join-Path $root 'tests/Checks.csproj'), '-c', 'Release', '--', (Join-Path $root 'Locales'))
Invoke-Dotnet @('run', '--project', (Join-Path $root 'native-checks/Checks.csproj'), '-c', 'Release', "-p:SPTPath=$SPTPath", '--', $SPTPath, (Join-Path $SPTPath 'BepInEx/plugins/RaidRoulette/RaidRoulette.Testing.dll'))

# Keep an existing staging folder rather than deleting paths recursively. A
# fresh staging tree prevents stale DLLs from entering the optional add-on ZIP.
if (Test-Path -LiteralPath $package) {
    Move-Item -LiteralPath $package -Destination ($package + '-previous-' + [guid]::NewGuid().ToString('N'))
}
New-Item -ItemType Directory -Force -Path $artifacts | Out-Null
$client = Join-Path $package 'BepInEx/plugins/RaidRouletteLanguages'
$server = Join-Path $package 'SPT_Runtime/user/mods/RaidRouletteLanguages'
New-Item -ItemType Directory -Force -Path $client, $server | Out-Null
Copy-Item -LiteralPath (Join-Path $root 'src/Client/bin/Release/netstandard2.1/RaidRoulette.LanguagePack.Client.dll') -Destination $client
Copy-Item -LiteralPath (Join-Path $root 'src/Server/bin/Release/net10.0/RaidRoulette.LanguagePack.dll') -Destination $server
foreach ($destination in @($client, $server)) {
    Copy-Item -LiteralPath (Join-Path $root 'Locales') -Destination $destination -Recurse
}
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination $package

$sourceNames = @('src', 'Locales', 'tests', 'native-checks', 'README.md', 'LICENSE', '.editorconfig', '.gitignore', 'build.ps1', 'build_catalogs.py', 'EnglishEvents.json')
$sourceFiles = foreach ($name in $sourceNames) {
    Get-ChildItem -LiteralPath (Join-Path $root $name) -Recurse -File | Where-Object {
        $_.FullName -notmatch '[\\/](bin|obj)[\\/]'
    }
}
$sourceHashes = foreach ($file in $sourceFiles) {
    [ordered]@{
        Path = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$installerHashes = foreach ($file in Get-ChildItem -LiteralPath $package -Recurse -File) {
    [ordered]@{
        Path = [IO.Path]::GetRelativePath($package, $file.FullName).Replace('\', '/')
        SHA256 = (Get-FileHash -LiteralPath $file.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
}
$provenance = [ordered]@{
    Package = 'Raid Roulette Language Pack 1.0.0 beta'
    Source = 'Git tag language-pack-v1.0.0'
    SourceCommit = (& git -C $root rev-parse HEAD).Trim()
    BaseClient = '2.0.146'
    BaseServer = '2.0.0-preview.68'
    CatalogChecks = 6333
    NativeApiChecks = 29
    LiveLanguageValidation = $false
    SourceFiles = @($sourceHashes)
    InstallerFiles = @($installerHashes)
}
$provenance | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $artifacts 'Provenance.json') -Encoding utf8
Compress-Archive -Path (Join-Path $package '*') -DestinationPath (Join-Path $artifacts 'Raid-Roulette-Language-Pack-1.0.0.zip') -Force

# ZipArchive permits an explicit readable-source file list, excluding all
# compiler caches and copied native references from the source distribution.
$sourceZip = Join-Path $artifacts 'Raid-Roulette-Language-Pack-1.0.0-Source.zip'
$stream = [IO.File]::Open($sourceZip, [IO.FileMode]::Create)
$archive = [IO.Compression.ZipArchive]::new($stream, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $sourceFiles) {
        $relative = [IO.Path]::GetRelativePath($root, $file.FullName).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $file.FullName, $relative) | Out-Null
    }
}
finally {
    $archive.Dispose()
    $stream.Dispose()
}
Get-FileHash -LiteralPath (Join-Path $artifacts 'Raid-Roulette-Language-Pack-1.0.0.zip'), $sourceZip -Algorithm SHA256 |
    ForEach-Object { $_.Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_.Path) } |
    Set-Content -LiteralPath (Join-Path $artifacts 'SHA256SUMS.txt') -Encoding utf8
Write-Output "Optional add-on packaged in $artifacts. No base RR files are replaced."

