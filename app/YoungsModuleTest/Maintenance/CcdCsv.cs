// SPDX-License-Identifier: Apache-2.0
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace YoungsModuleTest.Maintenance;

internal static class CcdCsv
{
    public const int MaxFileBytes = 128 * 1024;

    public static byte[] Parse(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) > MaxFileBytes)
            throw new InvalidDataException("CCD CSV exceeds 128 KiB.");
        using var reader = new StringReader(text);
        string? header = reader.ReadLine()?.TrimStart('\uFEFF').Trim();
        if (header != "像素索引,光强度" && header != "pixelIndex,intensity")
            throw new InvalidDataException("Expected CCD CSV header: pixelIndex,intensity.");
        var values = new byte[CcdFrameBuffer.FrameSize];
        int count = 0;
        bool trailingBlank = false;
        while (reader.ReadLine() is string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                trailingBlank = true;
                continue;
            }
            if (trailingBlank || count >= values.Length)
                throw new InvalidDataException("Expected one contiguous 3648-sample CCD frame.");
            string[] parts = line.Split(',');
            if (parts.Length != 2 ||
                !int.TryParse(parts[0].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int index) ||
                index != count ||
                !byte.TryParse(parts[1].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out byte intensity))
                throw new InvalidDataException($"Invalid CCD row {count + 2}; indices must be 0..3647 in order and intensities 0..255.");
            values[count++] = intensity;
        }
        if (count != values.Length)
            throw new InvalidDataException("Expected exactly 3648 CCD samples.");
        return values;
    }
}
