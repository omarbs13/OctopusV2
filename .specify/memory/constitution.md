<!--
Sync Impact Report
- Version change: sin versión previa (constitución inicial) -> 1.0.0
- Principios incorporados: sin principios previos -> 1. Operación Offline-First; 2. Arquitectura
	modular y límites de dependencia; 3. Integridad de datos y sincronización; 4. API y contratos
	explícitos; 5. Seguridad, permisos y configuración; 6. Calidad, pruebas y compilación
	reproducible; 7. Experiencia de caja y abstracciones de hardware; 8. Evolución sostenible y
	observabilidad.
- Secciones añadidas: Stack y restricciones técnicas; Proceso y convenciones.
- Secciones eliminadas: ninguna.
- Seguimiento: TODO(RATIFICATION_DATE), falta confirmar la fecha original de adopción.
Este informe es temporal y debe retirarse antes de confirmar la constitución en un commit.
-->
# Constitución de POS Offline-First

## Core Principles

### 1. Operación Offline-First
**NO NEGOCIABLE (MUST)**
- La caja MUST poder vender, cobrar, imprimir tickets, abrir y cerrar turnos y consultar
	el catálogo sin internet y sin latencia de red.
- Toda escritura MUST persistirse primero en SQLite. Ninguna venta ni interacción de la
	interfaz MUST esperar una respuesta de red.
- La sincronización MUST ejecutarse en segundo plano, admitir reintentos, ser idempotente
	y recuperarse de interrupciones a mitad de una operación.
- Ninguna operación de caja MUST depender de una llamada a la API en tiempo real.

**RECOMENDADO (SHOULD)**
- La interfaz SHOULD mostrar el estado de conexión y la cantidad o estado de cambios
	pendientes de sincronización.

**Justificación**: la continuidad de venta y la integridad local son requisitos centrales,
no modos degradados sujetos a disponibilidad de internet.

### 2. Arquitectura modular y límites de dependencia
**NO NEGOCIABLE (MUST)**
- El repositorio MUST ser un monorepo con una única solución .NET (`.sln`) bajo `src/`,
	pruebas bajo `tests/` organizadas por proyecto (Domain, Application, Infrastructure,
	Api y Desktop), y la aplicación Angular bajo `web/`, fuera de la solución .NET.
- Los proyectos MUST respetar estas responsabilidades: `Pos.Domain` contiene entidades,
	value objects y reglas puras; `Pos.Application` contiene casos de uso, validaciones e
	interfaces; `Pos.Contracts` contiene DTOs y contratos; `Pos.Infrastructure.Local`
	implementa persistencia SQLite y cola local; `Pos.Infrastructure.Cloud` implementa
	persistencia PostgreSQL; `Pos.Api` expone la API; `Pos.Desktop` contiene UI Avalonia,
	ViewModels y composición de dependencias.
- `Pos.Domain` MUST carecer de dependencias externas. `Pos.Application` MUST depender
	únicamente de `Pos.Domain` y `Pos.Contracts`.
- `Pos.Infrastructure.Local`, `Pos.Infrastructure.Cloud`, `Pos.Api` y `Pos.Desktop`
	MUST depender de `Pos.Application` según las reglas de referencia de la solución.
- `Pos.Desktop` MUST NOT referenciar `Pos.Api` ni `Pos.Infrastructure.Cloud`; accederá
	a servicios remotos solo por HTTP y contratos de `Pos.Contracts`. La UI MUST NOT
	acceder directamente a EF Core ni a SQLite.
- `Pos.Api` MUST referenciar `Pos.Application`, `Pos.Contracts` y
	`Pos.Infrastructure.Cloud`, y MUST NOT referenciar `Pos.Desktop` ni
	`Pos.Infrastructure.Local`.
- La solución MUST mantener Clean Architecture y un monolito modular. La lógica de
	negocio y validaciones compartidas residirán en Domain y Application; la web consumirá
	contratos mediante OpenAPI, no compartirá código C#.

**RECOMENDADO (SHOULD)**
- Los módulos SHOULD conservar límites explícitos para permitir evolución o extracción
	futura sin anticipar infraestructura distribuida.

**Justificación**: las dependencias dirigidas hacia el dominio aíslan las reglas de negocio
y permiten crecer sin acoplar la caja a la API o al proveedor de persistencia.

### 3. Integridad de datos y sincronización
**NO NEGOCIABLE (MUST)**
- Toda entidad sincronizable MUST tener un identificador GUID (se SHOULD preferir UUID v7
	generado en el cliente), `CreatedAt`, `UpdatedAt` en UTC, `IsDeleted` para borrado lógico
	y `RowVersion` o equivalente para concurrencia. MUST identificar `DeviceId`, `BranchId`
	y `RegisterId` para auditoría y resolución de conflictos.
- Los datos sincronizables MUST NOT borrarse físicamente. Las fechas MUST almacenarse en
	UTC y la hora local MUST limitarse a presentación. El código MUST NOT usar
	`DateTime.Now`; el tiempo MUST obtenerse mediante `IClock` inyectado.
- Los importes MUST usar `decimal` con precisión definida, nunca `double` ni `float`.
	Moneda e impuestos MUST ser configuración, no constantes de código.
- La numeración de tickets o folios MUST evitar colisiones entre cajas desconectadas,
	mediante prefijo de dispositivo o serie por caja.
- Las ventas MUST ser inmutables y append-only; las correcciones MUST expresarse mediante
	devoluciones o cancelaciones. Catálogo y precios MUST aplicar última escritura gana con
	auditoría. El inventario MUST sincronizarse mediante movimientos/deltas, no valores
	absolutos. La política de conflictos MUST documentarse por entidad.
- Las migraciones MUST usar EF Core Migrations versionadas para SQLite y PostgreSQL.
	El escritorio MUST migrar la base local al actualizar sin perder datos.
- Antes de implementar sincronización, MUST elegirse un único motor entre Dotmim.Sync y
	PowerSync mediante ADR y prueba de concepto. Toda sincronización MUST estar detrás de
	`ISyncService`. Si el motor elegido gestiona estado de sincronización, MUST NOT
	duplicarse esa función con columnas manuales.

**RECOMENDADO (SHOULD)**
- La generación cliente de UUID v7 SHOULD utilizar una implementación mantenida y
	compatible con el entorno .NET seleccionado.

**Justificación**: identificadores globales, políticas de conflicto explícitas y escrituras
locales recuperables evitan pérdida de datos y colisiones en operación desconectada.

### 4. API y contratos explícitos
**NO NEGOCIABLE (MUST)**
- `Pos.Api` MUST ser ASP.NET Core Web API dentro de la solución y MUST ofrecer endpoints
	versionados bajo `/api/v1/`, documentados con OpenAPI/Swagger, incluidos endpoints
	dedicados a sincronización.
- La API MUST revalidar toda entrada y MUST NOT confiar en validaciones del cliente.
	Controladores o endpoints MUST ser delgados; la lógica de aplicación MUST vivir en
	`Pos.Application`. El proyecto MUST elegir un único estilo de endpoints y mantenerlo.
- Los errores MUST seguir ProblemDetails y la API MUST exponer un healthcheck en `/health`.
	El desarrollo local MUST poder ejecutar la API junto con PostgreSQL mediante Docker
	Compose.
- Angular MUST usar TypeScript estricto y consumir la API. Sus DTOs MUST generarse desde
	OpenAPI con NSwag u OpenAPI Generator; MUST NOT escribirse DTOs manuales en la web.

**RECOMENDADO (SHOULD)**
- Los cambios incompatibles en contratos SHOULD introducir una nueva versión de API y
	documentar el período de transición.

**Justificación**: contratos verificables y validación en el servidor protegen la
consistencia entre escritorio, nube y web, incluso ante clientes antiguos o no confiables.

### 5. Seguridad, permisos y configuración
**NO NEGOCIABLE (MUST)**
- Secretos MUST NOT almacenarse en el repositorio; MUST obtenerse mediante User Secrets,
	variables de entorno o mecanismos seguros equivalentes.
- La autenticación remota MUST usar tokens JWT/OIDC. La sesión offline del cajero MUST
	poder autenticarse con PIN local almacenado como hash seguro.
- Los roles cajero, supervisor y administrador y sus permisos MUST aplicarse tanto en la
	aplicación como en la API. Acciones críticas (cancelaciones, descuentos, cortes de caja
	y cambios de precio) MUST generar registros de auditoría.
- URLs de API, cadenas de conexión y puertos MUST provenir de configuración por entorno
	(Development, Staging, Production), nunca de valores fijos en código. Escritorio,
	Angular y API MUST poder cambiar de destino solo mediante configuración.

**RECOMENDADO (SHOULD)**
- La protección criptográfica de la base local con datos sensibles SHOULD evaluarse,
	incluida SQLCipher, antes de almacenar esos datos en producción.

**Justificación**: el control de acceso debe mantenerse en operación local y remota, y la
configuración externa evita exponer secretos o acoplar compilaciones a un entorno.

### 6. Calidad, pruebas y compilación reproducible
**NO NEGOCIABLE (MUST)**
- Domain y Application MUST desarrollarse con TDD. Las reglas de precios, impuestos,
	descuentos y cierre de caja MUST contar con pruebas automatizadas.
- Los repositorios MUST tener pruebas de integración con SQLite en archivo temporal; la
	API MUST tener pruebas de integración con PostgreSQL mediante Testcontainers.
- MUST existir pruebas explícitas para corte de red durante una venta, reintentos,
	conflictos y sincronización duplicada.
- Nullable reference types MUST estar activados, los warnings MUST tratarse como errores
	y el repositorio MUST incluir análisis estático y `.editorconfig`.
- Un único `dotnet build` y `dotnet test` ejecutados desde la raíz MUST compilar y probar
	toda la solución .NET.

**RECOMENDADO (SHOULD)**
- La cobertura SHOULD concentrarse en reglas y fallos de negocio; los umbrales deberán
	medir riesgos reales, no perseguir un porcentaje sin valor diagnóstico.

**Justificación**: los escenarios de desconexión, concurrencia y dinero requieren evidencia
automatizada para impedir regresiones que una prueba exclusivamente visual no detectaría.

### 7. Experiencia de caja y abstracciones de hardware
**NO NEGOCIABLE (MUST)**
- Los flujos de caja MUST poder operarse con teclado, lector de códigos de barras y
	pantalla táctil, con los mínimos pasos razonables para una venta.
- Las operaciones de E/S MUST ser asíncronas y ejecutarse fuera del hilo de UI; la UI
	MUST permanecer receptiva.
- Impresión de tickets y control del cajón de dinero MUST abstraerse tras interfaces de
	hardware, incluyendo `IPrinterService`.

**RECOMENDADO (SHOULD)**
- Los flujos de cobro SHOULD priorizar entrada rápida por lector y navegación por teclado,
	manteniendo controles táctiles claros.

**Justificación**: la velocidad de atención y la continuidad operativa dependen de una UI
receptiva y de no acoplar reglas de caja a dispositivos concretos.

### 8. Evolución sostenible y observabilidad
**NO NEGOCIABLE (MUST)**
- La solución MUST conservar capacidad de evolucionar hacia multi-sucursal,
	multi-tenant, inventario centralizado, reportes en la nube y facturación fiscal por país
	sin hacer que esas capacidades sean dependencias de la operación local actual.
- El proyecto MUST evitar microservicios, colas externas e infraestructura adicional hasta
	que un requisito medible lo exija; el diseño modular MUST permitir extraer componentes
	si la necesidad se demuestra.
- Escritorio y API MUST usar logging estructurado con Serilog y correlación entre cliente
	y servidor.

**RECOMENDADO (SHOULD)**
- Los cambios que aumenten complejidad SHOULD demostrar el requisito medible que los
	justifica y SHOULD conservar observabilidad de fallos de sincronización y operación.

**Justificación**: mantener módulos claros y señales operativas útiles permite crecer con
evidencia sin pagar antes el costo de sistemas distribuidos.

## Stack y restricciones técnicas
- Escritorio MUST usar C# con la última versión LTS de .NET disponible para el proyecto,
	Avalonia UI, MVVM con CommunityToolkit.Mvvm y compiled bindings activados.
- La base local MUST usar SQLite mediante EF Core y Microsoft.Data.Sqlite.
- El backend MUST usar ASP.NET Core Web API en C# dentro de la misma solución .NET.
- La base de nube MUST ser PostgreSQL, accedida mediante EF Core con Npgsql.
- La web MUST usar Angular y TypeScript estricto; sus modelos de API MUST generarse desde
	OpenAPI.
- Los registros de decisiones arquitectónicas MUST almacenarse en `docs/adr/`.

## Proceso y convenciones
- Ninguna funcionalidad MUST implementarse sin especificación, plan y tareas. Cada plan
	MUST indicar los proyectos afectados entre Domain, Application, API, Desktop y web.
- Las decisiones arquitectónicas importantes MUST registrarse mediante ADR. Cualquier
	desviación de un principio NO NEGOCIABLE MUST tener un ADR aprobado antes de
	implementarse.
- El trabajo SHOULD organizarse en ramas cortas por funcionalidad y PRs pequeños; los
	commits MUST seguir Conventional Commits.
- El código MUST nombrarse en inglés; la documentación y los textos de usuario MUST estar
	en español. La localización MUST prepararse desde el inicio mediante recursos, no
	cadenas fijas.
- El logging MUST ser estructurado en escritorio y API, con correlación de solicitudes
	entre cliente y servidor.

## Governance
Esta constitución prevalece sobre convenciones locales incompatibles. Toda propuesta de
enmienda MUST actualizar este documento, explicar su impacto, obtener aprobación de los
mantenedores y revisar plantillas, planes y tareas afectados. La revisión MUST comprobar
que los principios NO NEGOCIABLES se cumplen; una desviación requiere un ADR aprobado
antes de implementarse. Las revisiones de diseño y PR MUST verificar el cumplimiento y
documentar excepciones aprobadas.

El versionado sigue SemVer: MAJOR para eliminar o redefinir de forma incompatible un
principio o regla; MINOR para añadir principios o ampliar materialmente las obligaciones;
PATCH para aclaraciones y cambios editoriales sin alterar obligaciones. La fecha de última
enmienda MUST actualizarse con cada cambio aprobado. La fecha de ratificación conserva la
fecha de adopción original.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): confirmar fecha original de adopción | **Last Amended**: 2026-09-28
