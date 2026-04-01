/*
CAMBIO CONTROLADO DE EMPLEADO EN PERVASIVE (TIME_ATTENDANCE)
-------------------------------------------------------------
Objetivo:
- Cambiar EMPLOYEE (si aplica), NAME, DEPT y SHIFT SOLO por rango de CHARGE_DATE.
- NO tocar tiempos: IN_TS_MIN, OUT_TS_MIN, DATE_IN/TIME_IN, DATE_OUT/TIME_OUT.

IMPORTANTE:
- Este script está en formato plantilla (reemplaza los valores entre <...>).
- Ejecuta primero los SELECT de PREVIEW.
- Después ejecuta UPDATE en una sesión controlada.
*/

/* ============================
   1) PARAMETROS (REEMPLAZAR)
   ============================
   <OLD_EMPLOYEE>  -> ejemplo: '00010'
   <NEW_EMPLOYEE>  -> ejemplo: '00110' (si no cambia número, usa mismo valor)
   <FROM_YYYYMMDD> -> ejemplo: '20260309'
   <TO_YYYYMMDD>   -> ejemplo: '20260311'
   <NEW_NAME>      -> ejemplo: 'NOMBRE ACTUALIZADO'
   <NEW_DEPT>      -> ejemplo: '1'
   <NEW_SHIFT>     -> ejemplo: '1'
*/

/* ============================
   2) PREVIEW (NO CAMBIA DATOS)
   ============================ */

SELECT COUNT(1) AS OldRowsInRange
FROM TIME_ATTENDANCE
WHERE EMPLOYEE = <OLD_EMPLOYEE>
  AND CHARGE_DATE >= <FROM_YYYYMMDD>
  AND CHARGE_DATE <= <TO_YYYYMMDD>;

SELECT COUNT(1) AS NewRowsInRange
FROM TIME_ATTENDANCE
WHERE EMPLOYEE = <NEW_EMPLOYEE>
  AND CHARGE_DATE >= <FROM_YYYYMMDD>
  AND CHARGE_DATE <= <TO_YYYYMMDD>;

-- Posibles conflictos por misma fecha destino (cuando cambia EMPLOYEE)
SELECT t.CHARGE_DATE
FROM TIME_ATTENDANCE t
WHERE t.EMPLOYEE = <OLD_EMPLOYEE>
  AND t.CHARGE_DATE >= <FROM_YYYYMMDD>
  AND t.CHARGE_DATE <= <TO_YYYYMMDD>
  AND EXISTS
  (
      SELECT 1
      FROM TIME_ATTENDANCE x
      WHERE x.EMPLOYEE = <NEW_EMPLOYEE>
        AND x.CHARGE_DATE = t.CHARGE_DATE
  )
ORDER BY t.CHARGE_DATE;

/* =====================================================
   3) UPDATE CONTROLADO (APLICA CAMBIOS SOLO EN RANGO)
   =====================================================
   Regla de seguridad:
   - Si OLD != NEW, omite filas conflictivas por CHARGE_DATE.
   - Si OLD == NEW, solo actualiza NAME/DEPT/SHIFT.
*/

UPDATE TIME_ATTENDANCE t
SET
    EMPLOYEE = <NEW_EMPLOYEE>,
    NAME = <NEW_NAME>,
    DEPT = <NEW_DEPT>,
    SHIFT = <NEW_SHIFT>
WHERE t.EMPLOYEE = <OLD_EMPLOYEE>
  AND t.CHARGE_DATE >= <FROM_YYYYMMDD>
  AND t.CHARGE_DATE <= <TO_YYYYMMDD>
  AND (
        <OLD_EMPLOYEE> = <NEW_EMPLOYEE>
        OR NOT EXISTS
           (
               SELECT 1
               FROM TIME_ATTENDANCE x
               WHERE x.EMPLOYEE = <NEW_EMPLOYEE>
                 AND x.CHARGE_DATE = t.CHARGE_DATE
           )
      );

/* ============================
   4) VALIDACION POST-CAMBIO
   ============================ */

SELECT
  EMPLOYEE,
  CHARGE_DATE,
  NAME,
  DEPT,
  SHIFT,
  IN_TS_MIN,
  OUT_TS_MIN,
  DATE_IN,
  TIME_IN,
  DATE_OUT,
  TIME_OUT
FROM TIME_ATTENDANCE
WHERE EMPLOYEE = <NEW_EMPLOYEE>
  AND CHARGE_DATE >= <FROM_YYYYMMDD>
  AND CHARGE_DATE <= <TO_YYYYMMDD>
ORDER BY CHARGE_DATE;

/*
NOTA:
- Este procedimiento NO modifica IN_TS_MIN/OUT_TS_MIN ni DATE/TIME IN/OUT.
- Si detectas conflictos por CHARGE_DATE, resuélvelos manualmente por caso.
*/
