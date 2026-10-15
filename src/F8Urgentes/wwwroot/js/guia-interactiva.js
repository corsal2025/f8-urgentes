/* Guía interactiva "Cómo usar el sistema" (F8 Urgentes): spotlight + Lico.
 * Port de docs/tematica-lico/guia-interactiva.js (kit "Lico") con la tarjeta de la Agenda de Prácticos
 * (indicador de sección y barra de progreso). Los pasos (PASOS_TOUR) son los de las páginas de F8.
 *
 * - Botón #btn-guia-global (cabecera, _Layout.cshtml): abre la guía en el primer paso de la página actual.
 * - Recorre varias páginas: un paso de otra página navega a ella con ?guia=1&paso=N y la guía sigue allá.
 * - Teclas: Esc cierra, flechas navegan.
 * - Reutiliza window.Lico (lico.js); solo define una copia propia si falta.
 * - Mientras está abierta existe #tour-overlay, y lico-juegos.js no hace travesuras.
 * API: iniciarGuiaInteractiva(paso), cerrarGuiaInteractiva().
 */
(function () {
  'use strict';

  if (window.iniciarGuiaInteractiva) return; // cargado dos veces: no duplicar eventos

  // Ruta (en minúsculas) -> URL a la que navega la guía y nombre de la sección.
  var PAGINAS = {
    '/index':            { url: '/Index',                   seccion: 'Solicitudes' },
    '/edit':             { url: '/Edit',                    seccion: 'Nueva solicitud' },
    '/caja':             { url: '/Caja',                    seccion: 'Caja' },
    '/carpetasextra':    { url: '/CarpetasExtra',           seccion: 'Carpetas Extra' },
    '/sincarpetas':      { url: '/SinCarpetas',             seccion: 'Sin Carpetas' },
    '/sectorf8':         { url: '/SectorF8?sector=Archivo', seccion: 'Colas PDF' },
    '/estadisticas':     { url: '/Estadisticas',            seccion: 'Estadísticas' },
    '/impresionmensual': { url: '/ImpresionMensual',        seccion: 'Impresión mensual' }
  };

  // pagina: clave de PAGINAS. target / fallbackTarget: selectores (el fallback se usa si el target no existe,
  // por ejemplo cuando la tabla no tiene filas). pose de Lico: saluda | explica (por defecto) | alerta | celebra.
  var PASOS_TOUR = [
    // ---------- SOLICITUDES: cabecera ----------
    {
      pagina: '/index', pose: 'saluda',
      target: '#btn-guia-global',
      titulo: 'Hola, soy Lico: tu guía',
      icono: '👋',
      descripcion: 'Este botón abre la guía en cualquier pantalla, empezando por los pasos de la página donde estás. Recorre <b>Solicitudes</b>, <b>Nueva solicitud</b>, <b>Caja</b>, <b>Carpetas Extra</b>, <b>Sin Carpetas</b>, las <b>colas PDF</b>, <b>Estadísticas</b> e <b>Impresión mensual</b>; al pasar a otra pantalla, la guía continúa allá. Usa <b>Siguiente / Anterior</b> o las flechas del teclado; <b>Esc</b> la cierra.'
    },
    {
      pagina: '/index',
      target: '.app-nav',
      titulo: 'Las secciones del sistema',
      icono: '🧭',
      descripcion: '<b>Solicitudes</b>: el listado diario de casos. <b>Nueva solicitud</b>: formulario para ingresar un caso. <b>Caja</b>: cola y cajas físicas de carpetas. <b>Carpetas Extra</b>: carpetas fuera del flujo de la matriz. <b>Sin Carpetas</b>: casos cerrados sin carpeta. La sección abierta se subraya en azul.'
    },
    {
      pagina: '/index',
      target: '.header-search',
      titulo: 'Buscador',
      icono: '🔍',
      descripcion: 'Busca en Solicitudes desde cualquier pantalla: escribe el <b>RUT completo</b> (con o sin puntos y guion) o <b>parte del nombre</b> y pulsa <b>Enter</b>. La <b>✕</b> quita la búsqueda y vuelve al listado completo.'
    },
    {
      pagina: '/index',
      target: '.header-sync-btn',
      titulo: 'Sincronizar con la matriz',
      icono: '🔄',
      descripcion: 'Lee la <b>matriz de distribución de carpetas</b> e importa a Solicitudes los casos marcados <b>«NO EXISTE CARPETA»</b> (o «NO HAY CARPETA») que aún no estén cargados. Al terminar, un aviso verde sobre la tabla resume cuántos casos se importaron y si hay alertas para revisar.'
    },
    {
      pagina: '/index',
      target: '#head-lico',
      fallbackTarget: '#lico-onoff',
      titulo: 'Consejos de Lico',
      icono: '💡',
      descripcion: 'Pulsa mi carita y te doy un <b>consejo</b> de la pantalla donde estás (y hago alguna gracia). Si pasas un rato sin tocar nada, hago una travesura; cualquier movimiento me espanta. El botón <b>☺</b> de al lado apaga mis animaciones y travesuras; la guía sigue disponible igual.'
    },

    // ---------- SOLICITUDES: filtros y accesos ----------
    {
      pagina: '/index',
      target: '.filter-tabs-group',
      titulo: 'Pestañas de filtro',
      icono: '🗂️',
      descripcion: '<b>Todos</b>; <b>Pendientes</b> (aún no subidos a CONASET); <b>Subidas</b> (SUBIDA A CONASET); <b>Solicitud de certificado</b> (estado CREAR CERTIFICADO); <b>Penúltimas carpetas</b> (con fecha de penúltima carpeta); <b>Sin agregar</b> (ingresados con el botón «Solicitudes sin agregar») y <b>Requiere revisión</b> (casos con datos por revisar). El número de la pestaña es cuántos casos tiene.'
    },
    {
      pagina: '/index',
      target: '.tools-group',
      titulo: 'Accesos rápidos',
      icono: '⚡',
      descripcion: '<b>Archivo</b> y <b>Oficina 43</b> abren la cola de impresión PDF de cada sector. <b>Estadísticas</b> muestra indicadores y gráficos. <b>Impresión mensual</b> arma el listado de carpetas subidas en el mes.'
    },
    {
      pagina: '/index', pose: 'alerta',
      target: '.alert-banner',
      fallbackTarget: '.dashboard-controls',
      titulo: 'Avisos',
      icono: '⚠️',
      descripcion: 'Aquí, sobre la tabla, aparecen los avisos: el recordatorio de <b>imprimir las penúltimas carpetas</b> del mes anterior (los primeros 10 días del mes, mientras falte imprimirlas), el resultado de <b>Sincronizar</b> y, en rojo, los <b>RUT duplicados</b> (más de un caso con el mismo RUT).'
    },
    {
      pagina: '/index',
      target: '#open-quick-add-btn',
      fallbackTarget: '.controls-row-actions',
      titulo: 'Solicitudes sin agregar',
      icono: '➕',
      descripcion: 'Abre un formulario para ingresar <b>uno o varios casos</b> de una vez: <b>Nombre completo</b> y <b>RUT</b> (obligatorios) y la fecha de <b>penúltima carpeta</b> si la tienes. «+ Agregar otro» suma filas. Quedan con fecha de citación de hoy y en la pestaña <b>Sin agregar</b>. A la izquierda verás cuántos casos muestra la lista.'
    },

    // ---------- SOLICITUDES: tabla ----------
    {
      pagina: '/index',
      target: 'table.cases-list thead',
      fallbackTarget: 'table.cases-list',
      titulo: 'Columnas de la tabla',
      icono: '📋',
      descripcion: '<b>Nombre</b> y <b>RUT</b> quedan fijos a la izquierda al desplazar la tabla. Luego vienen <b>Marcar</b>, <b>Pendiente carpeta</b>, <b>Fecha citación</b>, <b>Sector oficina</b> (de la matriz), <b>Estado actual</b>, <b>Código F8</b>, <b>Fecha penúltima carpeta</b>, <b>Sector carpeta</b>, <b>Fecha subida F8</b> y las acciones. Los casos ya subidos van al final.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody td.name-cell',
      fallbackTarget: 'table.cases-list thead th:nth-child(1)',
      titulo: 'Edición directa en la celda',
      icono: '✏️',
      descripcion: '<b>Nombre</b>, <b>RUT</b> y <b>Código F8</b> se corrigen escribiendo en la misma celda: se guardan al salir de ella o al pulsar <b>Enter</b>. Al copiar un RUT de la tabla se copia sin puntos.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody .marked-checkbox',
      fallbackTarget: 'table.cases-list thead th:nth-child(3)',
      titulo: 'Marcar',
      icono: '✔️',
      descripcion: 'Incluye el caso en la <b>próxima impresión de su sector</b> (cola PDF Archivo u Oficina 43). La fila se pinta de <b>amarillo</b>. Se guarda al instante y se excluye con «Pendiente carpeta»: marcar una desmarca la otra.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody .pendiente-checkbox',
      fallbackTarget: 'table.cases-list thead th:nth-child(4)',
      titulo: 'Pendiente carpeta',
      icono: '📁',
      descripcion: 'Indica que la carpeta física aún está pendiente. La fila se pinta de <b>morado</b> y el caso también entra en la cola de impresión de su sector. Al pulsar <b>Subir</b> se quitan ambas marcas.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody .estado-actual-select',
      fallbackTarget: 'table.cases-list thead th:nth-child(7)',
      titulo: 'Estado actual',
      icono: '🏷️',
      descripcion: 'Muestra el estado del caso y permite cambiarlo a <b>CREAR CERTIFICADO</b> cuando no se encuentra la carpeta; entonces aparece el botón <b>✉</b> en Acción para pedir el certificado a Matías por correo. Si el caso está <b>SUBIDA A CONASET</b>, el selector queda bloqueado.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody .fecha-penultima-input',
      fallbackTarget: 'table.cases-list thead th:nth-child(9)',
      titulo: 'Fecha penúltima carpeta y sector',
      icono: '📅',
      descripcion: 'Se guarda al elegir la fecha y define el <b>Sector carpeta</b>: antes de <b>junio de 2023</b> va a <b>Archivo</b>; desde junio de 2023, a <b>Oficina 43</b>; sin fecha queda <b>S/C</b>. Pulsa el encabezado de esta columna para ordenar por fecha (▲ / ▼) y recorrer las carpetas de un mes seguidas.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody button[title="Marcar subida"]',
      fallbackTarget: 'table.cases-list thead th:last-child',
      titulo: 'Subir',
      icono: '⬆️',
      descripcion: 'Deja el caso en <b>SUBIDA A CONASET</b> con la fecha de hoy en «Fecha subida F8» y le quita las marcas de impresión. El caso pasa al final de la lista y a la pestaña <b>Subidas</b>.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody .btn-folder-none, table.cases-list tbody .btn-folder-revert',
      fallbackTarget: 'table.cases-list thead th:last-child',
      titulo: 'Sin carpeta y Caja',
      icono: '📦',
      descripcion: '<b>Sin carpeta</b> cierra el caso sin carpeta: la fila queda gris y el caso aparece también en <b>Sin Carpetas</b>; <b>Revertir</b> lo deshace. <b>Caja</b> pide confirmación y envía el caso a la cola de <b>Caja</b>: sale de esta lista y vuelve si lo quitas de la cola.'
    },
    {
      pagina: '/index', pose: 'alerta',
      target: 'table.cases-list tbody td.action-cell .btn-danger',
      fallbackTarget: 'table.cases-list thead th:last-child',
      titulo: 'Eliminar una solicitud',
      icono: '🗑️',
      descripcion: 'El basurero borra la solicitud del sistema, previa confirmación. Úsalo solo para casos ingresados por error.'
    },
    {
      pagina: '/index',
      target: 'table.cases-list tbody',
      fallbackTarget: 'table.cases-list',
      titulo: 'Colores de las filas',
      icono: '🎨',
      descripcion: '<b>Amarillo</b>: marcado para imprimir. <b>Morado</b>: pendiente de carpeta. <b>Gris</b>: subida a CONASET o sin carpeta. <b>Rosado</b>: requiere revisión. <b>Rojo</b>: estado DENEGADA.'
    },

    // ---------- NUEVA SOLICITUD ----------
    {
      pagina: '/edit', pose: 'saluda',
      target: 'main .card',
      fallbackTarget: 'main',
      titulo: 'Nueva solicitud',
      icono: '📝',
      descripcion: 'Formulario para ingresar un caso a mano: <b>Nombre completo</b>, <b>RUT</b>, <b>Fecha petición</b>, <b>Estado</b>, <b>Estado actual</b>, <b>Fecha de subida</b> y <b>Código F8</b>. Nombre completo y RUT son obligatorios.'
    },
    {
      pagina: '/edit',
      target: '#Input_Rut',
      fallbackTarget: 'main .card',
      titulo: 'RUT validado',
      icono: '🪪',
      descripcion: 'El RUT se ordena con puntos y guion mientras escribes. Al guardar se revisa el <b>dígito verificador</b>: si no corresponde, el formulario avisa «RUT inválido» y no guarda.'
    },
    {
      pagina: '/edit',
      target: '#Input_Estado',
      fallbackTarget: 'main .card',
      titulo: 'Estado y Estado actual',
      icono: '🏷️',
      descripcion: 'Ambas listas ofrecen solo los estados conocidos del sistema. Puedes dejarlas en «-- seleccione --» si aún no corresponde.'
    },
    {
      pagina: '/edit',
      target: 'main form button[type="submit"]',
      fallbackTarget: 'main .card',
      titulo: 'Guardar',
      icono: '💾',
      descripcion: 'Guarda la solicitud y vuelve a <b>Solicitudes</b>, donde ya aparece en la lista.'
    },

    // ---------- CAJA ----------
    {
      pagina: '/caja', pose: 'saluda',
      target: '.caja-section h2',
      fallbackTarget: 'main',
      titulo: 'Cola de la próxima caja',
      icono: '📦',
      descripcion: 'Aquí esperan los casos enviados con el botón <b>Caja</b> desde Solicitudes, Carpetas Extra o Sin Carpetas, en el orden en que llegaron. El número indica cuántos hay en cola.'
    },
    {
      pagina: '/caja',
      target: '.queue-table',
      fallbackTarget: '.caja-section .table-card',
      titulo: 'Casos en la cola',
      icono: '📋',
      descripcion: 'Muestra N° de orden (según llegaron a la cola), nombre, RUT, Código F8, fecha de penúltima carpeta y sector. <b>Quitar</b> devuelve el caso a Solicitudes (pide confirmación). Con las casillas puedes elegir varios y usar <b>Quitar seleccionados</b>.'
    },
    {
      pagina: '/caja',
      target: 'div:has(> #boxNumberInput)',
      fallbackTarget: '#boxNumberInput',
      titulo: 'Rótulo de la caja',
      icono: '🔢',
      descripcion: 'Número de la caja que vas a cerrar; el sistema sugiere el siguiente. El código queda como <b>A{número}-PUC</b>, por ejemplo A1-PUC.'
    },
    {
      pagina: '/caja',
      target: '#btnCerrarCaja',
      fallbackTarget: '.caja-section',
      titulo: 'Cerrar caja',
      icono: '🔒',
      descripcion: 'Pide confirmación y guarda <b>todos los casos de la cola</b> en la caja con ese rótulo. Luego abre su <b>acta</b>: listado de carpetas con firmas de entrega y recepción, lista para imprimir o guardar como PDF. Está desactivado si la cola está vacía.'
    },
    {
      pagina: '/caja',
      target: '.boxes-history',
      fallbackTarget: '.caja-section ~ .caja-section',
      titulo: 'Cajas cerradas',
      icono: '🗃️',
      descripcion: 'Historial de cajas con su fecha de cierre y cantidad de casos. <b>Ver listado / imprimir</b> abre el acta de la caja. <b>Reabrir</b> devuelve sus casos a la cola para quitar, agregar o corregir antes de cerrarla de nuevo.'
    },

    // ---------- CARPETAS EXTRA ----------
    {
      pagina: '/carpetasextra', pose: 'saluda',
      target: 'main .dashboard-controls',
      fallbackTarget: 'main',
      titulo: 'Carpetas extras y penúltimas antiguas',
      icono: '📂',
      descripcion: 'Para carpetas que no vienen de la matriz: penúltimas carpetas antiguas o pedidos esporádicos. Se guardan con origen «Extra» y también aparecen en <b>Solicitudes</b> y <b>Estadísticas</b>. A la derecha, cuántas hay registradas.'
    },
    {
      pagina: '/carpetasextra',
      target: 'main .card form',
      fallbackTarget: 'main',
      titulo: 'Registrar una carpeta extra',
      icono: '✍️',
      descripcion: '<b>Nombre completo</b> y <b>RUT</b> son obligatorios (el RUT debe tener dígito verificador válido). Agrega Código F8, fecha de última carpeta, Estado y Estado actual si los tienes; si un estado no se reconoce, el caso queda en <b>Requiere revisión</b>. Pulsa <b>+ Guardar Carpeta Extra</b>.'
    },
    {
      pagina: '/carpetasextra',
      target: 'main .table-card table',
      fallbackTarget: 'main .table-card',
      titulo: 'Listado de carpetas extra',
      icono: '📋',
      descripcion: '<b>Caja</b> envía la carpeta a la cola de Caja (pide confirmación y sale de esta lista). El basurero la elimina, previa confirmación. Una fila rosada indica que requiere revisión.'
    },

    // ---------- SIN CARPETAS ----------
    {
      pagina: '/sincarpetas', pose: 'saluda',
      target: 'main .dashboard-controls',
      fallbackTarget: 'main',
      titulo: 'Módulo Sin Carpetas',
      icono: '📁',
      descripcion: 'Reúne los casos marcados con <b>Sin carpeta</b> en Solicitudes, primero los ingresados más recientemente. A la derecha, cuántas personas hay sin carpeta.'
    },
    {
      pagina: '/sincarpetas',
      target: 'main .table-card tbody .action-group',
      fallbackTarget: 'main .table-card table',
      titulo: 'Acciones sobre cada caso',
      icono: '↩️',
      descripcion: '<b>Revertir</b> quita la marca «Sin carpeta»: el caso sale de esta lista y deja de verse gris en Solicitudes. <b>Caja</b> lo envía a la cola de Caja. El basurero elimina la solicitud, previa confirmación.'
    },

    // ---------- COLAS PDF (SECTOR) ----------
    {
      pagina: '/sectorf8', pose: 'saluda',
      target: '.tool-nav',
      fallbackTarget: 'main',
      titulo: 'Barra de funciones',
      icono: '🧰',
      descripcion: 'En las pantallas de apoyo esta barra lleva a <b>Solicitudes</b>, las colas <b>PDF Archivo</b> y <b>PDF Oficina 43</b>, <b>Estadísticas</b>, <b>Impresión mensual</b> y <b>Caja</b>. La opción abierta se destaca en azul.'
    },
    {
      pagina: '/sectorf8',
      target: 'main .card.table-card table',
      fallbackTarget: 'main .card.table-card',
      titulo: 'Cola de impresión del sector',
      icono: '🖨️',
      descripcion: 'Lista los casos <b>marcados</b> o <b>pendientes de carpeta</b> cuyo sector (según la fecha de penúltima carpeta) es el de esta cola, y que aún no se imprimen. Los casos enviados a Caja no aparecen. Cada sector tiene su propia cola: elígela en la barra de arriba.'
    },
    {
      pagina: '/sectorf8',
      target: 'main .nav-group .btn-outline',
      fallbackTarget: 'main .card.table-card',
      titulo: 'Imprimir informe',
      icono: '📄',
      descripcion: 'Imprime o guarda como PDF el <b>INFORME</b> del sector con nombre, RUT y fecha de penúltima carpeta. El botón aparece cuando la cola tiene casos.'
    },
    {
      pagina: '/sectorf8',
      target: 'form[action*="ClearAll"] button',
      fallbackTarget: 'main .card.table-card',
      titulo: 'Marcar todos como impresos',
      icono: '✅',
      descripcion: 'Registra como impresos todos los casos de la cola y la deja vacía. No cambia el estado de los casos en Solicitudes.'
    },

    // ---------- ESTADÍSTICAS ----------
    {
      pagina: '/estadisticas', pose: 'saluda',
      target: '.stats-grid',
      fallbackTarget: 'main',
      titulo: 'Resumen',
      icono: '📊',
      descripcion: 'Totales, <b>pendientes vencidos</b> (más de 15 días hábiles), <b>cumplimiento del plazo legal</b>, días hasta la subida y casos en revisión, marcados o pendientes de carpeta. Rojo: pide atención.'
    },
    {
      pagina: '/estadisticas',
      target: '.stats-grid + .chart-grid',
      fallbackTarget: 'main',
      titulo: 'Subidas y plazo legal',
      icono: '🥧',
      descripcion: 'Proporción de <b>subidas frente a pendientes</b>, y de subidas <b>dentro y fuera</b> del plazo legal de 15 días hábiles. El porcentaje central resume cada gráfico.'
    },
    {
      pagina: '/estadisticas',
      target: '.stats-scroll > h2:nth-of-type(2) + .chart-card',
      fallbackTarget: 'main',
      titulo: 'Tendencia',
      icono: '📈',
      descripcion: '<b>Ingresos frente a subidas</b> por mes, últimos 12 meses (ingresos por fecha de citación). Más abajo: ingresos por semana, plazo cumplido por mes de subida e ingresos por día de la semana.'
    },
    {
      pagina: '/estadisticas',
      target: '.stats-scroll > h2:nth-of-type(3) + .chart-grid',
      fallbackTarget: 'main',
      titulo: 'Tiempos',
      icono: '⏱️',
      descripcion: 'Histogramas de <b>días desde la citación hasta la subida</b> y de <b>antigüedad de los pendientes</b> en días hábiles. Las barras rojas marcan los tramos que superan o suelen superar el plazo legal.'
    },
    {
      pagina: '/estadisticas',
      target: '.stats-scroll > h2:nth-of-type(4) + .chart-grid',
      fallbackTarget: 'main',
      titulo: 'Carpetas físicas (Caja)',
      icono: '📦',
      descripcion: 'Clasificación de las carpetas: en caja cerrada, en cola de caja, sin carpeta y sin clasificar; al centro, cuántas cajas se han cerrado. Al lado, cuántas carpetas tiene cada caja cerrada.'
    },
    {
      pagina: '/estadisticas',
      target: '.stats-scroll > h2:nth-of-type(5) + .chart-grid',
      fallbackTarget: 'main',
      titulo: 'Distribución',
      icono: '🧩',
      descripcion: 'Carga por <b>sector oficina</b> (subidas y pendientes) y reparto de los casos por sector oficina; más abajo, por estado, estado actual, sector de archivo y origen del registro.'
    },

    // ---------- IMPRESIÓN MENSUAL ----------
    {
      pagina: '/impresionmensual', pose: 'saluda',
      target: 'main .nav-group form',
      fallbackTarget: 'main',
      titulo: 'Mes de trabajo',
      icono: '🗓️',
      descripcion: 'Por defecto se abre el <b>mes anterior</b>. Elige otro mes y pulsa <b>Ver mes</b> para armar su listado.'
    },
    {
      pagina: '/impresionmensual',
      target: '.print-report',
      fallbackTarget: 'main',
      titulo: 'Listado del mes',
      icono: '🧾',
      descripcion: 'Casos <b>SUBIDA A CONASET</b> cuya fecha de subida cae en el mes elegido y que tienen fecha de penúltima carpeta, ordenados por fecha de subida. Incluye folio, fecha de generación, total y firma de quien solicita.'
    },
    {
      pagina: '/impresionmensual',
      target: 'main .nav-group button[onclick*="print"]',
      fallbackTarget: '.print-report',
      titulo: 'Imprimir / Guardar como PDF',
      icono: '🖨️',
      descripcion: 'Imprime el listado; a su lado eliges el <b>tamaño de hoja</b> (A4 o Letter). Al cerrar la impresión, el mes queda registrado como impreso automáticamente.'
    },
    {
      pagina: '/impresionmensual', pose: 'alerta',
      target: '#mark-printed-form',
      fallbackTarget: '.print-report',
      titulo: 'Vaciar listado',
      icono: '🧹',
      descripcion: 'Pide confirmación y registra el listado del mes como <b>impreso</b>. Si es el mes anterior, con eso se apaga el aviso «Recuerda imprimir las penúltimas carpetas» de Solicitudes.'
    },
    {
      pagina: '/impresionmensual', pose: 'celebra',
      target: '#btn-guia-global',
      titulo: '¡Terminaste el recorrido!',
      icono: '🎉',
      descripcion: 'Ya conoces todo F8 Urgentes. Cuando tengas una duda, vuelve a pulsar <b>Cómo usar el sistema</b> y te muestro los pasos de la pantalla donde estés.'
    }
  ];

  // ---- Lico: se usa el de lico.js; esta copia solo existe por si la página no carga lico.js ----
  if (!window.Lico || typeof window.Lico.svg !== 'function') {
    window.Lico = (function () {
      function licoSvg(pose) {
        var INK = '#23262a', AZ = '#26388c';
        var brazo = function (d) {
          return '<path d="' + d + '" stroke="' + AZ + '" stroke-width="9" fill="none" stroke-linecap="round"/>';
        };
        var mano = function (x, y) {
          return '<circle cx="' + x + '" cy="' + y + '" r="9" fill="#fff" stroke="' + AZ + '" stroke-width="3.5"/>';
        };
        var ojos = function (dx) {
          dx = dx || 0;
          return '<g class="lico-ojos">' +
            '<circle cx="' + (66 + dx) + '" cy="92" r="9" fill="' + INK + '"/><circle cx="' + (69 + dx) + '" cy="89" r="3" fill="#fff"/>' +
            '<circle cx="' + (114 + dx) + '" cy="92" r="9" fill="' + INK + '"/><circle cx="' + (117 + dx) + '" cy="89" r="3" fill="#fff"/></g>';
        };
        var bocaAbierta = '<path d="M72 108Q90 134 108 108Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
        var izq = '', der = '', cara = '', extra = '';

        if (pose === 'celebra') {
          izq = brazo('M24 104Q-2 90 6 54') + mano(6, 50);
          der = brazo('M156 104Q182 90 174 54') + mano(174, 50);
          cara = '<path d="M57 96q9-14 18 0M105 96q9-14 18 0" stroke="' + INK + '" stroke-width="4" fill="none" stroke-linecap="round"/>' +
            '<path d="M70 106Q90 140 110 106Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
          extra = '<rect x="30" y="8" width="8" height="14" rx="2" fill="#1fb5d9" transform="rotate(20 34 15)"/>' +
            '<rect x="188" y="30" width="8" height="14" rx="2" fill="#e889c4" transform="rotate(-25 192 37)"/>' +
            '<rect x="90" y="0" width="8" height="14" rx="2" fill="#f6d23c" transform="rotate(35 94 7)"/>' +
            '<circle cx="204" cy="86" r="5" fill="#2fb56a"/><circle cx="14" cy="22" r="5" fill="#e8503f"/>';
        } else if (pose === 'alerta') {
          izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
          der = brazo('M156 100Q184 88 184 62') + mano(184, 58);
          cara = '<g class="lico-ojos"><circle cx="66" cy="94" r="9" fill="' + INK + '"/><circle cx="114" cy="94" r="9" fill="' + INK + '"/>' +
            '<circle cx="69" cy="91" r="3" fill="#fff"/><circle cx="117" cy="91" r="3" fill="#fff"/></g>' +
            '<path d="M54 76l24 6M126 76l-24 6" stroke="' + INK + '" stroke-width="4" stroke-linecap="round"/>' +
            '<path d="M74 118q8-10 16 0t16 0" stroke="' + INK + '" stroke-width="4" fill="none" stroke-linecap="round"/>';
          extra = '<path d="M158 62q8 12 0 18q-8-6 0-18z" fill="#1fb5d9"/>' +
            '<circle cx="196" cy="34" r="18" fill="#fff" stroke="#e8503f" stroke-width="5"/>' +
            '<path d="M196 24v11l8 5" stroke="#e8503f" stroke-width="4" fill="none" stroke-linecap="round"/>';
        } else if (pose === 'explica') {
          izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
          der = brazo('M156 104Q190 108 204 90') + mano(206, 86);
          cara = ojos(3) + '<path d="M74 108Q90 126 106 108Z" fill="#e8503f" stroke="' + INK + '" stroke-width="3" stroke-linejoin="round"/>';
        } else { // saluda
          izq = brazo('M24 104Q6 120 12 142') + mano(12, 144);
          der = '<g class="lico-saludo">' + brazo('M156 100Q184 88 180 60') + mano(180, 56) + '</g>';
          cara = ojos(0) + bocaAbierta;
        }

        return '<svg class="lico-svg" viewBox="-6 -4 232 228" role="img" aria-label="Lico, la mascota de licencias de conducir">' +
          '<ellipse cx="90" cy="214" rx="52" ry="7" fill="rgba(27,33,80,.18)"/>' +
          '<g class="lico-cuerpo">' +
            '<rect x="52" y="160" width="14" height="30" rx="7" fill="' + AZ + '"/><rect x="104" y="160" width="14" height="30" rx="7" fill="' + AZ + '"/>' +
            '<ellipse cx="58" cy="194" rx="16" ry="9" fill="#f6d23c" stroke="' + AZ + '" stroke-width="3.5"/>' +
            '<ellipse cx="112" cy="194" rx="16" ry="9" fill="#f6d23c" stroke="' + AZ + '" stroke-width="3.5"/>' +
            izq +
            '<rect x="20" y="20" width="140" height="150" rx="24" fill="#fff" stroke="' + AZ + '" stroke-width="5"/>' +
            '<path d="M22.5 60V46a21.5 21.5 0 0 1 21.5-21.5h92a21.5 21.5 0 0 1 21.5 21.5V60z" fill="' + AZ + '"/>' +
            '<circle cx="44" cy="42" r="5" fill="#1fb5d9"/><circle cx="58" cy="42" r="5" fill="#2fb56a"/>' +
            '<circle cx="72" cy="42" r="5" fill="#e889c4"/><circle cx="86" cy="42" r="5" fill="#f6d23c"/>' +
            '<rect x="110" y="38" width="34" height="8" rx="4" fill="#fff" opacity=".9"/>' +
            cara +
            '<ellipse cx="48" cy="114" rx="9" ry="6" fill="#e889c4" opacity=".7"/><ellipse cx="132" cy="114" rx="9" ry="6" fill="#e889c4" opacity=".7"/>' +
            '<rect x="44" y="138" width="60" height="7" rx="3.5" fill="#d9dcee"/><rect x="44" y="151" width="40" height="7" rx="3.5" fill="#d9dcee"/>' +
            '<rect x="110" y="140" width="26" height="18" rx="4" fill="#f6d23c" stroke="#c9a400" stroke-width="2"/>' +
            der + extra +
          '</g></svg>';
      }

      function montarLicos() {
        var els = document.querySelectorAll('[data-lico]');
        for (var i = 0; i < els.length; i++) {
          if (!els[i].querySelector('svg')) {
            els[i].insertAdjacentHTML('afterbegin', licoSvg(els[i].getAttribute('data-lico')));
          }
        }
      }

      if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', montarLicos);
      } else {
        montarLicos();
      }
      return { svg: licoSvg, mount: montarLicos };
    })();
  }

  var tourPasoActual = 0;
  var tourKeyHandler = null;
  var tourToken = 0; // invalida posicionamientos pendientes al cambiar de paso o cerrar

  // Ruta actual normalizada: '/index', '/caja', '/sectorf8', ... ('/' cuenta como '/index').
  function rutaActual() {
    var p = location.pathname.toLowerCase().replace(/\/+$/, '');
    return p === '' ? '/index' : p;
  }

  function seccionDe(paso) {
    return (PAGINAS[paso.pagina] || {}).seccion || '';
  }

  // Primer paso de la página donde está el usuario; -1 si la página no tiene pasos.
  function primerPasoDePaginaActual() {
    var ruta = rutaActual();
    for (var i = 0; i < PASOS_TOUR.length; i++) {
      if (PASOS_TOUR[i].pagina === ruta) return i;
    }
    return -1;
  }

  // Cambia de paso; si el paso vive en otra página, navega y la guía continúa allá.
  function irAPaso(idx) {
    if (idx < 0 || idx >= PASOS_TOUR.length) return;
    var paso = PASOS_TOUR[idx];
    if (paso.pagina !== rutaActual()) {
      cerrarGuiaInteractiva();
      var url = PAGINAS[paso.pagina].url;
      location.href = url + (url.indexOf('?') >= 0 ? '&' : '?') + 'guia=1&paso=' + idx;
      return;
    }
    tourPasoActual = idx;
    renderPasoTour(false);
  }

  function escapar(txt) {
    return String(txt).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  }

  function iniciarGuiaInteractiva(pasoInicial) {
    cerrarGuiaInteractiva();
    tourPasoActual = (typeof pasoInicial === 'number' && pasoInicial >= 0 && pasoInicial < PASOS_TOUR.length) ? pasoInicial : 0;

    var overlay = document.createElement('div');
    overlay.id = 'tour-overlay';
    overlay.className = 'tour-overlay';
    overlay.innerHTML =
      '<button type="button" class="tour-btn-salir-flotante" id="tour-salir-flotante" title="Terminar y cerrar la guía">' +
        '<span aria-hidden="true">✕</span> Cerrar guía' +
      '</button>' +
      '<div id="tour-spotlight" class="tour-spotlight"></div>' +
      '<div id="tour-card" class="tour-card" role="dialog" aria-modal="true" aria-labelledby="tour-titulo" aria-describedby="tour-desc">' +
        '<div class="tour-card-header">' +
          '<span class="tour-paso-badge" id="tour-badge">Paso 1 de ' + PASOS_TOUR.length + '</span>' +
          '<button type="button" class="tour-btn-cerrar" id="tour-cerrar" title="Cerrar guía" aria-label="Cerrar guía">&times;</button>' +
        '</div>' +
        '<div class="tour-card-body">' +
          '<div class="tour-mascota" id="tour-mascota" aria-hidden="true"></div>' +
          '<div class="tour-card-texto">' +
            '<h3 id="tour-titulo" class="tour-card-titulo"></h3>' +
            '<p id="tour-desc" class="tour-card-desc"></p>' +
          '</div>' +
        '</div>' +
        '<span class="tour-progreso" aria-hidden="true"><i id="tour-prog"></i></span>' +
        '<div class="tour-card-footer">' +
          '<button type="button" class="btn-secondary" id="tour-prev">Anterior</button>' +
          '<div class="tour-dots" id="tour-dots"></div>' +
          '<button type="button" class="btn-primary" id="tour-next">Siguiente</button>' +
        '</div>' +
        '<div id="tour-flecha" class="tour-flecha"></div>' +
      '</div>';

    document.body.appendChild(overlay);

    document.getElementById('tour-cerrar').onclick = cerrarGuiaInteractiva;
    document.getElementById('tour-salir-flotante').onclick = cerrarGuiaInteractiva;
    overlay.onclick = function (e) {
      if (e.target === overlay) cerrarGuiaInteractiva();
    };

    tourKeyHandler = function (e) {
      if (e.key === 'Escape') {
        cerrarGuiaInteractiva();
      } else if (e.key === 'ArrowRight' && tourPasoActual < PASOS_TOUR.length - 1) {
        e.preventDefault();
        irAPaso(tourPasoActual + 1);
      } else if (e.key === 'ArrowLeft' && tourPasoActual > 0) {
        e.preventDefault();
        irAPaso(tourPasoActual - 1);
      }
    };
    window.addEventListener('keydown', tourKeyHandler);

    document.getElementById('tour-prev').onclick = function () {
      if (tourPasoActual > 0) irAPaso(tourPasoActual - 1);
    };

    document.getElementById('tour-next').onclick = function () {
      if (tourPasoActual < PASOS_TOUR.length - 1) {
        irAPaso(tourPasoActual + 1);
      } else {
        cerrarGuiaInteractiva();
        mostrarToast('¡Guía interactiva completada!');
      }
    };

    renderPasoTour(true);
    var next = document.getElementById('tour-next');
    if (next) { try { next.focus({ preventScroll: true }); } catch (err) { next.focus(); } }
  }

  // Rectángulo visible del elemento: recortado por la ventana y por los contenedores con scroll
  // (la tabla de Solicitudes y las estadísticas se desplazan dentro de su tarjeta, no la página).
  function areaVisible(el) {
    var r = el.getBoundingClientRect();
    var top = 0, left = 0, bottom = window.innerHeight, right = window.innerWidth;
    for (var p = el.parentElement; p && p !== document.body && p !== document.documentElement; p = p.parentElement) {
      var s = window.getComputedStyle(p);
      if (/(auto|scroll|hidden)/.test(s.overflowX + ' ' + s.overflowY)) {
        var pr = p.getBoundingClientRect();
        top = Math.max(top, pr.top);
        left = Math.max(left, pr.left);
        bottom = Math.min(bottom, pr.bottom);
        right = Math.min(right, pr.right);
      }
    }
    var a = {
      el: r,
      vista: { top: top, left: left, bottom: bottom, right: right },
      top: Math.max(r.top, top),
      left: Math.max(r.left, left),
      bottom: Math.min(r.bottom, bottom),
      right: Math.min(r.right, right)
    };
    a.tapado = tapado(el, a);
    a.completo = r.top >= top && r.bottom <= bottom && r.left >= left && r.right <= right && !a.tapado;
    return a;
  }

  // true si otro elemento de la página tapa el centro del target: las columnas fijas Nombre/RUT y el
  // encabezado de la tabla son "sticky" y quedan encima de las celdas desplazadas bajo ellos.
  function tapado(el, a) {
    if (!document.elementsFromPoint) return false;
    var pila = document.elementsFromPoint((a.left + a.right) / 2, (a.top + a.bottom) / 2);
    for (var i = 0; i < pila.length; i++) {
      var arriba = pila[i];
      if (arriba.closest && arriba.closest('#tour-overlay, #tour-toast')) continue;
      return !(arriba === el || el.contains(arriba) || arriba.contains(el));
    }
    return false;
  }

  // querySelector que no rompe la guía si el navegador no entiende el selector (por ejemplo :has).
  function q(sel) {
    if (!sel) return null;
    try { return document.querySelector(sel); } catch (err) { return null; }
  }

  function buscarTarget(paso) {
    var el = q(paso.target) || q(paso.fallbackTarget);
    // Sin tamaño (oculto por CSS, por ejemplo Lico bajo 900 px): se usa el fallback o main.
    if (el && !el.getClientRects().length) el = q(paso.fallbackTarget);
    return el || document.querySelector('main');
  }

  function renderPasoTour(esPrimerRender) {
    var token = ++tourToken;
    var paso = PASOS_TOUR[tourPasoActual];
    var card = document.getElementById('tour-card');
    var spot = document.getElementById('tour-spotlight');
    if (!card || !spot) return;

    var total = PASOS_TOUR.length;
    document.getElementById('tour-badge').textContent = 'Paso ' + (tourPasoActual + 1) + ' de ' + total;
    document.getElementById('tour-titulo').innerHTML = '<span class="tour-ico" aria-hidden="true">' + paso.icono + '</span> ' +
      (tourPasoActual + 1) + '. ' + paso.titulo;
    document.getElementById('tour-desc').innerHTML = paso.descripcion;
    document.getElementById('tour-mascota').innerHTML = window.Lico.svg(paso.pose || 'explica');
    document.getElementById('tour-prog').style.width = Math.round((tourPasoActual + 1) * 100 / total) + '%';
    document.getElementById('tour-prev').disabled = tourPasoActual === 0;

    var next = document.getElementById('tour-next');
    var esUltimo = tourPasoActual === total - 1;
    next.textContent = esUltimo ? '✔ ¡Finalizar!' : 'Siguiente';
    next.classList.toggle('tour-fin', esUltimo);

    // Indicador compacto: sección actual y avance dentro de ella.
    var seccion = seccionDe(paso);
    var deLaSeccion = PASOS_TOUR.filter(function (p) { return p.pagina === paso.pagina; });
    document.getElementById('tour-dots').innerHTML = '<span class="tour-seccion" title="' + escapar(seccion) + '">' +
      escapar(seccion) + ' <small>' + (deLaSeccion.indexOf(paso) + 1) + '/' + deLaSeccion.length + '</small></span>';

    var target = buscarTarget(paso);
    if (!target) return;

    var posicionar = function () {
      if (token !== tourToken || !document.getElementById('tour-card')) return;
      var a = areaVisible(target);
      var flecha = document.getElementById('tour-flecha');
      var vw = window.innerWidth;
      var vh = window.innerHeight;
      var cardW = card.offsetWidth || Math.min(520, vw - 32);
      var cardH = card.offsetHeight || 290;
      var pad = 6;
      var ancho = Math.max(0, a.right - a.left);
      var alto = Math.max(0, a.bottom - a.top);
      var esGrilla = alto > vh * 0.55;

      if (esPrimerRender) {
        spot.style.transition = 'none';
        card.style.transition = 'none';
      }

      // El resaltado cubre solo la parte visible del elemento (sin pasar del borde de la ventana).
      var sLeft = Math.max(4, Math.round(a.left - pad));
      var sTop = Math.max(4, Math.round(a.top - pad));
      var sRight = Math.min(vw - 4, Math.round(a.left + ancho + pad));
      var sBottom = Math.min(vh - 4, Math.round(a.top + alto + pad));
      spot.style.left = sLeft + 'px';
      spot.style.top = sTop + 'px';
      spot.style.width = Math.max(24, sRight - sLeft) + 'px';
      spot.style.height = Math.max(24, sBottom - sTop) + 'px';

      // Tarjeta al costado del elemento, si hay espacio (elementos altos como formularios y tablas).
      var alCostado = function () {
        var top = Math.round(Math.min(Math.max(16, a.top + alto / 2 - cardH / 2), vh - cardH - 16));
        if (vw - (a.left + ancho) - 32 >= cardW) return { left: Math.round(a.left + ancho + 16), top: top };
        if (a.left - 32 >= cardW) return { left: Math.round(a.left - cardW - 16), top: top };
        return null;
      };

      if (esGrilla) {
        var lado = alCostado();
        card.style.left = (lado ? lado.left : Math.max(16, Math.round((vw - cardW) / 2))) + 'px';
        card.style.top = (lado ? lado.top : Math.round(Math.max(16, (vh - cardH) / 2))) + 'px';
        flecha.style.display = 'none';
      } else {
        var centroX = a.left + ancho / 2;
        var cardLeft = Math.round(centroX - cardW / 2);
        if (cardLeft + cardW > vw - 16) cardLeft = vw - cardW - 16;
        if (cardLeft < 16) cardLeft = 16;

        // Debajo del elemento; si no cabe, encima; si tampoco, pegada al borde con más espacio.
        var cardTop = Math.round(a.top + alto + 14);
        var flechaArriba = true;
        if (cardTop + cardH > vh - 16) {
          cardTop = Math.round(a.top - cardH - 14);
          flechaArriba = false;
          if (cardTop < 16) cardTop = (vh - (a.top + alto) > a.top) ? vh - cardH - 16 : 16;
        }
        if (cardTop < 16) cardTop = 16;

        // Si no cabe ni arriba ni abajo, se intenta al costado; si tampoco, queda encima y sin flecha.
        var solapa = cardTop < a.top + alto + 4 && cardTop + cardH > a.top - 4;
        var costado = solapa ? alCostado() : null;
        if (costado) {
          cardLeft = costado.left;
          cardTop = costado.top;
        }

        card.style.left = cardLeft + 'px';
        card.style.top = cardTop + 'px';
        flecha.style.display = solapa ? 'none' : 'block';
        flecha.className = 'tour-flecha ' + (flechaArriba ? 'flecha-arriba' : 'flecha-abajo');
        flecha.style.left = Math.round(Math.max(24, Math.min(cardW - 36, centroX - cardLeft))) + 'px';
      }

      if (esPrimerRender) {
        void card.offsetHeight; // aplica left/top antes de reactivar las transiciones
        spot.style.transition = '';
        card.style.transition = '';
      }

      spot.classList.add('visible');
      card.classList.add('visible');
    };

    var a = areaVisible(target);
    var vw0 = window.innerWidth, vh0 = window.innerHeight;
    var cardW0 = card.offsetWidth || Math.min(520, vw0 - 32);
    var cardH0 = card.offsetHeight || 290;
    var alto0 = a.el.height;
    var cabeAfuera = alto0 > vh0 * 0.55 ||
      a.top - cardH0 - 30 >= 0 || a.bottom + cardH0 + 30 <= vh0 ||
      vw0 - a.right - 32 >= cardW0 || a.left - 32 >= cardW0;
    // Visible pero sin lugar para la tarjeta: si subirlo al inicio de su contenedor le deja espacio debajo, se sube.
    var subir = a.completo && !cabeAfuera && a.vista.top + alto0 + cardH0 + 30 <= vh0 && a.top > a.vista.top + 1;
    if (a.completo && !subir) {
      posicionar();
      return;
    }
    // Lo más grande que su contenedor no se centra (quedaría a medias): se lleva lo justo para verlo.
    // Si lo tapan las columnas fijas (a la izquierda), se lleva al borde derecho de la tabla.
    var grandeY = alto0 > (a.vista.bottom - a.vista.top) * 0.9;
    var grandeX = a.el.width > (a.vista.right - a.vista.left) * 0.9;
    try {
      target.scrollIntoView({
        behavior: 'auto',
        block: subir ? 'start' : grandeY ? 'nearest' : 'center',
        inline: grandeX ? 'nearest' : a.tapado ? 'end' : 'center'
      });
    } catch (err) {
      target.scrollIntoView();
    }
    setTimeout(posicionar, 80);
  }

  function cerrarGuiaInteractiva() {
    tourToken++;
    if (tourKeyHandler) {
      window.removeEventListener('keydown', tourKeyHandler);
      tourKeyHandler = null;
    }
    var o = document.getElementById('tour-overlay');
    if (o) o.remove();
  }

  var timerToast = null;
  function mostrarToast(mensaje) {
    var toast = document.getElementById('tour-toast');
    if (!toast) {
      toast = document.createElement('div');
      toast.id = 'tour-toast';
      toast.className = 'tour-toast';
      toast.setAttribute('role', 'status');
      document.body.appendChild(toast);
    }
    toast.innerHTML = '<span class="tour-toast-lico" aria-hidden="true">' + window.Lico.svg('celebra') + '</span><span></span>';
    toast.lastChild.textContent = mensaje;
    toast.classList.add('visible');
    clearTimeout(timerToast);
    timerToast = setTimeout(function () { toast.classList.remove('visible'); }, 3200);
  }

  // Botón de la cabecera: primer paso de esta página; en una página sin pasos, la guía parte en Solicitudes.
  function abrirDesdeBoton() {
    var i = primerPasoDePaginaActual();
    if (i < 0) irAPaso(0); else iniciarGuiaInteractiva(i);
  }

  function iniciar() {
    var btn = document.getElementById('btn-guia-global');
    if (btn) btn.addEventListener('click', abrirDesdeBoton);

    // Si viene de otra página de la guía (?guia=1&paso=N), continúa en ese paso.
    var params = new URLSearchParams(location.search);
    if (params.get('guia') !== '1') return;
    var paso = parseInt(params.get('paso'), 10);
    if (isNaN(paso) || paso < 0 || paso >= PASOS_TOUR.length || PASOS_TOUR[paso].pagina !== rutaActual()) {
      paso = primerPasoDePaginaActual();
    }
    params.delete('guia');
    params.delete('paso');
    if (window.history && window.history.replaceState) {
      var qs = params.toString();
      window.history.replaceState(window.history.state, document.title, location.pathname + (qs ? '?' + qs : '') + location.hash);
    }
    if (paso >= 0) setTimeout(function () { iniciarGuiaInteractiva(paso); }, 300);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciar);
  } else {
    iniciar();
  }

  window.iniciarGuiaInteractiva = iniciarGuiaInteractiva;
  window.cerrarGuiaInteractiva = cerrarGuiaInteractiva;
})();
