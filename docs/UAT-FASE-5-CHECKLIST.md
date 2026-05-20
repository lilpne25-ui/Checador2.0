# UAT · Fase 5 (Hardening, Telemetría y Salida a Producción)

## Objetivo
Validar con RH y Operaciones que la plataforma Web cubre operación diaria con controles de riesgo, telemetría y plan de rollback.

## Precondiciones
- API y Web en build Release vigente.
- Usuario `admin` con password vigente.
- SQL y reloj accesibles según política de hardening.
- Feature flags en `true` para módulos bajo prueba.

## Bloque A · Hardening y seguridad operativa
- [ ] Endpoint sensible sin permiso explícito devuelve `403`.
- [ ] Operaciones de riesgo piden confirmación textual cuando aplica.
- [ ] En modo `SyncAgent-only`, API responde bloqueo controlado (no `500`) en acciones directas al reloj.

## Bloque B · Telemetría y alertas
- [ ] `GET /ops/telemetry` responde `200` para admin.
- [ ] Estado global cambia a `warning/critical` cuando hay condiciones de riesgo.
- [ ] Se detectan alertas al menos para:
  - [ ] último sync no OK
  - [ ] stale de sync (>= 180 min)
  - [ ] reloj desconectado
  - [ ] fallos de autosync

## Bloque C · Feature flags y rollback
- [ ] Al desactivar módulo en API, rutas del módulo responden `503`.
- [ ] Al desactivar módulo en Web, el menú se oculta y la ruta retorna `404`.
- [ ] Roll-forward (reactivar) restaura operación del módulo sin efectos colaterales.

## Bloque D · Flujo RH mínimo
- [ ] Preview empleado y preview masivo funcionales.
- [ ] Alta manual (single/batch) operativa.
- [ ] Reportes (`excel|pdf`) operativos.
- [ ] Formato OT generado correctamente.

## Bloque E · Flujo Operaciones mínimo
- [ ] Monitor operativo carga estado SQL/reloj.
- [ ] Estado autosync visible.
- [ ] Historial reciente disponible.
- [ ] Módulo Pervasive permite preview + preview cleanup + run cleanup (con confirmación).

## Criterio de aprobación UAT
- Aprobado si todos los checks críticos están en verde:
  - Bloque A completo.
  - Bloque B completo.
  - Bloque C completo.
  - En D y E, al menos 90% de checks exitosos y sin incidentes críticos abiertos.

## Firma de aceptación
- Responsable RH:
- Responsable Operaciones:
- Fecha:
- Resultado: ✅ Aprobado / ⚠️ Condicional / ❌ Rechazado
- Observaciones:
