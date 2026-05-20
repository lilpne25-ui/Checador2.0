# Checador.Api (LAN, read-only)

API interna para consulta en LAN reutilizando `Checador.Application` y `Checador.Infrastructure.Access`.

## Alcance

- Solo lectura.
- Fuente de datos: SQL (`dbo.Employees`, `dbo.PunchLogs`, `dbo.SyncState`).
- Sin endpoints para sincronizar reloj ni modificar marcas.

## Endpoints

- `POST /auth/login`
- `POST /auth/change-password`
- `GET /health/status`
- `GET /preview?userId={id}&from={ISO8601}&to={ISO8601}`
- `GET /preview/all?from={ISO8601}&to={ISO8601}`
- `GET /kpi?from={ISO8601}&to={ISO8601}`
- `GET /admin/users` (solo `admin`)

## Seguridad minima (Prompt 3)

- Script SQL: `docs/sql/API-ROLES-AUTH-LAN.sql`
- Tablas: `dbo.ApiUsers`, `dbo.ApiRoles`, `dbo.ApiUserRoles`
- Roles iniciales: `consulta`, `admin`

Importante:
- El script SQL requiere definir password temporal local antes de ejecutar.
- No usar password temporal por defecto en ambientes compartidos.
- El usuario `admin` debe entrar con `MustChangePassword=1` y cambiar password de inmediato.

Ejemplo de ejecucion (ajusta host/db segun tu LAN):

```powershell
sqlcmd -S SQL_HOST,1433 -d ZKBD -E -i "docs/sql/API-ROLES-AUTH-LAN.sql"
```

## Configuracion

Orden de carga:

1. `appsettings.json`
2. `appsettings.local.json` (opcional, no versionado)
3. Variables de entorno `CHECADOR_*`

Ejemplo local en `src/Checador.Api/appsettings.local.json`:

```json
{
  "Sql": {
    "ConnectionString": "Server=tcp:SQL_HOST,1433;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://TU-FRONTEND.vercel.app"
    ]
  }
}
```

Tambien disponible: `src/Checador.Api/appsettings.local.example.json`.

### CORS para frontend remoto

- Si el frontend vive fuera de LAN (ejemplo Vercel), define `Cors:AllowedOrigins`.
- Si no defines origenes, la API mantiene comportamiento LAN sin CORS publico.
- Para subdominios, soporta wildcard al inicio, por ejemplo `https://*.vercel.app`.

## Arranque local

```powershell
dotnet run --project src/Checador.Api/Checador.Api.csproj
```

LAN interna:

```powershell
$env:ASPNETCORE_URLS="http://0.0.0.0:5085"
dotnet run --project src/Checador.Api/Checador.Api.csproj
```

## Restricciones operativas

- Esta API no debe leer ZKTeco directo.
- `SyncAgent` sigue siendo el unico proceso de lectura de reloj.
- Hardening activo: la API fuerza `Device.Enabled=false` y `EnforceSyncAgentOnly=true`.
