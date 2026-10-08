# 모든 텍스트 테이블을 문자열 열(column) 단위로 덤프하고 일본어/한국어 자동 비교
#   출력: tsv2/kr_<id>.tsv, tsv2/jp_<id>.tsv  (행번호.열번호 <TAB> 텍스트)
#         issues2.tsv (섹터, 행.열, 문제, 원문, 번역)
import glob, os, re
import txt2

def esc(s):
    return s.replace('\\', '\\\\').replace('\x1b', '\\e').replace('\n', '\\n').replace('\t', '\\t')

TAG = re.compile('\x1b(R|..)')

def sig(s, kind):
    return ','.join(sorted(m.group(0) for m in TAG.finditer(s) if len(m.group(0)) == 3 and m.group(0)[1] == kind))

def params(s):
    return ','.join(sorted(re.findall('%s(?:\x1bS\\d)?', s)))

def nums(s):
    p = TAG.sub(' ', s).replace('%s', ' ')
    p = ''.join(chr(ord('0') + ord(c) - ord('０')) if '０' <= c <= '９' else c for c in p)
    return ','.join(sorted(re.findall(r'\d+', p)))

def check(jp, kr):
    if not jp.strip() or jp.startswith('文字列無効'):
        return []
    if not kr.strip():
        return ['MISSING']
    out = []
    if any('\u3041' <= c <= '\u30fa' for c in kr):
        out.append('KANA')
    if params(jp) != params(kr):
        out.append('PARAM')
    if sig(jp, 'U') != sig(kr, 'U'):
        out.append('ICON')
    if sig(jp, 'P') != sig(kr, 'P'):
        out.append('BUTTON')
    if nums(jp) != nums(kr):
        out.append('NUMBER')
    return out

def pairs():
    han = sorted(glob.glob('han/*.bin'))[:44] + ['han/17834.bin']
    jpn = sorted(glob.glob('jpn/*.bin'))[:44] + ['jpn/17830.bin']
    return zip(han, jpn)

if __name__ == '__main__':
    os.makedirs('tsv2', exist_ok=True)
    issues = open('issues2.tsv', 'w', encoding='utf-8')
    for h, j in pairs():
        sid = os.path.basename(h)[:5]
        src = 'patched/' + os.path.basename(h) if os.path.exists('patched/' + os.path.basename(h)) else h
        kr, _ = txt2.read(src)
        jp, _ = txt2.read(j)
        with open(f'tsv2/kr_{sid}.tsv', 'w', encoding='utf-8') as fk, open(f'tsv2/jp_{sid}.tsv', 'w', encoding='utf-8') as fj:
            for i, (rk, rj) in enumerate(zip(kr, jp)):
                for c, (sk, sj) in enumerate(zip(rk, rj)):
                    if sk is None and sj is None:
                        continue
                    sk, sj = sk or '', sj or ''
                    fk.write(f'{i}.{c}\t{esc(sk)}\n')
                    fj.write(f'{i}.{c}\t{esc(sj)}\n')
                    for x in check(sj, sk):
                        issues.write(f'{sid}\t{i}.{c}\t{x}\t{esc(sj)}\t{esc(sk)}\n')
    issues.close()
