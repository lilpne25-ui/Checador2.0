# Manual RH Web · Fase 5

## Alcance
Guía rápida para RH sobre los flujos principales ya migrados a Web:
- preview de asistencias,
- correcciones manuales,
- reportes y formato OT,
- validaciones de seguridad.

## 1) Validación de asistencias
1. Usar **Preview empleado** para revisar casos puntuales.
2. Usar **Preview masivo** para validar el periodo completo.
3. Si hay incidencia, registrar `USERID`, fecha y tipo de anomalía.

## 2) Corrección operativa RH
- **Alta manual**:
  - puntual (`single`) o por lote (`batch`),
  - usar modos `0/1/2/3` según flujo real.
- Confirmar que el ajuste aparezca en preview posterior.

## 3) Reportes
- Generar reportes por empleado o masivo (`Excel`/`PDF`).
- Generar formato OT desde módulo de reportes.
- Validar carpeta de salida y nombre de archivo final.

## 4) Pervasive (con soporte técnico)
- Usar **preview** antes de cualquier limpieza.
- Para ejecución de limpieza nocturna, confirmar texto obligatorio `EJECUTAR LIMPIEZA`.
- Si hay duda de impacto, detener y pedir revisión a Operaciones.

## 5) Buenas prácticas
- No ejecutar acciones destructivas sin preview previo.
- Documentar cada cambio manual sensible (qué, cuándo, por qué).
- Reportar de inmediato cualquier mensaje de bloqueo por hardening o módulo deshabilitado.

## 6) Qué hacer si falta un módulo
Si un módulo no aparece en menú, puede estar temporalmente desactivado por feature flag (contingencia). Solicitar a Operaciones el estatus antes de escalar.
