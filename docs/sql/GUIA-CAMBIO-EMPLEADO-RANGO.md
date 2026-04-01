# Guía segura · Cambio de número y/o datos de empleado por rango

Objetivo: cambiar `USERID` y/o actualizar `Nombre/Dept/Shift` sin tocar horas, solo en ventana acotada.

## Archivos

- `docs/sql/CAMBIO-EMPLEADO-SQLSERVER-RANGO.sql`
- `docs/sql/CAMBIO-EMPLEADO-PERVASIVE-RANGO.sql`

## Orden recomendado (obligatorio)

1. Detener `SyncAgent` (o desactivar AutoSync).
2. Ejecutar **simulación** en SQL Server (`@ApplyChanges = 0`).
3. Revisar conflictos (`ConflictRows`) y muestra de filas.
4. Ejecutar SQL Server real (`@ApplyChanges = 1`).
5. Ejecutar script Pervasive (preview SELECT + UPDATE + validación).
6. Levantar `SyncAgent`.
7. Abrir `Admin SQL` y correr `Preview Pervasive`.
8. Validar 1 empleado de muestra en ambas bases.

## Qué sí cambia

- SQL Server:
  - `PunchLogs.UserId` (si cambias número) en rango.
  - `PunchLogs.EmployeeName` en rango.
  - `Employees` (nombre/depto/puesto/dept/shift) del USERID destino.
- Pervasive `TIME_ATTENDANCE`:
  - `EMPLOYEE` (si cambias número), `NAME`, `DEPT`, `SHIFT` en rango `CHARGE_DATE`.

## Qué NO cambia

- SQL Server: `PunchLogs.CheckTime`, `InOutMode`.
- Pervasive: `IN_TS_MIN`, `OUT_TS_MIN`, `DATE_IN/TIME_IN`, `DATE_OUT/TIME_OUT`.

## Validación mínima de salida

- En SQL Server:
  - conteo correcto en rango para USERID nuevo,
  - sin duplicados no resueltos,
  - tiempos intactos.
- En Pervasive:
  - EMPLOYEE/NAME/DEPT/SHIFT actualizados en rango,
  - tiempos intactos.
- En app:
  - `SyncAgent` operativo,
  - `Preview Pervasive` coherente (sin incidentes inesperados).

## Rollback práctico

Si algo no cuadra:

1. Detener `SyncAgent`.
2. Repetir scripts en sentido inverso (nuevo -> viejo) con mismo rango.
3. Validar de nuevo con preview.
4. Mantener `SimulationMode=true` en Pervasive hasta estabilizar.
