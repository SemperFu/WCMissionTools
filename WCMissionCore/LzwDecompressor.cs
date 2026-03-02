namespace WCMissionCore;

/// <summary>
/// LZW decompressor for Origin Systems' compressed game data files.
/// Used in Wing Commander 1/2 MODULE, CAMP, and other data files.
/// 
/// Format: variable-width codes starting at 9 bits, max 12 bits.
/// Code 256 = clear/reset dictionary. Code 257 = end of stream.
/// </summary>
public static class LzwDecompressor
{
    const int ClearCode = 256;
    const int EndCode = 257;
    const int FirstCode = 258;
    const int MaxBits = 12;
    const int MaxDictSize = 1 << MaxBits; // 4096

    public static byte[] Decompress(byte[] input, int inputOffset, int uncompressedSize)
    {
        var output = new byte[uncompressedSize];
        int outPos = 0;

        // Dictionary: each entry is a prefix code + append byte
        var dictPrefix = new int[MaxDictSize];
        var dictAppend = new byte[MaxDictSize];
        var dictLength = new int[MaxDictSize];

        // Decode buffer for building strings
        var decodeBuffer = new byte[MaxDictSize];

        int dictSize = FirstCode;
        int bitWidth = 9;

        // Bit reader state
        int bitPos = 0;
        int bytePos = inputOffset;

        int ReadCode()
        {
            // Read bitWidth bits from input, LSB first
            int code = 0;
            int bitsRead = 0;
            int bp = bitPos;
            int byp = bytePos;

            while (bitsRead < bitWidth)
            {
                if (byp >= input.Length) return EndCode;
                int bitsAvail = 8 - bp;
                int bitsNeeded = bitWidth - bitsRead;
                int grab = Math.Min(bitsAvail, bitsNeeded);
                int mask = (1 << grab) - 1;
                code |= ((input[byp] >> bp) & mask) << bitsRead;
                bitsRead += grab;
                bp += grab;
                if (bp >= 8) { bp = 0; byp++; }
            }

            bitPos = bp;
            bytePos = byp;
            return code;
        }

        void WriteString(int code)
        {
            // Build the string for this code in reverse, then write forward
            int len = 0;
            int c = code;
            while (c >= FirstCode)
            {
                decodeBuffer[len++] = dictAppend[c];
                c = dictPrefix[c];
            }
            decodeBuffer[len++] = (byte)c;

            // Write in reverse order (forward string)
            for (int i = len - 1; i >= 0 && outPos < uncompressedSize; i--)
                output[outPos++] = decodeBuffer[i];
        }

        byte FirstChar(int code)
        {
            while (code >= FirstCode)
                code = dictPrefix[code];
            return (byte)code;
        }

        void ResetDict()
        {
            dictSize = FirstCode;
            bitWidth = 9;
        }

        // Initialize dictionary with single-byte entries (0-255)
        ResetDict();

        int prevCode = ReadCode();
        if (prevCode == EndCode) return output;
        if (prevCode == ClearCode) { ResetDict(); prevCode = ReadCode(); }
        if (prevCode == EndCode) return output;

        // Write first code (single byte)
        if (outPos < uncompressedSize)
            output[outPos++] = (byte)prevCode;

        while (outPos < uncompressedSize)
        {
            int code = ReadCode();
            if (code == EndCode) break;

            if (code == ClearCode)
            {
                ResetDict();
                prevCode = ReadCode();
                if (prevCode == EndCode) break;
                if (outPos < uncompressedSize)
                    output[outPos++] = (byte)prevCode;
                continue;
            }

            if (code < dictSize)
            {
                // Code is in dictionary
                WriteString(code);

                // Add new entry: prevCode + first char of code's string
                if (dictSize < MaxDictSize)
                {
                    dictPrefix[dictSize] = prevCode;
                    dictAppend[dictSize] = FirstChar(code);
                    dictLength[dictSize] = (prevCode < FirstCode ? 1 : dictLength[prevCode]) + 1;
                    dictSize++;
                }
            }
            else
            {
                // Special case: code == dictSize (not yet in dictionary)
                // String is prevCode's string + first char of prevCode's string
                if (dictSize < MaxDictSize)
                {
                    dictPrefix[dictSize] = prevCode;
                    dictAppend[dictSize] = FirstChar(prevCode);
                    dictLength[dictSize] = (prevCode < FirstCode ? 1 : dictLength[prevCode]) + 1;
                    dictSize++;
                }
                WriteString(code);
            }

            // Increase bit width when dictionary grows past current capacity
            if (dictSize >= (1 << bitWidth) && bitWidth < MaxBits)
                bitWidth++;

            prevCode = code;
        }

        return output;
    }
}
