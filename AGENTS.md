# AGENT DE DESPLIEGUE - CHECADOR 2.0

Este archivo define el flujo obligatorio para que Codex despliegue **este proyecto**.

## 1) Alcance fijo

- Proyecto objetivo: `E:\checador 2.0`
- No crear ni tocar proyectos externos (ejemplo: FitTrack).
- Frontend público: Vercel (`vercel-proxy`).
- Backend + DB: corren en la PC local.

## 2) Configuracion de base de datos (local)

Usar uno de estos scripts antes de levantar servicios:

- `CONFIG-DB-PRUEBA.bat` -> usa `ZKBD_PRUEBA`
- `CONFIG-DB-REAL.bat` -> usa `ZKBD`

Estos scripts actualizan `Sql.ConnectionString` en:

- `src/Checador.Desktop/appsettings.local.json`
- `src/Checador.ConsoleApp/appsettings.local.json`
- `src/Checador.SyncAgent/appsettings.local.json`
- `src/Checador.Api/appsettings.local.json`

## 3) Levantar API y Web local

En PowerShell:

```powershell
cd "E:\checador 2.0"
dotnet run --no-launch-profile -c Release --project src\Checador.Api\Checador.Api.csproj -- --urls http://127.0.0.1:5085
dotnet run --no-launch-profile -c Release --project src\Checador.Web\Checador.Web.csproj -- --urls http://127.0.0.1:5095
```

Validar:

```powershell
curl.exe "http://127.0.0.1:5085/health/status"
curl.exe -I "http://127.0.0.1:5095/Login"
```

## 4) Exponer web por tunel

```powershell
cd "E:\checador 2.0"
npx localtunnel --port 5095
```

Tomar la URL `https://xxxx.loca.lt`.

## 5) Publicar proxy en Vercel

Editar `vercel-proxy/vercel.json` para apuntar al tunnel activo:

```json
{
  "rewrites": [
    { "source": "/", "destination": "https://TU-TUNNEL.loca.lt/Login" },
    { "source": "/(.*)", "destination": "https://TU-TUNNEL.loca.lt/$1" }
  ]
}
```

Deploy:

```powershell
cd "E:\checador 2.0\vercel-proxy"
npx --yes vercel@latest deploy --prod --yes --scope lilpne25-6180s-projects
```

La URL productiva actual se mantiene como alias del proyecto `checador`.

## 6) Validacion obligatoria post-deploy

```powershell
curl.exe -I "https://checador-kappa.vercel.app/Login"
1..5 | % { curl.exe -s -o NUL -w "%{http_code}`n" "https://checador-kappa.vercel.app/Login" }
```

Esperado: `200` en todas.

Si sale `503 Tunnel Unavailable`:

1. Reiniciar tunnel (`npx localtunnel --port 5095`).
2. Actualizar `vercel-proxy/vercel.json` con la nueva URL.
3. Redeploy en Vercel.

## 7) Reglas de seguridad

- Nunca subir secretos reales a git.
- Solo versionar `appsettings.local.example.json` (no `appsettings.local.json` con credenciales).
- Antes de commit, revisar:

```powershell
git status --short
rg -n "PWD=|Password=|User Id=|UID=" src docs
```

