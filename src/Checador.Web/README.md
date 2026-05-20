# Checador.Web (LAN temporal, read-only)

Web interna Razor Pages para consulta de reportes via `Checador.Api`.

## Alcance

- Login simple contra API (`/auth/login`).
- Vistas read-only:
  - KPI
  - Preview por empleado
  - Preview masivo
- Vista admin (`/Admin/Users`) visible solo para rol `admin`.
- Sin escritura de marcas ni acceso directo a ZKTeco.

## Configuracion

Orden de carga:

1. `appsettings.json`
2. `appsettings.local.json` (opcional, no versionado)
3. Variables `CHECADOR_*`

`WebApi:BaseUrl` define URL de `Checador.Api`.

Ejemplo local (`src/Checador.Web/appsettings.local.json`):

```json
{
  "WebApi": {
    "BaseUrl": "http://LAN_API_HOST:5085"
  }
}
```

Tambien disponible: `src/Checador.Web/appsettings.local.example.json`.

## Arranque

```powershell
dotnet run --project src/Checador.Web/Checador.Web.csproj
```

LAN interna:

```powershell
$env:ASPNETCORE_URLS="http://0.0.0.0:5095"
dotnet run --project src/Checador.Web/Checador.Web.csproj
```

## Dependencia operativa

- Requiere `Checador.Api` disponible en `WebApi:BaseUrl`.
- Si `MustChangePassword=true`, la web obliga cambio de password antes de usar reportes.
