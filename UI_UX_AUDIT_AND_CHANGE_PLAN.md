# UI/UX Audit and Change Plan - Checador.Web

## 1. Diagnostico general

Checador.Web ya tiene una base funcional correcta para una web LAN interna: Razor Pages, Bootstrap, formularios server-side, tablas simples, autenticacion por cookies y consumo de `Checador.Api`. La UI actual tambien tiene una primera capa visual con cards, metricas, alertas y estados vacios.

El principal problema no es funcional, sino de percepcion: el tema oscuro/cyan se siente mas como dashboard tecnico que como sistema empresarial de RH/operacion. Para un producto vendible o instalable en cliente, conviene una apariencia mas sobria, clara, confiable y facil de leer durante uso diario.

La recomendacion es conservar la arquitectura y mejorar el sistema visual con Bootstrap + CSS propio: layout mas limpio, paleta clara empresarial, navegacion mas compacta, tablas mas legibles, formularios mas ordenados, alertas consistentes y pagina de error profesional.

## 2. Problemas encontrados

| Prioridad | Problema | Evidencia | Archivos afectados | Recomendacion |
| --- | --- | --- | --- | --- |
| Critica | El layout profesional no se esta aplicando y la navegacion aparece como links HTML sueltos | Capturas actuales muestran `CH Checador Web LAN interno`, grupos de navegacion y usuario renderizados sin estilo, pegados arriba | `Pages/Shared/_Layout.cshtml`, `wwwroot/css/site.css`, `_Layout.cshtml.css` | Mover estilos criticos del shell/layout a `site.css` y dejar de depender del CSS scoped para la estructura base |
| Critica | La estructura visual no parece sistema interno empresarial | Capturas de Inicio, KPI, Preview, Reports, Employees y Ops muestran top area sin barra profesional, contenido full-width y jerarquia dispersa | `_Layout.cshtml`, `site.css` | Crear topbar profesional fija/semi-fija, navegacion agrupada, area de usuario/rol y contenedor principal consistente |
| Alta | Tema visual demasiado oscuro y saturado en azul/cyan | `site.css` define `color-scheme: dark`, fondos radiales y gradientes intensos | `wwwroot/css/site.css`, `_Layout.cshtml.css` | Cambiar a base clara con acentos sobrios, mejor contraste y superficies blancas |
| Alta | Navegacion superior se satura con modulos admin | `_Layout.cshtml` muestra consulta y administracion en la misma barra con textos largos | `Pages/Shared/_Layout.cshtml`, `_Layout.cshtml.css` | Acortar etiquetas, mejorar agrupacion visual y mantener estado activo claro |
| Alta | Error page sigue como plantilla ASP.NET generica | `Error.cshtml` muestra texto en ingles de Development Mode | `Pages/Error.cshtml` | Reemplazar por mensaje operativo limpio con Request ID |
| Media | Cards anidadas en inicio reducen limpieza visual | `Index.cshtml` contiene `section-card` con `section-card` internas | `Pages/Index.cshtml`, `site.css` | Usar grilla de accesos rapidos sin card dentro de card |
| Media | Formularios y tablas tienen buena base, pero dependen de tema oscuro | Formularios, tablas y validaciones usan clases correctas, pero la lectura prolongada es pesada | `site.css`, vistas `.cshtml` | Crear tokens visuales reutilizables para inputs, tabla, badges, paneles y validacion |
| Media | Botones de accion no tienen una matriz visual clara | Hay mezcla de `outline-warning`, `outline-danger`, `outline-primary` | `site.css`, vistas Admin | Homologar botones por intencion: primario, secundario, riesgo, advertencia |
| Media | Estados vacios existen, pero se ven tecnicos | `empty-state-icon` usa codigos como `EMP`, `OPS`, `PVS` | `site.css`, vistas con tablas | Mantenerlos, pero hacerlos mas discretos y empresariales |
| Baja | `site.js` conserva comentario de plantilla | No rompe nada, pero da percepcion de scaffolding | `wwwroot/js/site.js` | Limpiar cuando se agregue una interaccion real |
| Baja | Algunos textos internos no usan acentos por compatibilidad historica | El repo mezcla textos sin acentos y mojibake previo en documentos | Vistas `.cshtml`, docs | Mantener ASCII en esta fase para evitar mojibake |

## 3. Cambios recomendados

### Cambio 1 - Sistema visual claro empresarial

Archivos: `src/Checador.Web/wwwroot/css/site.css`, `src/Checador.Web/Pages/Shared/_Layout.cshtml.css`

Objetivo:
- Reemplazar tema oscuro por base clara.
- Usar variables CSS para color, borde, sombra, radio, spacing y estados.
- Mantener compatibilidad con Bootstrap.
- Evitar dependencias nuevas.

Prompt sugerido:

```text
Actua como Senior UI Engineer. En Checador.Web, mejora site.css y _Layout.cshtml.css para una UI empresarial clara, sobria y moderna. Usa solo Bootstrap y CSS propio. Define tokens CSS, mejora cards, tablas, formularios, alertas, botones, badges, empty states y responsive basico. No cambies Razor handlers, rutas, autenticacion ni consumo de API.
```

### Cambio 2 - Layout y navegacion superior

Archivos: `src/Checador.Web/Pages/Shared/_Layout.cshtml`

Objetivo:
- Mantener topbar LAN simple.
- Acortar etiquetas de navegacion.
- Separar visualmente consulta/admin/cuenta.
- Agregar estado activo mas claro.
- No cambiar rutas ni autorizacion.

Prompt sugerido:

```text
Audita _Layout.cshtml de Checador.Web y mejora solamente estructura visual y copy de navegacion. Conserva asp-page, roles, feature flags, logout y scripts existentes. Agrega clases/atributos utiles para CSS y accesibilidad, sin cambiar logica de autenticacion ni rutas.
```

### Cambio 3 - Login confiable

Archivos: `src/Checador.Web/Pages/Login.cshtml`, `site.css`

Objetivo:
- Card centrada mas limpia.
- Mensajes de error claros.
- Campos alineados y ayudas discretas.
- Mantener postback y validaciones existentes.

Prompt sugerido:

```text
Mejora visualmente Login.cshtml en Razor Pages usando clases Bootstrap y CSS propio. Mantiene el mismo modelo, form method post, asp-for, validation summary y partial de validacion. Enfocate en confianza, claridad y estilo de sistema interno empresarial.
```

### Cambio 4 - Tablas y formularios operativos

Archivos: `site.css`, vistas `Preview*`, `Admin/*`, `Ops/Index.cshtml`

Objetivo:
- Mejorar headers, zebra striping, hover, badges y acciones por fila.
- Hacer filtros mas escaneables.
- Mantener tablas responsive.
- No inventar filtros ni endpoints.

Prompt sugerido:

```text
Estandariza tablas y formularios en Checador.Web usando las clases existentes table-shell, table-grid, panel-form y status/table-badge. No cambies datos ni endpoints. Mejora legibilidad, espacios, estados vacios y acciones por fila con CSS propio compatible con Bootstrap.
```

### Cambio 5 - Pagina de error profesional

Archivos: `src/Checador.Web/Pages/Error.cshtml`

Objetivo:
- Quitar texto de plantilla generica.
- Mostrar mensaje operativo simple.
- Mantener Request ID cuando exista.
- Evitar revelar informacion sensible.

Prompt sugerido:

```text
Reemplaza la vista Error.cshtml de ASP.NET por una pagina de error limpia para Checador.Web. Usa las clases visuales del sistema, conserva Model.ShowRequestId y Model.RequestId, no agregues logica nueva y no muestres detalles tecnicos sensibles.
```

## 4. Checklist final de implementacion

- [x] Crear este archivo `UI_UX_AUDIT_AND_CHANGE_PLAN.md`.
- [x] Actualizar tokens y componentes base en `site.css`.
- [x] Actualizar layout visual en `_Layout.cshtml` y `_Layout.cshtml.css`.
- [x] Mejorar login sin tocar autenticacion.
- [x] Eliminar card anidada del inicio.
- [x] Mejorar pagina de error.
- [x] Confirmar que no se agregaron frameworks ni dependencias nuevas.
- [x] Confirmar que no se modificaron rutas, `HttpClient`, `WebApi:BaseUrl` ni `CHECADOR_`.
- [x] Ejecutar `dotnet build`.
- [x] Ejecutar arranque local de Checador.Web.
- [ ] Revisar manualmente en navegador: Login, Inicio, KPI, Preview masivo, Employees, Ops y Error.

## 5. Ajuste por capturas actuales

Problemas confirmados en capturas:
- [x] La navegacion aparece como links sueltos arriba; esto indica que el estilo del layout no esta aplicado en runtime.
- [x] El header no comunica jerarquia de producto, usuario, rol y sesion.
- [x] Las paginas quedan demasiado anchas y sin un contenedor de lectura consistente.
- [x] Tablas y formularios ya tienen estilos base, pero se ven como Bootstrap plano por falta de shell profesional.
- [x] Los estados visuales existen, pero necesitan clases reutilizables mas claras.

Cambios que se implementaran ahora:
- [ ] Redisenar `_Layout.cshtml` como barra superior profesional con navegacion agrupada.
- [ ] Mover estilos criticos de `.app-shell`, `.app-header`, `.app-nav` y contenedores a `site.css`.
- [ ] Crear/normalizar clases reutilizables: `.page-header`, `.stat-card`, `.data-card`, `.status-badge`, `.table-modern`, `.form-section`.
- [ ] Mantener Razor Pages, Bootstrap, autenticacion, rutas y endpoints sin cambios.
- [ ] Compilar y reiniciar Web.

Estado de implementacion:
- [x] `_Layout.cshtml` redisenado como topbar profesional.
- [x] Navegacion agrupada por `Consulta` y `Administracion`.
- [x] Usuario, rol y acciones de sesion ordenados en el header.
- [x] Estilos criticos movidos a `site.css`.
- [x] `_Layout.cshtml.css` neutralizado para no depender de CSS isolation.
- [x] Clases reutilizables agregadas a `site.css`.

## 6. Ajuste solicitado - Preview masivo tipo revision general

Problemas detectados:
- [x] `PreviewAll` se veia como tabla basica de resumen, sin flujo de revision uno por uno.
- [x] No habia seleccion visible de empleado ni panel lateral de detalle.
- [x] El usuario necesitaba una experiencia similar al preview masivo de escritorio: tabla maestra + detalle diario.

Cambios aplicados:
- [x] `PreviewAll.cshtml` se redisenio como workspace amplio con resumen superior, filtros compactos, tabla izquierda y detalle derecho.
- [x] `PreviewAll.cshtml.cs` ahora calcula estado visual, incidencias, dias sin registro, filtros y empleado seleccionado usando el DTO existente.
- [x] `site.css` incluye estilos `preview-*` para tabla maestra, seleccion, metricas y panel de detalle.
- [x] `_Layout.cshtml` permite modo ancho por pagina con `ViewData["LayoutMode"] = "Wide"`.

Limitacion respetada:
- [x] La API actual no entrega entrada, salida, comida o marcas crudas en `ApiReportDay`; por eso el detalle diario muestra solo Fecha, Estado, Horas, Extra y Observaciones derivadas del estatus real.
