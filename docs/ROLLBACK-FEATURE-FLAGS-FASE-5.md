# Rollback por Feature Flags · Fase 5 (Checador 2.0)

## Objetivo
Contar con un mecanismo de **rollback funcional inmediato** (sin redeploy) para desactivar módulos Web/API cuando se detecte incidente operativo.

## Ubicación de flags
- API: `src/Checador.Api/appsettings.json` (`FeatureFlags:Modules`)
- Web: `src/Checador.Web/appsettings.json` (`FeatureFlags:Modules`)
- Overrides locales de ejemplo:
  - `src/Checador.Api/appsettings.local.example.json`
  - `src/Checador.Web/appsettings.local.example.json`

## Mapa de módulos (API)

| Flag | Rutas afectadas |
|---|---|
| `EnableOps` | `/ops/*` |
| `EnableOpsTelemetry` | `/ops/telemetry` |
| `EnablePreview` | `/preview`, `/preview/all` |
| `EnableKpi` | `/kpi` |
| `EnableAdminUsers` | `/admin/users` |
| `EnableAdminEmployees` | `/admin/employees/*` |
| `EnableAdminPervasive` | `/admin/pervasive/*` |
| `EnableAdminManualPunch` | `/admin/manual-punch*` |
| `EnableReports` | `/reports/*` |

> Comportamiento API al desactivar: `503 Service Unavailable` + mensaje de módulo deshabilitado.

## Mapa de módulos (Web)

| Flag | Pantallas/menú afectados |
|---|---|
| `EnableOps` | `/Ops/*` + menú Operación |
| `EnableEmployees` | `/Admin/Employees`, `/Admin/EmployeeEdit` + menú Empleados |
| `EnableReassign` | `/Admin/Reassign` + menú Cambio por rango |
| `EnablePervasive` | `/Admin/Pervasive` + menú Pervasive |
| `EnableManualPunch` | `/Admin/ManualPunch` + menú Alta manual |
| `EnableReports` | `/Admin/Reports` + menú Reportes |
| `EnableAdminUsers` | `/Admin/Users` + menú Admin |

> Comportamiento Web al desactivar: menú oculto y ruta devuelve `404` con mensaje de módulo deshabilitado.

## Procedimiento rápido de rollback
1. Identificar módulo afectado (ejemplo: Pervasive).
2. Cambiar flag a `false` en API y Web para ese módulo.
3. Reiniciar `Checador.Api` y `Checador.Web`.
4. Validar:
   - API: ruta responde `503`.
   - Web: opción ya no aparece en menú y ruta devuelve `404`.
5. Registrar incidencia en bitácora operativa.

## Roll-forward (restaurar módulo)
1. Corregir causa raíz.
2. Revertir flag a `true`.
3. Reiniciar servicios.
4. Ejecutar smoke del módulo y checklist UAT mínimo.

## Recomendación de operación
Aplicar rollback por módulo antes de apagar todo el sistema. Esto mantiene disponibles KPI/preview y reduce impacto para RH/Operación.
