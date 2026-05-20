# UI_UX_AUDIT_AND_PROMPTS

## Resumen ejecutivo

Estado general actual:
- La app es funcional y consistente con un MVP interno, pero visualmente se percibe como plantilla Bootstrap base, no como SaaS premium de automatizacion/IA.
- La arquitectura de informacion ya cubre casos reales de operacion (Ops + Admin + consultas), pero la capa visual no acompana la complejidad funcional.
- Hay buena base de componentes semanticos (cards, tablas, formularios), pero sin sistema de diseno propio.

Nivel de profesionalismo visual actual:
- Medio-bajo para producto SaaS empresarial externo.
- Medio para herramienta interna temporal.

Principales problemas detectados:
1. Estilo visual generico (Bootstrap default) sin identidad de marca.
2. Navegacion superior saturada para modulo admin con demasiadas opciones al mismo nivel.
3. Jerarquia visual insuficiente en pantallas densas (Ops, Pervasive, Reports, Reassign).
4. Estados de carga, vacios y error inconsistentes o inexistentes en varias vistas.
5. Responsive y accesibilidad no tratados como sistema (se apoyan en defaults).
6. Copys con mojibake visible en varias vistas/mensajes (ejemplo: `OperaciÃ³n`, `SÃ­`).

Riesgos visuales/UX:
- Escalando funcionalidades, el navbar actual se volvera inmanejable.
- La friccion cognitiva en modulos admin puede provocar errores operativos.
- Sin design tokens, cada ajuste visual futuro aumenta inconsistencia tecnica.

Referencias visuales obligatorias:
- Logo azul marino/cyan/azul electrico: [No verificado] no se encontro imagen adjunta en esta sesion.
- Inspiracion Isorbit (`https://isorbit.com/`): aplicada como direccion de estilo propuesta, no como clon.

---

## Auditoria Fase 1

### 1) Arquitectura visual

Hallazgos:
- Layout unico con `navbar` superior y `container` central en todas las paginas (`Pages/Shared/_Layout.cshtml`).
- No hay separacion visual fuerte entre zonas funcionales (consulta vs admin vs operaciones).
- Navegacion admin se agrega al mismo nivel que navegacion de consulta, saturando la barra superior.
- Pantallas complejas dependen de bloques apilados de cards/tablas sin patron de densidad uniforme.

Impacto:
- Reduce escaneabilidad y priorizacion de acciones.
- Penaliza usabilidad en desktop medio y tablet.

### 2) UI actual

- Paleta: default Bootstrap (azul primario generico), sin tokens de marca.
- Tipografia: default Bootstrap/system; no hay escala tipografica de producto.
- Botones: mezcla de `btn-primary`, `btn-outline-*`, sin criterio semantico estable.
- Cards: presentes, pero sin sistema de encabezados/espaciado uniforme entre modulos.
- Formularios: funcionales, pero largos en admin sin agrupacion avanzada ni ayudas contextuales estables.
- Tablas: muchas `table table-sm table-striped`; no hay sticky headers, estados de orden o densidad configurable.
- Navbar/Sidebar: no hay sidebar; navbar crece en items admin y pierde legibilidad.
- Modales: no se detectaron modales en Web.
- Estados vacios: parciales (algunas tablas muestran `Sin registros`, otras solo alerta generica).
- Alertas: presentes, pero con mensajes largos y estilo uniforme insuficiente por severidad/contexto.
- Carga: no hay skeletons, spinners ni estados `loading` explicitos.
- Login/Dashboard/Admin: funcionales, pero visualmente basicos.

### 3) UX actual

- Flujo principal de consulta es simple (login -> index -> KPI/previews).
- Flujo admin contiene pasos correctos, pero con alta densidad de campos y confirmaciones textuales sin ayudas progresivas.
- Mensajes de error: en varios casos llegan como texto plano tecnico o generico.
- Feedback visual: correcto en exito/error basico, limitado en operaciones largas.
- Validaciones: presentes por DataAnnotations y server postback, pero sin feedback en tiempo real consistente.
- Friccion innecesaria: varias vistas cargan tablas completas sin mecanismos de enfoque jerarquico (filtros avanzados, resumen superior persistente).

### 4) Responsive

- Estructura basada en grid Bootstrap (`col-md-*`) ayuda en desktop/tablet.
- Riesgo en mobile: navbar con muchos items + formularios administrativos anchos + tablas densas.
- `Ops/Index`, `Admin/Pervasive`, `Admin/Reports`, `Admin/Reassign` son candidatas fuertes a romper escaneabilidad en pantallas pequenas.
- [No verificado] comportamiento real en viewport movil con pruebas visuales lado cliente.

### 5) Accesibilidad

- Positivo: uso general de `label asp-for`, controles semanticos, tablas con `thead`.
- Debilidades:
  - Contraste y focus dependen de defaults (`site.css` basico).
  - No hay patron claro para `focus-visible` de alto contraste de marca.
  - No hay estrategia de `aria-live` para resultados dinamicos de operaciones.
  - Mensajes de error no estandarizados para lectura rapida.
- [No verificado] navegacion completa solo teclado en todas las vistas complejas.

### 6) Sistema de diseno

- No existen tokens explicitos de color/spacing/radius/shadow.
- `site.css` y `_Layout.cshtml.css` son mayormente plantilla default.
- Inconsistencia en estilos de acciones (`outline-warning`, `outline-danger`, `outline-primary`) sin matriz semantica.
- Componentes repetidos manualmente (cards de resumen, tablas admin) sin componentes compartidos UI.

---

## Diagnostico por pantalla

### Login (`Pages/Login.cshtml`)
- Problemas UI: tarjeta y jerarquia visual basica, sin identidad premium.
- Problemas UX: falta ayuda contextual (entorno, rol, recuperacion/soporte).
- Prioridad: Alta.
- Recomendacion: layout centrado premium, branding claro, microcopy operativo y estados de error amigables.

### Index (`Pages/Index.cshtml`)
- Problemas UI: cards visualmente planas, poca diferenciacion de prioridad.
- Problemas UX: no hay CTA principal dominante ni estado rapido del sistema.
- Prioridad: Alta.
- Recomendacion: hero + resumen de estado + tarjetas con iconografia y prioridad.

### KPI (`Pages/Kpi.cshtml`)
- Problemas UI: tarjetas compactas sin jerarquia numerica premium.
- Problemas UX: sin filtros avanzados ni comparativos temporales.
- Prioridad: Media.
- Recomendacion: grid de metricas con codigos de estado y trend indicators.

### Preview por empleado (`Pages/PreviewEmployee.cshtml`)
- Problemas UI: tabla generica sin indicadores de estatus.
- Problemas UX: poca ayuda para buscar empleado y leer anomalias.
- Prioridad: Media.
- Recomendacion: chips de estatus, resumen previo, tabla con columnas priorizadas.

### Preview masivo (`Pages/PreviewAll.cshtml`)
- Problemas UI: lista tabular plana.
- Problemas UX: sin buscador/orden/filtro rapido.
- Prioridad: Media.
- Recomendacion: filtros, busqueda, orden y acciones contextuales.

### ChangePassword (`Pages/ChangePassword.cshtml`)
- Problemas UI: correcto pero muy basico.
- Problemas UX: no muestra reglas de password de forma preventiva.
- Prioridad: Media.
- Recomendacion: checklist de requisitos y feedback inline.

### Ops (`Pages/Ops/Index.cshtml`)
- Problemas UI: alta densidad de cards + formularios + tabla en una sola vista.
- Problemas UX: prioridad de acciones no clara; riesgo de operacion accidental.
- Prioridad: Alta.
- Recomendacion: separar monitorizacion vs acciones criticas, bloques colapsables y jerarquia por severidad.

### Admin Employees (`Pages/Admin/Employees.cshtml`)
- Problemas UI: toolbar de acciones extensa y uniforme.
- Problemas UX: exceso de caminos en una sola cabecera.
- Prioridad: Alta.
- Recomendacion: convertir toolbar en navegacion secundaria (tabs/segmented nav).

### Admin EmployeeEdit (`Pages/Admin/EmployeeEdit.cshtml`)
- Problemas UI: formulario correcto pero sin agrupacion visual avanzada.
- Problemas UX: accion destructiva muy cercana al formulario principal.
- Prioridad: Alta.
- Recomendacion: seccion destructiva aislada y doble confirmacion contextual.

### Admin Reassign (`Pages/Admin/Reassign.cshtml`)
- Problemas UI: formulario largo con muchos campos en bloque.
- Problemas UX: alto riesgo de error por carga cognitiva.
- Prioridad: Alta.
- Recomendacion: wizard por pasos (Parametros -> Preview -> Confirmacion -> Aplicar).

### Admin Pervasive (`Pages/Admin/Pervasive.cshtml`)
- Problemas UI: tabla extremadamente densa.
- Problemas UX: lectura de resultados operativos compleja.
- Prioridad: Alta.
- Recomendacion: resumen fijo superior, filtros por accion y tabla avanzada.

### Admin ManualPunch (`Pages/Admin/ManualPunch.cshtml`)
- Problemas UI: correcta base, faltan diferenciadores visuales entre modo puntual y lote.
- Problemas UX: formato batch puede inducir errores si no hay validacion anticipada.
- Prioridad: Media.
- Recomendacion: editor asistido con validacion de linea por linea y preview parseado.

### Admin Reports (`Pages/Admin/Reports.cshtml`)
- Problemas UI: dos formularios grandes consecutivos sin separacion fuerte de contexto.
- Problemas UX: ambiguedad entre reporte normal y formato OT.
- Prioridad: Media.
- Recomendacion: tabs o acordeon por tipo de reporte.

### Admin Users (`Pages/Admin/Users.cshtml`)
- Problemas UI: tabla funcional pero plana.
- Problemas UX: no hay filtros por rol/estado.
- Prioridad: Baja.
- Recomendacion: filtros rapidos y badges de estado.

### Error y Privacy (`Pages/Error.cshtml`, `Pages/Privacy.cshtml`)
- Problemas UI/UX: paginas template en ingles, no alineadas al producto.
- Prioridad: Media.
- Recomendacion: redaccion y estilo consistentes con producto interno.

---

## Diagnostico por componente

### Navbar principal
- Problema: saturacion de items y mezcla de contextos.
- Mejora: dividir en navegacion primaria + secundaria por modulo.
- Estilo esperado: barra slim dark navy con acentos cyan, active state claro, tipografia semibold.

### Cards de metricas/estado
- Problema: aspecto generico y baja jerarquia.
- Mejora: tokens de elevation, borde suave, icono/estado.
- Estilo esperado: surface dark premium, glow cyan moderado, numeros grandes.

### Formularios
- Problema: densidad alta y feedback limitado.
- Mejora: secciones, ayudas, validacion inline consistente.
- Estilo esperado: inputs con fondo contrastado, focus ring cyan electrico.

### Tablas
- Problema: densas, sin herramientas de exploracion.
- Mejora: sticky headers, filtros, columnas priorizadas.
- Estilo esperado: tabla empresarial minimal, zebra suave, hover contextual.

### Alertas y mensajes
- Problema: lenguaje tecnico o ambiguo en algunos flujos.
- Mejora: catalogo de mensajes por severidad y accion recomendada.
- Estilo esperado: alertas compactas con icono y CTA secundario.

### Estados vacios y carga
- Problema: cobertura parcial.
- Mejora: empty states por pantalla y loaders no intrusivos.
- Estilo esperado: mensajes cortos orientados a accion + ilustracion simple geométrica.

### Botones/acciones criticas
- Problema: semantica visual inconsistente.
- Mejora: matriz de variantes (`primary`, `secondary`, `danger`, `ghost`).
- Estilo esperado: rounded premium, transiciones suaves, contraste fuerte.

---

## Sistema visual recomendado

### Paleta (inspirada en logo + Isorbit)

Base:
- `--bg-900`: `#071426` (navy profundo)
- `--bg-800`: `#0D1E36`
- `--bg-700`: `#122845`
- `--surface-600`: `#182F52`
- `--surface-500`: `#1F3962`

Acentos:
- `--accent-cyan-500`: `#13D6FF`
- `--accent-blue-500`: `#2388FF`
- `--accent-gradient`: `linear-gradient(135deg, #13D6FF 0%, #2388FF 100%)`

Neutros:
- `--text-100`: `#F4F8FF`
- `--text-200`: `#D6E2F5`
- `--text-300`: `#9CB0CC`
- `--border-400`: `#2A4570`

Estados:
- `--success`: `#19C37D`
- `--warning`: `#F3B23C`
- `--danger`: `#F45D6A`

### Tipografia
- Sugerida: `"Plus Jakarta Sans", "Inter", "Segoe UI", sans-serif`
- Escala:
  - H1: 32/38 semibold
  - H2: 24/30 semibold
  - H3: 20/26 semibold
  - Body: 14-16 regular
  - Caption: 12-13 medium

### Espaciado
- Base 8px (`4, 8, 12, 16, 24, 32, 40, 48`).
- Formularios largos: bloques de 24px.

### Border radius
- Controles: 10px
- Cards: 14px
- Dialogos/paneles destacados: 18px

### Sombras
- `shadow-sm`: `0 4px 16px rgba(0,0,0,.18)`
- `shadow-md`: `0 10px 30px rgba(0,0,0,.28)`
- Glow acento: `0 0 0 1px rgba(19,214,255,.35), 0 0 24px rgba(19,214,255,.12)`

### Hover / focus
- Hover botones/cards: elevar 1-2px + brillo cyan leve.
- Focus visible: anillo `2px` cyan con contraste AA.

### Estilos por tipo
- Cards: fondo `surface`, borde `border-400`, titulo semibold, metrica prominente.
- Botones: primario con gradiente cyan-blue; secundarios `ghost` con borde.
- Tablas: encabezado fijo oscuro, filas zebra suaves, badges por estado.
- Formularios: labels 12-13 medium, ayudas bajo campo, errores en linea.
- Navegacion: topbar compacta + sidebar/tabs para admin.

---

## Roadmap de rediseño

### Fase 1: Base visual y tokens
- Definir variables CSS globales y tipografia.
- Estandarizar colores/espaciado/radius/shadows.

### Fase 2: Layout principal
- Redefinir header.
- Introducir navegacion modular (admin/consulta/ops).
- Mejorar jerarquia de pagina.

### Fase 3: Componentes globales
- Botones, badges, alertas, cards, tablas, formularios.
- Empty/loading/error states reutilizables.

### Fase 4: Pantallas principales
- Login, Index, KPI, PreviewEmployee, PreviewAll, Ops.
- Luego Admin (Employees, Reassign, Pervasive, Reports, ManualPunch, Users).

### Fase 5: Responsive y accesibilidad
- Breakpoints reales desktop/tablet/mobile.
- Navegacion compacta, tablas adaptativas, teclado y focus.

### Fase 6: Pulido premium
- Microinteracciones, consistencia final y QA visual.
- Correccion final de copy/mojibake.

---

## Prompts accionables para Codex

### PROMPT 1
**Objetivo**
Crear sistema de diseno y tokens visuales basados en logo azul/cyan y referencia Isorbit.

**Archivos probables a tocar**
- `src/Checador.Web/wwwroot/css/site.css`
- `src/Checador.Web/Pages/Shared/_Layout.cshtml.css`
- `src/Checador.Web/Pages/Shared/_Layout.cshtml`

**Instrucciones**
- Definir variables CSS globales (color, spacing, radius, shadow, typography).
- Aplicar tipografia consistente en toda la app.
- Mantener compatibilidad Bootstrap sin romper clases existentes.

**Restricciones**
- No cambiar logica C# ni endpoints.
- No romper rutas ni forms.

**Resultado esperado**
Base visual coherente SaaS premium lista para reutilizar.

### PROMPT 2
**Objetivo**
Redisenar layout principal, navbar/header y estructura tipo SaaS empresarial.

**Archivos probables a tocar**
- `Pages/Shared/_Layout.cshtml`
- `Pages/Shared/_Layout.cshtml.css`
- `wwwroot/css/site.css`

**Instrucciones**
- Separar navegacion principal y admin.
- Mejorar legibilidad en desktop y colapso en mobile.
- Introducir estado activo claro por ruta.

**Restricciones**
- No eliminar links funcionales.
- Mantener feature flags existentes.

**Resultado esperado**
Navegacion escalable y limpia, menos saturacion cognitiva.

### PROMPT 3
**Objetivo**
Redisenar dashboard principal con cards/metricas/jerarquia premium dark mode.

**Archivos probables a tocar**
- `Pages/Index.cshtml`
- `Pages/Kpi.cshtml`
- `Pages/Ops/Index.cshtml`
- CSS compartido

**Instrucciones**
- Crear hero/summary y cards de metricas con iconos y estado.
- Mejorar composicion visual de Ops por secciones.

**Restricciones**
- No tocar llamadas API ni handlers.

**Resultado esperado**
Dashboard legible, moderno y orientado a decisiones.

### PROMPT 4
**Objetivo**
Redisenar formularios, inputs, selects, validaciones y errores.

**Archivos probables a tocar**
- `Pages/Login.cshtml`
- `Pages/ChangePassword.cshtml`
- `Pages/Admin/*.cshtml`
- CSS global de formularios

**Instrucciones**
- Estandarizar labels/help/error states.
- Mejorar formularios largos con agrupacion visual.

**Restricciones**
- Mantener `asp-for`, validaciones y postbacks.

**Resultado esperado**
Formularios con menos friccion y mejor comprension.

### PROMPT 5
**Objetivo**
Redisenar tablas, filtros, busquedas, estados vacios y acciones.

**Archivos probables a tocar**
- `Pages/PreviewEmployee.cshtml`
- `Pages/PreviewAll.cshtml`
- `Pages/Admin/Employees.cshtml`
- `Pages/Admin/Pervasive.cshtml`
- `Pages/Admin/Users.cshtml`
- `Pages/Ops/Index.cshtml`

**Instrucciones**
- Crear toolbar de filtros/acciones por tabla.
- Aplicar estilo uniforme de encabezados/filas/badges.
- Agregar empty states claros.

**Restricciones**
- No alterar datos ni orden backend salvo query params existentes.

**Resultado esperado**
Tablas mas navegables y operables en escenarios reales.

### PROMPT 6
**Objetivo**
Mejorar responsive desktop/tablet/movil.

**Archivos probables a tocar**
- Layout + CSS global + vistas admin densas

**Instrucciones**
- Definir breakpoints concretos.
- Resolver overflow en navbar/tablas/formularios.
- Asegurar accion primaria visible en mobile.

**Restricciones**
- No eliminar funcionalidad por viewport.

**Resultado esperado**
Experiencia consistente en tres tamanos de pantalla.

### PROMPT 7
**Objetivo**
Mejorar accesibilidad (contraste, focus, teclado, labels).

**Archivos probables a tocar**
- CSS global
- Layout
- Formularios y alertas en paginas clave

**Instrucciones**
- Subir contraste a nivel AA.
- Implementar `:focus-visible` consistente.
- Revisar orden tab y mensajes de error.

**Restricciones**
- No cambiar reglas de negocio.

**Resultado esperado**
UI usable con teclado y mejor lectura para todos los usuarios.

### PROMPT 8
**Objetivo**
Agregar microinteracciones sutiles, hover, transiciones, sombras y glow cyan moderado.

**Archivos probables a tocar**
- CSS global
- Layout
- Cards/botones/tablas

**Instrucciones**
- Aplicar animaciones suaves (150-220ms).
- Evitar exceso de motion; mantener performance.

**Restricciones**
- Sin animaciones que bloqueen flujo operativo.

**Resultado esperado**
Percepcion premium moderna sin perder sobriedad empresarial.

### PROMPT 9
**Objetivo**
Pulido final de consistencia visual y revision completa sin romper funcionalidades.

**Archivos probables a tocar**
- Todos los .cshtml y CSS tocados en fases previas

**Instrucciones**
- Checklist final de consistencia (colores, spacing, copy, estados).
- Corregir textos con mojibake.
- Validar que roles/rutas/forms sigan funcionando.

**Restricciones**
- No introducir cambios backend ni contratos API.

**Resultado esperado**
Entrega final visual coherente, lista para uso interno profesional.

---

## Pendientes por validar

1. Logo adjunto real (archivo de imagen) para calibrar exacto de gradiente/forma/iconografia. [No verificado]
2. Capturas en navegador de cada pantalla para validar contraste real y densidad visual final. [No verificado]
3. Prueba responsive real en dispositivos/tablet (no solo inferencia por markup). [No verificado]
4. Recorrido completo solo teclado en todas las pantallas admin/ops. [No verificado]
5. Definir si el rediseno objetivo final sera dark-only o dark+light theme toggle.
