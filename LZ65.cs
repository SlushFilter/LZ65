namespace LZ65
{
    public class LZ65()
    {
        public const byte RAW_FLAG = 0b00000000; // RAW : Copy the next n bytes from source buffer to destination buffer.
        public const byte REP_FLAG = 0b01000000; // REP : Copy n bytes from destination buffer at idx.
        public const byte RLE_FLAG = 0b10000000; // RLE : Repeat the next source buffer byte n times.
        public const byte EOS_FLAG = 0b11000000; // EOS : End of Stream
        public const byte COM_MASK = 0b11000000; // Masking bits for command flag bits.
        public const byte LEN_MASK = 0b00111111; // Masking bits for length bits.
        public static void PrintBytes(byte[] data, int columns)
        {
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Green;
            for (int i = 0; i < data.Length; i = i + columns)
                Console.Write(BitConverter.ToString(data, i, ((columns + i) >= data.Length) ? data.Length - i : columns).Replace('-', ' '));
            Console.WriteLine();
            Console.ForegroundColor = fg;
        }


        public static Stream Compress(Stream inStream)
        {


            Span<byte> srcData = BufferSourceStream(inStream);

            // Compression Algorithm
            Console.WriteLine("");
            Console.WriteLine("[ Token Generation ]");

            List<LzToken> tokens = new List<LzToken>();
            LzToken? rawToken = null;
            LzToken repToken;
            LzToken rleToken;
            LzToken token;
            int idx = 0;
            while (idx < srcData.Length)
            {
                // Find best compression method (RLE > REP > RAW) and generate a token.
                rleToken = RunLengthAt(idx, srcData);
                repToken = RepLengthAt(idx, srcData);
                if (rleToken.length >= repToken.length)
                {
                    token = rleToken;
                }
                else
                {
                    token = repToken;
                }

                // Merge sequential RAW_FLAG tokens.
                if (token.commandFlag == RAW_FLAG)
                {
                    if (rawToken == null)
                    {
                        rawToken = token;
                    }
                    else
                    {
                        rawToken.length += token.length;
                    }
                }
                else
                {
                    // Push the last RAW_FLAG token if present.
                    if (rawToken != null)
                    {
                        tokens.Add(rawToken);
                        rawToken = null;
                    }
                    tokens.Add(token);
                }
                idx += token.length;
            }
            if(rawToken != null) tokens.Add(rawToken);
            tokens.Add(new LzToken(0, 0, EOS_FLAG));

            // Generate output bytes from Tokens
            MemoryStream outStream = new MemoryStream();
            BinaryWriter writer = new BinaryWriter(outStream);
            foreach (LzToken t in tokens)
                writer.Write(t.EmitCompressStream(srcData));

            return outStream;
        }
        public static Stream Decompress(Stream inStream)
        {
            // Buffer in source data.
            Span<byte> srcData = BufferSourceStream(inStream);

            // Decompress Algorithm
            Console.WriteLine("");
            Console.WriteLine("[ Token Generation ]");
            List<LzToken> tokens = new List<LzToken>();
            LzToken token;
            byte command;
            byte length;
            int step = 1;
            for (int idx = 0; idx < srcData.Length; idx += step)
            {

                command = (byte)(srcData[idx] & COM_MASK);
                length = (byte)(srcData[idx] & LEN_MASK);
                token = new LzToken(idx, length, command);

                switch (command)
                {
                    case RAW_FLAG:
                        step = length + 1;
                        break;
                    case REP_FLAG:
                    case RLE_FLAG:
                        step = 2;
                        break;
                    default:
                        step = 1;
                        break;
                }
                //Console.WriteLine(token.ToString());
                tokens.Add(token);
            }

            // Generate output bytes from Tokens            
            Span<byte> dstData = new Span<byte>(new byte[256]);
            int dstIdx = 0;
            foreach (LzToken t in tokens)
                dstIdx += t.WriteDecompressBuffer(srcData, dstData, dstIdx);
            return new MemoryStream(dstData.Slice(0, dstIdx).ToArray());
        }
        private static LzToken RunLengthAt(int idx, Span<byte> srcData)
        {
            byte testData = srcData[idx];
            int runLength = 1;

            for (int i = idx + 1; i < srcData.Length; i++)
                if (testData == srcData[i]) runLength++;
                else break;

            if (runLength == 1) return new LzToken(idx, 1, LZ65.RAW_FLAG);
            else return new LzToken(idx, runLength, LZ65.RLE_FLAG);
        }

        private static LzToken RepLengthAt(int idx, Span<byte> srcData)
        {
            int length = 0;
            int matchIdx = 0;
            int matchLen = 0;
            while ((matchIdx < idx) && ((length + matchIdx) < srcData.Length))
            {
                length++;
                if ((idx + length) > srcData.Length)
                    break;

                byte t = (byte)srcData.IndexOf<byte>(srcData.Slice(idx, length));
                if (t >= 0 && t < idx)
                {
                    matchIdx = t;
                    matchLen = length;
                }
                else
                {
                    break;
                }
            }
            if (matchLen > 1) return new LzToken(matchIdx, matchLen, LZ65.REP_FLAG);
            else return new LzToken(idx, 1, LZ65.RAW_FLAG);
        }
        private static Span<byte> BufferSourceStream(Stream srcStream)
        {
            BinaryReader reader = new BinaryReader(srcStream);
            Span<byte> span = new Span<byte>(new byte[srcStream.Length]);
            int srcSize = reader.Read(span);
            Console.WriteLine("Buffered in " + srcSize.ToString() + " bytes of data");
            return span;
        }
    }

    public class LzToken
    {
        public byte idx;
        public byte length;
        public byte commandFlag;

        public LzToken(int tokenIdx, int tokenLength, int tokenCommandFlag)
        {
            idx = (byte)tokenIdx;
            length = (byte)tokenLength;
            commandFlag = (byte)tokenCommandFlag;
        }
        public int WriteDecompressBuffer(Span<byte> srcData, Span<byte> dstData, int dstIdx)
        {
            
            int bytesWritten = 0;
            int readIdx;
            int writeIdx;
            switch (commandFlag)
            {
                case LZ65.RLE_FLAG:
                    byte b = srcData[idx + 1];
                    Span<byte> span = new Span<byte>(new byte[length]);
                    span.Fill(b);
                    span.CopyTo(dstData.Slice(dstIdx, length));
                    bytesWritten = length;
                    break;
                case LZ65.REP_FLAG:
                    readIdx = srcData[idx + 1];
                    writeIdx = dstIdx;
                    for (int i = 0; i < length; i++)
                        dstData[writeIdx++] = dstData[readIdx++];
                    bytesWritten = length;
                    break;
                case LZ65.RAW_FLAG:
                    readIdx = idx + 1;
                    writeIdx = dstIdx;
                    for(int i = 0; i < length; i++)
                        dstData[writeIdx++] = srcData[readIdx++];
                    bytesWritten = length;
                    break;
                case LZ65.EOS_FLAG:
                    bytesWritten = 0;
                    break;
                default:
                    break;
            }
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("{0} : ", ToString());
            LZ65.PrintBytes(dstData.Slice(dstIdx, length).ToArray(), 16);
            return bytesWritten;
        }

        /// <summary>
        /// Emits a sequence of Compressed Stream bytes from this token.
        /// </summary>
        /// <param name="srcData">The Original Source Data array to compress.</param>
        /// <returns>The byte sequence for the decompressor</returns>
        public Span<byte> EmitCompressStream(Span<byte> srcData)
        {
            byte command = (byte)(commandFlag | length);
            Span<byte> outData = null;

            switch (commandFlag)
            {
                case LZ65.EOS_FLAG:
                    outData = new Span<byte>([command]);
                    break;
                case LZ65.RLE_FLAG:
                    outData = new Span<byte>([command, srcData[idx]]);
                    break;
                case LZ65.REP_FLAG:
                    outData = new Span<byte>([command, idx]);
                    break;
                case LZ65.RAW_FLAG:
                    byte[] buffer = new byte[length + 1];
                    outData = new Span<byte>(buffer);
                    outData[0] = command;
                    srcData.Slice(idx, length).CopyTo(outData.Slice(1, length));
                    break;
                default:
                    break;
            }
            ConsoleColor fg = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("{0} : ", ToString());
            LZ65.PrintBytes(outData.ToArray(), 8);
            return outData;
        }

        public override string ToString()
        {
            string cmd;
            byte command = (byte)(commandFlag & LZ65.COM_MASK);
            switch (command)
            {
                case LZ65.RLE_FLAG:
                    cmd = "RLE";
                    break;
                case LZ65.REP_FLAG:
                    cmd = "REP";
                    break;
                case LZ65.RAW_FLAG:
                    cmd = "RAW";
                    break;
                case LZ65.EOS_FLAG:
                    cmd = "EOS";
                    break;
                default:
                    cmd = "??? : " + commandFlag;
                    break;
            }
            return string.Format("[{2}] IDX:{0:x2} LEN:{1:x2}", idx, length, cmd);
        }
    }

}
