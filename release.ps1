# 배포용 패치 다시 만들기: fixes/*.tsv, fixes_multi/*.tsv -> dist/WARRIORS_Abyss_KoFix_v*.zip
# 버전을 올릴 때는 KoPatch/Program.cs(Title), KoPatch/KoPatch.csproj(Version), mkdist.py(VER)를 함께 바꾼다.
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$py = if ($env:KOFIX_PYTHON) { $env:KOFIX_PYTHON } else { 'python' }  # Python 경로는 KOFIX_PYTHON 환경 변수로 지정 가능
$env:PYTHONIOENCODING = 'utf-8'

& $py mkpatchdata.py;                                   if ($LASTEXITCODE) { throw 'mkpatchdata 실패' }
dotnet publish KoPatch -c Release -o dist_build | Out-Null; if ($LASTEXITCODE) { throw 'publish 실패' }
& $py mkdist.py;                                        if ($LASTEXITCODE) { throw 'mkdist 실패' }
Write-Host '배포 파일 생성 완료 (dist 폴더)' -ForegroundColor Green
