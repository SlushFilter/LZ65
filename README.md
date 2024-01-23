Files included :
lz65.asm  - 6502 assembly source file that includes the LZ65 Decompression routine. Written for the CA65 compiler.
lz65.cs   - C# source file that contains the Compressor / Decompressor. Written for DotNet Core 8
            Routines are implemented as static methods in the LZ65 class.
Notes :
- There's room for optimization in the 6502 source file.
- I'll get around to writing the Compressor for 6502 at some point.
- Data size is limited to 256 bytes for Compressed and Decompressed data.
