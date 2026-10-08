# 항목에 문자열 위치가 여러 개 있는 텍스트 테이블 읽기
# 헤더: u32 count, u32 dataSize, u64 0, 항목[count] (stride = (파일크기-dataSize-16)/count)
# 항목 안의 각 u32 필드 중 데이터 영역을 가리키는 상대 오프셋을 문자열로 해석
import struct, sys

def read(path):
    d = open(path, 'rb').read()
    n, ds = struct.unpack_from('<II', d, 0)
    stride = (len(d) - ds - 16) // n
    data0 = 16 + n * stride
    rows = []
    for i in range(n):
        base = 16 + i * stride
        row = []
        for k in range(0, stride, 4):
            p = base + k
            v = struct.unpack_from('<i', d, p)[0]
            t = p + v
            if v > 0 and data0 <= t < len(d) and (t == data0 or d[t - 1] == 0):
                e = d.index(b'\0', t)
                row.append(d[t:e].decode('utf-8', 'replace'))
            else:
                row.append(None)
        rows.append(row)
    return rows, stride

if __name__ == '__main__':
    rows, stride = read(sys.argv[1])
    print('stride', stride)
    for i, r in enumerate(rows[:int(sys.argv[2]) if len(sys.argv) > 2 else None]):
        print(i, r)
