# Contratos de 025-coordinated-licensing

## Contratos compartidos con OctopusAdmin (raíz del repositorio)

Viven en `contracts/` en la raíz porque deben ser **idénticos** en ambos repositorios:

| Archivo | Contenido |
|---------|-----------|
| [`contracts/module-catalog.json`](../../../contracts/module-catalog.json) | Catálogo de los 9 módulos (GUID, key, nombre, descripción, orden, módulo base) y su versión. |
| [`contracts/license-format.md`](../../../contracts/license-format.md) | Formato de licencia v3: sobre, contenido, bytes firmados, reglas de vigencia y antigüedad, orden de verificación y vector de prueba. |
| [`contracts/license-request.md`](../../../contracts/license-request.md) | Solicitud `.octoreq` (formato 2) y cálculo del ID de máquina. |

## Contratos internos del POS (esta carpeta)

| Archivo | Contenido |
|---------|-----------|
| [`blocked-mode.md`](blocked-mode.md) | Qué está disponible en bloqueo total, mensajes al operador y comportamiento de la pantalla "Ayuda > Licencia". |
