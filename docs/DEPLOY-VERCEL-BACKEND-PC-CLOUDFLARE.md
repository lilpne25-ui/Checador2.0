# Deploy hibrido: Frontend en Vercel + API/DB en tu PC

Objetivo: mantener `Checador.Api` + SQL Server en tu equipo local, y exponer solo la API por un tunnel seguro para que el frontend en Vercel funcione.

## Arquitectura

1. `Vercel Frontend` (publico)
2. `Cloudflare Tunnel` (publico -> privado, sin abrir puertos en router)
3. `Checador.Api` en `127.0.0.1:5085` (tu PC)
4. `SQL Server` local/LAN (tu PC o servidor interno)
5. `Checador.SyncAgent` sigue local para ZKTeco (sin exponer reloj)

## Requisitos

1. Dominio administrado en Cloudflare.
2. Tunnel creado en Cloudflare Zero Trust con hostname publico (ejemplo: `api-checador.tudominio.com`).
3. Token del tunnel (`CHECADOR_TUNNEL_TOKEN`).
4. `cloudflared` instalado en la PC backend:

```powershell
winget install --id Cloudflare.cloudflared
```

## Paso 1: configurar CORS en la API

En `src/Checador.Api/appsettings.local.json` define origen(es) del frontend.

Ejemplo:

```json
{
  "Sql": {
    "ConnectionString": "Server=tcp:SQL_HOST,1433;Database=ZKBD;Trusted_Connection=True;TrustServerCertificate=True;Connection Timeout=30;"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://tu-frontend.vercel.app",
      "https://*.vercel.app"
    ]
  }
}
```

Tambien puedes usar variables de entorno:

- `CHECADOR_Cors__AllowedOrigins__0=https://tu-frontend.vercel.app`
- `CHECADOR_Cors__AllowedOrigins__1=https://*.vercel.app`

## Paso 2: iniciar API + tunnel

Desde la raiz del repo:

```powershell
$env:CHECADOR_TUNNEL_TOKEN="PEGA_AQUI_TOKEN_CLOUDFLARE"
$env:CHECADOR_CORS_ORIGIN="https://tu-frontend.vercel.app"
.\INICIAR-API-CLOUDFLARE-TUNNEL.cmd
```

El script:

1. compila `Checador.Api` en Release,
2. levanta API en `http://127.0.0.1:5085`,
3. ejecuta `cloudflared tunnel run --token ...`.

## Paso 3: frontend en Vercel

En variables de entorno del frontend (Vercel), apunta la base URL al hostname del tunnel:

- `NEXT_PUBLIC_API_BASE_URL=https://api-checador.tudominio.com`

Si no usas Next.js, aplica equivalente en tu stack (`VITE_API_URL`, `REACT_APP_API_URL`, etc).

## Paso 4: validacion rapida

1. Local backend:
   - `http://127.0.0.1:5085/health/status` responde `ok`.
2. Publico (desde internet):
   - `https://api-checador.tudominio.com/health/status` responde `ok`.
3. Frontend Vercel:
   - login y consultas sin error CORS.

## Hardening minimo recomendado

1. Mantener `SyncAgent` como unico lector de reloj (ya esta en hardening actual).
2. En Cloudflare Access, restringir acceso a API por email dominio empresa o allowlist.
3. No usar `AllowAnyOrigin`; mantener lista explicita de origenes.
4. Rotar el token del tunnel si se comparte por error.
5. Monitorear logs de `cloudflared` y `Checador.Api`.
6. Respaldos SQL diarios.

## Operacion diaria

1. Encender PC backend.
2. Ejecutar `INICIAR-API-CLOUDFLARE-TUNNEL.cmd`.
3. Verificar health local y publico.
4. Ejecutar `Checador.SyncAgent` en esta misma red local.

## Escalamiento futuro (cuando crezca)

1. Mover API a App Service/VM.
2. Mantener SyncAgent local por sucursal.
3. Dejar SQL gestionado central (Azure SQL o SQL Server dedicado).
