# fixes/*.tsv 를 원본(backup) 한국어 텍스트에 적용해 out/LINKDATA_HAN 생성
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$tool = '.\bin\LinkTool.exe'

if (-not (Test-Path 'han\00042.bin')) { & $tool extract backup\LINKDATA_HAN han }
New-Item -ItemType Directory -Force patched, out | Out-Null

$args_ = @()
foreach ($fix in Get-ChildItem fixes\*.tsv) {
    $id = $fix.BaseName
    & $tool textpatch "han\$id.bin" "patched\$id.bin" $fix.FullName | Out-Null
    if ($LASTEXITCODE) { throw "textpatch 실패: $id" }
    $args_ += "$([int]$id)=patched\$id.bin"
    Write-Host "적용: $id ($((Get-Content $fix -Encoding utf8 | Where-Object { $_ -and -not $_.StartsWith('#') }).Count)줄)"
}
# 항목당 문자열이 여러 개인 테이블은 Python 패처 사용 (fixes_multi/*.tsv, 키 = 행.열)
$py = if ($env:KOFIX_PYTHON) { $env:KOFIX_PYTHON } else { 'python' }  # Python 경로는 KOFIX_PYTHON 환경 변수로 지정 가능
$env:PYTHONIOENCODING = 'utf-8'
foreach ($fix in Get-ChildItem fixes_multi\*.tsv -ErrorAction SilentlyContinue) {
    $id = $fix.BaseName
    & $py txtpatch.py "han\$id.bin" "patched\$id.bin" $fix.FullName | Out-Null
    if ($LASTEXITCODE) { throw "txtpatch 실패: $id" }
    $args_ += "$([int]$id)=patched\$id.bin"
    Write-Host "적용: $id ($((Get-Content $fix -Encoding utf8 | Where-Object { $_ -and -not $_.StartsWith('#') }).Count)줄, 다중 문자열)"
}
& $tool repack backup\LINKDATA_HAN out\LINKDATA_HAN @args_
if ($LASTEXITCODE) { throw 'repack 실패' }
Write-Host '빌드 완료: out\LINKDATA_HAN' -ForegroundColor Green
