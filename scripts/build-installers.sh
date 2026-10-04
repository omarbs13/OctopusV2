#!/usr/bin/env bash
# Genera los paquetes distribuibles en artifacts/installers/:
#   win    Pos-<v>-win-x64-portable.exe y, si hay Inno Setup, Pos-<v>-win-x64-setup.exe
#   linux  Pos-<v>-x86_64.AppImage y pos_<v>_amd64.deb
# Uso: scripts/build-installers.sh [win|linux|all]   (por omisión: all)
# La versión sale de <Version> en Directory.Build.props.
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
target="${1:-all}"
case "$target" in win|linux|all) ;; *) echo "Uso: $0 [win|linux|all]" >&2; exit 2 ;; esac

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$root/Directory.Build.props" | head -n1)"
if [[ -z "$version" ]]; then echo "No se encontró <Version> en Directory.Build.props" >&2; exit 1; fi

app="Pos"            # AssemblyName de src/Pos.Desktop
pkg_name="pos"       # nombre del paquete, del binario en /usr/bin y del .desktop
icon_png="$root/src/Pos.Desktop/Assets/pos.png"
desktop_file="$root/packaging/linux/$pkg_name.desktop"
publish_root="$root/artifacts/publish"
out="$root/artifacts/installers"
tools="$root/.tools"
mkdir -p "$out"

publish() {
  local rid="$1"
  echo "==> dotnet publish ($rid)"
  rm -rf "${publish_root:?}/$rid"
  dotnet publish "$root/src/Pos.Desktop" -c Release -r "$rid" --self-contained \
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none \
    -o "$publish_root/$rid"
}

# Busca ISCC nativo o dentro del prefijo de Wine; imprime el comando o nada.
find_iscc() {
  if command -v iscc >/dev/null; then echo "iscc"; return; fi
  if command -v wine >/dev/null; then
    local prefix="${WINEPREFIX:-$HOME/.wine}/drive_c"
    for dir in "Program Files (x86)/Inno Setup 6" "Program Files/Inno Setup 6"; do
      if [[ -f "$prefix/$dir/ISCC.exe" ]]; then echo "wine $prefix/$dir/ISCC.exe"; return; fi
    done
  fi
}

build_windows() {
  publish win-x64
  cp "$publish_root/win-x64/$app.exe" "$out/$app-$version-win-x64-portable.exe"

  local iscc
  iscc="$(find_iscc)"
  if [[ -z "$iscc" ]]; then
    echo "!! No se encontró Inno Setup: se omitió el setup.exe de Windows."
    echo "   Ejecuta scripts/build-installers.ps1 en Windows, o instala Inno Setup 6 bajo Wine."
    return
  fi

  echo "==> Inno Setup"
  local iss="$root/packaging/windows/$app.iss"
  local src="$publish_root/win-x64"
  local dst="$out"
  if [[ "$iscc" == wine* ]]; then
    iss="$(winepath -w "$iss")"; src="$(winepath -w "$src")"; dst="$(winepath -w "$dst")"
  fi
  # shellcheck disable=SC2086 # $iscc puede ser "wine <ruta>"
  $iscc /Q "/DAppVersion=$version" "/DSourceDir=$src" "/DOutputDir=$dst" "$iss"
}

appimagetool_cmd() {
  if command -v appimagetool >/dev/null; then echo "appimagetool"; return; fi
  local tool="$tools/appimagetool-x86_64.AppImage"
  if [[ ! -x "$tool" ]]; then
    echo "==> Descargando appimagetool en $tools" >&2
    mkdir -p "$tools"
    curl -fsSL -o "$tool" \
      "https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-x86_64.AppImage"
    chmod +x "$tool"
  fi
  echo "$tool"
}

build_appimage() {
  echo "==> AppImage"
  local work appdir
  work="$(mktemp -d)"
  appdir="$work/$app.AppDir"
  mkdir -p "$appdir/usr/bin"
  install -m 755 "$publish_root/linux-x64/$app" "$appdir/usr/bin/$pkg_name"
  install -m 644 "$desktop_file" "$appdir/$pkg_name.desktop"
  install -m 644 "$icon_png" "$appdir/$pkg_name.png"
  ln -s "$pkg_name.png" "$appdir/.DirIcon"
  cat > "$appdir/AppRun" <<EOF
#!/bin/sh
HERE="\$(dirname "\$(readlink -f "\$0")")"
exec "\$HERE/usr/bin/$pkg_name" "\$@"
EOF
  chmod 755 "$appdir/AppRun"

  local tool
  tool="$(appimagetool_cmd)"
  # Extraer y ejecutar evita necesitar FUSE en la máquina que empaqueta.
  ARCH=x86_64 APPIMAGE_EXTRACT_AND_RUN=1 "$tool" --no-appstream "$appdir" \
    "$out/$app-$version-x86_64.AppImage"
  rm -rf "$work"
}

# Arma el .deb solo con ar y tar, para que funcione también en distros que no son Debian.
build_deb() {
  echo "==> .deb"
  local work pkg
  work="$(mktemp -d)"
  pkg="$work/pkg"
  install -D -m 755 "$publish_root/linux-x64/$app" "$pkg/opt/$pkg_name/$app"
  mkdir -p "$pkg/usr/bin"
  ln -s "/opt/$pkg_name/$app" "$pkg/usr/bin/$pkg_name"
  install -D -m 644 "$desktop_file" "$pkg/usr/share/applications/$pkg_name.desktop"
  install -D -m 644 "$icon_png" "$pkg/usr/share/icons/hicolor/256x256/apps/$pkg_name.png"

  local size
  size="$(du -sk "$pkg" | cut -f1)"
  mkdir -p "$work/control"
  cat > "$work/control/control" <<EOF
Package: $pkg_name
Version: $version
Section: misc
Priority: optional
Architecture: amd64
Installed-Size: $size
Depends: libc6, libfontconfig1, libx11-6, libice6, libsm6, libicu76 | libicu74 | libicu72 | libicu70 | libicu67
Maintainer: POS <soporte@pos.local>
Description: Punto de venta de escritorio
 Punto de venta sin conexion con catalogo de productos, inventario, ventas,
 turnos de caja, usuarios y reportes exportables a PDF y Excel.
EOF

  local tar_opts=(--owner=0 --group=0 --numeric-owner)
  echo "2.0" > "$work/debian-binary"
  tar "${tar_opts[@]}" -czf "$work/control.tar.gz" -C "$work/control" .
  tar "${tar_opts[@]}" -czf "$work/data.tar.gz" -C "$pkg" .
  local deb="$out/${pkg_name}_${version}_amd64.deb"
  rm -f "$deb"
  (cd "$work" && ar rc "$deb" debian-binary control.tar.gz data.tar.gz)
  rm -rf "$work"
}

build_linux() {
  if [[ ! -f "$icon_png" ]]; then
    echo "Falta $icon_png; ejecuta primero scripts/make-icons.sh" >&2
    exit 1
  fi
  publish linux-x64
  build_appimage
  build_deb
}

[[ "$target" == win || "$target" == all ]] && build_windows
[[ "$target" == linux || "$target" == all ]] && build_linux

echo
echo "Listo. Paquetes en $out:"
ls -lh "$out"
