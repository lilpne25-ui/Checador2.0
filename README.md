# Checador 2.0

> **Bitácora operativa obligatoria (AI Cache):**
> Antes de cambios nuevos, revisar y luego actualizar `docs/AI-CACHE-CHECADOR.md`.

Proyecto empresarial para procesar asistencias desde checador ZKTeco (modo directo) y/o `att2000.mdb`, calcular jornada/horas extra e imprimir reportes de Excel.

Modo actual recomendado: **reportes desde SQL Server** (`DataSource.Primary = Sql`) y sincronización incremental desde el checador.

## Estructura

- `src/Checador.Domain`: entidades y contratos.
- `src/Checador.Application`: motor de procesamiento de marcas.
- `src/Checador.Infrastructure.Access`: lectura desde Access vía OLEDB.
- `src/Checador.Infrastructure.Excel`: generación de Excel con ClosedXML.
- `src/Checador.ConsoleApp`: app de ejecución operativa.

## Requisitos

1. Instalar .NET SDK 8+ en el servidor/equipo ejecutor.
2. Instalar **Microsoft Access Database Engine (ACE OLEDB 12.0 o 16.0)** (solo si se usa Access/fallback).
3. SQL Server local habilitado (en este equipo: instancia default `MSSQLSERVER`) con conexión Windows Auth.
   - Cadena usada por defecto: `Server=.;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;`
4. Permisos de lectura en:
   - `\\ociserver\INNOVAX\AREA DE TRABAJO\C.- RECURSOS HUMANOS\att2000.mdb`
5. Permisos de escritura en:
   - `\\ociserver\INNOVAX\AREA DE TRABAJO\C.- RECURSOS HUMANOS\REPORTS`

## Ejecución esperada

1. Configurar `src/Checador.ConsoleApp/appsettings.json`.
2. Compilar solución.
3. Ejecutar consola e indicar:
   - USERID
   - Fecha inicio
   - Fecha fin
4. El sistema genera archivo en `...\Año\Semana_xx\Empleado.xlsx`.

## Arranque rápido (este equipo)

- Ya se instaló .NET 8 SDK en modo usuario en `C:\Users\TI\.dotnet`.
- Para abrir el sistema directamente, ejecuta `INICIAR-CHECADOR.cmd` desde la raíz del proyecto.

## Configuración oficial (importante)

- Edita solo estos archivos fuente:
   - `src/Checador.Desktop/appsettings.json`
   - `src/Checador.ConsoleApp/appsettings.json`
   - `src/Checador.SyncAgent/appsettings.json`
- Para valores sensibles usa `appsettings.local.json` (opcional, fuera de control de cambios) en cada proyecto.
- También puedes usar variables de entorno con prefijo `CHECADOR_`.
- No edites `bin/Debug` o `bin/Release`.
- Si quieres sincronizar configuración manualmente, ejecuta `SINCRONIZAR-CONFIG.cmd` (sincroniza `appsettings.json` y `appsettings.local.json` si existe).

Si deseas correr manualmente sin script, usa `dotnet run` sobre `src/Checador.ConsoleApp`.

## Interfaz gráfica (recomendada)

- Proyecto: `src/Checador.Desktop` (Windows Forms).

La pantalla principal incluye botón `Admin SQL`, donde puedes:

- Descargar logs del checador a SQL Server (incremental).
- Ejecutar migración inicial histórica `att2000.mdb -> ZKBD` (una sola vez).
- Ver catálogo de empleados del reloj.
- Renombrar empleados (SQL y dispositivo cuando SDK lo permite).
- Consultar historial por empleado/rango desde SQL.
- Activar auto-sync cada X minutos.

### Corte definitivo de Access

1. Ejecuta en `Admin SQL` la opción **Migración inicial Access→SQL**.
2. Verifica totales/historial en SQL.
3. Mantén `DataSource.Primary = "Sql"` en `appsettings.json`.
4. Deja `Device.FallbackToAccess = false`.

Con eso, los reportes se generan desde SQL y ya no dependen de Access para operación normal.

La interfaz permite:

- Capturar `USERID`.
- Seleccionar rango de fechas.
- Seleccionar carpeta de salida.
- Seleccionar plantilla `formato.xlsx` o `formato.xlsm` (con macros).
- Generar y abrir el último reporte.

## Reglas de negocio implementadas

- 2 marcas: jornada continua.
- 3 marcas: incompleto (sin cálculo).
- 4 marcas: cálculo completo con comida.
- > 4 marcas: toma primeras 4 (o incidencia si política estricta).
- Redondeo configurable (por defecto 5 minutos).

## Siguiente fase recomendada

- Crear ETL hacia SQL Server (`AsistenciasRaw`, `AsistenciasProcesadas`).
- Migrar cálculos/consulta analítica al motor SQL.
- Publicar API + servicio Windows para ejecución programada.
