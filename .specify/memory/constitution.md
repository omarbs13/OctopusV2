<!--
Sync Impact Report
- Version change: 1.2.0 -> 1.3.0 (MINOR, según lo pidió el responsable del proyecto). Nota: la
	regla de versionado de esta constitución reserva MINOR para agregar principios o secciones;
	esta enmienda acota el alcance del Principio I (podría leerse como redefinición, MAJOR, o
	como aclaración, PATCH). Se respeta MINOR por ser guía materialmente ampliada.
- Principios modificados: I. La venta nunca se detiene (mismo título). Se aclara que protege
	contra fallos técnicos, no contra la falta de licencia; sin módulo POS activo el sistema
	puede bloquearse con dos garantías: (1) el vencimiento no interrumpe una venta en curso
	(se permite terminarla, cobrarla y cerrar el turno abierto) y (2) siempre quedan disponibles
	inicio de sesión, pantalla de licencia y exportación de respaldo. Se amplía la justificación.
- Motivo: la funcionalidad 025 (licenciamiento coordinado con OctopusAdmin) exige el bloqueo
	total sin módulo POS, que contradecía la lectura literal del Principio I.
- Impacto en el código existente: ninguno inmediato. La spec 025 debe incorporar la garantía de
	venta en curso y turno abierto (hoy no la contempla explícitamente).
- Plan de migración: no aplica (no afecta datos ni esquema).
- Secciones añadidas: ninguna. Secciones eliminadas: ninguna.
- Plantillas: plan-template, spec-template y tasks-template leen la constitución al ejecutarse;
	no requieren cambios.
- Pendientes: ninguno. specs/025-coordinated-licensing/spec.md ya incorpora la garantía de venta
	en curso y cierre de turno (FR-028, FR-030a y casos límite).
Este informe es temporal y debe retirarse antes de confirmar la constitución en un commit.
-->
# Constitución de POS

Punto de venta de escritorio para Windows y Linux, desarrollado en .NET 10 con Avalonia y
SQLite local. El objetivo actual es un POS estable, en producción con clientes reales. Una
versión web con API solo se desarrollará si un cliente la solicita; hasta entonces está fuera
del proyecto.

## Core Principles

### I. La venta nunca se detiene (estabilidad primero)
- El POS funciona completamente sin conexión a internet; ninguna funcionalidad del flujo de
	venta puede depender de la red.
- Toda operación que modifique varios registros (una venta, un cobro, un corte de caja) se
	ejecuta en una única transacción: se guarda completa o no se guarda.
- Un error inesperado nunca debe cerrar la aplicación ni perder la venta en curso; se registra
	en el log y se muestra al operador un mensaje comprensible, sin detalles técnicos.
- SQLite opera en modo WAL. La aplicación realiza respaldos automáticos de la base de datos y
	siempre respalda antes de aplicar migraciones.
- La estabilidad tiene prioridad sobre funcionalidades nuevas: un defecto que afecte ventas,
	cobros o cortes se corrige antes de continuar con otra funcionalidad.
- Este principio protege contra fallos técnicos, no contra la falta de licencia. Sin el módulo
	POS activo, el sistema puede bloquearse según las reglas de licenciamiento, siempre con dos
	garantías:
	- El vencimiento nunca interrumpe una venta en curso: se permite terminarla, cobrarla y
		cerrar el turno abierto antes de aplicar el bloqueo.
	- Siempre quedan disponibles el inicio de sesión, la pantalla de licencia y la exportación
		de respaldo, porque los datos pertenecen al cliente.

**Justificación**: el POS está en producción con clientes reales; una venta perdida o una caja
detenida tiene un costo directo para el negocio del cliente. El bloqueo por licencia es una
decisión comercial y no un fallo, pero nunca debe dejar una venta a medias, un turno sin cerrar
ni al cliente sin acceso a sus datos.

### II. Arquitectura por capas con dependencias hacia el centro
- La solución se organiza en `Pos.Domain`, `Pos.Application`, `Pos.Infrastructure` y
	`Pos.Desktop`, más sus proyectos de pruebas.
- `Pos.Domain` no depende de ningún otro proyecto ni de librerías de infraestructura.
- `Pos.Application` depende solo de `Pos.Domain` y define las interfaces (puertos) que necesita:
	persistencia, reloj, usuario actual y dispositivos.
- `Pos.Infrastructure` implementa esas interfaces (EF Core, SQLite, dispositivos) y depende de
	Application y Domain.
- `Pos.Desktop` depende de Application. Referencia Infrastructure únicamente para registrar
	dependencias en el arranque (composition root). Ningún ViewModel ni vista accede al
	`DbContext` ni a SQL.
- Estas reglas se verifican con pruebas automáticas de arquitectura que forman parte de la
	suite.
- Dentro de cada capa, el código se organiza por funcionalidad (por ejemplo
	`Productos/CrearProducto`), no por tipo técnico.

**Justificación**: las dependencias dirigidas hacia el dominio aíslan las reglas de negocio de
la UI y de la persistencia. Las pruebas de arquitectura impiden que esos límites se erosionen
con el tiempo.

### III. La lógica de negocio vive en el núcleo
- Las reglas de negocio viven en Domain; la orquestación y la validación de entrada, en
	Application mediante casos de uso.
- Los ViewModels solo coordinan la interfaz: invocan casos de uso y presentan resultados. No
	calculan totales, impuestos ni descuentos.
- Cada caso de uso valida su entrada y devuelve un resultado explícito de éxito o de error de
	negocio; las excepciones se reservan para fallas inesperadas.

**Justificación**: las reglas de negocio concentradas en el núcleo se pueden probar sin UI y
se pueden reutilizar si algún día existe otra interfaz.

### IV. Integridad de datos
- Los identificadores son GUID v7 generados en la aplicación (`Guid.CreateVersion7`); no se
	usan identificadores autoincrementales en entidades de negocio.
- Toda fecha se almacena en UTC y se convierte a hora local solo al mostrarla.
- Las entidades de negocio incluyen:
	- Fecha y usuario de creación.
	- Fecha y usuario de última modificación.
	- Borrado lógico (`DeletedAt`).
	- Una versión entera para concurrencia optimista.
- No se eliminan físicamente registros con valor histórico o contable (ventas, cobros, cortes,
	movimientos de inventario); se anulan o se marcan como borrados.
- Los importes monetarios se representan en el dominio con un value object de dinero y se
	almacenan como enteros en centavos. Nunca se usa `double` ni `float` para dinero.
- El esquema cambia solo mediante migraciones de EF Core versionadas en el repositorio.
- El esquema se gestiona con EF Core Code First: el modelo en C# es la fuente de verdad y no
	se escriben scripts SQL manuales de esquema.
- Las migraciones se aplican automáticamente al arrancar, en este orden obligatorio:
	1. Verificar que haya una sola instancia de la aplicación en ejecución.
	2. Detectar si la base es más nueva que la aplicación; en ese caso no abrirla.
	3. Respaldar la base con la API de backup de SQLite.
	4. Migrar.
	5. Si la migración falla, restaurar el respaldo y no continuar.
- Una migración publicada nunca se modifica ni se elimina; las correcciones se hacen con
	migraciones nuevas.
- El SQL generado de cada migración se revisa antes de integrarla, prestando atención a las
	reconstrucciones de tablas.
- Por cada versión publicada se conserva una base de ejemplo. Una prueba automática migra
	todas esas bases a la versión actual y verifica la integridad de los datos.
- Los catálogos fijos se siembran con `HasData`; los datos propios de cada instalación se
	crean en el asistente de primer arranque.

**Justificación**: estas decisiones son baratas hoy y muy costosas de introducir después,
porque obligarían a migrar datos de clientes en producción.

### V. Multiplataforma real (Windows y Linux)
- Todo el código fuera de los adaptadores de plataforma debe funcionar igual en Windows y
	Linux.
- El acceso al hardware (impresora de tickets, cajón de dinero, lector de códigos, báscula) se
	define mediante interfaces en Application, con implementaciones por sistema operativo en
	Infrastructure.
- Las rutas de archivos, configuración y datos se resuelven con las APIs multiplataforma de
	.NET; no se escriben rutas fijas.
- La integración continua compila y ejecuta las pruebas en Windows y en Linux.

**Justificación**: solo se puede garantizar el soporte a los dos sistemas operativos si se
verifica de forma continua en ambos.

### VI. Calidad verificable
- `dotnet build` y `dotnet test` ejecutados desde la raíz terminan sin errores ni
	advertencias; las advertencias se tratan como errores.
- Política de pruebas mínimas. Por cada funcionalidad solo se escriben pruebas unitarias para:
	- Reglas de negocio con cálculos (dinero, precios, inventario).
	- Validaciones que protegen la integridad de datos.
	- El defecto corregido, si lo hay: todo defecto corregido incluye una prueba que lo
		reproduce.
- Cada regla tiene una prueba que cubre el caso válido y el caso límite más importante; no se
	prueban todas las combinaciones.
- No se escriben pruebas de interfaz, de ViewModels ni de código sin lógica (mapeos,
	configuraciones, DTOs).
- Siempre son obligatorias, sin excepción: las pruebas de arranque y migraciones (incluida la
	migración de las bases de ejemplo del Principio IV), las de consistencia de inventario y las
	pruebas de arquitectura del Principio II.
- La persistencia se prueba contra SQLite real (conexión en memoria o archivo temporal), nunca
	con el proveedor InMemory de EF Core.
- Al implementar, se ejecutan solo las pruebas del proyecto modificado. La integración continua
	ejecuta la suite completa en Windows y Linux (Principio V).

**Justificación**: las pruebas se concentran donde un error cuesta dinero o datos al cliente
(importes, inventario, integridad, arranque y migraciones), y se evita el costo de mantener
pruebas de bajo valor. El proveedor InMemory no reproduce transacciones, restricciones ni
concurrencia de SQLite. Las pruebas de regresión evitan que un defecto corregido vuelva a
aparecer.

### VII. Simplicidad (YAGNI)
- No se construye nada para un escenario hipotético. Quedan fuera hasta que un cliente lo
	requiera: API, aplicación web, sincronización, multi-sucursal, microservicios y colas de
	mensajes.
- Solo se aceptan preparaciones para el futuro cuyo costo presente sea mínimo y cuya ausencia
	obligaría a migrar datos (las definidas en el Principio IV).
- No se usan MediatR ni repositorios genéricos. Los casos de uso son clases simples y los
	repositorios, cuando existan, son específicos por agregado.
- Cada dependencia externa nueva debe justificarse en el plan de la funcionalidad.

**Justificación**: la complejidad sin un requisito real frena la entrega y agrega puntos de
falla a un producto cuya prioridad es la estabilidad.

### VIII. Soporte y diagnóstico en campo
- El logging es estructurado, con Serilog, y escribe en archivos rotativos dentro de la
	carpeta de datos de la aplicación.
- Cada error registrado incluye contexto suficiente para reproducirlo (operación, usuario,
	identificadores), sin datos sensibles.
- La aplicación muestra su versión y permite exportar los logs y un respaldo de la base para
	soporte técnico.

**Justificación**: el POS opera en equipos de clientes sin acceso directo del equipo de
desarrollo; el diagnóstico depende de lo que la aplicación registre y permita exportar.

### IX. Seguridad local
- Las contraseñas de usuarios se almacenan con un hash robusto (por ejemplo PBKDF2 o Argon2),
	nunca en texto plano.
- Las operaciones sensibles (anular ventas, abrir el cajón sin venta, descuentos fuera de
	rango, cortes) quedan registradas en una bitácora de auditoría.
- No se incluyen secretos ni credenciales en el repositorio.

**Justificación**: el manejo de efectivo exige trazabilidad de quién hizo cada operación
sensible y protección de las credenciales de los operadores.

## Restricciones técnicas

- .NET 10 (LTS), C# con nullable habilitado.
- Avalonia (versión estable más reciente) con patrón MVVM mediante CommunityToolkit.Mvvm;
	inyección de dependencias con Microsoft.Extensions.Hosting.
- EF Core con proveedor SQLite.
- FluentValidation para la validación de entrada en Application.
- Serilog para logging; xUnit para pruebas; NetArchTest para pruebas de arquitectura.
- `Directory.Build.props` con `TreatWarningsAsErrors` y analizadores habilitados; gestión
	central de paquetes con `Directory.Packages.props`.
- Idioma: el código (tipos, métodos, variables) en inglés; la interfaz de usuario, los
	mensajes al operador y la documentación en español.

## Flujo de desarrollo

- Toda funcionalidad sigue el flujo de Spec Kit: especificación, plan, tareas e
	implementación.
- El plan de cada funcionalidad debe incluir una verificación explícita de cumplimiento de
	esta constitución; cualquier desviación se justifica por escrito en el plan.
- Cada funcionalidad se entrega con las pruebas que exige el Principio VI y con la
	documentación necesaria para operarla o darle soporte.

## Governance

- Esta constitución prevalece sobre cualquier otra guía o práctica del proyecto.
- Las enmiendas requieren documentar el motivo, el impacto en el código existente y un plan de
	migración si aplica.
- La constitución usa versionado semántico:
	- MAJOR: eliminar o redefinir principios.
	- MINOR: agregar principios o secciones.
	- PATCH: aclaraciones.
- El cumplimiento se verifica en la revisión de cada plan, según la verificación exigida en
	"Flujo de desarrollo".

**Version**: 1.3.0 | **Ratified**: 2026-09-29 | **Last Amended**: 2026-10-03
