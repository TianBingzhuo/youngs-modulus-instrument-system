// SPDX-License-Identifier: Apache-2.0
using System.Text;
using YoungsModuleTest.Maintenance;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    passed++;
    Console.WriteLine("PASS " + name);
}
void Invalid(Action action, string name)
{
    try { action(); }
    catch (InvalidDataException) { Check(true, name); return; }
    throw new Exception("Expected InvalidDataException: " + name);
}
var expected = Enumerable.Range(0, CcdFrameBuffer.FrameSize).Select(i => (byte)(i % 256)).ToArray();
var frames = new CcdFrameBuffer();
Check(frames.Append(expected.AsSpan(0, 1000)), "append partial frame");
Check(frames.TakeFrame() == null, "partial frame not published");
Check(frames.Append(expected.AsSpan(1000)), "append remaining bytes");
Check(frames.TakeFrame()!.SequenceEqual(expected), "split frame byte order");
Check(frames.TakeFrame() == null, "frame consumed once");
Check(frames.Append(expected.Concat(expected).ToArray()), "append two frames");
Check(frames.TakeFrame()!.SequenceEqual(expected) && frames.TakeFrame()!.SequenceEqual(expected), "coalesced frames");
Check(!frames.Append(new byte[CcdFrameBuffer.Capacity + 1]), "oversized input rejected");
Check(frames.TakeFrame() == null, "overflow clears input");
frames.Append(new byte[CcdFrameBuffer.Capacity]);
Check(!frames.Append(new byte[1]), "cumulative overflow");
frames.Append(expected);
frames.Clear();
Check(frames.TakeFrame() == null, "stop clears stale bytes");
string csv = "pixelIndex,intensity\n" + string.Join("\n", expected.Select((v, i) => $"{i},{v}")) + "\n";
Check(CcdCsv.Parse(csv).SequenceEqual(expected), "CSV round trip");
Check(CcdCsv.Parse("\uFEFF" + csv.Replace("pixelIndex,intensity", "像素索引,光强度").Replace("\n", "\r\n")).SequenceEqual(expected), "Chinese header BOM CRLF");
Invalid(() => CcdCsv.Parse("wrong\n" + csv), "wrong header");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "1,0\n")), "unordered index");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "0,256\n")), "intensity overflow");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "0,-1\n")), "negative intensity");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "0,NaN\n")), "non-numeric intensity");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "0,0,1\n")), "extra column");
Invalid(() => CcdCsv.Parse(csv.Replace("0,0\n", "")), "short frame");
Invalid(() => CcdCsv.Parse(csv + "3648,1\n"), "long frame");
Invalid(() => CcdCsv.Parse(csv.Replace("10,10\n", "\n10,10\n")), "row gap");
Invalid(() => CcdCsv.Parse(new string('x', CcdCsv.MaxFileBytes + 1)), "CSV size limit");
// Only a new random test directory is used; real experiment records are never opened.
string temp = Path.Combine(Path.GetTempPath(), "youngs-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temp);
try
{
    string path = Path.Combine(temp, "records.json");
    Check(await RecordFile.ReadAsync(path) == null, "missing records");
    await RecordFile.WriteAsync(path, "[]");
    Check(await RecordFile.ReadAsync(path) == "[]", "initial write");
    await RecordFile.WriteAsync(path, "[1]");
    Check(await RecordFile.ReadAsync(path) == "[1]", "record replacement");
    try
    {
        await RecordFile.WriteAsync(path, new string('x', RecordFile.MaxBytes + 1));
        throw new Exception("Oversized write accepted.");
    }
    catch (InvalidDataException) { Check(await RecordFile.ReadAsync(path) == "[1]", "failed write preserves original"); }
    await File.WriteAllBytesAsync(path, new byte[] { 0xFF });
    try
    {
        await RecordFile.ReadAsync(path);
        throw new Exception("Invalid UTF-8 accepted.");
    }
    catch (DecoderFallbackException) { Check(true, "invalid UTF-8 rejected"); }
    await RecordFile.WriteAsync(path, "0");
    await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
    {
        await RecordFile.Gate.WaitAsync();
        try
        {
            int count = int.Parse((await RecordFile.ReadAsync(path))!);
            await RecordFile.WriteAsync(path, (count + 1).ToString());
        }
        finally { RecordFile.Gate.Release(); }
    }));
    Check(await RecordFile.ReadAsync(path) == "20", "no lost updates");
    Check(Directory.GetFiles(temp, "*.tmp").Length == 0, "temporary file cleanup");
    using (var file = File.Create(path)) file.SetLength(RecordFile.MaxBytes + 1L);
    try
    {
        await RecordFile.ReadAsync(path);
        throw new Exception("Oversized read accepted.");
    }
    catch (InvalidDataException) { Check(true, "record read size limit"); }
    string directory = Path.Combine(temp, "not-a-file");
    Directory.CreateDirectory(directory);
    try
    {
        await RecordFile.WriteAsync(directory, "[]");
        throw new Exception("Directory replaced.");
    }
    catch (IOException) { Check(Directory.Exists(directory), "failed replacement preserves target"); }
    catch (UnauthorizedAccessException) { Check(Directory.Exists(directory), "failed replacement preserves target"); }
    Check(Directory.GetFiles(temp, "*.tmp").Length == 0, "failure cleanup");
}
finally { Directory.Delete(temp, recursive: true); }
Console.WriteLine($"{passed} maintenance checks passed (no hardware access).");
