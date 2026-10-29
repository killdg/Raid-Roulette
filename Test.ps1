param([Parameter(Mandatory=$true)][string]$SptRoot,[Parameter(Mandatory=$true)][string]$TaskEditionSource,[string]$Dotnet='dotnet',[switch]$IncludeHarmonySmoke)
$ErrorActionPreference='Stop'
$SptRoot=[IO.Path]::GetFullPath($SptRoot)
$runtime=Join-Path $SptRoot 'SPT_Runtime'
$TaskEditionSource=[IO.Path]::GetFullPath($TaskEditionSource)
function Invoke-Dotnet([string[]]$Arguments){& $Dotnet @Arguments;if($LASTEXITCODE -ne 0){throw "Checks failed with exit code $LASTEXITCODE"}}
$backend=@('run','--project',(Join-Path $PSScriptRoot 'src\Tests\Checks.csproj'),"-p:SPTBinaries=$runtime","-p:RaidRouletteTasksSource=$TaskEditionSource",'--')
if(!$IncludeHarmonySmoke){$backend+='--skip-harmony-smoke'}
$backend+=@('--native-db',(Join-Path $runtime 'SPT_Data\database'),'--native-map',(Join-Path $runtime 'SPT_Data\database\locations\tarkovstreets\base.json'))
Invoke-Dotnet $backend
$menu=@('run','--project',(Join-Path $PSScriptRoot 'src\MenuTests\Checks.csproj'),"-p:SPTPath=$SptRoot",'--')
$audio=Join-Path $PSScriptRoot 'assets\Audio'
if((Test-Path -LiteralPath (Join-Path $audio 'Thunder1.wav')) -and (Test-Path -LiteralPath (Join-Path $audio 'ThunderReplacement.wav')) -and (Test-Path -LiteralPath (Join-Path $audio 'HeavyRain.wav'))){$menu+=@('--storm-audio',$audio)}
Invoke-Dotnet $menu
Invoke-Dotnet @('run','--project',(Join-Path $PSScriptRoot 'src\ClientChecks\Checks.csproj'),"-p:SPTPath=$SptRoot",'--',$SptRoot,'unavailable',(Join-Path $PSScriptRoot 'src\Client\bin\Release\netstandard2.1\RaidRoulette.Testing.dll'),(Join-Path $TaskEditionSource 'src\QuestPackClient\bin\Release\netstandard2.1\RaidRoulette.Quests.Client.dll'),(Join-Path $PSScriptRoot 'src\RaidRoulette\bin\Release\RaidRoulette\RaidRoulette.dll'),(Join-Path $TaskEditionSource 'src\QuestPackServer\bin\Release\net10.0\RaidRoulette.Quests.dll'))
