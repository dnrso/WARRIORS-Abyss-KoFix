# 패치된 한국어 아카이브 설치 (-Restore 옵션: 원본 복구)
param([switch]$Restore)
$ErrorActionPreference = 'Stop'
$game = Join-Path $PSScriptRoot '..\WARRIORSAbyss'
$src = Join-Path $PSScriptRoot ($(if ($Restore) { 'backup' } else { 'out' }))

if (Get-Process -Name 'WARRIORSAbyss', 'WARRIORS Abyss' -ErrorAction SilentlyContinue) {
    Write-Host '게임이 실행 중입니다. 게임을 종료한 뒤 다시 실행하세요.' -ForegroundColor Red
    exit 1
}
foreach ($ext in 'BIN', 'IDX') {
    Copy-Item (Join-Path $src "LINKDATA_HAN.$ext") (Join-Path $game "LINKDATA_HAN.$ext") -Force
}
Write-Host $(if ($Restore) { '원본 복구 완료' } else { '패치 설치 완료' }) -ForegroundColor Green
