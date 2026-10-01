# Investigación: Licencia local con período de evaluación

Todas las decisiones se tomaron sin dependencias nuevas.

## 1. ID de máquina

- **Decisión**: ID = SHA-256 (hexadecimal) de una cadena estable obtenida así, en este orden: Linux `/etc/machine-id` (o `/var/lib/dbus/machine-id`); Windows `HKLM\SOFTWARE\Microsoft\Cryptography\MachineGuid`; reserva: MAC de la primera interfaz física ordenada por nombre. Se antepone una sal fija de la aplicación para que el ID no revele el identificador del sistema.
- **Justificación**: es el UUID del sistema operativo que pide la historia 1, estable ante cambios de red y de disco, y se obtiene sin permisos elevados. Es recuperable siempre que se borre el `.lic`.
- **Alternativas**: solo MAC (cambia con adaptadores virtuales y VPN); UUID del disco (requiere privilegios en Linux); WMI (solo Windows y más lento).

## 2. Archivo `license.lic`

- **Decisión**: archivo binario con encabezado de versión, nonce y contenido cifrado y autenticado con AES-256-GCM. La clave se deriva (HKDF-SHA256) de un secreto de la aplicación más el ID de máquina. Contenido: ID de máquina, fecha del primer arranque (UTC), última fecha vista (UTC) y la concesión del proveedor vigente, si existe.
- **Justificación**: cualquier edición manual rompe la etiqueta de autenticación; copiarlo a otra máquina cambia la clave y falla el descifrado, por lo que se rechaza aun sin comparar el ID. Un secreto dentro del ejecutable es extraíble; se acepta porque el control es deliberadamente local (supuesto de la especificación).
- **Alternativas**: JSON con HMAC (legible y editable en apariencia); registrar la fecha en la base de datos (se podría editar con cualquier cliente SQLite).

## 3. Firma de las licencias importables

- **Decisión**: ECDSA P-256 sobre el contenido canónico del archivo. La app incluye solo la clave **pública**; la privada la conserva el proveedor fuera del repositorio. En compilaciones `DEBUG` se admite una clave pública de desarrollo por variable de entorno para pruebas manuales; el código se excluye de `Release`.
- **Justificación**: cumple la aclaración de que solo el proveedor emite licencias, valida sin conexión y no pone secretos en el repositorio (Principio IX).
- **Alternativas**: HMAC con secreto compartido (el secreto iría dentro del ejecutable y cualquiera podría emitir); RSA (archivos más grandes sin ventaja).
- **Fuera de alcance**: la herramienta de emisión no se incluye en el repositorio; las pruebas usan un emisor de apoyo con un par de claves de prueba.

## 4. Cálculo de días y reloj retrasado

- **Decisión**: la fecha efectiva es el máximo entre el reloj actual y la "última fecha vista" guardada. Los días se cuentan por fecha de calendario local: `restantes = 30 − (hoy − inicio)` en días de calendario, y con concesión `restantes = fechaFin − hoy`. Con `restantes ≤ 0` el modo es lectura (el día 30 desde el primer arranque ya vence, coherente con la regla de la especificación). La "última fecha vista" se actualiza al arrancar y al cambiar el día.
- **Justificación**: retroceder el reloj no devuelve días (FR-016) y el estado se recalcula con el reloj en cada consulta, por lo que cruzar la medianoche con la app abierta bloquea sin reiniciar.
- **Alternativas**: contar horas exactas (confunde al usuario con "29.6 días"); consultar hora de red (prohibido: sin conexión).

## 5. Borrar el `.lic` no debe reiniciar el período

- **Decisión**: al regenerar, la fecha de primer arranque es la menor entre hoy y la fecha de creación del primer usuario registrado en la base (`IInstallationAgeReader`). Con base vacía o nueva, es hoy. El ID se recalcula del hardware, por lo que se conserva.
- **Justificación**: sin esto, borrar un archivo reinicia los 30 días y el bloqueo es trivial de evadir. Es barato y reutiliza datos existentes. La cota sigue siendo local (supuesto aceptado).
- **Alternativas**: guardar la fecha en varios archivos ocultos (frágil); no hacer nada (rompe la historia 3).

## 6. Dónde se aplica el bloqueo

- **Decisión**: en `AccessControl.CheckAsync`, que ya pasa por todos los casos de uso con permiso. Con modo lectura, `Sell`, `ViewReports` y `ManageUsers` devuelven `LicenseExpired`. La apertura de turno se verifica en `OpenShiftHandler` porque `OperateShift` también autoriza cerrar y contar el turno (FR-010). `HasAsync` (que decide qué se muestra) no cambia.
- **Justificación**: un punto único evita olvidar un caso de uso y no altera los casos de uso de lectura. `ManageLicense` (nuevo, solo Administrador) nunca se bloquea, para poder activar.
- **Alternativas**: verificar en cada ViewModel (se puede olvidar y duplica lógica, viola el Principio III); decorar todos los handlers (más código).

## 7. Interfaz

- **Decisión**: el `Navigator` consulta el estado antes de abrir una opción bloqueada y muestra el mensaje con el contacto sin navegar; la pantalla segura de recuperación pasa a ser Inicio en modo lectura. Inicio muestra `LicenseCard` (registrada con `AddDashboardCard`). El login recibe un aviso rojo cuando quedan ≤ 1 días. Los textos van en `Strings.resx`.
- **Justificación**: mantiene los ViewModels delgados y reutiliza los puntos de extensión existentes (tarjetas, permisos de navegación).

## 8. Importar una licencia

- **Decisión**: se verifica en este orden: formato, firma, ID de máquina, y que su fecha de emisión no sea anterior a la concesión vigente (evita regresar a una licencia más corta). Si todo es válido, se escribe el `.lic` y el estado se recalcula sin reiniciar. Cualquier fallo conserva el `.lic` actual y devuelve un mensaje específico. Se registra en la bitácora de auditoría.
- **Justificación**: cumple FR-011/FR-012 y el Principio IX. Con un `.lic` dañado o de otra máquina también se puede importar para recuperarse.

## 9. Contacto del proveedor

- **Decisión**: constante de configuración `VendorContact` (teléfono y email) en un único lugar de Application, usada por todos los mensajes.
- **Pendiente para el responsable**: valores reales del contacto.

## 10. Exclusión de diagnóstico

- **Decisión**: `license.lic` vive en la raíz de la carpeta de datos, fuera de `logs/`; el exportador solo empaqueta `logs/*.log`, `info.json` y la copia opcional de la base, así que no entra. Una prueba lo comprueba. El contenido cifrado tampoco se escribe en los logs.
