param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$SptRoot = [IO.Path]::GetFullPath($SptRoot)
$runtime = Join-Path $SptRoot 'SPT_Runtime'
$requiredFiles = @(
    'SPT_Runtime/SPTarkov.Server.Core.dll',
    'EscapeFromTarkov_Data/Managed/Assembly-CSharp.dll',
    'BepInEx/core/BepInEx.dll'
)
foreach ($relative in $requiredFiles)
{
    if (!(Test-Path -LiteralPath (Join-Path $SptRoot $relative)))
    {
        throw "Required SPT file missing: $relative"
    }
}

function Invoke-Dotnet
{
    param([string[]]$Arguments)

    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet failed with exit code $LASTEXITCODE"
    }
}

Invoke-Dotnet @(
    'build', (Join-Path $PSScriptRoot 'src/RaidRoulette/RaidRoulette.csproj'),
    "-p:SPTBinaries=$runtime", '-c', 'Release', '--nologo'
)
Invoke-Dotnet @(
    'build', (Join-Path $PSScriptRoot 'src/Client/RaidRoulette.Testing.csproj'),
    "-p:SPTPath=$SptRoot", '-c', 'Release', '--nologo'
)

# Build artifacts stay inside this checkout. Supplying an SPT root only reads
# reference assemblies; this script never installs into that root or edits profiles.
$install = Join-Path $PSScriptRoot 'artifacts/INSTALL'
$backend = Join-Path $install 'SPT_Runtime/user/mods/RaidRoulette'
$client = Join-Path $install 'BepInEx/plugins/RaidRoulette'
New-Item -ItemType Directory -Path $backend, $client -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/RaidRoulette/bin/Release/RaidRoulette/RaidRoulette.dll') -Destination $backend
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/Client/bin/Release/netstandard2.1/RaidRoulette.Testing.dll') -Destination $client
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $install

# Keep examples separate from active preferences: an update must not overwrite a
# user's config.json. Include every owned recording and Fence illustration.
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src/RaidRoulette/config.example.json') -Destination $backend
foreach ($asset in @(
    @{ Source = 'assets/Audio'; Destination = (Join-Path $client 'Audio') },
    @{ Source = 'assets/FenceImages'; Destination = (Join-Path $backend 'FenceImages') }
))
{
    New-Item -ItemType Directory -Path $asset.Destination -Force | Out-Null
    foreach ($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot $asset.Source) -File)
    {
        Copy-Item -LiteralPath $file.FullName -Destination $asset.Destination
    }
}
Write-Output "Built artifacts under $install. No game files were changed."
