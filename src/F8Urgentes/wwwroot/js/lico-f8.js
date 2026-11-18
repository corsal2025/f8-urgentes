/* Lico en el hero 3D de F8 Urgentes: saludo, lámpara, carpeta e inclinación al puntero. */
(function () {
  'use strict';

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
        var countEl = document.getElementById('heroLicoCount');
        var count = countEl ? (parseInt(countEl.textContent, 10) || 0) : 0;
        decirHero(count === 0 ? '¡Al día! No hay solicitudes pendientes en vista.' : 'Tu mesa muestra ' + count + (count === 1 ? ' solicitud pendiente.' : ' solicitudes pendientes.'));
        var mesa = document.getElementById('mi-mesa') || document.querySelector('.card.table-card');
        if (mesa && mesa.scrollIntoView) {
          mesa.scrollIntoView({ behavior: movimientoPausado() ? 'auto' : 'smooth', block: 'start' });
        }
      });
    }
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', iniciarHero);
  } else {
    iniciarHero();
  }
})();
