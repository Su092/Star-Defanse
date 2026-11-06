# 星轨防线 · Unity 侧常用命令
#
# 用法：powershell -ExecutionPolicy Bypass -File ST/scripts/check.ps1 [-Task all]
#   -Task tests    逻辑自检
#   -Task verify   资源校验
#   -Task build    打包
#   -Task shots    自动截图（需先打包）
#   -Task all      依次执行以上四项

param(
    [ValidateSet("tests", "verify", "build", "shots", "all")]
    [string]$Task = "all"
)

$ErrorActionPreference = "Stop"

$Unity = "D:\Unity\6000.6.4f1\Editor\Unity.exe"
$Project = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$RepoRoot = Split-Path $Project -Parent
$Exe = Join-Path $Project "Build\ST.exe"

function Invoke-Unity([string]$method, [string]$log) {
    Write-Host "==> $method"

    # 每次运行前删掉旧日志，避免编译失败时读到上一次的结果、误报成功
    $unityLog = Join-Path $Project $log
    if (Test-Path $unityLog) { Remove-Item $unityLog -Force }

    & $Unity -batchmode -quit -projectPath $Project -logFile $unityLog `
        -executeMethod $method -nographics | Out-Null
    $code = $LASTEXITCODE

    if (Test-Path $unityLog) {
        $errors = Get-Content $unityLog | Select-String -Pattern "error CS\d+"
        if ($errors) {
            Write-Host "编译失败：" -ForegroundColor Red
            $errors | Select-Object -First 12 | ForEach-Object { Write-Host "  $_" }
            return 1
        }
    }
    return $code
}

function Show-Log([string]$file, [string]$pattern) {
    $path = Join-Path $Project $file
    if (-not (Test-Path $path)) { Write-Host "  (缺少 $file)"; return }
    Get-Content $path | Select-String -Pattern $pattern
}

switch ($Task) {
    "tests" {
        $code = Invoke-Unity "StarLane.EditorTools.STSetup.RunSimTests" "unity_tests.log"
        Show-Log "tests.log" "FAIL|RESULT"
        exit $code
    }
    "verify" {
        $code = Invoke-Unity "StarLane.EditorTools.STBuild.VerifyResources" "unity_verify.log"
        Show-Log "verify.log" "FAIL|RESULT"
        exit $code
    }
    "build" {
        $code = Invoke-Unity "StarLane.EditorTools.STBuild.BuildPlayer" "unity_build.log"
        if ($code -ne 0) { Write-Host "构建失败 (exit=$code)" -ForegroundColor Red; exit $code }
        Show-Log "build.log" "RESULT|ERRORS|SIZE"
        exit $code
    }
        "shots" {
        if (-not (Test-Path $Exe)) { Write-Host "先执行 -Task build"; exit 1 }
        & $Exe -autoshot -screen-width 1080 -screen-height 1920 -screen-fullscreen 0 | Out-Null
        Start-Sleep -Seconds 2
        $log = Join-Path $env:USERPROFILE "AppData\LocalLow\StarLane Studio\星轨防线 STARLANE BULWARK\Player.log"
        if (Test-Path $log) { Get-Content $log -Encoding UTF8 | Select-String -Pattern "PASS|FAIL|INPUT RESULT|SHOTS DONE|FSM|AIM" }
        Write-Host "截图目录: $(Join-Path $Project 'Shots')"
        exit 0
    }
    "all" {
        & $PSCommandPath -Task tests
        & $PSCommandPath -Task verify
        & $PSCommandPath -Task build
        & $PSCommandPath -Task shots
    }
}
