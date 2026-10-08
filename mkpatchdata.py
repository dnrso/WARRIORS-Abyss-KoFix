# 배포용 패치 데이터 생성: fixes/*.tsv (행 번호 = 열 0), fixes_multi/*.tsv (행.열)
#   출력: KoPatch/patch.tsv  —  항목ID <TAB> 행.열 <TAB> 원래 번역 <TAB> 새 번역   (\e \n \t \\ 이스케이프)
import glob, os
import txt2
from txtpatch import esc

out = {}
for path in sorted(glob.glob('fixes/*.tsv')) + sorted(glob.glob('fixes_multi/*.tsv')):
    sid = os.path.basename(path)[:5]
    rows, _ = txt2.read(f'han/{sid}.bin')  # 원본(백업에서 푼) 한국어
    for line in open(path, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line or line.startswith('#'):
            continue
        key, text = line.split('\t', 1)
        r, c = (map(int, key.split('.')) if '.' in key else (int(key), 0))
        orig = esc(rows[r][c])
        if orig == text:
            continue
        out[(int(sid), r, c)] = (orig, text)  # 같은 줄이 여러 번 나오면 마지막 것

os.makedirs('KoPatch', exist_ok=True)
with open('KoPatch/patch.tsv', 'w', encoding='utf-8', newline='\n') as f:
    for (sid, r, c), (orig, new) in sorted(out.items()):
        f.write(f'{sid}\t{r}.{c}\t{orig}\t{new}\n')
by = {}
for sid, _, _ in out:
    by[sid] = by.get(sid, 0) + 1
print('patch lines', len(out), by)
