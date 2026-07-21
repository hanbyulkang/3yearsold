/* ============================================================
   발랑 (Balanc) 발표 자료 — 슬라이드 진행 · 목업 캡처 자동 반영
   ============================================================ */
(function () {
  'use strict';

  var slidesEl = document.getElementById('slides');
  var slides = Array.prototype.slice.call(document.querySelectorAll('.slide'));
  var bar = document.getElementById('bar');
  var counter = document.getElementById('counter');
  var toc = document.getElementById('toc');
  var tocGrid = document.getElementById('tocGrid');
  var note = document.getElementById('note');
  var i = 0;

  /* ---------- 목업 프레임 채우기 ----------
     <div class="mock" data-no="A-09" data-nm="견종 추천" data-shot="a-09">
     → shots/a-09.png 가 있으면 그 이미지로, 없으면 점선 자리표시로 남는다.
     확장자는 png → jpg → webp 순으로 시도한다. */

  var EXT = ['png', 'jpg', 'jpeg', 'webp'];

  document.querySelectorAll('.mock[data-shot]').forEach(function (m) {
    var no = m.dataset.no || '';
    var nm = m.dataset.nm || '';
    var base = 'shots/' + m.dataset.shot;

    var ph = document.createElement('div');
    ph.className = 'ph';
    ph.innerHTML =
      '<div class="no">' + no + '</div>' +
      '<div class="nm">' + nm + '</div>' +
      '<div class="file">' + base + '.png</div>';
    m.appendChild(ph);

    var k = 0;
    var img = document.createElement('img');
    img.className = 'shot';
    img.alt = no + ' ' + nm;
    img.style.zIndex = '2';
    img.onerror = function () {
      k += 1;
      if (k < EXT.length) { img.src = base + '.' + EXT[k]; return; }
      img.remove();              // 캡처가 아직 없다 — 자리표시 유지
    };
    img.onload = function () { ph.style.display = 'none'; };
    img.src = base + '.' + EXT[0];
    m.appendChild(img);
  });

  /* ---------- 데모 영상 자리 ----------
     video/demo.mp4 를 넣으면 자동으로 플레이어가 붙는다. */

  var vs = document.getElementById('demoVideo');
  if (vs && vs.dataset.src) {
    var probe = document.createElement('video');
    probe.src = vs.dataset.src;
    probe.preload = 'metadata';
    probe.onloadedmetadata = function () {
      vs.innerHTML = '';
      probe.controls = true;
      probe.playsInline = true;
      vs.appendChild(probe);
      vs.style.border = 'none';
    };
  }

  /* ---------- 내용이 넘치면 그 슬라이드만 자동으로 줄인다 ----------
     장식용 배경(.cover-bg/.cover-blocks)만 남기고 나머지를 .fitbox로 감싼 뒤,
     넘치지 않을 때까지 zoom을 내린다. 슬라이드가 잘리는 일이 없다. */

  slides.forEach(function (s) {
    var box = document.createElement('div');
    box.className = 'fitbox';
    Array.prototype.slice.call(s.children).forEach(function (c) {
      if (!c.classList.contains('cover-bg') && !c.classList.contains('cover-blocks')) {
        box.appendChild(c);
      }
    });
    s.appendChild(box);
  });

  function autofit(s) {
    var box = s.querySelector('.fitbox');
    if (!box) return;
    var z = 1;
    box.style.zoom = '1';
    while (box.scrollHeight > box.clientHeight + 1 && z > 0.62) {
      z -= 0.02;
      box.style.zoom = String(z);
    }
  }

  /* ---------- 뷰포트에 맞춰 슬라이드 통째로 축소 ---------- */

  function fit() {
    var pad = 44;
    var s = Math.min(
      (window.innerWidth - pad) / 1280,
      (window.innerHeight - pad) / 720
    );
    slidesEl.style.transform = 'scale(' + s + ')';
  }

  /* ---------- 진행 ---------- */

  function go(n) {
    i = Math.max(0, Math.min(slides.length - 1, n));
    slides.forEach(function (s, k) { s.classList.toggle('on', k === i); });
    autofit(slides[i]);
    note.textContent = slides[i].dataset.note || '';
    note.classList.toggle('has', !!slides[i].dataset.note);
    bar.style.width = ((i + 1) / slides.length * 100) + '%';
    counter.textContent = (i + 1) + ' / ' + slides.length;
    if (location.hash !== '#' + (i + 1)) {
      history.replaceState(null, '', '#' + (i + 1));
    }
  }

  /* ---------- 목차 ---------- */

  slides.forEach(function (s, k) {
    var d = document.createElement('div');
    d.className = 'toc-item';
    d.innerHTML = '<span class="n">' + (k + 1) + '</span>' + (s.dataset.toc || '슬라이드 ' + (k + 1));
    d.onclick = function () { go(k); toc.classList.remove('on'); };
    tocGrid.appendChild(d);
  });

  document.getElementById('prev').onclick = function () { go(i - 1); };
  document.getElementById('next').onclick = function () { go(i + 1); };
  document.getElementById('tocBtn').onclick = function () { toc.classList.toggle('on'); };
  document.getElementById('noteBtn').onclick = function (e) { e.stopPropagation(); note.classList.toggle('open'); };
  toc.onclick = function (e) { if (e.target === toc) toc.classList.remove('on'); };

  document.addEventListener('keydown', function (e) {
    if (e.key === 'ArrowRight' || e.key === 'PageDown' || e.key === ' ') { go(i + 1); e.preventDefault(); }
    else if (e.key === 'ArrowLeft' || e.key === 'PageUp') { go(i - 1); }
    else if (e.key === 'Home') { go(0); }
    else if (e.key === 'End') { go(slides.length - 1); }
    else if (e.key === 't' || e.key === 'T') { toc.classList.toggle('on'); }
    else if (e.key === 'n' || e.key === 'N') { note.classList.toggle('open'); }
    else if (e.key === 'Escape') { toc.classList.remove('on'); note.classList.remove('open'); }
  });

  // 클릭으로 넘기기 (좌측 1/4 = 이전)
  document.querySelector('.stage').addEventListener('click', function (e) {
    if (toc.classList.contains('on')) return;
    go(e.clientX < window.innerWidth * 0.25 ? i - 1 : i + 1);
  });

  window.addEventListener('resize', fit);
  fit();
  go(parseInt((location.hash || '#1').slice(1), 10) - 1 || 0);
})();
