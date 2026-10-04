#!/usr/bin/env bash
# Genera src/Pos.Desktop/Assets/pos.png (ícono de Linux: .desktop, AppImage y .deb) a partir del
# frame más grande de src/Pos.Desktop/Assets/pos.ico. Si ese frame mide menos de 256 px, lo
# escala a 256x256 y avisa. Requiere ImageMagick (magick o convert).
# Uso: scripts/make-icons.sh
set -euo pipefail

root="$(cd "$(dirname "$0")/.." && pwd)"
assets="$root/src/Pos.Desktop/Assets"
ico="$assets/pos.ico"
png="$assets/pos.png"

if command -v magick >/dev/null; then
  convert_cmd=(magick); identify_cmd=(magick identify)
elif command -v convert >/dev/null; then
  convert_cmd=(convert); identify_cmd=(identify)
else
  echo "Falta ImageMagick (magick o convert)." >&2
  exit 1
fi

# Índice y tamaño del frame más grande (por área).
read -r index width height < <(
  "${identify_cmd[@]}" -format '%p %w %h\n' "$ico" | sort -k2,2n -k3,3n | tail -n1
)
echo "Frame más grande de pos.ico: #$index (${width}x${height})"

if (( width < 256 || height < 256 )); then
  "${convert_cmd[@]}" "$ico[$index]" -resize 256x256 -background none -gravity center \
    -extent 256x256 "$png"
  echo "!! El frame más grande mide ${width}x${height}; se escaló a 256x256."
  echo "   Conviene un pos.ico con un frame de 256 px para que el ícono de Linux se vea nítido."
else
  "${convert_cmd[@]}" "$ico[$index]" "$png"
fi

echo "Ícono escrito en $png"
