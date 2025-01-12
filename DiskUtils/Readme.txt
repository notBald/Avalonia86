This is a modified DiskUtils libary from https://github.com/DiscUtils/DiscUtils

The libary is combinded with https://github.com/adamhathcock/sharpcompress
Which again uses ZstdSharp.Port: https://github.com/oleg-st/ZstdSharp
 Also Mit lisenced

Note, someone has already solved the problem of adding more decompression methods:
 - https://github.com/LTRData/DiscUtils/commit/608c43d1c44e819c6e500864e2483ecb0f3f630c (Lz4)
 - For my work see the "ReadBlock" method of VfsSquashFileSystemReader.cs (I added xo)

Documentation very usefull in my work:
 https://dr-emann.github.io/squashfs/

Additional documentation:
 https://github.com/AppImage/AppImageSpec/blob/master/draft.md
 https://www.kernel.org/doc/html/latest/filesystems/squashfs.html

Any future work on this libary should be done from the basis of the maintained fork: https://github.com/LTRData/DiscUtils
 (Though TargetPath is not implemented, on the other hand, bugs have been fixed)

My changes are light
 - Removed many files/methodes not needed for reading SquashFs.
 - Allow SymNodes to get the TargetPath, so directories can be itterated (Files: Symlink.cs and SymlinkContentBuffer).
 - Added xo decompression support (Files: VfsSquashFileSystemReader).