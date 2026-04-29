# Checador 2.0

> **Bitácora operativa obligatoria (AI Cache):**
> Antes de hacer cambios, revisar y luego actualizar `docs/AI-CACHE-CHECADOR.md`.

Sistema empresarial para sincronizar asistencias desde checador ZKTeco hacia SQL Server, procesar jornadas/horas extra, generar reportes (Excel/PDF) y opcionalmente espejar información operativa a Pervasive.

## Estado actual (real)

- Framework: **.NET 8** (`net8.0-windows`).
- Modo operativo principal: **SQL Server** (`DataSource.Primary = "Sql"`).
- Lectura de marcas: **ZKTeco directo** + histórico SQL.
- Reportes: Excel (`.xlsx/.xlsm`) y PDF.
- UI principal: `Checador.Desktop` (WinForms).
- Servicio desacoplado de sincronización: `Checador.SyncAgent`.

## Qué hace hoy el sistema

1. Sincroniza empleados y marcas del reloj a SQL (`Employees`, `PunchLogs`, `SyncState`).
2. Crea automáticamente base/esquema SQL si no existen (bootstrap inicial).
3. Procesa jornadas con heurísticas de `InOutMode` (incluyendo casos invertidos/ruidosos).
4. Calcula horas trabajadas y horas extra con autorización por empleado.
5. Aplica reglas por horario para conteo de extra:
   - Horario 1: extra desde `07:00`
   - Horario 2: extra desde `08:00`
   - Horario 3: extra desde `18:00`
6. Genera reporte con plantilla corporativa (`formato.xlsx` / `formato.xlsm`) y opción PDF.
7. Permite espejo a Pervasive (`TIME_ATTENDANCE`) con preview y candados operativos.

## Arquitectura de proyectos

- `src/Checador.Domain`: modelos y contratos.
- `src/Checador.Application`: motor de procesamiento de asistencia.
- `src/Checador.Infrastructure.Access`: SQL, Access, ZKTeco directo, sync y espejo Pervasive.
- `src/Checador.Infrastructure.Excel`: generación de reportes con ClosedXML y exportación PDF.
- `src/Checador.Desktop`: interfaz WinForms para operación diaria.
- `src/Checador.ConsoleApp`: utilidades operativas, auditorías y recálculos.
- `src/Checador.SyncAgent`: agente de sincronización desacoplado.

## Configuración activa de referencia

### Desktop (`src/Checador.Desktop/appsettings.json`)

- SQL: `Server=TI;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;`
- Reloj: `192.168.1.201:4370`
- `DataSource.Primary = "Sql"`
- `PervasiveSync.Enabled = true`
- Salida reportes: `\\ociserver\INNOVAX\AREA DE TRABAJO\C.- RECURSOS HUMANOS\REPORTS`

### Console (`src/Checador.ConsoleApp/appsettings.json`)

- SQL: `Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;`
- `PervasiveSync.Enabled = false` (en consola por defecto)

### SyncAgent (`src/Checador.SyncAgent/appsettings.json`)

- SQL: `Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;`
- `PervasiveSync.Enabled = true`

> Recomendación: credenciales sensibles deben ir en `appsettings.local.json` o variables de entorno `CHECADOR_*`.

## Requisitos

1. .NET SDK 8+.
2. SQL Server accesible desde el equipo ejecutor.
3. SDK/COM del reloj ZKTeco disponible (si se usa modo directo).
4. ACE OLEDB únicamente si usarás Access/fallback.
5. Permisos de red en rutas UNC configuradas para plantillas/salidas.

### Requisito especial: COM ZKTeco (`zkemkeeper`)

Si en otra PC aparece el mensaje `No se encontró zkemkeeper (CZKEM/ZKEM)`, ejecuta:

1. Instalar SDK StandAlone de ZKTeco (x86).
2. Abrir PowerShell **como Administrador** en la raíz del proyecto.
3. Ejecutar:

- `.\instalar-zkteco-sdk-admin.ps1`
- (opcional) `.\instalar-zkteco-sdk-admin.ps1 -DllPath "C:\Ruta\zkemkeeper.dll"`

El script valida ProgID/CLSID y registra `zkemkeeper.dll` con `regsvr32` x86 (`SysWOW64`).

## Arranque rápido

- Desktop (recomendado para operación diaria):
  - `INICIAR-CHECADOR.cmd`
- SyncAgent:
  - `INICIAR-SYNCAGENT.cmd`
- Sincronizar config fuente -> bin:
  - `SINCRONIZAR-CONFIG.cmd`

## Operación en `Admin SQL` (Desktop)

Desde el botón **Admin SQL** puedes:

- Descargar logs del reloj a SQL (incremental).
- Ver catálogo de empleados y metadatos RH.
- Editar nombre/área/puesto/horario/autorización de extra.
- Consultar historial por empleado y rango.
- Ejecutar preview de payload Pervasive.
- Gestionar autosync.

## Comandos útiles de consola

- Modo interactivo (reporte por empleado/rango):
  - `dotnet run --project src/Checador.ConsoleApp/Checador.ConsoleApp.csproj`
- Daemon de sincronización:
  - `dotnet run --project src/Checador.ConsoleApp/Checador.ConsoleApp.csproj -- --daemon-sync`
- Reconciliación excepcional marzo 2026:
  - `dotnet run --project src/Checador.ConsoleApp/Checador.ConsoleApp.csproj -- --reconcile-march-2026-once`
- Auditoría marzo (detalle completo):
  - `dotnet run --project src/Checador.ConsoleApp/Checador.ConsoleApp.csproj -- --audit-march-2026-full-detail`

## Reportes

- Formatos: Excel y PDF.
- Plantillas soportadas: `formato.xlsx` y `formato.xlsm`.
- Salida configurable por `Report.OutputDirectory`.
- El reporte usa datos de SQL en modo operativo normal.

## Despliegue en otra máquina (cascarón)

Sí se puede usar como cascarón: al conectar a SQL, el sistema inicializa DB/esquema automáticamente.

Pasos mínimos:

1. Clonar repo.
2. Configurar `appsettings.local.json` (Desktop/Console/SyncAgent) con conexión SQL destino.
   - Puedes partir de estas plantillas:
   - `src/Checador.Desktop/appsettings.local.example.json`
   - `src/Checador.ConsoleApp/appsettings.local.example.json`
   - `src/Checador.SyncAgent/appsettings.local.example.json`
3. Ejecutar `INICIAR-CHECADOR.cmd` o `dotnet run`.
4. Primer flujo SQL crea la estructura necesaria si no existe.

## Notas de operación

- No editar archivos dentro de `bin/` o `obj/`.
- Editar solo configuraciones fuente en `src/*/appsettings.json` + `appsettings.local.json`.
- Si hay cambios de lógica/reglas, documentarlos en `docs/AI-CACHE-CHECADOR.md`.
