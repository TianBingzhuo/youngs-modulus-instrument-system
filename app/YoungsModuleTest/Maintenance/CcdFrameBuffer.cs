// SPDX-License-Identifier: Apache-2.0
using System;
using System.Collections.Generic;

namespace YoungsModuleTest.Maintenance;

// The original protocol assumes 3648 raw bytes per response, without a header.
internal sealed class CcdFrameBuffer
{
    public const int FrameSize = 3648;
    public const int Capacity = FrameSize * 4;
    private readonly Queue<byte> _bytes = new(Capacity);
    private readonly object _gate = new();

    public bool Append(ReadOnlySpan<byte> bytes)
    {
        lock (_gate)
        {
            if (bytes.Length > Capacity - _bytes.Count)
            {
                _bytes.Clear();
                return false;
            }
            foreach (byte value in bytes) _bytes.Enqueue(value);
            return true;
        }
    }

    public byte[]? TakeFrame()
    {
        lock (_gate)
        {
            if (_bytes.Count < FrameSize) return null;
            var frame = new byte[FrameSize];
            for (int i = 0; i < frame.Length; i++) frame[i] = _bytes.Dequeue();
            return frame;
        }
    }

    public void Clear()
    {
        lock (_gate) _bytes.Clear();
    }
}
