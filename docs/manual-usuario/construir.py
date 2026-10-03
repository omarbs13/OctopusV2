"""Genera manual-de-usuario.pdf a partir de manual.html.

Uso (desde esta carpeta):
    python3 -m venv .venv && .venv/bin/pip install weasyprint
    .venv/bin/python construir.py

Cada captura se declara en manual.html así:

    <figure class="captura" id="IMG-07-02" data-titulo="Punto de venta con productos"
            data-wireframe="punto-de-venta">
      Qué capturar: …
    </figure>

Si existe imagenes/IMG-07-02.png (o .jpg, .jpeg, .webp) se inserta la imagen real. Si no, se dibuja
un recuadro con las instrucciones y, si se indicó data-wireframe, el wireframe SVG de ejemplo.
El apéndice "Lista de capturas" se genera solo y marca cuáles faltan.

El logotipo de la portada es imagenes/logo.png (o .jpg, .jpeg, .webp, .svg) si existe; si no, el
logotipo predeterminado de la aplicación (src/Pos.Desktop/Resources/Logo.axaml).
"""

import html
import re
import sys
from datetime import date
from pathlib import Path

from wireframes import WIREFRAMES

AQUI = Path(__file__).resolve().parent
RAIZ = AQUI.parent.parent
IMAGENES = AQUI / "imagenes"
EXTENSIONES = (".png", ".jpg", ".jpeg", ".webp")
LOGO_AXAML = RAIZ / "src" / "Pos.Desktop" / "Resources" / "Logo.axaml"
DIBUJO = re.compile(r'<GeometryDrawing Brush="(?P<color>[^"]+)" Geometry="(?P<ruta>[^"]+)"')

FIGURA = re.compile(r'<figure class="captura"(?P<attrs>[^>]*)>(?P<cuerpo>.*?)</figure>', re.S)
ATRIBUTO = re.compile(r'([\w-]+)="([^"]*)"')
TITULO = re.compile(r'<h([12])(?P<clase>[^>]*?) id="(?P<id>[^"]+)"[^>]*>(?P<texto>.*?)</h\1>', re.S)


def version():
    props = (RAIZ / "Directory.Build.props").read_text(encoding="utf-8")
    m = re.search(r"<Version>([^<]+)</Version>", props)
    return m.group(1) if m else "—"


def buscar_imagen(ident):
    for ext in EXTENSIONES:
        ruta = IMAGENES / f"{ident}{ext}"
        if ruta.exists():
            return ruta
    return None


def logotipo():
    """Logotipo de la portada: imagenes/logo.* o, si no existe, el de la aplicación en SVG."""
    for ext in (*EXTENSIONES, ".svg"):
        ruta = IMAGENES / f"logo{ext}"
        if ruta.exists():
            return f'<img src="{ruta.as_uri()}" alt="Logotipo"/>'
    # Mismo dibujo que la pantalla de carga (cuadrícula de 160 x 160).
    trazos = "".join(
        f'<path fill="{m.group("color")}" d="{m.group("ruta")}"/>'
        for m in DIBUJO.finditer(LOGO_AXAML.read_text(encoding="utf-8")))
    return f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 160 160" role="img" aria-label="Logotipo">{trazos}</svg>'


def exportar_wireframes():
    destino = IMAGENES / "wireframes"
    destino.mkdir(parents=True, exist_ok=True)
    for nombre, fn in WIREFRAMES.items():
        (destino / f"{nombre}.svg").write_text(fn(), encoding="utf-8")


def procesar_figuras(contenido):
    capturas = []

    capitulos = [(pos, texto) for nivel, _, texto, pos in titulos(contenido) if nivel == "1"]

    def seccion_de(pos):
        return next((texto for inicio, texto in reversed(capitulos) if inicio < pos), "")

    def reemplazar(m):
        attrs = dict(ATRIBUTO.findall(m.group("attrs")))
        ident = attrs["id"]
        titulo = attrs.get("data-titulo", "")
        instrucciones = m.group("cuerpo").strip()
        wire = attrs.get("data-wireframe")
        imagen = buscar_imagen(ident)
        capturas.append({
            "id": ident,
            "titulo": titulo,
            "seccion": seccion_de(m.start()),
            "lista": imagen is not None,
            "instrucciones": re.sub(r"\s+", " ", re.sub("<[^>]+>", "", instrucciones)),
        })
        pie = f'<figcaption><b>Figura {ident}.</b> {html.escape(titulo)}</figcaption>'
        if imagen:
            return (f'<figure class="captura real" id="{ident}">'
                    f'<img src="{imagen.as_uri()}" alt="{html.escape(titulo)}"/>{pie}</figure>')
        dibujo = ""
        if wire:
            dibujo = (f'<div class="wire"><div class="wire-etiqueta">Ejemplo orientativo (wireframe)</div>'
                      f'{WIREFRAMES[wire]()}</div>')
        return (
            f'<figure class="captura pendiente" id="{ident}">'
            f'<div class="ph-cabecera"><span class="ph-id">{ident}</span>'
            f'<span class="ph-estado">CAPTURA PENDIENTE</span></div>'
            f'<div class="ph-titulo">{html.escape(titulo)}</div>'
            f'<div class="ph-instr">{instrucciones}</div>'
            f'<div class="ph-archivo">Guardar como: <code>imagenes/{ident}.png</code></div>'
            f'{dibujo}{pie}</figure>'
        )

    return FIGURA.sub(reemplazar, contenido), capturas


def titulos(contenido):
    """(nivel, id, texto numerado, posición) de cada h1/h2, con la numeración que pone el CSS."""
    cap = sec = 0
    ultimo_numerado = False
    for m in TITULO.finditer(contenido):
        nivel, texto = m.group(1), re.sub("<[^>]+>", "", m.group("texto"))
        numerado = 'class="num"' in m.group("clase")
        if nivel == "1":
            sec = 0
            if numerado:
                cap += 1
                texto = f"{cap}. {texto}"
            ultimo_numerado = numerado
        elif ultimo_numerado:
            sec += 1
            texto = f"{cap}.{sec}  {texto}"
        yield nivel, m.group("id"), texto, m.start()


def indice(contenido):
    filas = []
    for nivel, ident, texto, _ in titulos(contenido):
        if ident == "indice":
            continue
        filas.append(f'<li class="toc-{nivel}"><a href="#{ident}">{texto}</a></li>')
    return '<ul class="toc">' + "\n".join(filas) + "</ul>"


def lista_capturas(capturas):
    filas = []
    for c in capturas:
        estado = '<span class="ok">✔ Lista</span>' if c["lista"] else '<span class="falta">Pendiente</span>'
        filas.append(
            f'<tr><td><a href="#{c["id"]}"><code>{c["id"]}</code></a></td>'
            f'<td><b>{html.escape(c["titulo"])}</b><br/><span class="mini">{html.escape(c["instrucciones"])}</span></td>'
            f'<td>{html.escape(c["seccion"])}</td><td>{estado}</td></tr>'
        )
    hechas = sum(c["lista"] for c in capturas)
    return (
        f'<p>Capturas listas: <b>{hechas} de {len(capturas)}</b>. Guarde cada imagen en la carpeta '
        f'<code>docs/manual-usuario/imagenes/</code> con el nombre indicado y vuelva a ejecutar '
        f'<code>construir.py</code>.</p>'
        '<table class="tabla capturas"><thead><tr><th>Archivo</th><th>Qué capturar</th>'
        '<th>Capítulo</th><th>Estado</th></tr></thead><tbody>' + "\n".join(filas) + "</tbody></table>"
    )


def main():
    try:
        from weasyprint import HTML
    except ImportError:
        sys.exit("Falta WeasyPrint: python3 -m venv .venv && .venv/bin/pip install weasyprint")

    contenido = (AQUI / "manual.html").read_text(encoding="utf-8")
    contenido = contenido.replace("{{VERSION}}", version())
    contenido = contenido.replace("{{FECHA}}", date.today().strftime("%d/%m/%Y"))
    contenido = contenido.replace("{{LOGO}}", logotipo())
    contenido, capturas = procesar_figuras(contenido)
    contenido = contenido.replace("{{INDICE}}", indice(contenido))
    contenido = contenido.replace("{{LISTA_CAPTURAS}}", lista_capturas(capturas))

    exportar_wireframes()
    salida = AQUI / "manual-de-usuario.pdf"
    HTML(string=contenido, base_url=str(AQUI)).write_pdf(salida)
    pendientes = sum(not c["lista"] for c in capturas)
    print(f"PDF generado: {salida}")
    print(f"Capturas: {len(capturas)} en total, {pendientes} pendientes")


if __name__ == "__main__":
    main()
