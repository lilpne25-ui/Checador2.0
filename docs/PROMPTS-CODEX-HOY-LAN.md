# Prompts Codex - Ejecucion Hoy (LAN, Sin Costo)

## Objetivo de hoy
Implementar base web interna en LAN para Checador 2.0 sin abrir internet y sin romper operacion actual:

- API interna .NET reutilizando `Checador.Application` + `Checador.Infrastructure.Access`.
- Usuarios/roles basicos en SQL (`consulta` / `admin`).
- `SyncAgent` se mantiene como unico proceso que toca ZKTeco.
- Web interna LAN solo lectura (reportes/preview) por rol.
- Credenciales fuera de archivos compartidos (`appsettings.local.json` + `CHECADOR_*`).

## Reglas fijas para todos los prompts

- No exponer SQL Server ni ZKTeco a internet.
- No tocar ni reescribir logica critica de asistencia.
- No mover integracion COM `zkemkeeper` fuera de `SyncAgent`/infra actual.
- Cambios incrementales, compatibles y con rollback simple.
- Cada fase debe compilar y validar antes de pasar a la siguiente.

## Prompt 1 - Auditoria tecnica inicial (sin cambios)

```text
Actua como arquitecto/tech lead .NET senior.

Contexto:
- Repo: E:\checador 2.0
- Debes leer completo: docs/AI-CACHE-CHECADOR.md
- No modifiques codigo todavia.

Objetivo:
1) Confirmar rutas reales de:
- logica critica (AttendanceProcessor y orquestacion)
- integracion ZKTeco (zkemkeeper)
- Sync SQL (Employees, PunchLogs, SyncState)
- espejo Pervasive
- formularios/servicios actuales

2) Proponer diseno minimo de API LAN + Web LAN con reutilizacion maxima de codigo.

3) Enumerar riesgos de romper operacion y como evitarlos.

Formato de salida:
- Evidencia con rutas reales.
- [No verificado] cuando aplique.
- Lista corta de decisiones tecnicas para hoy.
```

## Prompt 2 - Crear API LAN minima (implementacion)

```text
Implementa una API interna local en .NET (solo LAN) reutilizando el core actual.

Objetivo:
- Crear nuevo proyecto `src/Checador.Api` (ASP.NET Core).
- Reutilizar servicios de `Checador.Application` + `Checador.Infrastructure.Access` por DI.
- Exponer endpoints de solo lectura para:
  - health/status
  - preview por empleado/rango
  - preview masivo (read-only)
  - resumen KPI read-only
- NO exponer endpoints que alteren ZKTeco directo.

Restricciones:
- No mover la lectura de reloj fuera de `SyncAgent`.
- No duplicar logica de asistencia, consumir clases existentes.
- Mantener compatibilidad .NET 8.
- Configuracion por `appsettings.json` + `appsettings.local.json` + `CHECADOR_*`.

Entregables:
- Proyecto API compilando.
- Endpoints documentados en un `README` del proyecto.
- Validacion con `dotnet build` y prueba local de endpoints.
```

## Prompt 3 - Roles basicos en SQL (implementacion)

```text
Implementa seguridad minima por usuarios/roles para la API LAN.

Objetivo:
- Crear tablas SQL minimas (si no existen):
  - ApiUsers
  - ApiRoles
  - ApiUserRoles
- Roles iniciales: `consulta`, `admin`.
- Semilla inicial de usuario admin local (credencial temporal obligando cambio).
- En API aplicar autorizacion por rol:
  - endpoints preview/reportes: `consulta` o `admin`
  - endpoints administrativos (si existen): solo `admin`

Restricciones:
- No romper tablas actuales (`Employees`, `PunchLogs`, `SyncState`).
- No hardcodear secretos en codigo.
- Usa migracion/bootstrapping incremental y reversible.

Entregables:
- Script SQL en `docs/sql/`.
- Integracion auth en API.
- Evidencia de prueba por rol.
```

## Prompt 4 - Blindaje SyncAgent como unico lector ZKTeco

```text
Asegura por codigo y configuracion que SyncAgent sea el unico proceso que toca ZKTeco.

Objetivo:
- Verificar que la API no invoque lectura directa del reloj.
- Documentar y reforzar en config que Desktop/Web/API consumen SQL para consulta.
- Agregar guardas/logs para detectar intentos de acceso directo no autorizado al reloj fuera del agente.

Restricciones:
- No tocar la mecanica principal de SyncAgent ya validada.
- No cambiar reglas criticas de attendance.

Entregables:
- Cambios minimos de hardening.
- Nota tecnica en `docs/` explicando el flujo permitido.
```

## Prompt 5 - Web LAN temporal read-only

```text
Implementa una web interna temporal (LAN) enfocada en lectura.

Objetivo:
- Crear `src/Checador.Web` (React + TypeScript) o alternativa minima .NET Razor, la mas rapida y estable para hoy.
- Login simple contra API.
- Vistas read-only:
  - resumen KPI
  - preview por empleado
  - preview masivo
- Mostrar acciones de admin solo si rol = admin (aunque hoy sean solo vistas).

Restricciones:
- Sin exposicion a internet.
- Sin escritura de marcas ni manipulacion de ZKTeco.
- UX simple, funcional y clara.

Entregables:
- App corriendo en LAN.
- Config de URL API por entorno local.
- Guia corta de arranque.
```

## Prompt 6 - Higiene de secretos/config

```text
Aplica higiene de configuracion para evitar exposicion de credenciales.

Objetivo:
- Mover secretos reales a `appsettings.local.json` (no versionado) y variables `CHECADOR_*`.
- Crear/actualizar `appsettings.local.example.json` en Desktop/SyncAgent/Console/API/Web-backend.
- Revisar y limpiar credenciales sensibles de archivos compartidos/documentacion.
- Documentar procedimiento de configuracion local por maquina.

Restricciones:
- No romper arranque actual.
- Mantener prioridad de `appsettings.local.json` ya implementada.

Entregables:
- Archivos example listos.
- README de configuracion local actualizado.
- Checklist de verificacion post-cambio.
```

## Prompt 7 - Validacion integral y handoff

```text
Ejecuta validacion integral y deja handoff para continuar manana.

Objetivo:
1) Build de solucion y proyectos nuevos.

2) Smoke tests:
- SyncAgent operativo
- API LAN responde
- Web LAN consume API
- autorizacion por roles funciona

3) Verificar que no se toco la integracion ZKTeco fuera de SyncAgent.

4) Generar reporte final en Markdown con:
- cambios realizados
- rutas modificadas
- comandos ejecutados
- riesgos pendientes
- siguiente fase para abrir acceso externo seguro (sin implementarla aun)

Formato:
- Directo, tecnico, sin relleno.
- Marcar [No verificado] donde aplique.
```

## Orden recomendado de ejecucion hoy

1. Prompt 1
2. Prompt 2
3. Prompt 3
4. Prompt 4
5. Prompt 5
6. Prompt 6
7. Prompt 7

## Criterio de exito de hoy

- API + Web en LAN funcionando con roles basicos.
- SyncAgent intacto como unico lector de reloj.
- Sin exposicion a internet.
- Secretos fuera de archivos compartidos.
- Base lista para siguiente fase (acceso externo seguro).
