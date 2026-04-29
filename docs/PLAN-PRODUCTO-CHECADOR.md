# Plan Producto Checador - Igual o Superior

## Objetivo
Convertir `Checador 2.0` en un producto vendible tipo HR Attendance manteniendo el core actual (sync SQL/Pervasive, reglas de calculo, reportes) y agregando capa Web/API para autoservicio y escalabilidad.

## Estrategia (practica)
- Conservar el core actual de `src/Checador.Application` y `src/Checador.Infrastructure.Access`.
- Exponer capacidades por API antes de reescribir logica.
- Agregar modulo web por fases: primero P0 (igualar), luego P1 (superar).
- Cada fase con entregables demoables y medibles.

## Base actual (ya resuelto)
- Sync reloj -> SQL con catalogo de empleados y marcas.
- Calculo de jornada, incidencias tecnicas y horas extra por horario.
- Reportes Excel/PDF y vistas KPI en desktop.
- Operacion administrativa fuerte en `Admin SQL` (preview, limpieza, correcciones, autosync).

## Arquitectura objetivo
1. `Checador.Core` (actual): calculo, sync, reglas de negocio.
2. `Checador.API` (nuevo): API REST + autenticacion + auditoria.
3. `Checador.Web` (nuevo): portal colaborador + panel RH/operaciones.
4. `Checador.Workers` (nuevo): alertas, notificaciones, integraciones externas.
5. `Checador.BI` (nuevo): indicadores multi-sede y tableros ejecutivos.

## Fase P0 - Igualar rapido
Objetivo: cubrir funciones clave comparables a soluciones comerciales.

### P0.1 Incidencias HR + alertas automaticas
Alcance funcional:
- Retardo, inasistencia, permiso, vacacion, incapacidad.
- Catalogo de motivos y estados (pendiente, aprobado, rechazado).
- Alertas automaticas a RH/jefes por eventos criticos.

Backlog tecnico:
- Crear modulo API de incidencias (CRUD + aprobacion).
- Normalizar reglas de deteccion diaria por empleado.
- Worker de alertas (email/whatsapp interno o webhook).
- Pantallas web RH: bandeja de incidencias y filtro por sede/departamento.

Criterios de aceptacion:
- RH ve incidencias del dia en menos de 10 segundos.
- Cada incidencia queda auditada (quien, cuando, antes/despues).
- Alertas criticas disparan en menos de 5 minutos.

### P0.2 Flujo formal de horas extra y compensacion
Alcance funcional:
- Solicitud -> aprobacion -> cierre de horas extra.
- Politica de compensacion (pago, tiempo por tiempo, mixto).
- Bloqueo de cambios sin trazabilidad.

Backlog tecnico:
- API de solicitudes/decisiones de horas extra.
- Motor de reglas de compensacion parametrizable por empresa/sede.
- Pantalla web para aprobadores y seguimiento por estatus.
- Exportable para prenomina.

Criterios de aceptacion:
- No existe hora extra pagable sin aprobacion registrada.
- Se puede auditar cualquier cambio por usuario y timestamp.

### P0.3 Portal web colaborador
Alcance funcional:
- Tarjeta de asistencia personal por rango de fechas.
- Historial de marcas y comprobantes.
- Solicitud de permiso/vacacion/incapacidad.
- Estado de solicitudes y horas extra.

Backlog tecnico:
- Autenticacion por usuario/rol y recuperacion de acceso.
- API de self-service con permisos por rol.
- UI responsive (movil y desktop).
- Descarga de comprobantes por evento o periodo.

Criterios de aceptacion:
- Colaborador consulta su tarjeta sin soporte de RH.
- Las solicitudes llegan a aprobador con SLA visible.

## Fase P1 - Superior
Objetivo: diferenciarse por operacion inteligente y data accionable.

### P1.1 Geolocalizacion y geocerca para marcaje movil
Alcance funcional:
- Marcaje movil con coordenadas y precision.
- Validacion por geocerca configurable por centro de trabajo.
- Evidencia de ubicacion en auditoria.

Backlog tecnico:
- API de marcaje movil con validacion anti-spoof basica.
- Configuracion de geocercas por sede (radio, horario, tolerancia).
- Panel de incidencias por marcajes fuera de zona.

Criterios de aceptacion:
- Marcajes fuera de geocerca quedan bloqueados o marcados para revision.
- Auditoria muestra coordenada y precision de cada evento movil.

### P1.2 Integracion de nomina con conciliacion
Alcance funcional:
- Exportacion/importacion por API o archivo (CSV/XLSX).
- Bitacora de conciliacion (enviado, recibido, diferencias, corregido).
- Reproceso seguro por periodo.

Backlog tecnico:
- Conector nominal: mapeo de campos y validaciones.
- Jobs de envio y recepcion con reintentos.
- Pantalla de conciliacion con semaforos y evidencias.

Criterios de aceptacion:
- Cada corrida de prenomina tiene folio y resultado auditable.
- Diferencias quedan identificadas por empleado y concepto.

### P1.3 Dashboard multi-sede en tiempo real
Alcance funcional:
- Ranking de horas extra por sede/departamento.
- Ausentismo, retardos, cumplimiento por turno.
- Alertas ejecutivas y tendencias semanales/mensuales.

Backlog tecnico:
- Modelo de datos para agregaciones (near real-time).
- API analytics con filtros por sede/periodo.
- Dashboard web con vistas ejecutivas y operativas.

Criterios de aceptacion:
- Indicadores actualizados cada 5-10 minutos.
- Direccion identifica top riesgos de asistencia en una sola vista.

## Seguridad y cumplimiento (transversal)
- RBAC por rol (colaborador, jefe, RH, admin).
- Auditoria inmutable de cambios criticos.
- Politicas de retencion, respaldo y recuperacion.
- Evidencia legal MX en reportes y trazabilidad de asistencia.

## Plan de ejecucion sugerido (12 semanas)
1. Semanas 1-2: diseno API base, autenticacion, modelo incidencias.
2. Semanas 3-4: incidencias HR + alertas (P0.1) en produccion piloto.
3. Semanas 5-6: horas extra con aprobacion/compensacion (P0.2).
4. Semanas 7-8: portal colaborador MVP (P0.3).
5. Semanas 9-10: geocerca movil + evidencias (P1.1).
6. Semanas 11-12: integracion nomina + dashboard inicial (P1.2/P1.3).

## Tablero de ejecucion
| Item | Estado | Owner | Entregable |
|---|---|---|---|
| API base + Auth | Pendiente | Backend | Login, roles, permisos |
| P0.1 Incidencias + alertas | Pendiente | Backend/Web | Bandeja RH + notificaciones |
| P0.2 Horas extra formal | Pendiente | Backend/Web | Flujo aprobacion + bitacora |
| P0.3 Portal colaborador | Pendiente | Web | Tarjeta, historial, solicitudes |
| P1.1 Geocerca movil | Pendiente | Mobile/API | Marcaje geolocalizado |
| P1.2 Nomina + conciliacion | Pendiente | Backend | Integrador + trazabilidad |
| P1.3 Dashboard multi-sede | Pendiente | Data/Web | KPI en tiempo real |

## Definicion de terminado (DoD)
- Funcionalidad demoable con datos reales.
- Pruebas minimas automatizadas en reglas criticas.
- Auditoria y seguridad activas para el modulo.
- Manual operativo corto para RH.
- KPI de adopcion medible (uso real en operacion).

## Primeros pasos inmediatos (esta semana)
1. Crear `Checador.API` (ASP.NET Core) y `Checador.Web` (React/Next).
2. Implementar Auth + Roles + Auditoria base.
3. Entregar P0.1 en version MVP (incidencias + alertas).
4. Probar en una sede piloto con usuarios reales de RH.
