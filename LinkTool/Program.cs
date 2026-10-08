using System.IO.Compression;
using System.Text;

// LINKDATA tool for WARRIORS Abyss
// IDX entry (32 bytes): u64 offset, u64 size, u64 storedSize, u64 compressed(0/1)
// Compressed blob: u32 chunkSize, u32 chunkCount, u32 totalSize, u32[chunkCount] chunkSizes, pad to 0x80,
//                  then each chunk: u32 zlibLen + zlib data, padded to 0x80

static class P
{
    record Entry(long Off, long Size, long Stored, long Comp);

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

    static byte[] Decompress(byte[] b)
    {
        int chunkSize = BitConverter.ToInt32(b, 0), count = BitConverter.ToInt32(b, 4), total = BitConverter.ToInt32(b, 8);
        var sizes = new int[count];
        for (int i = 0; i < count; i++) sizes[i] = BitConverter.ToInt32(b, 12 + i * 4);
        var outp = new MemoryStream(total);
        long pos = Align(12 + count * 4);
        for (int i = 0; i < count; i++)
        {
            int zlen = BitConverter.ToInt32(b, (int)pos);
            using (var z = new ZLibStream(new MemoryStream(b, (int)pos + 4, zlen), CompressionMode.Decompress))
                z.CopyTo(outp);
            pos = Align(pos + sizes[i]);
        }
        if (outp.Length != total) throw new Exception($"size mismatch {outp.Length} != {total}");
        return outp.ToArray();
    }

    static byte[] Compress(byte[] data)
    {
        const int chunkSize = 0x10000;
        int count = (data.Length + chunkSize - 1) / chunkSize;
        var chunks = new List<byte[]>();
        for (int i = 0; i < count; i++)
        {
            var ms = new MemoryStream();
            using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true))
                z.Write(data, i * chunkSize, Math.Min(chunkSize, data.Length - i * chunkSize));
            chunks.Add(ms.ToArray());
        }
        var o = new MemoryStream();
        var w = new BinaryWriter(o);
        w.Write(chunkSize); w.Write(count); w.Write(data.Length);
        foreach (var c in chunks) w.Write(c.Length + 4);
        o.SetLength(Align(o.Length)); o.Position = o.Length;
        foreach (var c in chunks)
        {
            w.Write(c.Length); w.Write(c);
            o.SetLength(Align(o.Length)); o.Position = o.Length;
        }
        return o.ToArray();
    }

    static byte[] ReadEntry(FileStream f, Entry e)
    {
        var b = new byte[e.Stored];
        f.Position = e.Off; f.ReadExactly(b);
        return e.Comp == 1 ? Decompress(b) : b;
    }

    // Text table: u32 count, u32 dataSize, u64 0, {u32 relOff, extra bytes}[count] (stride 8 or 12), strings
    static (List<string>, byte[][]) ReadText(string path)
    {
        var d = File.ReadAllBytes(path);
        int n = BitConverter.ToInt32(d, 0), stride = (d.Length - BitConverter.ToInt32(d, 4) - 0x10) / n;
        var t = new List<string>(); var extra = new byte[n][];
        for (int i = 0; i < n; i++)
        {
            int p = 0x10 + i * stride, s = p + BitConverter.ToInt32(d, p), e = s;
            extra[i] = d[(p + 4)..(p + stride)];
            while (d[e] != 0) e++;
            t.Add(Encoding.UTF8.GetString(d, s, e - s));
        }
        return (t, extra);
    }

    static byte[] WriteText(List<string> t, byte[][] extra)
    {
        int n = t.Count, stride = 4 + extra[0].Length, head = 0x10 + n * stride;
        var data = new MemoryStream();
        var offs = new int[n];
        var seen = new Dictionary<string, int>();
        for (int i = 0; i < n; i++)
        {
            if (!seen.TryGetValue(t[i], out offs[i]))
            {
                seen[t[i]] = offs[i] = (int)data.Length;
                data.Write(Encoding.UTF8.GetBytes(t[i])); data.WriteByte(0);
            }
        }
        var o = new MemoryStream(); var w = new BinaryWriter(o);
        w.Write(n); w.Write((int)data.Length); w.Write(0L);
        for (int i = 0; i < n; i++) { w.Write(head + offs[i] - (0x10 + i * stride)); w.Write(extra[i]); }
        data.WriteTo(o);
        return o.ToArray();
    }

    static readonly System.Text.RegularExpressions.Regex Tag = new(@"\x1b(R|..)");

    static string Sig(string s, char kind) =>
        string.Join(",", Tag.Matches(s).Select(m => m.Value).Where(v => v.Length == 3 && v[1] == kind).Order());

    static string Nums(string s)
    {
        var plain = Tag.Replace(s, " ").Replace("%s", " ");
        plain = new string(plain.Select(c => c is >= '０' and <= '９' ? (char)('0' + c - '０') : c).ToArray());
        return string.Join(",", System.Text.RegularExpressions.Regex.Matches(plain, @"\d+").Select(m => m.Value).Order());
    }

    static IEnumerable<string> Check(string jp, string kr)
    {
        if (jp.Trim().Length == 0 || jp.StartsWith("文字列無効")) yield break;
        if (kr.Trim().Length == 0) { yield return "MISSING"; yield break; }
        if (kr.Any(c => c is >= 'ぁ' and <= 'ヺ')) yield return "KANA";
        string ps(string s) => string.Join(",", System.Text.RegularExpressions.Regex.Matches(s, @"%s(\x1bS\d)?").Select(m => m.Value).Order());
        if (ps(jp) != ps(kr)) yield return "PARAM";
        if (Sig(jp, 'U') != Sig(kr, 'U')) yield return "ICON";
        if (Sig(jp, 'P') != Sig(kr, 'P')) yield return "BUTTON";
        if (Nums(jp) != Nums(kr)) yield return "NUMBER";
    }

    static string Esc(string s) => s.Replace("\\", "\\\\").Replace("\x1b", "\\e").Replace("\n", "\\n").Replace("\t", "\\t");
    static string Unesc(string s) => s.Replace("\\\\", "\0").Replace("\\e", "\x1b").Replace("\\n", "\n").Replace("\\t", "\t").Replace("\0", "\\");

    static int Main(string[] a)
    {
        switch (a.Length > 0 ? a[0] : "")
        {
            case "extract": // extract <base> <outdir> [maxSize]   (base = path without .IDX/.BIN)
            {
                var idx = ReadIdx(a[1] + ".IDX");
                using var f = File.OpenRead(a[1] + ".BIN");
                Directory.CreateDirectory(a[2]);
                long max = a.Length > 3 ? long.Parse(a[3]) : long.MaxValue;
                for (int i = 0; i < idx.Count; i++)
                    if (idx[i].Size > 0 && idx[i].Size <= max)
                        try { File.WriteAllBytes(Path.Combine(a[2], $"{i:D5}.bin"), ReadEntry(f, idx[i])); }
                        catch (Exception ex) { Console.Error.WriteLine($"skip {i}: {ex.Message}"); }
                return 0;
            }
            case "find": // find <dir> <text>   searches UTF-8 and UTF-16LE
            {
                var pats = new[] { Encoding.UTF8.GetBytes(a[2]), Encoding.Unicode.GetBytes(a[2]) };
                foreach (var p in Directory.GetFiles(a[1]).Order())
                {
                    var d = File.ReadAllBytes(p);
                    for (int k = 0; k < 2; k++)
                        for (int i = d.AsSpan().IndexOf(pats[k]); i >= 0; )
                        {
                            Console.WriteLine($"{Path.GetFileName(p)} 0x{i:X} {(k == 0 ? "utf8" : "utf16")}");
                            int n = d.AsSpan(i + 1).IndexOf(pats[k]);
                            i = n < 0 ? -1 : i + 1 + n;
                        }
                }
                return 0;
            }
            case "strings": // strings <file>   lists null-terminated UTF-8 strings as offset<TAB>text
            {
                var d = File.ReadAllBytes(a[1]);
                var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                int s = 0;
                for (int i = 0; i < d.Length; i++)
                    if (d[i] == 0)
                    {
                        if (i - s >= 2) o.WriteLine($"{s:X}\t" + Encoding.UTF8.GetString(d, s, i - s).Replace("\n", "\\n"));
                        s = i + 1;
                    }
                o.Flush();
                return 0;
            }
            case "textdump": // textdump <file> <out.tsv>   text table: u32 count, u32 dataSize, u64 0, {u32 relOff, u32 flags}[count], strings
            {
                var (t, _) = ReadText(a[1]);
                File.WriteAllLines(a[2], t.Select((s, i) => $"{i}\t{Esc(s)}"), new UTF8Encoding(false));
                return 0;
            }
            case "textpatch": // textpatch <file> <out> <patch.tsv>   patch lines: index<TAB>text (\e = ESC, \n = newline)
            {
                var (t, flags) = ReadText(a[1]);
                if (flags[0].Length != 4)
                    throw new Exception("항목당 문자열이 여러 개인 테이블입니다. txtpatch.py를 사용하세요.");
                foreach (var line in File.ReadAllLines(a[3], Encoding.UTF8))
                {
                    if (line.Length == 0 || line.StartsWith('#')) continue;
                    var p = line.Split('\t', 2);
                    int i = int.Parse(p[0]);
                    Console.WriteLine($"[{i}] {Esc(t[i])}\n  -> {p[1]}");
                    t[i] = Unesc(p[1]);
                }
                File.WriteAllBytes(a[2], WriteText(t, flags));
                return 0;
            }
            case "compare": // compare <kr.bin> <jp.bin> <label>   prints label, index, issue, jp, kr
            {
                var (kr, _) = ReadText(a[1]);
                var (jp, _) = ReadText(a[2]);
                var o = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
                for (int i = 0; i < kr.Count; i++)
                    foreach (var issue in Check(jp[i], kr[i]))
                        o.WriteLine($"{a[3]}\t{i}\t{issue}\t{Esc(jp[i])}\t{Esc(kr[i])}");
                o.Flush();
                return 0;
            }
            case "repack": // repack <base> <outBase> <id>=<file> ...   writes full copy of archive with replaced entries
            {
                var idx = ReadIdx(a[1] + ".IDX");
                var rep = a.Skip(3).Select(s => s.Split('=', 2)).ToDictionary(s => int.Parse(s[0]), s => s[1]);
                using var f = File.OpenRead(a[1] + ".BIN");
                using var o = File.Create(a[2] + ".BIN");
                var iw = new BinaryWriter(File.Create(a[2] + ".IDX"));
                for (int i = 0; i < idx.Count; i++)
                {
                    var e = idx[i];
                    byte[] stored; long size = e.Size, comp = e.Comp;
                    if (rep.TryGetValue(i, out var file))
                    {
                        var raw = File.ReadAllBytes(file);
                        size = raw.Length;
                        stored = comp == 1 ? Compress(raw) : raw;
                    }
                    else
                    {
                        stored = new byte[e.Stored];
                        f.Position = e.Off; f.ReadExactly(stored);
                    }
                    long off = o.Position;
                    o.Write(stored);
                    o.SetLength(Align(o.Length)); o.Position = o.Length;
                    iw.Write(off); iw.Write(size); iw.Write((long)stored.Length); iw.Write(comp);
                }
                iw.Close();
                return 0;
            }
        }
        Console.WriteLine("usage: extract <base> <outdir> | find <dir> <text> | repack <base> <outBase> <id>=<file>...");
        return 1;
    }
}
