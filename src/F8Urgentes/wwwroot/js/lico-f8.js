/* Lico en la cabecera de F8 Urgentes: consejos al pulsarlo, saludo según la hora y reacción a los avisos.
 * Misma dinámica que iniciarLicoCabecera() de la Agenda de Prácticos (public/app.js).
 * Requiere #head-lico y #head-lico-burbuja en _Layout.cshtml; LicoJuegos (lico-juegos.js) es opcional.
 */
(function () {
  'use strict';

  // ---- Consejos: generales + propios de la página abierta (los de la página pesan el doble) ----
  var FRASES = [
    'El buscador de arriba encuentra un caso por RUT o nombre desde cualquier pantalla.',
    '"Sincronizar" trae de la matriz los casos "NO HAY CARPETA" y actualiza los ya subidos.',
    'Si me muevo demasiado, apágame con el botón ☺ que está a mi lado.',
    'Si pasas un rato sin tocar nada, hago alguna travesura. Mueve el mouse y me voy.',
    'Mi licencia está al día. ¿Y la tuya?',
    'Una carpeta bien archivada hoy es una búsqueda menos mañana.'
  ];
  var FRASES_PAGINA = {
    index: [
      'Las pestañas de arriba filtran la lista: Pendientes, Subidas, Penúltimas carpetas, Sin agregar y Requiere revisión.',
      'Fila amarilla: marcada para imprimir. Fila lila: carpeta pendiente.',
      'Pulsa el encabezado "Fecha penúltima carpeta" para ordenar por esa fecha.',
      '"Solicitudes sin agregar" abre un formulario para ingresar varios casos de una vez.'
    ],
    edit: ['Completa los datos de la nueva solicitud y guarda: aparecerá en Solicitudes.'],
    caja: [
      'Los casos que envías a "Caja" desde Solicitudes esperan aquí hasta cerrar la caja.',
      'Al cerrar una caja queda su acta lista para imprimir.'
    ],
    carpetasextra: ['Ingresa arriba las carpetas extra; quedan listadas abajo.'],
    sincarpetas: ['Aquí quedan las solicitudes marcadas como "Sin carpeta".'],
    estadisticas: [
      'El plazo legal se cuenta en 15 días hábiles.',
      'Los números cuentan una historia: compara ingresos y subidas mes a mes.'
    ],
    sectorf8: ['Esta es la cola de impresión del sector. Yo desaparezco al imprimir.'],
    impresionmensual: ['Aquí sale el listado del mes para imprimir las penúltimas carpetas.']
  };

  function pagina() {
    var seg = (location.pathname.split('/')[1] || 'index').toLowerCase();
    return seg;
  }

  function saludoHora() {
    var hr = new Date().getHours();
    var s = hr < 12 ? 'Buenos días' : hr < 20 ? 'Buenas tardes' : 'Buenas noches';
    return s + '. Soy Lico. Pulsa mi carita cuando quieras un consejo.';
  }

  function iniciar() {
    var btn = document.getElementById('head-lico');
    var burbuja = document.getElementById('head-lico-burbuja');
    if (!btn || !burbuja) return;
    var ultimo = -1, timer = null;

    function cerrar() {
      clearTimeout(timer);
      burbuja.hidden = true;
      btn.classList.remove('habla');
    }
    function decir(txt, ms) {
      burbuja.textContent = txt; // siempre como texto, nunca como HTML
      burbuja.hidden = false;
      btn.classList.add('habla');
      clearTimeout(timer);
      timer = setTimeout(cerrar, ms || 7000);
    }

    btn.addEventListener('click', function () {
      var propias = FRASES_PAGINA[pagina()] || [];
      var pool = FRASES.concat(propias, propias);
      var i;
      do { i = Math.floor(Math.random() * pool.length); } while (i === ultimo && pool.length > 1);
      ultimo = i;
      decir(pool[i], 7000);
    });

    // Saludo según la hora: una sola vez por sesión y solo con Lico activado
    try {
      if (!sessionStorage.getItem('f8-lico-saludo') && (!window.LicoJuegos || window.LicoJuegos.activo())) {
        sessionStorage.setItem('f8-lico-saludo', '1');
        setTimeout(function () { if (burbuja.hidden) decir(saludoHora(), 5500); }, 1500);
      }
    } catch (e) { /* sin sessionStorage: sin saludo */ }

    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && !burbuja.hidden) cerrar(); });
    document.addEventListener('click', function (e) {
      if (!burbuja.hidden && !(e.target.closest && e.target.closest('.head-lico-wrap'))) cerrar();
    });

    // Avisos que trae la página (sincronización, certificados, RUT duplicados): Lico celebra o se alerta
    if (window.LicoJuegos) {
      if (document.querySelector('main .alert-danger')) {
        setTimeout(function () { window.LicoJuegos.reaccionar('alerta'); }, 600);
      } else if (document.querySelector('main .alert-success')) {
        setTimeout(function () { window.LicoJuegos.reaccionar('celebra'); }, 600);
      }
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciar);
  } else {
    iniciar();
  }
})();
