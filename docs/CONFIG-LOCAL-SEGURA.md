# Configuracion Local Segura (Prompt 6)

Objetivo: mantener secretos y credenciales fuera de archivos versionados.

## Regla de carga

Todos los componentes usan este orden:

1. `appsettings.json` (base segura, sin secretos)
2. `appsettings.local.json` (por maquina, no versionado)
3. Variables de entorno `CHECADOR_*`

## Archivos example disponibles

- `src/Checador.Desktop/appsettings.local.example.json`
- `src/Checador.ConsoleApp/appsettings.local.example.json`
- `src/Checador.SyncAgent/appsettings.local.example.json`
- `src/Checador.Api/appsettings.local.example.json`
- `src/Checador.Web/appsettings.local.example.json`

## Paso a paso por maquina

1. Copiar cada `appsettings.local.example.json` a `appsettings.local.json` en el mismo proyecto.
2. Rellenar secretos reales solo en esos archivos locales:
   - `Sql:ConnectionString`
   - `Access:ConnectionString` (si aplica)
   - `PervasiveSync:ConnectionString` (si aplica)
   - `Device:Devices` (IPs/CommKey de reloj)
   - `Report:OutputDirectory` (si aplica)
3. Opcional: sobreescribir valores con variables `CHECADOR_*`.

## Variables de entorno (ejemplos)

```powershell
$env:CHECADOR_Sql__ConnectionString="Server=tcp:SQL_HOST,1433;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"
$env:CHECADOR_PervasiveSync__ConnectionString="Driver={Pervasive ODBC Unicode Interface};ServerName=PERVASIVE_HOST;DBQ=GLOBALPIN;UID=<USER>;PWD=<PASSWORD>;"
$env:CHECADOR_WebApi__BaseUrl="http://LAN_API_HOST:5085"
```

Nota: usar doble guion bajo `__` para separar secciones.

## Checklist post-cambio

- [ ] `git status` no muestra `appsettings.local.json`.
- [ ] `dotnet build Checador.sln -c Release` compila sin errores.
- [ ] Desktop abre sin exponer secretos en archivos versionados.
- [ ] SyncAgent corre con config local y sigue como unico lector ZKTeco.
- [ ] API responde `GET /health/status`.
- [ ] Web inicia y autentica contra API.
- [ ] Ningun `appsettings.json` versionado contiene `PWD=`, `Password=`, `UID=` real.
