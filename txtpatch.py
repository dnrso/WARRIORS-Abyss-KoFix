# 항목당 문자열이 여러 개인 텍스트 테이블 패치
#   python txtpatch.py <in.bin> <out.bin> <patch.tsv>
#   patch.tsv: "행.열<TAB>텍스트" (\e = ESC, \n = 줄바꿈), #으로 시작하면 주석
# 문자열 열 = 모든 항목에서 유효한 문자열 위치를 가리키는 u32 필드. 그 외 필드는 원본 그대로 유지.
import struct, sys
import txt2

def unesc(s):
    return s.replace('\\\\', '\0').replace('\\e', '\x1b').replace('\\n', '\n').replace('\\t', '\t').replace('\0', '\\')

def esc(s):
    return s.replace('\\', '\\\\').replace('\x1b', '\\e').replace('\n', '\\n').replace('\t', '\\t')

def load(path):
    d = open(path, 'rb').read()
    rows, stride = txt2.read(path)
    n = len(rows)
    cols = [k for k in range(stride // 4) if all(r[k] is not None for r in rows)]
    return d, rows, stride, cols

def build(d, rows, stride, cols):
    n = len(rows)
    head = 16 + n * stride
    entries = bytearray(d[16:head])
    data = bytearray()
    seen = {}
    for i, r in enumerate(rows):
        for k in cols:
            s = r[k]
            if s not in seen:
                seen[s] = len(data)
                data += s.encode('utf-8') + b'\0'
            field = 16 + i * stride + k * 4
            struct.pack_into('<i', entries, i * stride + k * 4, head + seen[s] - field)
    out = bytearray(d[:16])
    struct.pack_into('<II', out, 0, n, len(data))
    return bytes(out + entries + data)

if __name__ == '__main__':
    src, dst, patch = sys.argv[1:4]
    d, rows, stride, cols = load(src)
    for line in open(patch, encoding='utf-8'):
        line = line.rstrip('\n')
        if not line or line.startswith('#'):
            continue
        key, text = line.split('\t', 1)
        i, k = map(int, key.split('.'))
        if k not in cols:
            raise SystemExit(f'{key}: 문자열 열이 아님')
        print(f'[{key}] {esc(rows[i][k])}\n    -> {text}')
        rows[i][k] = unesc(text)
    new = build(d, rows, stride, cols)
    open(dst, 'wb').write(new)
    # 검증: 다시 읽어 문자열과 비문자열 필드가 의도대로인지 확인
    rows2, _ = txt2.read(dst)
    assert [[r[k] for k in cols] for r in rows2] == [[r[k] for k in cols] for r in rows], 'string mismatch'
    for i in range(len(rows)):
        for k in range(stride // 4):
            if k not in cols:
                a = d[16 + i * stride + k * 4:16 + i * stride + k * 4 + 4]
                b = new[16 + i * stride + k * 4:16 + i * stride + k * 4 + 4]
                assert a == b, f'field changed {i}.{k}'
    print('verified', len(rows), 'rows, string columns', cols)
