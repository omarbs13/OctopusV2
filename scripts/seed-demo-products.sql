-- Da de alta 10 000 productos demo con control de inventario y código de barras EAN-13 válido.
--
-- Uso (con la app CERRADA y sobre una copia o una carpeta de POS_DATA_DIR, no sobre datos reales):
--   sqlite3 /ruta/a/datos/data/pos.db < scripts/seed-demo-products.sql
--
-- Qué inserta:
--   * 8 categorías demo (solo las que no existan; "Bebidas" se reutiliza si ya está).
--   * 10 000 productos: SKU DEMO-00001 … DEMO-10000, unidad Pieza (H87), código de barras
--     75099xxxxxxxC (EAN-13 con dígito verificador calculado), existencia mínima y punto de reorden.
--   * Para ~95 % de ellos, la existencia inicial: fila en ProductStocks y movimiento INITIAL
--     (secuencia 1). El resto queda sin movimientos (existencia 0), como un producto recién creado.
--
-- Todo va en una transacción: si algo falla (p. ej. ya se corrió antes y los SKU DEMO-… existen),
-- se detiene y no deja nada a medias. No genera entradas de bitácora (AuditEntries).
-- SQLite no tiene WHILE: las 10 000 filas salen de un CTE recursivo.

.bail on
PRAGMA foreign_keys = ON;

BEGIN IMMEDIATE;

-- Contexto: fecha UTC en el formato que usa EF y el usuario que figura como autor.
CREATE TEMP TABLE ctx AS
SELECT strftime('%Y-%m-%d %H:%M:%f', 'now') AS now,
       COALESCE(
         (SELECT Id FROM Users WHERE IsSystem = 0 AND IsActive = 1 AND DeletedAt IS NULL
           ORDER BY CreatedAt LIMIT 1),
         (SELECT Id FROM Users ORDER BY CreatedAt LIMIT 1)) AS user_id;

-- Categorías demo. NameKey es el nombre en minúsculas y sin acentos (clave única).
CREATE TEMP TABLE demo_cat (idx INTEGER PRIMARY KEY, name TEXT, name_key TEXT);
INSERT INTO demo_cat VALUES
  (0, 'Abarrotes', 'abarrotes'),
  (1, 'Bebidas', 'bebidas'),
  (2, 'Lácteos', 'lacteos'),
  (3, 'Botanas', 'botanas'),
  (4, 'Limpieza', 'limpieza'),
  (5, 'Cuidado personal', 'cuidado personal'),
  (6, 'Panadería', 'panaderia'),
  (7, 'Enlatados', 'enlatados');

INSERT INTO Categories (Id, Name, NameKey, Description, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, DeletedAt, Version)
SELECT upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-'
             || substr('89AB', 1 + abs(random()) % 4, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))),
       c.name, c.name_key, 'Categoría de productos demo', 1, x.now, x.user_id, x.now, x.user_id, NULL, 1
FROM demo_cat c, ctx x
WHERE NOT EXISTS (SELECT 1 FROM Categories k WHERE k.NameKey = c.name_key AND k.DeletedAt IS NULL);

-- Productos base: 5 por categoría, con su forma de búsqueda y un precio base en centavos.
CREATE TEMP TABLE demo_base (idx INTEGER PRIMARY KEY, cat INTEGER, name TEXT, search TEXT, price INTEGER);
INSERT INTO demo_base VALUES
  ( 0, 0, 'Arroz', 'arroz', 3200),
  ( 1, 0, 'Frijol negro', 'frijol negro', 4500),
  ( 2, 0, 'Azúcar', 'azucar', 3800),
  ( 3, 0, 'Aceite vegetal', 'aceite vegetal', 5200),
  ( 4, 0, 'Harina de trigo', 'harina de trigo', 2900),
  ( 5, 1, 'Refresco de cola', 'refresco de cola', 2200),
  ( 6, 1, 'Agua natural', 'agua natural', 1500),
  ( 7, 1, 'Jugo de naranja', 'jugo de naranja', 3100),
  ( 8, 1, 'Té helado', 'te helado', 2000),
  ( 9, 1, 'Bebida energética', 'bebida energetica', 3500),
  (10, 2, 'Leche entera', 'leche entera', 2800),
  (11, 2, 'Yogur natural', 'yogur natural', 2400),
  (12, 2, 'Queso panela', 'queso panela', 6500),
  (13, 2, 'Crema ácida', 'crema acida', 3300),
  (14, 2, 'Mantequilla', 'mantequilla', 4100),
  (15, 3, 'Papas fritas', 'papas fritas', 1900),
  (16, 3, 'Cacahuates japoneses', 'cacahuates japoneses', 1700),
  (17, 3, 'Palomitas', 'palomitas', 1500),
  (18, 3, 'Totopos', 'totopos', 2600),
  (19, 3, 'Chicharrones', 'chicharrones', 2100),
  (20, 4, 'Detergente en polvo', 'detergente en polvo', 5800),
  (21, 4, 'Jabón para trastes', 'jabon para trastes', 3400),
  (22, 4, 'Cloro', 'cloro', 2300),
  (23, 4, 'Suavizante de telas', 'suavizante de telas', 4700),
  (24, 4, 'Limpiador multiusos', 'limpiador multiusos', 3900),
  (25, 5, 'Champú', 'champu', 6200),
  (26, 5, 'Jabón de tocador', 'jabon de tocador', 1800),
  (27, 5, 'Pasta dental', 'pasta dental', 3600),
  (28, 5, 'Desodorante', 'desodorante', 5500),
  (29, 5, 'Papel higiénico', 'papel higienico', 4900),
  (30, 6, 'Pan de caja', 'pan de caja', 4200),
  (31, 6, 'Galletas de avena', 'galletas de avena', 2700),
  (32, 6, 'Pan dulce', 'pan dulce', 1200),
  (33, 6, 'Tortillinas', 'tortillinas', 2500),
  (34, 6, 'Mantecadas', 'mantecadas', 3000),
  (35, 7, 'Atún en agua', 'atun en agua', 2400),
  (36, 7, 'Chiles jalapeños', 'chiles jalapenos', 2100),
  (37, 7, 'Elote amarillo', 'elote amarillo', 2300),
  (38, 7, 'Sardinas en tomate', 'sardinas en tomate', 2800),
  (39, 7, 'Frijoles refritos', 'frijoles refritos', 2000);

-- Marcas ficticias.
CREATE TEMP TABLE demo_brand (idx INTEGER PRIMARY KEY, name TEXT, search TEXT);
INSERT INTO demo_brand VALUES
  ( 0, 'La Huerta', 'la huerta'),      ( 1, 'Don Pancho', 'don pancho'),
  ( 2, 'Sol de Oro', 'sol de oro'),    ( 3, 'El Rancho', 'el rancho'),
  ( 4, 'Doña Luz', 'dona luz'),        ( 5, 'Monte Azul', 'monte azul'),
  ( 6, 'La Pradera', 'la pradera'),    ( 7, 'Los Altos', 'los altos'),
  ( 8, 'Buen Día', 'buen dia'),        ( 9, 'La Abuela', 'la abuela'),
  (10, 'Tres Ríos', 'tres rios'),      (11, 'El Sabino', 'el sabino'),
  (12, 'Campo Verde', 'campo verde'),  (13, 'La Esperanza', 'la esperanza'),
  (14, 'Mar Azul', 'mar azul'),        (15, 'El Faro', 'el faro'),
  (16, 'Alborada', 'alborada'),        (17, 'La Cosecha', 'la cosecha'),
  (18, 'Nube Blanca', 'nube blanca'),  (19, 'Valle Alto', 'valle alto'),
  (20, 'El Roble', 'el roble'),        (21, 'Cumbre', 'cumbre'),
  (22, 'Brisa', 'brisa'),              (23, 'Aurora', 'aurora'),
  (24, 'Las Palmas', 'las palmas');

-- Presentaciones, con un factor de precio en porcentaje.
CREATE TEMP TABLE demo_pres (idx INTEGER PRIMARY KEY, name TEXT, search TEXT, factor INTEGER);
INSERT INTO demo_pres VALUES
  (0, 'Chico', 'chico', 60),
  (1, 'Mediano', 'mediano', 100),
  (2, 'Grande', 'grande', 160),
  (3, 'Familiar', 'familiar', 220),
  (4, 'Económico', 'economico', 85),
  (5, 'Clásico', 'clasico', 100),
  (6, 'Light', 'light', 110),
  (7, 'Premium', 'premium', 180),
  (8, 'Paquete 3 pzas', 'paquete 3 pzas', 270),
  (9, 'Paquete 6 pzas', 'paquete 6 pzas', 500);

-- 40 bases × 25 marcas × 10 presentaciones = 10 000 combinaciones, todas con nombre distinto.
-- Cantidades en milésimas (1 pieza = 1000). Los valores aleatorios se guardan primero en una tabla
-- para que cada uno se evalúe una sola vez (en un CTE, SQLite puede recalcular random() por uso).
CREATE TEMP TABLE demo_raw AS
WITH RECURSIVE n(i) AS (SELECT 0 UNION ALL SELECT i + 1 FROM n WHERE i < 9999)
SELECT n.i,
       n.i % 40 AS b,
       (n.i / 40) % 25 AS m,
       n.i / 1000 AS p,
       '75099' || printf('%07d', n.i + 1) AS d12,
       abs(random()) % 100 AS r_stock,
       (5 + abs(random()) % 16) * 1000 AS min_stock
FROM n;

CREATE TEMP TABLE demo_product AS
SELECT r.i,
       upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-'
             || substr('89AB', 1 + abs(random()) % 4, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))) AS id,
       upper(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)), 2) || '-'
             || substr('89AB', 1 + abs(random()) % 4, 1) || substr(hex(randomblob(2)), 2) || '-' || hex(randomblob(6))) AS movement_id,
       'DEMO-' || printf('%05d', r.i + 1) AS sku,
       -- EAN-13: pesos 1 y 3 alternados de izquierda a derecha sobre los 12 dígitos (3 en las posiciones pares).
       r.d12 || ((10 - ((
           substr(r.d12, 1, 1) + 3 * substr(r.d12, 2, 1) + substr(r.d12, 3, 1) + 3 * substr(r.d12, 4, 1)
         + substr(r.d12, 5, 1) + 3 * substr(r.d12, 6, 1) + substr(r.d12, 7, 1) + 3 * substr(r.d12, 8, 1)
         + substr(r.d12, 9, 1) + 3 * substr(r.d12, 10, 1) + substr(r.d12, 11, 1) + 3 * substr(r.d12, 12, 1)
       ) % 10)) % 10) AS barcode,
       b.name || ' ' || br.name || ' ' || p.name AS name,
       b.search || ' ' || br.search || ' ' || p.search AS name_search,
       -- Precio: base × presentación × variación de ±15 %, redondeado a 50 centavos.
       max(500, (b.price * p.factor / 100 * (85 + abs(random()) % 31) / 100) / 50 * 50) AS price_cents,
       b.cat,
       r.min_stock,
       r.min_stock / 2000 * 1000 AS reorder_point,
       -- 5 % sin existencia, 10 % por debajo del mínimo y el resto con existencia holgada.
       CASE
         WHEN r.r_stock < 5 THEN 0
         WHEN r.r_stock < 15 THEN (1 + abs(random()) % 4) * 1000
         ELSE (20 + abs(random()) % 181) * 1000
       END AS on_hand
FROM demo_raw r
JOIN demo_base b ON b.idx = r.b
JOIN demo_brand br ON br.idx = r.m
JOIN demo_pres p ON p.idx = r.p;

INSERT INTO Products (Id, Barcode, CreatedAt, CreatedBy, DeletedAt, IsActive, Name, NameSearch, PriceCents, Sku,
                      UnitCode, UpdatedAt, UpdatedBy, Version, MinimumStock, TracksInventory, IsCritical, CategoryId,
                      ReorderPoint)
SELECT d.id, d.barcode, x.now, x.user_id, NULL, 1, d.name, d.name_search, d.price_cents, d.sku,
       'H87', x.now, x.user_id, 1, d.min_stock, 1, 0,
       (SELECT k.Id FROM Categories k JOIN demo_cat c ON c.name_key = k.NameKey
         WHERE c.idx = d.cat AND k.DeletedAt IS NULL),
       d.reorder_point
FROM demo_product d, ctx x
ORDER BY d.i;

INSERT INTO ProductStocks (ProductId, OnHand, MovementCount, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, Version)
SELECT d.id, d.on_hand, 1, x.now, x.user_id, x.now, x.user_id, 1
FROM demo_product d, ctx x
WHERE d.on_hand > 0;

INSERT INTO InventoryMovements (Id, ProductId, Sequence, Type, Quantity, ResultingStock, Reason, Reference, CreatedAt, CreatedBy)
SELECT d.movement_id, d.id, 1, 'INITIAL', d.on_hand, d.on_hand, 'Inventario inicial (datos demo)', NULL, x.now, x.user_id
FROM demo_product d, ctx x
WHERE d.on_hand > 0;

COMMIT;

SELECT 'Productos demo: ' || (SELECT count(*) FROM Products WHERE Sku LIKE 'DEMO-%')
    || ' | con existencia: ' || (SELECT count(*) FROM ProductStocks s JOIN Products p ON p.Id = s.ProductId WHERE p.Sku LIKE 'DEMO-%')
    || ' | categorías: ' || (SELECT count(*) FROM Categories WHERE DeletedAt IS NULL) AS resultado;
