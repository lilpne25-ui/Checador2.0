# Manual de Operaciones Web · Fase 5

## Alcance
Este manual cubre operación diaria desde Web para:
- monitoreo operativo,
- sync/autosync,
- validación de estado,
- control de incidentes por feature flags.

## 1) Inicio de jornada
1. Ingresar a `Checador.Web` con usuario de operación/admin.
2. Verificar módulo **Operación**:
   - estado SQL,
   - estado reloj,
   - último sync.
3. Revisar `GET /ops/telemetry` (desde API monitor o cliente interno) para confirmar estado `ok` o `warning` controlado.

## 2) Monitoreo continuo
- Mantener monitor web en refresco normal.
- Si el estado del reloj cae o el sync queda stale:
  - verificar conectividad,
  - revisar `LastMessage` y alertas en telemetría,
  - escalar si severidad es `critical`.

## 3) Gestión de incidentes (rollback por módulo)
1. Identificar módulo afectado (ej. `admin.pervasive`).
2. Cambiar feature flag correspondiente a `false` (API/Web).
3. Reiniciar servicios.
4. Confirmar bloqueo controlado del módulo.
5. Mantener el resto de módulos operativos.

Referencia: `docs/ROLLBACK-FEATURE-FLAGS-FASE-5.md`.

## 4) Cierre de jornada
- Verificar que no haya alertas críticas abiertas.
- Registrar eventos relevantes:
  - fallos de sync,
  - tiempos fuera de SLA,
  - módulos desactivados/reactivados.

## 5) Escalamiento recomendado
Escalar a desarrollo cuando:
- exista `critical` persistente en telemetría,
- se requiera desactivar más de un módulo a la vez,
- haya error no controlado en acciones de operación.
