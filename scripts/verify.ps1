param(
    [Parameter(Mandatory=$true)][string]$Editor,
    [ValidateSet('EditMode','PlayMode','Prepare','Build')][string]$Mode='EditMode'
)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path $PSScriptRoot -Parent
$evidence=Join-Path $projectRoot 'artifacts'
New-Item -ItemType Directory -Force $evidence | Out-Null
$unityArgs=@('-batchmode','-nographics','-projectPath',$projectRoot,'-logFile',(Join-Path $evidence "$Mode.log"))
if($Mode -in @('EditMode','PlayMode')) {
    $unityArgs+=@('-runTests','-testPlatform',$Mode,'-testResults',(Join-Path $evidence "$Mode.xml"))
} else {
    $method=if($Mode -eq 'Build'){'BuildWindows'}else{'CreateScene'}
    $unityArgs+=@('-quit','-executeMethod',"CraneTactics.Editor.BuildOneNight.$method")
}
$quotedArgs=$unityArgs | ForEach-Object { '"' + $_ + '"' }
$process=Start-Process -FilePath $Editor -ArgumentList $quotedArgs -WindowStyle Hidden -PassThru -Wait
if($process.ExitCode -ne 0){throw "Unity exited with $($process.ExitCode); see artifacts/$Mode.log"}
if($Mode -in @('EditMode','PlayMode')) {
    [xml]$result=Get-Content (Join-Path $evidence "$Mode.xml")
    if($result.'test-run'.result -ne 'Passed'){throw 'Unity tests did not pass'}
    Write-Output "$Mode passed: $($result.'test-run'.passed) tests"
}
