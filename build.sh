#!/bin/sh
set -e

mkdir -p dist pub

# Windows
dotnet publish Avalonia86 -r win-x64     -c Release --self-contained true -o dist/win-x64
dotnet publish Avalonia86 -r win-arm64   -c Release --self-contained true -o dist/win-arm64

# Linux
dotnet publish Avalonia86 -r linux-x64   -c Release --self-contained true -o dist/linux-x64
dotnet publish Avalonia86 -r linux-arm64 -c Release --self-contained true -o dist/linux-arm64

# macOS
dotnet publish Avalonia86 -r osx-x64     -c Release --self-contained true -o dist/osx-x64
dotnet publish Avalonia86 -r osx-arm64   -c Release --self-contained true -o dist/osx-arm64

# Package Windows zip
cd dist
zip -q -r ../pub/Avalonia-86-for-Windows-x64.zip     win-x64
zip -q -r ../pub/Avalonia-86-for-Windows-ARM64.zip   win-arm64

# Package macOS zip
zip -q -r ../pub/Avalonia-86-for-Mac-x64.zip         osx-x64
zip -q -r ../pub/Avalonia-86-for-Mac-ARM64.zip       osx-arm64
cd ..

# Package Linux AppImage (x64)
APPDIR=dist/Avalonia86-x64.AppDir
mkdir -p "$APPDIR/usr/bin"
cp -r dist/linux-x64/* "$APPDIR/usr/bin/"
cat > "$APPDIR/Avalonia86.desktop" <<EOF
[Desktop Entry]
Name=Avalonia 86
Exec=Avalonia86
Icon=Avalonia86
Type=Application
Categories=Utility;
EOF
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/bin:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/Avalonia86" "$@"
EOF
chmod +x "$APPDIR/AppRun"
if command -v appimagetool >/dev/null 2>&1; then
  appimagetool "$APPDIR" "pub/Avalonia-86-for-Linux-x64.AppImage"
else
  tar -czf pub/Avalonia-86-for-Linux-x64.tar.gz -C dist linux-x64
fi

# Package Linux AppImage (arm64)
APPDIR=dist/Avalonia86-arm64.AppDir
mkdir -p "$APPDIR/usr/bin"
cp -r dist/linux-arm64/* "$APPDIR/usr/bin/"
cat > "$APPDIR/Avalonia86.desktop" <<EOF
[Desktop Entry]
Name=Avalonia 86
Exec=Avalonia86
Icon=Avalonia86
Type=Application
Categories=Utility;
EOF
cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
SELF=$(readlink -f "$0")
HERE=${SELF%/*}
export PATH="${HERE}/usr/bin:${PATH}"
export LD_LIBRARY_PATH="${HERE}/usr/bin:${LD_LIBRARY_PATH}"
exec "${HERE}/usr/bin/Avalonia86" "$@"
EOF
chmod +x "$APPDIR/AppRun"
if command -v appimagetool >/dev/null 2>&1; then
  appimagetool "$APPDIR" "pub/Avalonia-86-for-Linux-ARM64.AppImage"
else
  tar -czf pub/Avalonia-86-for-Linux-ARM64.tar.gz -C dist linux-arm64
fi
