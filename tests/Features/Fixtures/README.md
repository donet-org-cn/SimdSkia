# Independent DEFLATE fixture

`fixed.zlib` was generated outside the engine using Python's zlib, with `compressobj(level=9, wbits=15, strategy=zlib.Z_FIXED)`. Input consists of 32768 bytes: byte i is `(i * 17 + i // 7) % 256`. The first DEFLATE block type is 1 (fixed Huffman). This is test data, not a codec/runtime dependency. The engine decoder independently validates back-references, decoded content and Adler-32. Original SkiaSharp PNG fixtures exercise dynamic Huffman blocks separately; the managed encoder/decoder exercise stored blocks.
