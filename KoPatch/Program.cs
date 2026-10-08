using System.IO.Compression;
using System.Text;

// WARRIORS Abyss 한국어 오역 수정 패치
// LINKDATA_HAN.IDX/.BIN 안의 텍스트 테이블에서, 원래 번역이 예상과 같은 문장만 새 번역으로 바꾼다.

static class KoPatch
{
    const string Title = "WARRIORS Abyss 한국어 오역 수정 패치 v1.0";
    const string Base = "LINKDATA_HAN";
    const string BackupExt = ".kofix_backup";

    record Entry(long Off, long Size, long Stored, long Comp);
    record Patch(int Id, int Row, int Col, string Orig, string New);

    static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine(Title);
        Console.WriteLine(new string('=', 50));
        int code;
        try { code = Run(args); }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine("[오류] " + ex.Message);
            code = 1;
        }
        if (!args.Contains("--no-pause"))
        {
            Console.WriteLine();
            Console.WriteLine("아무 키나 누르면 종료합니다.");
            try { Console.ReadKey(true); } catch { }
        }
        return code;
    }

    static int Run(string[] args)
    {
        string dir = FindDataDir(args.FirstOrDefault(a => !a.StartsWith("--")));
        Console.WriteLine("게임 데이터 폴더: " + dir);
        string idx = Path.Combine(dir, Base + ".IDX"), bin = Path.Combine(dir, Base + ".BIN");

        string mode = args.Contains("--apply") ? "1" : args.Contains("--restore") ? "2" : null;
        if (mode == null)
        {
            Console.WriteLine();
            Console.WriteLine("  1. 패치 적용");
            Console.WriteLine("  2. 원래 번역으로 복구 (패치 전 백업으로 되돌림)");
            Console.Write("번호를 입력하세요: ");
            mode = Console.ReadLine()?.Trim();
        }
        CheckNotLocked(idx, bin);
        return mode switch
        {
            "1" => Apply(idx, bin),
            "2" => Restore(idx, bin),
            _ => throw new Exception("1 또는 2를 입력하세요."),
        };
    }

    // ---------- 폴더 찾기 ----------

    static string FindDataDir(string arg)
    {
        var candidates = new List<string>();
        if (arg != null) candidates.Add(arg.Trim('"'));
        string exeDir = AppContext.BaseDirectory;
        candidates.Add(exeDir);
        candidates.Add(Environment.CurrentDirectory);
        foreach (var c in candidates)
            foreach (var d in new[] { c, Path.Combine(c, "WARRIORSAbyss") })
                if (File.Exists(Path.Combine(d, Base + ".IDX")) && File.Exists(Path.Combine(d, Base + ".BIN")))
                    return Path.GetFullPath(d);
        while (true)
        {
            Console.WriteLine("게임 폴더를 찾지 못했습니다.");
            Console.WriteLine("이 프로그램을 게임 폴더(WARRIORS Abyss.exe가 있는 곳)에 넣고 실행하거나,");
            Console.Write("게임 폴더 경로를 입력하세요 (탐색기에서 폴더를 끌어다 놓아도 됩니다): ");
            string p = Console.ReadLine()?.Trim().Trim('"');
            if (string.IsNullOrEmpty(p)) throw new Exception("게임 폴더가 지정되지 않았습니다.");
            foreach (var d in new[] { p, Path.Combine(p, "WARRIORSAbyss") })
                if (File.Exists(Path.Combine(d, Base + ".IDX")) && File.Exists(Path.Combine(d, Base + ".BIN")))
                    return Path.GetFullPath(d);
        }
    }

    static void CheckNotLocked(params string[] files)
    {
        foreach (var f in files)
        {
            try { using var s = new FileStream(f, FileMode.Open, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { throw new Exception("게임이 실행 중입니다. 게임을 완전히 종료한 뒤 다시 실행하세요."); }
        }
    }

    // ---------- 적용 / 복구 ----------

    static int Apply(string idxPath, string binPath)
    {
        var patches = LoadPatches();
        var idx = ReadIdx(idxPath);
        var replaced = new Dictionary<int, byte[]>();
        int applied = 0, already = 0, mismatch = 0;

        using (var f = File.OpenRead(binPath))
        {
            foreach (var g in patches.GroupBy(p => p.Id))
            {
                var raw = ReadEntry(f, idx[g.Key]);
                var table = TextTable.Parse(raw);
                int changed = 0;
                foreach (var p in g)
                {
                    string cur = table.Get(p.Row, p.Col);
                    if (cur == p.New) already++;
                    else if (cur == p.Orig) { table.Set(p.Row, p.Col, p.New); changed++; }
                    else mismatch++;
                }
                if (changed > 0) replaced[g.Key] = table.Build();
                applied += changed;
            }
        }

        Console.WriteLine();
        Console.WriteLine($"수정 대상 {patches.Count}줄: 새로 적용 {applied}, 이미 적용됨 {already}, 원문 불일치로 건너뜀 {mismatch}");
        if (mismatch > patches.Count / 2)
            Console.WriteLine("※ 건너뛴 줄이 많습니다. 게임 버전이 패치 제작 시점(2026-03 Ultimate Edition)과 다를 수 있습니다.");
        if (applied == 0)
        {
            Console.WriteLine(already > 0 ? "이미 패치가 적용되어 있습니다." : "적용할 수 있는 수정이 없습니다.");
            return mismatch > 0 && already == 0 ? 1 : 0;
        }

        // 최초 적용 시 원본 백업
        if (!File.Exists(idxPath + BackupExt))
        {
            File.Copy(binPath, binPath + BackupExt);
            File.Copy(idxPath, idxPath + BackupExt);
            Console.WriteLine("원래 파일 백업: " + Path.GetFileName(binPath) + BackupExt + ", " + Path.GetFileName(idxPath) + BackupExt);
        }

        Repack(idxPath, binPath, idx, replaced);
        Console.WriteLine("패치 적용 완료!");
        return 0;
    }

    static int Restore(string idxPath, string binPath)
    {
        if (!File.Exists(idxPath + BackupExt) || !File.Exists(binPath + BackupExt))
            throw new Exception("백업 파일이 없습니다. 이 프로그램으로 패치한 적이 없거나 백업이 삭제되었습니다.");
        File.Copy(binPath + BackupExt, binPath, true);
        File.Copy(idxPath + BackupExt, idxPath, true);
        File.Delete(binPath + BackupExt);
        File.Delete(idxPath + BackupExt);
        Console.WriteLine();
        Console.WriteLine("원래 번역으로 복구했습니다.");
        return 0;
    }

    static List<Patch> LoadPatches()
    {
        using var s = typeof(KoPatch).Assembly.GetManifestResourceStream("patch.tsv")!;
        using var r = new StreamReader(s, Encoding.UTF8);
        var list = new List<Patch>();
        string line;
        while ((line = r.ReadLine()) != null)
        {
            if (line.Length == 0) continue;
            var p = line.Split('\t');
            var rc = p[1].Split('.');
            list.Add(new Patch(int.Parse(p[0]), int.Parse(rc[0]), int.Parse(rc[1]), Unesc(p[2]), Unesc(p[3])));
        }
        return list;
    }

    static string Unesc(string s) =>
        s.Replace("\\\\", "\0").Replace("\\e", "\x1b").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\0", "\\");

    // ---------- LINKDATA 아카이브 ----------
    // IDX 항목 32바이트: u64 오프셋, u64 크기, u64 저장 크기, u64 압축 여부
    // 압축 데이터: u32 청크크기, u32 청크수, u32 전체크기, u32[청크수] 청크별 크기, 0x80 정렬, 청크마다 u32 길이 + zlib

    static List<Entry> ReadIdx(string p)
    {
        var d = File.ReadAllBytes(p);
        var l = new List<Entry>();
        for (int i = 0; i < d.Length / 32; i++)
            l.Add(new(BitConverter.ToInt64(d, i * 32), BitConverter.ToInt64(d, i * 32 + 8),
                      BitConverter.ToInt64(d, i * 32 + 16), BitConverter.ToInt64(d, i * 32 + 24)));
        return l;
    }

    static long Align(long v) => (v + 0x7F) & ~0x7FL;

    static byte[] ReadEntry(FileStream f, Entry e)
    {
        var b = new byte[e.Stored];
        f.Position = e.Off; f.ReadExactly(b);
        return e.Comp == 1 ? Decompress(b) : b;
    }

    static byte[] Decompress(byte[] b)
    {
        int count = BitConverter.ToInt32(b, 4), total = BitConverter.ToInt32(b, 8);
        var o = new MemoryStream(total);
        long pos = Align(12 + count * 4);
        for (int i = 0; i < count; i++)
        {
            int size = BitConverter.ToInt32(b, 12 + i * 4), zlen = BitConverter.ToInt32(b, (int)pos);
            using (var z = new ZLibStream(new MemoryStream(b, (int)pos + 4, zlen), CompressionMode.Decompress))
                z.CopyTo(o);
            pos = Align(pos + size);
        }
        if (o.Length != total) throw new Exception("압축 해제 크기 불일치");
        return o.ToArray();
    }

    static byte[] Compress(byte[] data)
    {
        const int chunk = 0x10000;
        int count = (data.Length + chunk - 1) / chunk;
        var parts = new List<byte[]>();
        for (int i = 0; i < count; i++)
        {
            var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                z.Write(data, i * chunk, Math.Min(chunk, data.Length - i * chunk));
            parts.Add(ms.ToArray());
        }
        var o = new MemoryStream(); var w = new BinaryWriter(o);
        w.Write(chunk); w.Write(count); w.Write(data.Length);
        foreach (var c in parts) w.Write(c.Length + 4);
        o.SetLength(Align(o.Length)); o.Position = o.Length;
        foreach (var c in parts) { w.Write(c.Length); w.Write(c); o.SetLength(Align(o.Length)); o.Position = o.Length; }
        return o.ToArray();
    }

    static void Repack(string idxPath, string binPath, List<Entry> idx, Dictionary<int, byte[]> replaced)
    {
        string tmpBin = binPath + ".tmp", tmpIdx = idxPath + ".tmp";
        using (var f = File.OpenRead(binPath))
        using (var o = File.Create(tmpBin))
        using (var iw = new BinaryWriter(File.Create(tmpIdx)))
        {
            for (int i = 0; i < idx.Count; i++)
            {
                var e = idx[i];
                byte[] stored; long size = e.Size;
                if (replaced.TryGetValue(i, out var raw))
                {
                    size = raw.Length;
                    stored = e.Comp == 1 ? Compress(raw) : raw;
                }
                else
                {
                    stored = new byte[e.Stored];
                    f.Position = e.Off; f.ReadExactly(stored);
                }
                long off = o.Position;
                o.Write(stored);
                o.SetLength(Align(o.Length)); o.Position = o.Length;
                iw.Write(off); iw.Write(size); iw.Write((long)stored.Length); iw.Write(e.Comp);
            }
        }
        // 새 파일을 다시 읽어 수정 항목이 정상인지 확인한 뒤 교체
        var newIdx = ReadIdx(tmpIdx);
        using (var f = File.OpenRead(tmpBin))
            foreach (var (id, raw) in replaced)
                if (!ReadEntry(f, newIdx[id]).AsSpan().SequenceEqual(raw))
                    throw new Exception("검증 실패: 패치 결과가 올바르지 않습니다. 원본은 변경되지 않았습니다.");
        File.Move(tmpBin, binPath, true);
        File.Move(tmpIdx, idxPath, true);
    }
}

// 텍스트 테이블: u32 개수, u32 문자열영역크기, u64 0, 항목[개수] (항목 크기 = (파일크기-영역크기-16)/개수), 문자열 영역
// 항목 안의 u32 필드 중 모든 항목에서 문자열 시작을 가리키는 필드 = 문자열 열 (필드 위치 기준 상대 오프셋)
class TextTable
{
    byte[] d; int n, stride; List<int> cols; string[][] rows;

    public static TextTable Parse(byte[] d)
    {
        var t = new TextTable { d = d };
        t.n = BitConverter.ToInt32(d, 0);
        t.stride = (d.Length - BitConverter.ToInt32(d, 4) - 16) / t.n;
        int data0 = 16 + t.n * t.stride, k = t.stride / 4;
        t.rows = new string[t.n][];
        for (int i = 0; i < t.n; i++)
        {
            t.rows[i] = new string[k];
            for (int c = 0; c < k; c++)
            {
                int p = 16 + i * t.stride + c * 4, v = BitConverter.ToInt32(d, p), s = p + v;
                if (v > 0 && s >= data0 && s < d.Length && (s == data0 || d[s - 1] == 0))
                {
                    int e = Array.IndexOf(d, (byte)0, s);
                    t.rows[i][c] = Encoding.UTF8.GetString(d, s, e - s);
                }
            }
        }
        t.cols = Enumerable.Range(0, k).Where(c => t.rows.All(r => r[c] != null)).ToList();
        return t;
    }

    public string Get(int r, int c) => r < n && cols.Contains(c) ? rows[r][c] : null;

    public void Set(int r, int c, string s) => rows[r][c] = s;

    public byte[] Build()
    {
        int head = 16 + n * stride;
        var entries = d[16..head];
        var data = new MemoryStream();
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
            foreach (int c in cols)
            {
                string s = rows[i][c];
                if (!seen.TryGetValue(s, out int off))
                {
                    seen[s] = off = (int)data.Length;
                    data.Write(Encoding.UTF8.GetBytes(s)); data.WriteByte(0);
                }
                int field = 16 + i * stride + c * 4;
                BitConverter.TryWriteBytes(entries.AsSpan(i * stride + c * 4), head + off - field);
            }
        var o = new MemoryStream();
        o.Write(d, 0, 16);
        o.Write(entries);
        data.WriteTo(o);
        var r = o.ToArray();
        BitConverter.TryWriteBytes(r.AsSpan(4), (int)data.Length);
        return r;
    }
}
