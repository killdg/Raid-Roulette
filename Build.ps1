param([Parameter(Mandatory=$true)][string]$SptRoot,[string]$Dotnet='dotnet')
$ErrorActionPreference='Stop'
$SptRoot=[IO.Path]::GetFullPath($SptRoot)
$runtime=Join-Path $SptRoot 'SPT_Runtime'
foreach($relative in @('SPT_Runtime\SPTarkov.Server.Core.dll','EscapeFromTarkov_Data\Managed\Assembly-CSharp.dll','BepInEx\core\BepInEx.dll')){
 if(!(Test-Path -LiteralPath (Join-Path $SptRoot $relative))){throw "Required SPT/game file missing: $relative"}
}
function Invoke-Dotnet([string[]]$Arguments){& $Dotnet @Arguments;if($LASTEXITCODE -ne 0){throw "dotnet failed with exit code $LASTEXITCODE"}}
Invoke-Dotnet @('build',(Join-Path $PSScriptRoot 'src\RaidRoulette\RaidRoulette.csproj'),"-p:SPTBinaries=$runtime",'-c','Release','--nologo')
Invoke-Dotnet @('build',(Join-Path $PSScriptRoot 'src\Client\RaidRoulette.Testing.csproj'),"-p:SPTPath=$SptRoot",'-c','Release','--nologo')
$backend=Join-Path $PSScriptRoot 'artifacts\INSTALL\SPT_Runtime\user\mods\RaidRoulette'
$client=Join-Path $PSScriptRoot 'artifacts\INSTALL\BepInEx\plugins\RaidRoulette'
New-Item -ItemType Directory -Path $backend,$client -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\RaidRoulette\bin\Release\RaidRoulette\RaidRoulette.dll') -Destination $backend
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\Client\bin\Release\netstandard2.1\RaidRoulette.Testing.dll') -Destination $client
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'src\RaidRoulette\config.example.json') -Destination $backend
$audio=Join-Path $client 'Audio'
New-Item -ItemType Directory -Path $audio -Force | Out-Null
foreach($name in @('Thunder1.wav','ThunderReplacement.wav','HeavyRain.wav')){
 $source=Join-Path $PSScriptRoot ('assets\Audio\'+$name)
 if(Test-Path -LiteralPath $source){Copy-Item -LiteralPath $source -Destination $audio}else{Write-Host "Optional recording absent: $name"}
}
Write-Host "Built artifacts under $(Join-Path $PSScriptRoot 'artifacts\INSTALL'). No game files were changed."
