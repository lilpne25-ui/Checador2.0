# Checador 2.0 — Documento de contexto y timing

Fecha: 27 de marzo de 2026

Propósito
--------
Este documento reúne el contexto técnico del proyecto "Checador 2.0", describe la arquitectura, los componentes clave, riesgos críticos y propone un plan de trabajo con estimaciones de tiempo (timing) para presentar a tu jefe. Incluye opciones aceleradas y de alcance completo, criterios de aceptación y pasos inmediatos (DryRun, backup, despliegue seguro).

Resumen ejecutivo
-----------------
- Checador 2.0 es una solución en .NET 6 compuesta por una aplicación de escritorio (WinForms) para operación y administración, una consola/daemon para tareas programadas y un agente de sincronización. Lee marcas desde relojes ZKTeco (SDK COM) y desde Access (.mdb) como fallback; persiste en SQL Server y opcionalmente sincroniza a Pervasive (Btrieve) para sistemas heredados; genera reportes Excel por plantilla (ClosedXML).
- El riesgo más crítico son las escrituras sobre `dbo.PunchLogs` y la tabla `TIME_ATTENDANCE` de Pervasive (Btrieve). Operaciones de remapeo de USERID han provocado errores en Btrieve (ej.: "The key field is not modifiable"). Por eso todas las operaciones destructivas deben planearse con backup, previews y DryRuns.

Alcance del documento
---------------------
- Describir el estado actual del proyecto y sus componentes.
- Enumerar dependencias y requerimientos de entorno.
- Identificar los puntos de riesgo y mitigaciones.
- Proponer tareas concretas, tiempos estimados y rutas de validación. 

Estado actual (resumen)
-----------------------
- Lenguaje y plataforma: C# (.NET 6), WinForms (UI), proyectos: `Checador.Desktop`, `Checador.ConsoleApp`, `Checador.SyncAgent`, `Checador.Application`, `Checador.Infrastructure.*`, `Checador.Domain`.
- Integraciones clave:
  - ZKTeco SDK COM (zkemkeeper) — lectura directa de empleados, logs y fotos.
  - Access `.mdb` (ACE OLEDB) — fallback de marcas.
  - SQL Server — esquema con `dbo.Employees`, `dbo.PunchLogs`, `dbo.SyncState`, `dbo.WorkSchedules` y un índice único `UX_PunchLogs_User_Time (UserId,CheckTime)`.
  - Pervasive (ODBC/Btrieve) — espejo a `TIME_ATTENDANCE` (opcional).
  - ClosedXML — generación de reportes Excel (.xlsx/.xlsm) con preservación de macros.

Componentes y archivos clave
----------------------------
- UI / Admin:
  - `src/Checador.Desktop/Form1.cs` — generación y preview de reportes, test de conexión.
  - `src/Checador.Desktop/SyncAdminForm.cs` — panel administrativo (descarga, rename, delete, cambio por rango, preview Pervasive).
- Lógica de negocio:
  - `src/Checador.Application/AttendanceProcessor.cs` — reglas para calcular jornada, manejo nocturno, redondeo, políticas de 2/4 marcas.
  - `src/Checador.Application/AttendanceReportOrchestrator.cs` — orquestador de extracción y generación.
- Infraestructura:
  - `src/Checador.Infrastructure.Access/SqlDeviceSyncService.cs` — ingestión en SQL, EnsureSchema, limpieza correctiva, mirror orchestration.
  - `src/Checador.Infrastructure.Access/PervasiveAttendanceMirrorService.cs` — lógica del espejo y preview/apply.
  - `src/Checador.Infrastructure.Access/ZkTecoDirectAttendanceDataSource.cs` — comunicación con SDK COM.
  - `src/Checador.Infrastructure.Excel/ClosedXmlReportGenerator.cs` — generación de Excel desde plantilla.

Dependencias y requisitos de entorno
----------------------------------
- .NET 6 runtime (x86 o x64 según binarios).
- Registro del SDK COM `zkemkeeper` (posible 32-bit). La app que use el SDK debe ejecutarse en la misma arquitectura.
- Microsoft Access Database Engine (ACE) instalado (12/16) con la bitness correcta para leer `.mdb` vía OLEDB.
- Cliente ODBC para Pervasive y credenciales de acceso.
- Permisos SQL: capacidad para crear BD / tablas (EnsureSchema) y hacer backups.
- Rutas UNC accesibles para plantillas y salida de reportes.

Puntos de riesgo (priorizados)
-------------------------------
ALTO
- Escrituras en `dbo.PunchLogs` o `TIME_ATTENDANCE`: afectan nómina y datos históricos.
  - Consecuencias: nómina errónea, pérdida de auditoría, trabajo manual extenso.
  - Mitigación: backup completo, `SimulationMode=true` (si aplica), preview obligatorio, ejecución por lotes y verificación por muestreo.

ALTO
- Cambios masivos de USERID (reassign por rango) con conflicto en índice único (UserId+CheckTime).
  - Consecuencias: errores PK, filas omitidas, inconsistencias entre SQL y Pervasive.
  - Mitigación: usar preview (cuentas y conflictos), permitir `allowSkipConflicts` u opción SQL-only, generar CSV backups antes de aplicar.

MEDIO
- Operaciones sobre Pervasive (Btrieve) — limitaciones de Btrieve pueden impedir modificaciones de claves.
  - Consecuencia: operaciones fallan en el motor ODBC/Btrieve (error Btrieve 10) aunque SQL se haya modificado.
  - Mitigación: preferir SQL-only para correcciones urgentes; coordinar con DBA Pervasive; usar scripts idempotentes y preview.

MEDIO
- Cambios en `AttendanceProcessor` (reglas de redondeo, policies): requieren pruebas de RH porque afectan cálculo de horas.
  - Mitigación: tests unitarios, staging con datos reales, feature flags.

BAJO
- Cambios de UI (formato, estilos): impacto mínimo en datos, pero probar interacciones que llamen a operaciones destructivas.

Recomendaciones operativas (normas)
-----------------------------------
1. Antes de cualquier escritura destructiva: BACKUP completo de la BD.
2. Habilitar `SimulationMode=true` en el espejo Pervasive antes de aplicar cambios de espejo.
3. Ejecutar `Preview...` desde UI/servicio y revisar reporte de conflictos/días/filas.
4. Generar CSV backup de filas afectadas (por usuario y rango) y almacenar en un share seguro.
5. Aplicar cambios por lotes (`MaxRowsPerRun`) y validar after each batch.
6. Registrar y documentar cada cambio (bitácora) y notificar a RH antes de cerrar el cambio.

Propuesta de trabajo y timing (estimaciones)
-------------------------------------------
Supuestos:
- 1 desarrollador (senior) asignado. Acceso dev y credenciales disponibles. Entorno de staging con copia de datos.
- Horas laborales por día: 8h.

Lista de tareas (estimado en horas)
1) Preparación y reproducción del problema (clonar repo, montar entorno, confirmar drivers): 4h
2) Implementar corrección mínima (SQL-only reassign, botón UI opción SOLO SQL, backup CSV, pausa auto-sync): 8h
3) Crear script T-SQL seguro para mapeos (ej.: 2022->174, 2019->176, eliminación 2020) y revisarlo con DBA: 2h
4) PowerShell automatizado para backup + aplicar script + logs (DryRun/Apply): 2h
5) DryRun en staging y validación (incluye fixes menores): 4h
6) Unit tests para `AttendanceProcessor` (cobertura de casos críticos): 8h
7) Integración/Staging tests del flujo Sync -> SQL -> (preview) Pervasive: 8h
8) CI/CD y empaquetado del release (build y deploy binarios): 3h
9) Documentación y runbook para operaciones (how-to, rollback): 3h

Total estimado (suma): 40 horas
Contingencia recomendada (25%): 10 horas
Total con contingencia: 50 horas (~6.25 días laborales)

Opciones de entrega (timelines)
- Opción A — Arreglo urgente (mínimo viable) — 2.5 días
  - Alcance: tareas 1→5 (no incluye tests completos). Entrega rápida para resolver inconsistencia y dar continuidad operativa.
  - Riesgo residual: sin pruebas unitarias completas; requiere monitoreo post-despliegue.

- Opción B — Fase completa (recomendada) — 2 semanas (1 desarrollador) o 1 semana (2 desarrolladores)
  - Alcance: todas las tareas 1→9, incluye tests unitarios e integración, CI/CD y runbook.
  - Ventaja: menor riesgo en producción, cobertura de pruebas y rollback claro.

- Opción C — Mantenimiento extendido con auditoría RH — 3 semanas
  - Alcance: Opción B + validación con RH, revisiones de políticas de redondeo y pruebas en dataset real.

Plan sugerido (si el jefe autoriza ahora)
---------------------------------------
Dia 0 (Preparación)
- Confirmar accesos y detener SyncAgent/auto-sync.
- Snapshot/backup manual.

Día 1 (Dev):
- Implementar SQL-only reassign + UI opción y backups CSV.
- Crear PowerShell de backup + apply (DryRun disponible).

Día 2 (Staging):
- Ejecutar DryRun y revisar logs y previews.
- Ajustes y pruebas adicionales.

Día 3 (Prod pequeña):
- Si validado, aplicar en producción en ventana controlada (por lotes), monitorear 4–8 h.

Si se aprueba trabajo completo, añadir 4–6 días adicionales para tests, CI y documentación.

Criterios de aceptación (AC)
---------------------------
1. DryRun muestra 0 errores y produce backup exitoso (.bak) y CSV de filas afectadas.
2. Script T-SQL aplica sin errores en staging; conteos coinciden con preview.
3. Índice único no queda violado ni se pierden filas (salvo las clasificadas como conflictos y documentadas).
4. Unit tests `AttendanceProcessor` pasan en CI y validan los casos críticos.
5. Runbook con pasos para rollback probado y documentado.

Rollback y restauración
-----------------------
- Si algo sale mal tras aplicar: restaurar BD desde el .bak creado antes del cambio.
- Si el equipo tiene CSVs de backup, proveer script para reimportar filas manualmente (o ejecutar restauración parcial mediante MERGE/INSERT según caso).

Accesos y permisos requeridos
----------------------------
- Cuenta con permisos para crear backups y ejecutar scripts (DBA o usuario con roles).
- Acceso a la carpeta de Reports (UNC) para almacenar backups/CSV.
- Acceso a la red y a los relojes ZKTeco (si se van a realizar pruebas directas con dispositivo).

Entregables
-----------
1. Código: cambios en `SqlDeviceSyncService` y `SyncAdminForm` o alternativa SQL-only.
2. Script T-SQL seguro `REPARAR_CAMBIO_NUMERO_RANGO.sql` con bloques transaccionales.
3. PowerShell `apply_repair_and_backup.ps1` con modos DryRun/Apply.
4. CSV backups con filas afectadas (Documentos\ChecadorBackups\...)
5. Tests unitarios y pasos de CI.
6. Runbook de operación y rollback.

Notas, restricciones y consideraciones finales
---------------------------------------------
- Pervasive (Btrieve) presenta restricciones de modificación de campos clave; por eso recomendamos la modalidad SQL-only para correcciones urgentes y coordinar la actualización del espejo con especialistas de Pervasive.
- El repo puede haber cambiado respecto al análisis inicial; antes de aplicar en producción, clonar la rama actual y confirmar que los scripts y utilidades existan y estén revisados.
- Recomiendo que el cambio sea aprobado en un ticket con dueño (ej.: Dev: Nombre, DBA: Nombre, Operaciones: Nombre, RH: Nombre) y una ventana de mantenimiento definida.

Próximos pasos inmediatos (si tu jefe da luz verde)
--------------------------------------------------
1. Decidir opción (A urgente / B completa / C extendida).
2. Otorgar acceso DB/servidor y confirmar ventana de mantenimiento.
3. Ejecutar DryRun: comando PowerShell (ejemplo, sustituir TU_SERVIDOR):

```powershell
PowerShell -ExecutionPolicy Bypass -File "E:\checador 2.0\tools\apply_repair_and_backup.ps1" -SqlServer "TU_SERVIDOR" -Database "ZKBD" -DryRun
```

4. Revisar log en `C:\Users\<usuario>\Documents\ChecadorBackups\<timestamp>\apply_repair_<timestamp>.log` y notificar.

Contacto y propietarios
------------------------
- Propietario técnico (sugerido): [Nombre del desarrollador]
- DBA: [Nombre DBA]
- Responsable de RH: [Nombre RH]

Si quieres, genero ahora:
- 1) la versión final del script T-SQL parametrizado para los mapeos que indicastes (2022->174, 2019->176, eliminar 2020) listo para revisión por DBA.
- 2) el comando PowerShell listo con el nombre del servidor (pega el host/instancia y digo el comando exacto listo para ejecutar).

---
Documento generado por el equipo de soporte / desarrollo a partir del análisis del código base disponible y los incidentes reportados. Si deseas, lo adapto con fechas concretas y nombres de responsables para convertirlo en un plan para tu jefe.
