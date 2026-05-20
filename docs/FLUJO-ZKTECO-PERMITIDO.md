# Flujo Permitido ZKTeco (Prompt 4)

## Regla operativa

- `Checador.SyncAgent` es el unico proceso autorizado para conexion directa a ZKTeco (COM `zkemkeeper`).
- `Checador.Desktop`, `Checador.ConsoleApp`, `Checador.Api` y Web LAN solo consultan SQL para preview/reportes.

## Hardening aplicado

1. Guardia de proceso en `ZkTecoDirectAttendanceDataSource`:
   - valida `DirectDeviceOptions.EnforceSyncAgentOnly`.
   - permite solo ensamblados/procesos listados en `AllowedEntryAssemblies` (default: `Checador.SyncAgent`).
2. Si un proceso no autorizado intenta tocar reloj:
   - se bloquea la conexion antes de `Connect_Net`.
   - se lanza excepcion controlada.
   - se registra intento en `logs/direct-device-guard.log`.
3. En `Checador.Api` se fuerza por codigo:
   - `Device.Enabled=false`
   - `Device.PreferDirect=false`
   - `Device.FallbackToAccess=false`
   - `Device.EnforceSyncAgentOnly=true`

## Configuracion esperada (`Device`)

```json
{
  "EnforceSyncAgentOnly": true,
  "AllowedEntryAssemblies": [ "Checador.SyncAgent" ]
}
```

## Validacion recomendada

1. Ejecutar SyncAgent y confirmar sincronizacion normal.
2. Intentar sync directo desde Desktop/Console:
   - debe fallar con mensaje de bloqueo.
   - debe generar linea en `logs/direct-device-guard.log`.
3. Validar API/Web:
   - endpoints de consulta responden contra SQL sin tocar reloj.
