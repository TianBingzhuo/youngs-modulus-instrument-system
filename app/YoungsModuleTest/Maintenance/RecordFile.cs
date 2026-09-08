// SPDX-License-Identifier: Apache-2.0
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YoungsModuleTest.Maintenance;

internal static class RecordFile
{
    public const int MaxBytes = 32 * 1024 * 1024;
    // Home and Experiment share this gate for the complete read-modify-write cycle.
    public static SemaphoreSlim Gate { get; } = new(1, 1);
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static async Task<string?> ReadAsync(string path)
    {
        try
        {
            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true);
            if (input.Length > MaxBytes) throw new InvalidDataException("Experiment record file exceeds 32 MiB.");
            using var bytes = new MemoryStream();
            var buffer = new byte[8192];
            int count;
            while ((count = await input.ReadAsync(buffer.AsMemory())) != 0)
            {
                if (bytes.Length + count > MaxBytes) throw new InvalidDataException("Experiment record file exceeds 32 MiB.");
                await bytes.WriteAsync(buffer.AsMemory(0, count));
            }
            return Utf8.GetString(bytes.ToArray()).TrimStart('\uFEFF');
        }
        catch (FileNotFoundException) { return null; }
    }

    public static async Task WriteAsync(string path, string json)
    {
        byte[] bytes = Utf8.GetBytes(json);
        if (bytes.Length > MaxBytes) throw new InvalidDataException("Experiment record file exceeds 32 MiB.");
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8192, true))
            {
                await output.WriteAsync(bytes.AsMemory());
                output.Flush(flushToDisk: true);
            }
            // Same-directory replacement avoids truncating the original on a failed write.
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }
}
