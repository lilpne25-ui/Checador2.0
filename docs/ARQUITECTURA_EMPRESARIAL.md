# Arquitectura empresarial propuesta

## Flujo

1. Lectura de marcas en Access (`att2000.mdb`) en modo compartido.
2. Transformación y validación por día/empleado.
3. Cálculo de horas trabajadas y extras.
4. Generación de reporte Excel centralizado.

## Diseño por capas

- **Domain**: modelos y contratos puros.
- **Application**: reglas de negocio (motor de asistencia).
- **Infrastructure**:
  - Access: repositorio OLEDB.
  - Excel: salida de reportes.
- **ConsoleApp**: composición de dependencias y ejecución.

## Consideraciones operativas

- Ejecutar con cuenta de servicio con permisos UNC.
- Definir timeout para Access (actual 120s).
- Registrar errores para bloqueos/red/permisos.
- Configuración de ruta y plantilla por `appsettings.json`.

## Fase 2 (recomendada)

- Replicación ETL Access ➜ SQL Server.
- Servicios:
  - `Checador.Etl.Worker`
  - `Checador.Calculo.Worker`
- Exposición de API para consulta de historial consolidado.
