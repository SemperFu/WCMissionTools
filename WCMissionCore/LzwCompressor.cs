namespace WCMissionCore;

/// <summary>
/// LZW compressor for Origin Systems' game data files.
/// Produces output compatible with LzwDecompressor / WCToolbox LzwCodec.
///
/// Format: variable-width codes starting at 9 bits, max 12 bits (LSB-first).
/// Code 256 = clear/reset dictionary. Code 257 = end of stream.
/// </summary>
public static class LzwCompressor
{
    const int ClearCode = 256;
    const int EndCode = 257;
    const int FirstCode = 258;
    const int MaxBits = 12;
    const int MaxDictSize = 1 << MaxBits; // 4096

    public static byte[] Compress(byte[] input)
    {
        if (input.Length == 0)
            return [];

        using var ms = new MemoryStream();
        int bitWidth = 9;

        // Bit writer state
        int bitBuffer = 0;
        int bitsInBuffer = 0;

        void WriteCode(int code)
        {
            bitBuffer |= code << bitsInBuffer;
            bitsInBuffer += bitWidth;
            while (bitsInBuffer >= 8)
            {
                ms.WriteByte((byte)(bitBuffer & 0xFF));
                bitBuffer >>= 8;
                bitsInBuffer -= 8;
            }
        }

        void FlushBits()
        {
            if (bitsInBuffer > 0)
            {
                ms.WriteByte((byte)(bitBuffer & 0xFF));
                bitBuffer = 0;
                bitsInBuffer = 0;
            }
        }

        // Dictionary: maps (prefix, byte) → code
        // Use a flat hash table for speed
        var dict = new Dictionary<(int prefix, byte append), int>();
        int dictSize = FirstCode;

        void ResetDict()
        {
            dict.Clear();
            dictSize = FirstCode;
            bitWidth = 9;
        }

        // Emit initial clear code
        WriteCode(ClearCode);

        int w = input[0]; // current prefix (starts as first literal byte)

        for (int i = 1; i < input.Length; i++)
        {
            byte c = input[i];
            var key = (w, c);

            if (dict.TryGetValue(key, out int existingCode))
            {
                // Extend the match
                w = existingCode;
            }
            else
            {
                // Output code for w
                WriteCode(w);

                // Add new dictionary entry
                if (dictSize < MaxDictSize)
                {
                    dict[key] = dictSize;
                    dictSize++;

                    // Increase bit width when we've used all codes at current width
                    if (dictSize > (1 << bitWidth) && bitWidth < MaxBits)
                        bitWidth++;
                }
                else
                {
                    // Dictionary full — emit clear code and reset
                    WriteCode(ClearCode);
                    ResetDict();
                }

                // Start new string with c
                w = c;
            }
        }

        // Output code for remaining string
        WriteCode(w);

        // Emit end code
        WriteCode(EndCode);
        FlushBits();

        return ms.ToArray();
    }
}
