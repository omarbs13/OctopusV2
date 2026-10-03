# Manual de usuario

Fuente y generador de `manual-de-usuario.pdf`.

| Archivo | Contenido |
|---|---|
| `manual.html` | Texto del manual. Cada captura es un `<figure class="captura" id="IMG-…">` |
| `wireframes.py` | Wireframes SVG de ejemplo de las pantallas principales |
| `construir.py` | Genera el PDF (índice, versión, capturas y lista de capturas) |
| `imagenes/` | Capturas reales: `IMG-07-01.png`, etc. Los wireframes se exportan a `imagenes/wireframes/` |
| `imagenes/logo.png` | Logotipo de la portada (opcional; también `.jpg`, `.webp` o `.svg`). Sin él se usa el logotipo de la aplicación |

## Generar el PDF

```bash
cd docs/manual-usuario
python3 -m venv .venv && .venv/bin/pip install weasyprint
.venv/bin/python construir.py
```

## Agregar capturas

Guarde la captura en `imagenes/` con el código de su figura (`IMG-07-01.png`, `.jpg` o `.webp`) y vuelva a
generar el PDF: el recuadro de «captura pendiente» se reemplaza por la imagen. El Apéndice D del PDF lista
todas las capturas y su estado. La versión se toma de `Directory.Build.props`.
