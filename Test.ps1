param(
    [Parameter(Mandatory = $true)]
    [string]$SptRoot,
    [string]$TaskEditionSource,
    [string]$Dotnet = 'dotnet'
)

$ErrorActionPreference = 'Stop'
$SptRoot = [IO.Path]::GetFullPath($SptRoot)
$runtime = Join-Path $SptRoot 'SPT_Runtime'

function Invoke-Dotnet
{
    param([string[]]$Arguments)

    & $Dotnet @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "Checks failed with exit code $LASTEXITCODE"
    }
}

# Pure menu/policy checks and native Fence lifecycle checks require only SPT
# reference assemblies and bundled artwork. They do not start or alter a raid.
Invoke-Dotnet @(
    'run', '--project', (Join-Path $PSScriptRoot 'src/MenuTests/Checks.csproj'),
    "-p:SPTPath=$SptRoot", '-c', 'Release', '--',
    '--storm-audio', (Join-Path $PSScriptRoot 'assets/Audio')
)
Invoke-Dotnet @(
    'run', '--project', (Join-Path $PSScriptRoot 'src/FenceChecks/Checks.csproj'),
    "-p:SPTBinaries=$runtime", '-c', 'Release', '--',
    (Join-Path $PSScriptRoot 'assets')
)

# Cross-package signature checks additionally inspect the separate Tasks build.
# This optional verification dependency is not a runtime gameplay dependency.
if ($TaskEditionSource)
{
    $TaskEditionSource = [IO.Path]::GetFullPath($TaskEditionSource)
    Invoke-Dotnet @(
        'run', '--project', (Join-Path $PSScriptRoot 'src/ClientChecks/Checks.csproj'),
        "-p:SPTPath=$SptRoot", '-c', 'Release', '--',
        $SptRoot,
        (Join-Path $SptRoot 'BepInEx/plugins/spt/spt-common.dll'),
        (Join-Path $PSScriptRoot 'src/Client/bin/Release/netstandard2.1/RaidRoulette.Testing.dll'),
        (Join-Path $TaskEditionSource 'src/Client/bin/Release/netstandard2.1/RaidRoulette.Tasks.Client.dll'),
        (Join-Path $PSScriptRoot 'src/RaidRoulette/bin/Release/RaidRoulette/RaidRoulette.dll'),
        (Join-Path $TaskEditionSource 'src/Server/bin/Release/net10.0/RaidRoulette.Tasks.dll')
    )
}
else
{
    Write-Output 'Cross-package ClientChecks skipped: supply -TaskEditionSource with matching built Tasks assemblies.'
}
