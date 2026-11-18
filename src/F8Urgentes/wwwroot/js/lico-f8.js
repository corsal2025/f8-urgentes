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
      'Las pestañas de arriba filtran la lista: Pendientes, Subidas, Solicitud de certificado, Sin agregar y Requiere revisión.',
      'Fila amarilla: marcada para imprimir.',
      '"Solicitudes sin agregar" abre un formulario para ingresar varios casos de una vez.'
    ],
    edit: ['Completa los datos de la nueva solicitud y guarda: aparecerá en Solicitudes.'],
    carpetasextra: ['Ingresa arriba las carpetas extra; quedan listadas abajo.'],
    estadisticas: [
      'El plazo legal se cuenta en 15 días hábiles.',
      'Los números cuentan una historia: compara ingresos y subidas mes a mes.'
    ],
    impresionmensual: ['Aquí sale el listado del mes para imprimir las carpetas subidas.']
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

  function iniciarHero() {
    var hero = document.querySelector('.lico-hero');
    var scene = document.querySelector('.li-scene');
    if (!hero || !scene) return;

    var saludoBtn = document.getElementById('btn-lico-saludo');
    var lampBtn = scene.querySelector('.lico-lampara');
    var carpetaBtn = scene.querySelector('.lico-carpeta');
    var bubbleMsg = scene.querySelector('.studio-bubble p') || scene.querySelector('.studio-bubble');
    var restingText = bubbleMsg ? bubbleMsg.textContent.trim() : '';

    var SALUDOS_HERO = [
      '¡Hola! Soy Lico. Cada solicitud F8 tiene su lugar.',
      'Recuerda usar "Sincronizar" para traer casos nuevos de la matriz.',
      'Marca filas en amarillo para incluirlas en la cola PDF de su sector.',
      '¡Aquí estoy! Cuidando tus carpetas y solicitudes urgentes.'
    ];
    var idxSaludo = 0;
    var timerBubble = null;
    var timerAnim = null;

    function movimientoPausado() {
      return document.documentElement.classList.contains('lico-quieto') ||
        (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);
    }

    function decirHero(txt) {
      if (!bubbleMsg) return;
      clearTimeout(timerBubble);
      bubbleMsg.textContent = txt;
      timerBubble = setTimeout(function () {
        if (bubbleMsg.textContent === txt && restingText) {
          bubbleMsg.textContent = restingText;
        }
      }, 5000);
    }

    function animarEscena(cls) {
      if (movimientoPausado()) return;
      clearTimeout(timerAnim);
      scene.classList.remove('lico-saludando', 'lico-abre-carpeta');
      void scene.offsetWidth;
      scene.classList.add(cls);
      timerAnim = setTimeout(function () {
        scene.classList.remove('lico-saludando', 'lico-abre-carpeta');
      }, 1300);
    }

    hero.addEventListener('pointermove', function (e) {
      if (movimientoPausado() || e.pointerType === 'touch') return;
      var rect = hero.getBoundingClientRect();
      if (!rect.width || !rect.height) return;
      var xPct = (e.clientX - rect.left) / rect.width;
      var yPct = (e.clientY - rect.top) / rect.height;
      var rotX = ((0.5 - yPct) * 5).toFixed(2);
      var rotY = ((xPct - 0.5) * 7).toFixed(2);
      scene.style.setProperty('--li-x', rotX + 'deg');
      scene.style.setProperty('--li-y', rotY + 'deg');
    });

    hero.addEventListener('pointerleave', function () {
      scene.style.setProperty('--li-x', '0deg');
      scene.style.setProperty('--li-y', '0deg');
    });

    var motionBtn = document.getElementById('lico-onoff');
    if (motionBtn) {
      motionBtn.addEventListener('click', function () {
        setTimeout(function () {
          if (movimientoPausado()) {
            scene.style.setProperty('--li-x', '0deg');
            scene.style.setProperty('--li-y', '0deg');
          }
        }, 0);
      });
    }

    if (saludoBtn) {
      saludoBtn.addEventListener('click', function () {
        var texto = SALUDOS_HERO[idxSaludo % SALUDOS_HERO.length];
        idxSaludo++;
        decirHero(texto);
        animarEscena('lico-saludando');
      });
    }

    if (lampBtn) {
      var labelEl = lampBtn.querySelector('[data-lico-luz-etiqueta]') || lampBtn.querySelector('.lico-objeto-etiqueta');
      var lampOff = false;
      try { lampOff = window.localStorage.getItem('f8-lico-lamp-off') === '1'; } catch (e) { lampOff = false; }

      function aplicarLuz() {
        lampBtn.classList.toggle('is-lamp-off', lampOff);
        scene.classList.toggle('is-lamp-off', lampOff);
        lampBtn.setAttribute('aria-pressed', lampOff ? 'false' : 'true');
        if (labelEl) {
          labelEl.textContent = lampOff ? 'Luz apagada' : 'Luz encendida';
        }
      }
      aplicarLuz();

      lampBtn.addEventListener('click', function () {
        lampOff = !lampOff;
        try { window.localStorage.setItem('f8-lico-lamp-off', lampOff ? '1' : '0'); } catch (e) { /* sin almacenamiento */ }
        aplicarLuz();
        decirHero(lampOff ? 'Un poco más de calma. La luz está apagada.' : '¡Qué bien! Mi rincón vuelve a tener luz.');
      });
    }

    if (carpetaBtn) {
      carpetaBtn.addEventListener('click', function () {
        animarEscena('lico-abre-carpeta');
        var tabla = document.querySelector('.card.table-card');
        if (tabla && tabla.scrollIntoView) {
          tabla.scrollIntoView({ behavior: movimientoPausado() ? 'auto' : 'smooth', block: 'start' });
        }
      });
    }
  }

  function arrancar() {
    iniciar();
    iniciarHero();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', arrancar);
  } else {
    arrancar();
  }
})();
