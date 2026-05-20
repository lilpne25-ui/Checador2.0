# Configuración remota MSSQL (red Ethernet)

Esta guía permite conectar múltiples PCs cliente al SQL Server en `SQL_HOST:1433`, base `ZKBD`.

## Datos de conexión

- Servidor SQL: `SQL_HOST`
- Puerto: `1433`
- Base de datos: `ZKBD`

Autenticación recomendada en este entorno: **Windows integrada** (`Trusted_Connection=True`).

Connection string recomendada en el proyecto:

`Server=tcp:SQL_HOST,1433;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;`

Alternativa si tu servidor requiere SQL Login (solo si tu DBA lo confirma):

`Server=tcp:SQL_HOST,1433;Database=ZKBD;User Id=<usuario_sql>;Password=<password>;TrustServerCertificate=True;Connection Timeout=30;`

## 1) Configuración en el servidor SQL (SQL_HOST)

1. Abrir **SQL Server Configuration Manager**.
2. Ir a **SQL Server Network Configuration > Protocols for <instancia>**.
3. Habilitar **TCP/IP**.
4. En **TCP/IP > Properties > IP Addresses**:
   - Verificar `TCP Port = 1433` (IPAll o IP específica).
5. Reiniciar el servicio de SQL Server.
6. Confirmar el modo de autenticación requerido por tu entorno (Windows integrada o SQL Login).

## 2) Firewall del servidor

Permitir tráfico entrante TCP en puerto `1433` (Windows Defender Firewall o firewall corporativo).

## 3) Configuración en cada PC cliente

En este repositorio se usan archivos locales para no modificar el `appsettings.json` original:

- `src/Checador.Desktop/appsettings.local.json`
- `src/Checador.ConsoleApp/appsettings.local.json`
- `src/Checador.SyncAgent/appsettings.local.json`

Cada uno contiene solo override de `Sql.ConnectionString`.

## 4) Validar conectividad desde cliente

Ejecutar:

- `docs/VALIDAR-CONEXION.cmd`

Resultado esperado:

- `[OK] Conexion TCP exitosa ...`

Si falla:

- Revisar IP/puerto.
- Verificar firewall en servidor y cliente.
- Confirmar que SQL esté escuchando en TCP 1433.

## 5) Prueba funcional de app

1. Iniciar `Checador.Desktop` o `Checador.SyncAgent` desde su script correspondiente.
2. Verificar que no aparezcan errores de conexión SQL al arrancar/sincronizar.
3. En caso de error, confirmar permisos sobre base `ZKBD` para el usuario Windows o SQL Login configurado.

## Recomendaciones de seguridad

- Evitar usar `sa` en producción cuando sea posible.
- Crear un usuario SQL dedicado con permisos mínimos necesarios sobre `ZKBD`.
- Restringir acceso de red al puerto 1433 solo a la subred autorizada.

