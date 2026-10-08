# 배포 묶음 생성: dist/WARRIORS_Abyss_KoFix_v1.0.zip (실행 파일 + 사용설명서 + 수정목록)
import os, re, zipfile

VER = '1.0'
SECTIONS = {
    42: '무기·특성·유보·염마장·망자의 기억 효과 설명',
    435: '소환기 효과 설명',
    182: '시스템 메시지',
    200: '미션 메시지',
    489: '옵션·키 설정 메시지',
}

def clean(s):
    s = re.sub(r'%s\\eS(\d)', r'{\1}', s)
    s = re.sub(r'\\e(?:[CUAP].|R)', '', s)
    return s.replace('\\n', ' / ').replace('%%', '%')

rows = [l.rstrip('\n').split('\t') for l in open('KoPatch/patch.tsv', encoding='utf-8')]
lines = [f'WARRIORS Abyss 한국어 오역 수정 패치 v{VER} - 수정 목록', '',
         '{0}, {1} 등은 게임이 수치·이름을 채워 넣는 자리입니다.',
         '같은 문장이 게임 데이터 여러 곳에 있는 경우 한 번만 적었습니다.', '']
for sid, title in SECTIONS.items():
    seen = set()
    items = []
    for s, key, orig, new in rows:
        if int(s) == sid and (orig, new) not in seen:
            seen.add((orig, new))
            items.append((clean(orig), clean(new)))
    n = sum(1 for r in rows if int(r[0]) == sid)
    lines.append(f'■ {title} ({len(items)}종, 데이터 {n}곳)')
    lines.append('')
    for o, nw in items:
        lines += [f'  수정 전: {o}', f'  수정 후: {nw}', '']
open('KoPatch/수정목록.txt', 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(lines))

readme = f'''WARRIORS Abyss 한국어 오역 수정 패치 v{VER}
==================================================

공식 한국어 번역에서 일본어 원문과 뜻이 다르거나 수치가 잘못 표시되는
문장을 원문에 맞게 고치는 비공식 패치입니다.

■ 수정 내용 ({len(rows)}곳)
  - 무기·특성·유보·염마장·망자의 기억 효과 설명 : {sum(1 for r in rows if r[0]=='42')}곳
      예) 조운(어나더) 광룡신창 "속성 계열 증표 1개당…" → "증표 「속」 1개당 공격력 및 민첩 +2%"
          공격력 수치·피눈물 수치가 표시되지 않던 문장, +/- 부호가 반대로 된 문장,
          "회피 후"가 "회복"으로 번역된 문장 등
  - 소환기 효과 설명 : {sum(1 for r in rows if r[0]=='435')}곳 (속성 오역, 누락 등. 소환기 이름은 그대로)
  - 비어 있던 시스템·미션·옵션 메시지 번역 : {sum(1 for r in rows if r[0] in ('182','200','489'))}곳
  자세한 내용은 "수정목록.txt"를 참고하세요.
  용어(영걸, 증표, 피눈물 등)와 고유명사는 공식 번역을 그대로 따릅니다.

■ 사용 방법
  1. 게임을 완전히 종료합니다.
  2. WARRIORS_Abyss_KoFix.exe 를 게임 폴더(WARRIORS Abyss.exe 가 있는 폴더)에 넣고 실행합니다.
     (다른 곳에서 실행하면 게임 폴더 경로를 물어봅니다. 폴더를 창에 끌어다 놓아도 됩니다.)
  3. "1. 패치 적용"을 선택합니다.

■ 원래대로 되돌리기
  같은 프로그램을 실행하고 "2. 원래 번역으로 복구"를 선택합니다.
  패치할 때 만든 백업(WARRIORSAbyss 폴더의 *.kofix_backup 파일)으로 되돌립니다.
  백업 파일은 복구 전까지 지우지 마세요.

■ 안내
  - 바뀌는 파일은 WARRIORSAbyss\\LINKDATA_HAN.BIN / LINKDATA_HAN.IDX 두 개뿐입니다 (한국어 텍스트).
  - 문장마다 "원래 번역이 예상한 문장과 같은지" 확인한 뒤에만 바꿉니다.
    게임 업데이트 등으로 문장이 달라졌으면 그 문장은 건너뛰고, 결과에 건너뛴 개수가 표시됩니다.
  - 여러 번 실행해도 안전합니다. 이미 적용된 문장은 다시 바꾸지 않습니다.
  - 게임 업데이트나 Steam "파일 무결성 검사" 후에는 패치가 풀릴 수 있습니다. 다시 실행하면 됩니다.
  - 서명되지 않은 프로그램이라 Windows SmartScreen 경고가 나올 수 있습니다.
    ("추가 정보" → "실행")
  - 설치가 필요 없습니다 (.NET 런타임 포함).

■ 대상
  WARRIORS Abyss (Ultimate Edition, PC) 한국어 텍스트
'''
open('KoPatch/README.txt', 'w', encoding='utf-8-sig', newline='\r\n').write(readme)

os.makedirs('dist', exist_ok=True)
zp = f'dist/WARRIORS_Abyss_KoFix_v{VER}.zip'
with zipfile.ZipFile(zp, 'w', zipfile.ZIP_DEFLATED) as z:
    z.write('dist_build/WARRIORS_Abyss_KoFix.exe', 'WARRIORS_Abyss_KoFix.exe')
    z.write('KoPatch/README.txt', 'README.txt')
    z.write('KoPatch/수정목록.txt', '수정목록.txt')
print(zp, os.path.getsize(zp), 'bytes;', len(lines), 'list lines')
