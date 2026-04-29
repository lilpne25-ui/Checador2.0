# Reporte Ejecutivo - Checador 2.0

Fecha: 28 de abril de 2026  
Dirigido a: Equipo interno / Dirección

## 1) Qué es el sistema
Checador 2.0 es un sistema empresarial de control de asistencia que sincroniza marcas desde relojes ZKTeco hacia SQL Server, procesa jornadas y horas extra con reglas por horario, y genera reportes operativos en Excel y PDF para RH.

## 2) Qué cubre actualmente (alcance real)
- Sincronización de empleados y marcas del reloj a base SQL.
- Creación automática de estructura base en SQL (bootstrap inicial).
- Cálculo de jornada laboral (entradas/salidas) con manejo de casos invertidos o ruidosos.
- Cálculo de horas trabajadas y horas extra por reglas de horario.
- Administración operativa desde módulo Admin SQL:
  - edición de datos de empleado (nombre, área, puesto, horario, autorización de extra),
  - consulta de historial por empleado y rango,
  - herramientas de revisión y operación de sincronización.
- Generación de reportes en plantilla corporativa (Excel) y salida PDF.
- Opción de espejo operativo a Pervasive para escenarios que lo requieren.

## 3) Cobertura por áreas
- RH: control de asistencia, horas extra y reportes por periodo.
- Operaciones: sincronización continua, monitoreo y corrección administrativa.
- Dirección: reportes consolidados exportables para toma de decisiones.
- TI: arquitectura por capas (.NET 8), separando dominio, aplicación, infraestructura, desktop y agente de sincronización.

## 4) Opciones de base de datos (importante)
### Opción A: Base de datos en nube
- La base de datos se aloja en infraestructura cloud (por ejemplo: Azure SQL, AWS RDS o VPS administrado).
- Ventajas: acceso remoto más simple, escalabilidad, respaldo administrado, menor dependencia de infraestructura local.
- Condición económica: el costo mensual de nube/hosting/base de datos lo asume el cliente.

### Opción B: Base de datos local (on-premise)
- La base de datos se hospeda en servidores propios del cliente.
- Ventajas: mayor control interno y cumplimiento de políticas IT locales.
- Condición económica: el cliente asume su infraestructura interna (servidor, respaldos, red, seguridad y mantenimiento de hardware).

## 5) Estimación de precio del sistema (MXN)
Estimación recomendada para comercialización del producto, considerando el estado actual del software:

### Licencia e implementación inicial
- Rango sugerido: $120,000 a $180,000 MXN (pago único).
- Incluye:
  - instalación y configuración inicial,
  - conexión a reloj(es) y SQL,
  - parametrización base de horarios/reglas,
  - puesta en marcha y validación operativa,
  - capacitación corta al equipo clave.

### Soporte y mantenimiento mensual (recomendado)
- Rango sugerido: $8,000 a $18,000 MXN / mes.
- Incluye:
  - soporte correctivo,
  - ajustes menores operativos,
  - seguimiento de estabilidad y respaldo de configuración,
  - acompañamiento técnico para continuidad.

Nota comercial:
- El costo de nube (si se elige Opción A) no está incluido en la licencia del sistema.
- En Opción B no hay costo de nube, pero la operación del servidor local la cubre el cliente.

## 6) Conclusión ejecutiva
Checador 2.0 ya cubre operación real de asistencia, cálculo y reporteo para RH. Es un producto vendible en su estado actual, con posibilidad de escalar a módulos web/API y analítica avanzada en una siguiente fase sin reescribir el core.
