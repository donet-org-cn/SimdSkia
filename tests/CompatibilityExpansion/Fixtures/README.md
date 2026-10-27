# GIF fixtures

These small synthetic GIFs were encoded with Pillow 13.0.0 for this test suite
(MIT test content, no external artwork). `disposal.gif` exercises delta frames,
local palettes, transparency and the keep/background/previous disposal methods;
`interlaced.gif` exercises interlaced scan order. The same checked-in bytes are
decoded by isolated SkiaSharp 3.119.4 and SimdSkia test executables. Every frame
and a backwards seek are compared as raw pixels. No Python is needed to run them.
