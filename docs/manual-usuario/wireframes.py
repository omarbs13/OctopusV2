"""Wireframes SVG de las pantallas principales.

Son ejemplos visuales aproximados (no capturas reales): sirven de guía mientras se toman las
capturas definitivas. Cada función devuelve un SVG como texto.
"""

from html import escape

ACCENT = "#1565C0"
INK = "#1F2933"
MUTED = "#6B7785"
LINE = "#C9D1DA"
FILL = "#F3F5F8"
WHITE = "#FFFFFF"
OK = "#2E7D32"
WARN = "#E65100"
BAD = "#C62828"
FONT = "DejaVu Sans, Noto Sans, sans-serif"


class Svg:
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.parts = []

    def add(self, s):
        self.parts.append(s)
        return self

    def rect(self, x, y, w, h, fill=WHITE, stroke=LINE, r=4, sw=1, dash=None):
        d = f' stroke-dasharray="{dash}"' if dash else ""
        return self.add(
            f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill}" '
            f'stroke="{stroke}" stroke-width="{sw}"{d}/>'
        )

    def text(self, x, y, t, size=11, color=INK, weight="normal", anchor="start"):
        return self.add(
            f'<text x="{x}" y="{y}" font-family="{FONT}" font-size="{size}" fill="{color}" '
            f'font-weight="{weight}" text-anchor="{anchor}">{escape(t)}</text>'
        )

    def line(self, x1, y1, x2, y2, color=LINE, sw=1):
        return self.add(f'<line x1="{x1}" y1="{y1}" x2="{x2}" y2="{y2}" stroke="{color}" stroke-width="{sw}"/>')

    def button(self, x, y, w, label, primary=False, h=26, color=None):
        fill = color or (ACCENT if primary else WHITE)
        stroke = fill if (primary or color) else LINE
        tc = WHITE if (primary or color) else INK
        self.rect(x, y, w, h, fill=fill, stroke=stroke, r=4)
        return self.text(x + w / 2, y + h / 2 + 4, label, size=10, color=tc, anchor="middle")

    def field(self, x, y, w, label=None, value="", h=24, placeholder=False):
        if label:
            self.text(x, y - 5, label, size=9, color=MUTED)
        self.rect(x, y, w, h, fill=WHITE, stroke=LINE, r=3)
        if value:
            self.text(x + 7, y + h / 2 + 4, value, size=10, color=MUTED if placeholder else INK)
        return self

    def check(self, x, y, label, on=True):
        self.rect(x, y, 12, 12, fill=ACCENT if on else WHITE, stroke=ACCENT if on else LINE, r=2)
        if on:
            self.add(f'<path d="M{x+3} {y+6} l2.5 3 l4 -6" stroke="white" stroke-width="1.6" fill="none"/>')
        return self.text(x + 18, y + 10, label, size=10)

    def table(self, x, y, w, cols, rows, row_h=22, highlight=None):
        """cols: lista de (título, ancho relativo, alineación)."""
        total = sum(c[1] for c in cols)
        widths = [w * c[1] / total for c in cols]
        self.rect(x, y, w, row_h, fill=FILL, stroke=LINE, r=0)
        cx = x
        for (title, _, align), cw in zip(cols, widths):
            tx = cx + cw - 6 if align == "r" else cx + 6
            self.text(tx, y + 15, title, size=9, color=MUTED, weight="bold", anchor="end" if align == "r" else "start")
            cx += cw
        for i, row in enumerate(rows):
            ry = y + row_h * (i + 1)
            fill = "#E3EEFB" if highlight == i else WHITE
            self.rect(x, ry, w, row_h, fill=fill, stroke=LINE, r=0)
            cx = x
            for (cell, (_, _, align), cw) in zip(row, cols, widths):
                color = INK
                if isinstance(cell, tuple):
                    cell, color = cell
                tx = cx + cw - 6 if align == "r" else cx + 6
                self.text(tx, ry + 15, cell, size=10, color=color, anchor="end" if align == "r" else "start")
                cx += cw
        return y + row_h * (len(rows) + 1)

    def card(self, x, y, w, h, title, value, sub=None, color=INK):
        self.rect(x, y, w, h, fill=WHITE, stroke=LINE, r=6)
        self.text(x + 10, y + 18, title, size=9, color=MUTED)
        self.text(x + 10, y + 42, value, size=16, color=color, weight="bold")
        if sub:
            self.text(x + 10, y + 58, sub, size=9, color=MUTED)
        return self

    def note(self, x, y, n):
        """Marcador numerado para referenciar zonas desde el texto."""
        self.add(f'<circle cx="{x}" cy="{y}" r="9" fill="{WARN}"/>')
        return self.text(x, y + 4, str(n), size=10, color=WHITE, weight="bold", anchor="middle")

    def svg(self):
        body = "\n".join(self.parts)
        return (
            f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {self.w} {self.h}" '
            f'width="{self.w}" height="{self.h}">\n{body}\n</svg>'
        )


def window(w, h, title="POS"):
    s = Svg(w, h)
    s.rect(0.5, 0.5, w - 1, h - 1, fill=WHITE, stroke="#9AA5B1", r=8)
    s.add(f'<path d="M0.5 30 V8.5 a8 8 0 0 1 8 -8 H{w-8.5} a8 8 0 0 1 8 8 V30 Z" fill="#E4E8ED"/>')
    s.text(14, 20, title, size=10, color=MUTED)
    for i, c in enumerate(["#9AA5B1"] * 3):
        s.add(f'<circle cx="{w - 18 - i * 16}" cy="15" r="5" fill="{c}"/>')
    return s


SIDEBAR_ITEMS = [
    ("Inicio", False), ("Ventas", True), ("Caja", True), ("Clientes", True), ("Reportes", True),
    ("Descuentos", True), ("Catálogos", True), ("Inventario", True), ("Administración", True),
    ("Configuración", True), ("Ayuda", True),
]


def shell(w, h, active, sub=None, user="Ana López · Administrador"):
    """Ventana con menú lateral. sub: (grupo, [opciones], opción activa)."""
    s = window(w, h)
    side_w = 170
    s.rect(1, 30, side_w, h - 31, fill="#F7F9FB", stroke="#F7F9FB", r=0)
    s.line(side_w + 1, 30, side_w + 1, h - 1)
    s.text(16, 54, "☰  POS", size=12, weight="bold", color=ACCENT)
    y = 80
    for name, group in SIDEBAR_ITEMS:
        is_active = name == active and not sub
        if is_active:
            s.rect(8, y - 15, side_w - 16, 24, fill="#E3EEFB", stroke="#E3EEFB", r=4)
        s.text(20, y + 1, name, size=10.5, color=ACCENT if is_active else INK,
               weight="bold" if is_active or (sub and sub[0] == name) else "normal")
        if group:
            s.text(side_w - 20, y + 1, "▾" if sub and sub[0] == name else "▸", size=9, color=MUTED)
        y += 26
        if sub and sub[0] == name:
            for opt in sub[1]:
                if opt == sub[2]:
                    s.rect(8, y - 15, side_w - 16, 24, fill="#E3EEFB", stroke="#E3EEFB", r=4)
                s.text(34, y + 1, opt, size=10, color=ACCENT if opt == sub[2] else INK)
                y += 24
    s.line(8, h - 44, side_w - 8, h - 44)
    s.add(f'<circle cx="24" cy="{h - 24}" r="9" fill="{ACCENT}"/>')
    name, role = user.split(" · ")
    s.text(40, h - 27, name, size=9.5, weight="bold")
    s.text(40, h - 15, role, size=8.5, color=MUTED)
    return s, side_w + 16


def wf_login():
    s = window(520, 340)
    s.rect(130, 60, 260, 250, fill=WHITE, stroke=LINE, r=8)
    s.add(f'<circle cx="260" cy="95" r="18" fill="{ACCENT}"/>')
    s.text(260, 100, "POS", size=10, color=WHITE, weight="bold", anchor="middle")
    s.text(260, 135, "Iniciar sesión", size=14, weight="bold", anchor="middle")
    s.field(155, 160, 210, "Usuario", "ana")
    s.field(155, 210, 210, "Contraseña", "••••••••")
    s.button(155, 255, 210, "Entrar", primary=True, h=30)
    s.note(380, 172, 1)
    s.note(380, 222, 2)
    s.note(380, 270, 3)
    return s.svg()


def wf_home():
    s, x0 = shell(760, 470, "Inicio")
    s.text(x0, 50, "Inicio", size=15, weight="bold")
    cw = 132
    cards = [
        ("Ventas del día", "$8,420.50", "37 ventas", INK),
        ("Turno actual", "T-000124", "Desde 08:02", INK),
        ("Productos activos", "1,248", None, INK),
        ("Alertas de existencia", "3 · 11", "Urgentes · En alerta", BAD),
    ]
    for i, (t, v, sub, c) in enumerate(cards):
        s.card(x0 + i * (cw + 10), 75, cw, 68, t, v, sub, c)
    s.rect(x0, 155, 350, 200, fill=WHITE, stroke=LINE, r=6)
    s.text(x0 + 10, 174, "Ventas de los últimos 7 días", size=9.5, color=MUTED)
    bars = [60, 85, 70, 110, 95, 130, 75]
    for i, b in enumerate(bars):
        s.rect(x0 + 25 + i * 45, 340 - b, 26, b, fill=ACCENT, stroke=ACCENT, r=2)
    s.rect(x0 + 360, 155, 208, 200, fill=WHITE, stroke=LINE, r=6)
    s.text(x0 + 370, 174, "Productos más vendidos", size=9.5, color=MUTED)
    for i, p in enumerate(["Coca-Cola 600 ml", "Pan blanco", "Leche 1 L", "Huevo 12 pz", "Jabón"]):
        s.text(x0 + 370, 200 + i * 26, f"{i+1}. {p}", size=10)
    s.rect(x0, 365, 568, 85, fill="#FFF4E5", stroke="#F5C27A", r=6)
    s.text(x0 + 10, 385, "Alertas", size=10, weight="bold", color=WARN)
    s.text(x0 + 10, 405, "Diferencias de arqueo (1): T-000119 · Luis · 28/09 · -$150.00", size=9.5)
    s.text(x0 + 10, 425, "Productos críticos con existencia baja (2): Leche 1 L (LEC-01): 2 pz", size=9.5)
    s.note(x0 - 6, 40, 1)
    s.note(25, 103, 2)
    s.note(x0 + 4 * (cw + 10) - 14, 70, 3)
    s.note(150, 470 - 24, 4)
    return s.svg()


def wf_product_editor():
    s, x0 = shell(760, 500, "Catálogos", ("Catálogos", ["Productos", "Categorías"], "Productos"))
    s.text(x0, 50, "← Regresar al listado     Nuevo producto", size=12, weight="bold")
    s.field(x0, 90, 300, "Nombre", "Coca-Cola 600 ml")
    s.field(x0 + 315, 90, 140, "SKU", "COC-600")
    s.field(x0, 140, 220, "Código de barras (opcional)", "7501055300846")
    s.field(x0 + 235, 140, 100, "Precio", "18.50")
    s.field(x0 + 350, 140, 105, "Unidad de medida", "Pieza  ▾")
    s.field(x0, 190, 220, "Categoría", "Bebidas  ▾")
    s.check(x0 + 235, 196, "Activo")
    s.rect(x0 + 470, 75, 100, 100, fill=FILL, stroke=LINE, r=6, dash="4 3")
    s.text(x0 + 520, 130, "Imagen", size=10, color=MUTED, anchor="middle")
    s.button(x0 + 470, 182, 100, "Seleccionar…")
    s.line(x0, 232, x0 + 570, 232)
    s.text(x0, 252, "Inventario", size=11, weight="bold")
    s.check(x0, 264, "Controla inventario")
    s.field(x0, 300, 140, "Existencia mínima", "12")
    s.field(x0 + 155, 300, 140, "Punto de reorden", "4")
    s.field(x0 + 310, 300, 140, "Existencia actual", "36", placeholder=True)
    s.check(x0, 340, "Producto crítico")
    s.text(x0 + 18, 368, "Aparece en las alertas de Inicio cuando su existencia es baja o se agota.", size=9, color=MUTED)
    s.button(x0, 430, 110, "Guardar", primary=True, h=30)
    s.button(x0 + 120, 430, 110, "Recargar", h=30)
    s.note(x0 + 300, 85, 1)
    s.note(x0 + 455, 135, 2)
    s.note(x0 + 450, 296, 3)
    return s.svg()


def wf_stock():
    s, x0 = shell(760, 400, "Inventario", ("Inventario", ["Existencias", "Movimientos", "Entrada de mercancía", "Proveedores"], "Existencias"))
    s.text(x0, 50, "Existencias", size=15, weight="bold")
    s.field(x0, 75, 260, None, "Buscar por nombre, SKU o código (Ctrl+F)", placeholder=True)
    s.field(x0 + 270, 75, 120, None, "Todos  ▾")
    s.check(x0 + 400, 81, "Incluir inactivos", on=False)
    s.button(x0 + 430, 110, 140, "Registrar movimiento", primary=True)
    s.table(x0, 145, 570, [("Nombre", 3, "l"), ("SKU", 1.4, "l"), ("Existencia", 1.2, "r"), ("Unidad", 1, "l"), ("Mínimo", 1, "r"), ("Estado", 1.4, "l")], [
        ["Coca-Cola 600 ml", "COC-600", "36", "pz", "12", ("Normal", OK)],
        ["Leche entera 1 L", "LEC-01", "2", "pz", "10", ("Baja", WARN)],
        ["Pan blanco", "PAN-01", "0", "pz", "5", ("Sin existencia", BAD)],
        ["Azúcar a granel", "AZU-KG", "14.250", "kg", "5", ("Normal", OK)],
        ["Huevo 12 pz", "HUE-12", "8", "pz", "6", ("Normal", OK)],
    ], highlight=1)
    return s.svg()


def wf_pos():
    s, x0 = shell(860, 540, "Ventas", ("Ventas", ["Punto de venta", "Ventas realizadas", "Mi turno"], "Punto de venta"),
                  user="Luis Pérez · Cajero")
    w = 860
    s.rect(x0, 40, w - x0 - 16, 26, fill="#E8F5E9", stroke="#A5D6A7", r=4)
    s.text(x0 + 8, 57, "Turno T-000124 · desde 08:02 · Luis Pérez · 37 ventas", size=9.5, color=OK)
    s.button(w - 16 - 230, 42, 70, "Ingreso", h=22)
    s.button(w - 16 - 155, 42, 70, "Retiro", h=22)
    s.button(w - 16 - 80, 42, 76, "Corte Z", h=22)
    s.field(x0, 80, 470, None, "Escanee o escriba un código, SKU o nombre y presione Enter", h=34, placeholder=True)
    s.button(x0 + 480, 84, 90, "Buscar (F2)")
    s.button(x0 + 576, 84, 92, "Cliente…")
    s.table(x0, 125, 470, [("Producto", 3, "l"), ("SKU", 1.3, "l"), ("Cantidad", 1, "r"), ("Precio", 1.1, "r"), ("Importe", 1.2, "r")], [
        ["Coca-Cola 600 ml", "COC-600", "2", "$18.50", "$37.00"],
        ["Pan blanco", "PAN-01", "6", "$3.50", "$21.00"],
        ["Azúcar a granel", "AZU-KG", "1.500", "$28.00", "$42.00"],
        ["Leche entera 1 L", "LEC-01", "1", "$27.00", "$27.00"],
    ], row_h=26, highlight=3)
    rx = x0 + 480
    s.rect(rx, 125, w - rx - 16, 200, fill=FILL, stroke=LINE, r=6)
    s.text(rx + 12, 148, "Artículos: 10.5", size=10, color=MUTED)
    s.text(rx + 12, 172, "Subtotal", size=10, color=MUTED)
    s.text(w - 30, 172, "$127.00", size=10, anchor="end")
    s.text(rx + 12, 192, "Descuento", size=10, color=MUTED)
    s.text(w - 30, 192, "-$0.00", size=10, anchor="end")
    s.text(rx + 12, 232, "Total", size=12, color=MUTED)
    s.text(w - 30, 236, "$127.00", size=24, weight="bold", anchor="end")
    s.button(rx + 12, 270, w - rx - 40, "Cobrar (F12)", primary=True, h=40)
    by = 345
    labels = ["Cantidad (F4)", "Quitar (Supr)", "Descuento (F7)", "Descuento a la venta", "Aplicar cupón", "Abrir cajón", "Cancelar venta (F8)"]
    bx = x0
    for lab in labels:
        bw = 8 + len(lab) * 6.2
        if bx + bw > w - 16:
            bx = x0
            by += 34
        s.button(bx, by, bw, lab, h=28, color=BAD if "Cancelar" in lab else None)
        bx += bw + 8
    s.text(x0, 500, "F2 Buscar · F4 Cantidad · F7 Descuento · Supr Quitar · F12 Cobrar · F8 Cancelar", size=9, color=MUTED)
    s.note(x0 - 4, 40, 1)
    s.note(x0 - 4, 97, 2)
    s.note(x0 - 4, 150, 3)
    s.note(w - 24, 140, 4)
    s.note(x0 - 4, 360, 5)
    return s.svg()


def wf_checkout():
    s = Svg(560, 420)
    s.rect(0.5, 0.5, 559, 419, fill=WHITE, stroke="#9AA5B1", r=8)
    s.text(20, 32, "Cobro", size=15, weight="bold")
    s.text(540, 34, "Total  $127.00", size=15, weight="bold", anchor="end")
    s.line(20, 48, 540, 48)
    s.text(20, 72, "Efectivo", size=11, weight="bold")
    s.field(20, 90, 160, "Recibido", "200")
    s.button(190, 90, 70, "Exacto", h=24)
    for i, b in enumerate(["$20", "$50", "$100", "$200", "$500", "$1,000"]):
        s.button(20 + i * 62, 124, 56, f"{i+1}·{b}", h=24)
    s.text(20, 178, "Tarjeta o transferencia", size=11, weight="bold")
    s.field(20, 196, 120, "Monto", "")
    s.field(150, 196, 120, "Forma de pago", "Tarjeta ▾")
    s.field(280, 196, 160, "Referencia (opcional)", "")
    s.button(450, 196, 90, "Agregar pago", h=24)
    s.text(20, 250, "Venta a crédito", size=11, weight="bold", color=MUTED)
    s.text(150, 250, "(solo si se eligió un cliente con crédito)", size=9, color=MUTED)
    s.rect(20, 270, 520, 70, fill=FILL, stroke=LINE, r=6)
    s.text(34, 295, "Pagado", size=10, color=MUTED)
    s.text(526, 295, "$200.00", size=11, anchor="end")
    s.text(34, 325, "Cambio", size=12, color=OK, weight="bold")
    s.text(526, 327, "$73.00", size=18, color=OK, weight="bold", anchor="end")
    s.button(330, 360, 210, "Confirmar cobro", primary=True, h=34)
    s.text(20, 382, "Enter o F12 confirman · Esc regresa · F5 exacto · 1 a 6 billetes", size=9, color=MUTED)
    s.note(10, 100, 1)
    s.note(10, 206, 2)
    s.note(10, 300, 3)
    return s.svg()


def wf_open_shift():
    s = Svg(440, 230)
    s.rect(0.5, 0.5, 439, 229, fill=WHITE, stroke="#9AA5B1", r=8)
    s.text(20, 34, "Abrir turno", size=15, weight="bold")
    s.text(20, 60, "Capture el efectivo con el que inicia el turno para dar cambio.", size=10, color=MUTED)
    s.field(20, 100, 200, "Fondo inicial", "500.00")
    s.button(220, 180, 100, "Cancelar", h=30)
    s.button(330, 180, 90, "Abrir turno", primary=True, h=30)
    return s.svg()


def wf_close_shift():
    s = Svg(720, 330)
    for i, (title, active) in enumerate([("1 · Conteo", False), ("2 · Cifras", True), ("3 · Cierre", False)]):
        x = 10 + i * 238
        s.rect(x, 10, 228, 310, fill=WHITE, stroke=ACCENT if active else LINE, r=8, sw=2 if active else 1)
        s.text(x + 14, 34, title, size=12, weight="bold", color=ACCENT if active else INK)
    x = 10
    s.text(x + 14, 62, "Turno de Luis Pérez", size=9.5, color=MUTED)
    s.field(x + 14, 100, 200, "Efectivo contado en caja", "4,180.00")
    s.text(x + 14, 150, "No se muestra el esperado", size=9, color=MUTED)
    s.text(x + 14, 165, "(arqueo ciego)", size=9, color=MUTED)
    s.button(x + 114, 280, 100, "Continuar", primary=True)
    x = 248
    rows = [("Efectivo esperado", "$4,330.00", INK), ("Efectivo contado", "$4,180.00", INK),
            ("Diferencia", "-$150.00", BAD), ("", "Faltante", BAD), ("Tarjeta", "$2,140.00", INK), ("Transferencia", "$610.00", INK)]
    for i, (a, b, c) in enumerate(rows):
        s.text(x + 14, 64 + i * 22, a, size=10, color=MUTED)
        s.text(x + 214, 64 + i * 22, b, size=10.5, color=c, anchor="end", weight="bold" if c != INK else "normal")
    s.field(x + 14, 210, 200, "Comentario (obligatorio)", "Faltó cambio de $150")
    s.button(x + 14, 280, 100, "Volver a contar")
    s.button(x + 120, 280, 94, "Confirmar", primary=True)
    x = 486
    s.text(x + 14, 64, "Turno T-000124 cerrado", size=10.5, weight="bold", color=OK)
    s.text(x + 14, 88, "Corte Z  Z-000057", size=10)
    s.rect(x + 40, 105, 150, 160, fill=FILL, stroke=LINE, r=2, dash="3 3")
    for i in range(8):
        s.line(x + 55, 125 + i * 16, x + 175 - (i % 3) * 20, 125 + i * 16, color="#AAB4BF")
    s.text(x + 115, 120, "CORTE Z", size=8, anchor="middle", weight="bold")
    s.button(x + 14, 280, 100, "Reimprimir corte")
    s.button(x + 120, 280, 94, "Terminar", primary=True)
    return s.svg()


def wf_return():
    s = Svg(620, 420)
    s.rect(0.5, 0.5, 619, 419, fill=WHITE, stroke="#9AA5B1", r=8)
    s.text(20, 34, "Devolver artículos · Venta V-001532", size=14, weight="bold")
    s.rect(20, 48, 580, 26, fill="#FFF4E5", stroke="#F5C27A", r=4)
    s.text(30, 65, "Se regresarán las existencias de lo devuelto. Un Administrador debe autorizarlo.", size=9.5, color=WARN)
    s.check(20, 86, "Seleccionar todo", on=False)
    s.table(20, 106, 580, [("Producto", 3, "l"), ("Disponible", 1, "r"), ("A devolver", 1, "r")], [
        ["Coca-Cola 600 ml", "2", "1"],
        ["Pan blanco", "6", "0"],
        ["Leche entera 1 L", "1", "1"],
    ])
    s.field(20, 222, 580, "Motivo", "Producto en mal estado")
    s.text(20, 272, "Compensación", size=9, color=MUTED)
    s.add(f'<circle cx="28" cy="288" r="6" fill="{ACCENT}"/>')
    s.text(40, 292, "Reintegro", size=10)
    s.add(f'<circle cx="128" cy="288" r="6" fill="white" stroke="{LINE}"/>')
    s.text(140, 292, "Nota de crédito", size=10)
    s.rect(20, 305, 580, 52, fill=FILL, stroke=LINE, r=6)
    s.text(32, 325, "Total a devolver: $45.50", size=11, weight="bold")
    s.text(32, 345, "Efectivo: $45.50", size=10, color=MUTED)
    s.button(380, 372, 100, "Conservar", h=30)
    s.button(490, 372, 110, "Devolver", primary=True, h=30)
    return s.svg()


def wf_customer():
    s, x0 = shell(760, 430, "Clientes", ("Clientes", ["Clientes"], "Clientes"))
    s.text(x0, 50, "Cliente: María Hernández", size=14, weight="bold")
    s.text(x0, 80, "Teléfono: 55 1234 5678 · Email: maria@correo.mx · RUC: —", size=9.5, color=MUTED)
    cw = 135
    for i, (t, v, c) in enumerate([("Límite", "$3,000.00", INK), ("Saldo pendiente", "$1,240.00", WARN), ("Disponible", "$1,760.00", OK), ("Días vencido", "12", BAD)]):
        s.card(x0 + i * (cw + 10), 92, cw, 56, t, v, None, c)
    s.button(x0 + 380, 160, 90, "Editar")
    s.button(x0 + 478, 160, 92, "Desactivar")
    s.rect(x0, 160, 140, 26, fill="#E3EEFB", stroke=LINE, r=4)
    s.text(x0 + 70, 177, "Ventas a crédito", size=10, anchor="middle", color=ACCENT, weight="bold")
    s.rect(x0 + 140, 160, 80, 26, fill=WHITE, stroke=LINE, r=4)
    s.text(x0 + 180, 177, "Abonos", size=10, anchor="middle")
    s.table(x0, 196, 570, [("Folio", 1.2, "l"), ("Fecha", 1.4, "l"), ("Monto", 1.2, "r"), ("Saldo", 1.2, "r"), ("Estado", 1.5, "l"), ("Días vencido", 1.1, "r")], [
        ["V-001410", "02/09/2026", "$840.00", "$640.00", ("Vencida", BAD), "12"],
        ["V-001502", "24/09/2026", "$600.00", "$600.00", ("Pendiente", WARN), "—"],
        ["V-001377", "28/08/2026", "$450.00", "$0.00", ("Pagada", OK), "—"],
    ])
    s.button(x0, 300, 130, "Registrar abono", primary=True)
    return s.svg()


def wf_sales_report():
    s, x0 = shell(780, 480, "Reportes", ("Reportes", ["Ventas", "Arqueo", "Inventario", "Compras", "Créditos"], "Ventas"))
    s.text(x0, 50, "Reporte de ventas", size=15, weight="bold")
    x = x0
    for i, p in enumerate(["Hoy", "Ayer", "Últimos 7 días", "Este mes", "Mes anterior", "Personalizado"]):
        bw = 14 + len(p) * 6
        s.button(x, 66, bw, p, primary=(i == 3), h=22)
        x += bw + 5
    s.field(x0, 104, 150, None, "Cajero: Todos ▾")
    s.field(x0 + 160, 104, 170, None, "Categoría: Todas ▾")
    s.button(x0 + 340, 104, 80, "Consultar", primary=True, h=24)
    s.button(x0 + 430, 104, 75, "PDF", h=24)
    s.button(x0 + 513, 104, 75, "Excel", h=24)
    cw = 112
    for i, (t, v) in enumerate([("Total vendido", "$186,420"), ("Ventas", "1,284"), ("Ticket promedio", "$145.19"), ("Efectivo", "$121,300"), ("Tarjeta", "$52,980")]):
        s.card(x0 + i * (cw + 7), 140, cw, 60, t, v)
    s.rect(x0, 210, 588, 140, fill=WHITE, stroke=LINE, r=6)
    s.text(x0 + 10, 228, "Ventas por día", size=9.5, color=MUTED)
    pts = [80, 95, 70, 110, 120, 90, 130, 115, 140, 125, 150, 135]
    path = " ".join(f"{'M' if i == 0 else 'L'}{x0 + 30 + i * 48} {340 - p * 0.75}" for i, p in enumerate(pts))
    s.add(f'<path d="{path}" stroke="{ACCENT}" stroke-width="2.5" fill="none"/>')
    s.table(x0, 360, 588, [("Folio", 1, "l"), ("Fecha y hora", 1.6, "l"), ("Cajero", 1.4, "l"), ("Total", 1, "r")], [
        ["V-001532", "02/10/2026 13:41", "Luis Pérez", "$127.00"],
        ["V-001531", "02/10/2026 13:37", "Luis Pérez", "$58.50"],
        ["V-001530", "02/10/2026 13:30", "Ana López", "$310.00"],
    ], row_h=20)
    return s.svg()


def wf_purchase():
    s, x0 = shell(780, 470, "Inventario", ("Inventario", ["Existencias", "Movimientos", "Entrada de mercancía", "Proveedores"], "Entrada de mercancía"))
    s.text(x0, 50, "Entrada de mercancía", size=15, weight="bold")
    s.field(x0, 85, 250, "Proveedor", "Distribuidora del Centro ▾")
    s.field(x0 + 260, 85, 150, "Número de factura", "F-10234")
    s.field(x0 + 420, 85, 168, "Fecha de factura", "02/10/2026")
    s.field(x0, 125, 588, None, "Buscar producto por nombre, SKU o código de barras (Enter agrega)", placeholder=True)
    s.table(x0, 160, 588, [("Núm.", 0.6, "l"), ("Producto", 3, "l"), ("Unidad", 1, "l"), ("Cantidad", 1.1, "r"), ("Costo unitario", 1.4, "r"), ("Importe", 1.3, "r")], [
        ["1", "Coca-Cola 600 ml", "pz", "48", "$12.40", "$595.20"],
        ["2", "Leche entera 1 L", "pz", "24", "$19.80", "$475.20"],
        ["3", ("Vaso promocional · Bonificación", OK), "pz", "12", "$0.00", "$0.00"],
    ])
    for i, (a, b) in enumerate([("Subtotal", "$1,070.40"), ("Impuestos", "$171.26"), ("Total", "$1,241.66")]):
        s.text(x0 + 470, 278 + i * 22, a, size=10, color=MUTED, anchor="end")
        s.text(x0 + 588, 278 + i * 22, b, size=11 if i < 2 else 13, weight="bold" if i == 2 else "normal", anchor="end")
    s.button(x0 + 330, 360, 120, "Descartar", h=32)
    s.button(x0 + 460, 360, 128, "Registrar compra", primary=True, h=32)
    return s.svg()


def wf_authorization():
    s = Svg(440, 260)
    s.rect(0.5, 0.5, 439, 259, fill=WHITE, stroke="#9AA5B1", r=8)
    s.text(20, 34, "Autorización de administrador", size=14, weight="bold")
    s.text(20, 60, "Esta operación requiere autorización de un administrador:", size=10, color=MUTED)
    s.text(20, 76, "Cancelar o devolver venta", size=10, weight="bold")
    s.field(20, 112, 400, "Usuario", "ana")
    s.field(20, 162, 400, "Contraseña", "••••••••")
    s.button(210, 210, 100, "Cancelar", h=30)
    s.button(320, 210, 100, "Autorizar", primary=True, h=30)
    return s.svg()


def wf_audit():
    s, x0 = shell(800, 440, "Administración", ("Administración", ["Usuarios", "Bitácora"], "Bitácora"))
    s.text(x0, 50, "Bitácora de auditoría", size=15, weight="bold")
    s.field(x0, 82, 95, "Desde", "01/10/2026")
    s.field(x0 + 102, 82, 95, "Hasta", "02/10/2026")
    s.field(x0 + 204, 82, 110, "Usuario", "Todos ▾")
    s.field(x0 + 321, 82, 120, "Tipo de evento", "Todos ▾")
    s.field(x0 + 448, 82, 80, "Entidad", "Todas ▾")
    s.button(x0 + 535, 82, 73, "Buscar", primary=True, h=24)
    s.table(x0, 118, 360, [("Fecha y hora", 1.5, "l"), ("Evento", 1.7, "l"), ("Usuario", 1, "l")], [
        ["02/10 13:52", "Producto modificado", "ana"],
        ["02/10 13:20", "Venta cancelada", "luis"],
        ["02/10 12:05", "Retiro de efectivo", "luis"],
        ["02/10 08:02", "Turno abierto", "luis"],
        ["02/10 08:01", "Inicio de sesión", "luis"],
    ], highlight=0)
    dx = x0 + 370
    s.rect(dx, 118, 238, 250, fill=FILL, stroke=LINE, r=6)
    s.text(dx + 10, 138, "Producto modificado", size=11, weight="bold")
    s.text(dx + 10, 156, "Registro: Coca-Cola 600 ml", size=9.5, color=MUTED)
    s.table(dx + 8, 168, 222, [("Campo", 1, "l"), ("Antes", 1, "l"), ("Después", 1, "l")], [
        ["Precio", "$17.50", "$18.50"],
        ["Categoría", "—", "Bebidas"],
    ], row_h=20)
    s.button(dx + 10, 240, 218, "Ver historial del registro", h=24)
    s.button(x0, 380, 110, "Exportar PDF", h=26)
    s.button(x0 + 118, 380, 110, "Exportar Excel", h=26)
    return s.svg()


WIREFRAMES = {
    "login": wf_login,
    "inicio": wf_home,
    "producto": wf_product_editor,
    "existencias": wf_stock,
    "punto-de-venta": wf_pos,
    "cobro": wf_checkout,
    "abrir-turno": wf_open_shift,
    "corte-z": wf_close_shift,
    "devolucion": wf_return,
    "cliente": wf_customer,
    "reporte-ventas": wf_sales_report,
    "compra": wf_purchase,
    "autorizacion": wf_authorization,
    "bitacora": wf_audit,
}
