# Plan de Paridad Web vs Desktop (Checador 2.0)

## Objetivo
Llevar `Checador.Web` a un nivel **casi igual** a `Checador.Desktop` en capacidades operativas, administrativas y de RH, manteniendo seguridad, trazabilidad y control de riesgos.

> Definición de “casi igual” para este plan:
> - La web cubre al menos **85-90%** de los flujos diarios que hoy resuelve Desktop.
> - Las funciones de alto riesgo (mantenimiento destructivo / direct device avanzado) pueden quedar con doble confirmación o restringidas por rol.

---

## Estado actual resumido

### Web (hoy)
- Login / Logout / cambio de password.
- KPI.
- Preview por empleado.
- Preview masivo.
- Vista admin de usuarios API (solo lectura).

### Desktop (hoy)
Además de lo anterior, incluye:
- Operación de sync (descarga de logs, autosync, sync hora reloj).
- Monitor en vivo (reloj + SQL + últimas marcas).
- Administración de empleados (renombrar, depto/puesto, horario, extra, DEPT/SHIFT).
- Eliminación de empleado.
- Cambio de número por rango.
- Alta manual de asistencias (incluye lote y modos 0/1/2/3).
- Preview Pervasive y limpieza nocturna.
- Administración de horarios RH.
- Generación de formato OT RH.
- Generación de reportes Excel/PDF desde UI.

---

## Principios de implementación
1. **No duplicar lógica de negocio**: reutilizar `Checador.Application` y `Checador.Infrastructure.Access` vía API.
2. **Web como orquestador, no reimplementador**: toda acción crítica pasa por endpoints auditados.
3. **Feature flags + rollout gradual**: habilitar módulos por fases y por rol.
4. **Trazabilidad obligatoria**: cada acción sensible debe dejar bitácora (usuario, timestamp, antes/después).
5. **Seguridad por diseño**: RBAC estricto, confirmaciones dobles, validaciones de impacto.

---

## Matriz de paridad funcional (gap)

| Módulo | Desktop | Web actual | Gap |
|---|---|---|---|
| Auth y cambio password | ✅ | ✅ | Bajo |
| KPI / Preview empleado / masivo | ✅ | ✅ | Bajo |
| Sync operativo (descarga logs) | ✅ | ✅ | Bajo-Medio |
| AutoSync controlado | ✅ | ✅ | Bajo-Medio |
| Monitor en vivo reloj+SQL | ✅ | ✅ | Bajo |
| Admin empleados (editar RH/Pervasive/horario/extra) | ✅ | ✅ | Bajo |
| Eliminar empleado | ✅ | ✅ | Bajo |
| Cambio USERID por rango | ✅ | ✅ | Bajo |
| Alta manual de asistencias | ✅ | ✅ | Bajo |
| Preview Pervasive | ✅ | ✅ | Bajo |
| Limpieza nocturna Pervasive | ✅ | ✅ | Bajo |
| Administración de horarios RH | ✅ | ❌ | Alto |
| Formato OT RH | ✅ | ✅ | Bajo |
| Generación reportes Excel/PDF desde UI | ✅ | ✅ | Bajo |

---

## Roadmap propuesto por fases

## Fase 0 · Fundaciones API/Seguridad (1-2 semanas)
**Objetivo:** preparar backend para soportar operación web sin riesgos.

### Entregables
- RBAC por permisos finos (no solo rol):
  - `ops.sync.run`
  - `ops.sync.autosync.manage`
  - `ops.device.time.sync`
  - `admin.employees.edit`
  - `admin.employees.delete`
  - `admin.employees.reassign`
  - `admin.manual_punch.create`
  - `admin.pervasive.preview`
  - `admin.pervasive.cleanup`
  - `admin.schedules.manage`
- Auditoría estructurada para acciones críticas.
- Convención de endpoint + contratos de respuesta homogéneos.

### Criterios de aceptación
- Todo endpoint sensible requiere permiso explícito.
- Todo cambio sensible deja evento auditable.

---

## Fase 1 · Operación diaria (2-3 semanas)
**Objetivo:** cubrir operación de monitoreo y sync desde web.

### Funciones a migrar
- Ejecutar “Descargar registros”.
- Activar/desactivar AutoSync + intervalo.
- Sincronizar hora del reloj.
- Monitor en vivo (estado reloj, hora, usuarios reloj, último sync, últimos logs).

### API mínima sugerida
- `POST /ops/sync/run`
- `GET /ops/sync/status`
- `POST /ops/sync/autosync/start`
- `POST /ops/sync/autosync/stop`
- `PUT /ops/sync/autosync/interval`
- `POST /ops/device/time/sync`
- `GET /ops/live-status`
- `GET /ops/recent-history?top=5`

### Criterios de aceptación
- RH/Operación puede trabajar sin abrir Desktop para sync diario.
- Estado en vivo refresca cada 15s sin romper sesión.

---

## Fase 2 · Administración de empleados (2-3 semanas)
**Objetivo:** migrar administración principal de catálogo.

### Funciones a migrar
- Ver catálogo de empleados.
- Editar: nombre SQL, depto, puesto, horario, autoriza extra, DEPT, SHIFT.
- Eliminar empleado (con confirmación reforzada).
- Cambio de número por rango con preview y validación previa.

### API mínima sugerida
- `GET /admin/employees`
- `GET /admin/employees/{userId}`
- `PUT /admin/employees/{userId}`
- `DELETE /admin/employees/{userId}?deleteHistoryRows=false`
- `POST /admin/employees/reassign/preview`
- `POST /admin/employees/reassign/apply`

### Criterios de aceptación
- Se conserva equivalencia funcional del diálogo `Renombrar` de Desktop.
- El cambio por rango no aplica sin preview + confirmación explícita.

---

## Fase 3 · RH avanzado y reportes (2-3 semanas)
**Objetivo:** cubrir flujos que hoy dependen de formularios RH del Desktop.

### Funciones a migrar
- Alta manual de asistencias (incluye lote y modos 0/1/2/3).
- Generación de reportes Excel/PDF (individual y masivo).
- Formato OT (captura y exportación).

### API mínima sugerida
- `POST /admin/manual-punch`
- `POST /admin/manual-punch/batch`
- `POST /reports/generate` (`excel|pdf`, individual|masivo)
- `POST /reports/overtime-format/generate`

### Criterios de aceptación
- RH puede resolver incidencias manuales sin Desktop.
- Reportes quedan funcionalmente equivalentes (mismos cálculos y datos base).

---

## Fase 4 · Pervasive + mantenimiento especializado (2 semanas)
**Objetivo:** cerrar brecha de operación técnica avanzada.

### Funciones a migrar
- Preview completo de payload Pervasive.
- Limpieza nocturna con alcance/rango/control por usuario.

### API mínima sugerida
- `GET /admin/pervasive/preview`
- `POST /admin/pervasive/night-cleanup/preview`
- `POST /admin/pervasive/night-cleanup/run`

### Criterios de aceptación
- El operador puede validar/ejecutar limpieza con los mismos candados que Desktop.

---

## Fase 5 · Hardening + salida a producción (1-2 semanas)
**Objetivo:** estabilizar y cerrar calidad operativa.

### Entregables
- Pruebas de regresión de cálculos críticos.
- Telemetría y alertas (errores de operación, timeout SQL/Pervasive, fallos de reloj).
- Manual RH + manual Operaciones.
- Feature flags por módulo y rollback plan.

### Criterios de aceptación
- UAT aprobado por RH y Operaciones.
- Desktop deja de ser dependencia diaria para >85% de casos.

---

## UX/Web sugerido (módulos)
1. **Operación**: Sync, AutoSync, monitor en vivo, estado SQL/reloj.
2. **Empleados**: catálogo, edición, eliminación, cambio por rango.
3. **RH**: alta manual, OT, reportes.
4. **Pervasive**: preview y limpieza nocturna.
5. **Configuración**: horarios RH, parámetros operativos permitidos.
6. **Auditoría**: historial de acciones críticas.

---

## Riesgos y mitigaciones

### Riesgo 1: Exponer funciones críticas sin control
- Mitigación: permisos finos + confirmación doble + bitácora obligatoria.

### Riesgo 2: Divergencia de lógica Desktop/Web
- Mitigación: toda lógica de negocio en servicios compartidos/API, no en UI web.

### Riesgo 3: Degradación por refresco en vivo
- Mitigación: polling controlado (15-30s), límites de query, caché corto.

### Riesgo 4: Operaciones destructivas por error humano
- Mitigación: previews obligatorios, dry-run, “Are you sure?” con texto confirmado.

---

## Indicadores de éxito (KPIs de migración)
- % de operaciones diarias resueltas desde web.
- Tiempo promedio para ejecutar sync/monitoreo/incidencia.
- Número de veces que se requiere abrir Desktop por semana.
- Errores operativos por módulo (antes vs después).
- Satisfacción de RH/Operación (encuesta interna).

---

## Prioridad recomendada (MoSCoW)

### Must (obligatorio para “casi igual”)
- Fase 1 completa.
- Fase 2 completa.
- Alta manual + reportes (Fase 3 parcial).

### Should
- OT RH completo.
- Pervasive preview completo.

### Could
- Limpieza nocturna avanzada con más automatización.
- Dashboard ejecutivo enriquecido.

### Won’t (por ahora)
- Operaciones directas de bajo nivel del SDK ZKTeco desde navegador sin pasar por API controlada.

---

## Plan de ejecución sugerido (12 semanas)
- Semanas 1-2: Fase 0.
- Semanas 3-5: Fase 1.
- Semanas 6-8: Fase 2.
- Semanas 9-10: Fase 3.
- Semana 11: Fase 4.
- Semana 12: Fase 5 + UAT + go-live.

---

## Primer sprint sugerido (arranque inmediato)
1. Implementar permisos finos + auditoría base.
2. Exponer endpoints de `sync run`, `live-status` y `recent-history`.
3. Construir página web de Operación (monitor + botón sync).
4. UAT interno de Operación (sin tocar aún flujos destructivos).

---

## Estado de ejecución (actualizado 2026-05-04)

### Fase 0 / Sprint 1 backend
- ✅ Permisos finos en API implementados (claim `permission` + catálogo por rol).
- ✅ Policies por permiso habilitadas para módulos `preview`, `kpi`, `admin` y `ops`.
- ✅ Endpoints operativos iniciales `/ops/*` publicados.
- ✅ Build Release de `Checador.Api` validado.
- ✅ Smoke de autorización validado en runtime:
  - `consulta_demo` accede a `preview/kpi` y queda bloqueado en `admin/ops`.

### Fase 1 · Operación diaria (cerrada)
- ✅ Endpoints operativos completos: `sync run/status`, `autosync start/stop/interval/status`, `device time sync`, `live-status`, `recent-history`.
- ✅ Módulo web `Ops` operativo con acciones de sync, autosync, hora de reloj y monitor en vivo.
- ✅ Refresco automático de monitor cada 15 segundos en web sin romper sesión.
- ✅ Trazabilidad auditada en acciones de operación.

### Fase 2 · Administración de empleados (cerrada)
- ✅ Catálogo web de empleados (`GET /admin/employees`) y detalle (`GET /admin/employees/{userId}`).
- ✅ Edición web/API de datos RH y catálogo (`PUT /admin/employees/{userId}`).
- ✅ Eliminación con confirmación reforzada (`DELETE /admin/employees/{userId}`).
- ✅ Cambio por rango con validación previa y aplicación explícita:
  - `POST /admin/employees/reassign/preview`
  - `POST /admin/employees/reassign/apply`
- ✅ Nueva pantalla web para cambio por rango con preview + confirmación textual obligatoria.

### Fase 3 · RH avanzado y reportes (cerrada)
- ✅ Alta manual puntual y por lote:
  - `POST /admin/manual-punch`
  - `POST /admin/manual-punch/batch`
- ✅ UI web de alta manual con modos `0/1/2/3` y captura por lote.
- ✅ Reportes por empleado/masivo en `Excel|PDF` desde web (`POST /reports/generate`).
- ✅ Formato OT RH desde API/Web (`POST /reports/overtime-format/generate`) con exportación a archivo.

### Fase 4 · Pervasive + mantenimiento especializado (cerrada)
- ✅ Preview completo de payload Pervasive desde API/Web:
  - `GET /admin/pervasive/preview?top=...`
- ✅ Limpieza nocturna controlada con validación por rango/USERID:
  - `POST /admin/pervasive/night-cleanup/preview`
  - `POST /admin/pervasive/night-cleanup/run`
- ✅ Confirmación reforzada en ejecución (texto obligatorio: `EJECUTAR LIMPIEZA`).
- ✅ UI web dedicada (`/Admin/Pervasive`) con:
  - tabla de preview,
  - preview de impacto (candidatas/procesables/estimado de renglones partidos),
  - ejecución auditada de limpieza nocturna.

### Fase 5 · Hardening + salida a producción (iniciada)
- ✅ Feature flags por módulo en API/Web para rollback rápido sin redeploy.
- ✅ Endpoint de telemetría operativa consolidada:
  - `GET /ops/telemetry` (alertas por sync stale, estado de reloj, fallos de autosync y hardening).
- ✅ Base documental de salida:
  - `docs/UAT-FASE-5-CHECKLIST.md`
  - `docs/ROLLBACK-FEATURE-FLAGS-FASE-5.md`
  - `docs/MANUAL-OPERACIONES-WEB-FASE-5.md`
  - `docs/MANUAL-RH-WEB-FASE-5.md`
- ⏳ Pendiente para cierre de fase:
  - ejecución UAT formal RH/Operaciones,
  - afinar umbrales de alertamiento por sitio,
  - consolidar KPIs de adopción (>85% casos diarios sin Desktop).

### Nota operativa actual
- En modo hardening SyncAgent-only, acciones directas al reloj pueden responder bloqueo controlado para proteger operación (sin 500).
- `admin` autentica correctamente; las acciones sensibles requieren password vigente (sin `mustChangePassword=true`).

### Siguiente bloque recomendado
1. Ejecutar `docs/UAT-FASE-5-CHECKLIST.md` con RH y Operaciones.
2. Ajustar umbrales de alertas y playbooks según resultados UAT.
3. Completar acta de salida a producción + seguimiento de KPIs de adopción.

---

## Definición de terminado de este plan
- Existe roadmap ejecutable por fases.
- Cada fase tiene scope, endpoints y criterios de aceptación.
- Está claro qué falta para paridad “casi igual” entre web y desktop.
