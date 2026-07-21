/* ============================================================
   D+ 와이어프레임 렌더러
   - window.SCREENS 배열을 읽어 화면 카드를 그린다.
   - 화면 정의 스키마는 screens/_SCHEMA.md 참고.
   ============================================================ */
(function () {
  'use strict';

  var doc = document.getElementById('doc');
  var grid = document.getElementById('grid');
  var navList = document.getElementById('navList');
  var search = document.getElementById('search');
  var countEl = document.getElementById('count');
  var emptyEl = document.getElementById('empty');
  var groupChips = document.getElementById('groupChips');

  var SCREENS = (window.SCREENS || []).slice();
  SCREENS.sort(function (a, b) { return a.no.localeCompare(b.no); });

  var activeGroup = 'ALL';
  var query = '';

  /* ---------- 렌더 ---------- */

  function screenCard(s) {
    var wide = s.wide ? ' wide' : '';
    var frameCls = 'frame' + (s.wide ? ' desktop' : '') + (s.tall ? ' tall' : '');
    var notes = '';
    if (s.notes && s.notes.length) {
      notes = '<div class="notes"><div class="cap">개발 주석</div><ul>' +
        s.notes.map(function (n) { return '<li>' + n + '</li>'; }).join('') +
        '</ul></div>';
    }
    return '' +
      '<section class="screen' + wide + '" id="' + s.id + '" data-group="' + s.group + '" data-no="' + s.no + '">' +
        '<header class="screen-head">' +
          '<div><span class="no">' + s.no + '</span><span class="ttl">' + s.title + '</span>' +
            (s.prd ? '<span class="prd">PRD ' + s.prd + '</span>' : '') +
          '</div>' +
          (s.purpose ? '<div class="purpose">' + s.purpose + '</div>' : '') +
        '</header>' +
        '<div class="' + frameCls + '">' + s.html + '</div>' +
        notes +
      '</section>';
  }

  function render() {
    grid.innerHTML = SCREENS.map(screenCard).join('');
    buildNav();
    buildGroupChips();
    applyFilter();
  }

  function groups() {
    var seen = [];
    SCREENS.forEach(function (s) { if (seen.indexOf(s.group) < 0) seen.push(s.group); });
    return seen;
  }

  function buildNav() {
    var html = '';
    groups().forEach(function (g) {
      var items = SCREENS.filter(function (s) { return s.group === g; });
      html += '<div class="nav-group">' + g + '<span class="cnt">' + items.length + '</span></div>';
      items.forEach(function (s) {
        html += '<a href="#' + s.id + '" data-id="' + s.id + '"><span class="no">' + s.no + '</span>' + s.title + '</a>';
      });
    });
    navList.innerHTML = html;
  }

  function buildGroupChips() {
    var html = '<button class="tgl" data-group="ALL" aria-pressed="true">전체</button>';
    groups().forEach(function (g) {
      html += '<button class="tgl" data-group="' + g + '" aria-pressed="false">' + g + '</button>';
    });
    groupChips.innerHTML = html;
  }

  function applyFilter() {
    var shown = 0;
    SCREENS.forEach(function (s) {
      var el = document.getElementById(s.id);
      if (!el) return;
      var hay = (s.no + ' ' + s.title + ' ' + s.group + ' ' + (s.prd || '') + ' ' + (s.purpose || '')).toLowerCase();
      var okG = activeGroup === 'ALL' || s.group === activeGroup;
      var okQ = !query || hay.indexOf(query) >= 0;
      var ok = okG && okQ;
      el.classList.toggle('hide', !ok);
      if (ok) shown++;
    });
    countEl.textContent = shown + ' / ' + SCREENS.length + ' 화면';
    emptyEl.style.display = shown ? 'none' : 'block';
  }

  /* ---------- 툴바 ---------- */

  document.getElementById('viewGallery').addEventListener('click', function () {
    setView('gallery');
  });
  document.getElementById('viewSingle').addEventListener('click', function () {
    setView('single');
  });

  function setView(mode) {
    doc.setAttribute('data-view', mode);
    document.getElementById('viewGallery').setAttribute('aria-pressed', String(mode === 'gallery'));
    document.getElementById('viewSingle').setAttribute('aria-pressed', String(mode === 'single'));
    if (mode === 'single' && !document.querySelector('.screen.active')) {
      var first = SCREENS[0];
      if (first) activate(first.id);
    }
  }

  function activate(id) {
    Array.prototype.forEach.call(document.querySelectorAll('.screen'), function (el) {
      el.classList.toggle('active', el.id === id);
    });
  }

  document.getElementById('notesToggle').addEventListener('click', function () {
    var on = doc.getAttribute('data-notes') === 'on';
    doc.setAttribute('data-notes', on ? 'off' : 'on');
    this.setAttribute('aria-pressed', String(!on));
    this.textContent = on ? '개발 주석 숨김' : '개발 주석 표시';
  });

  groupChips.addEventListener('click', function (e) {
    var btn = e.target.closest('[data-group]');
    if (!btn) return;
    activeGroup = btn.getAttribute('data-group');
    Array.prototype.forEach.call(groupChips.querySelectorAll('[data-group]'), function (b) {
      b.setAttribute('aria-pressed', String(b === btn));
    });
    applyFilter();
  });

  search.addEventListener('input', function () {
    query = this.value.trim().toLowerCase();
    applyFilter();
  });

  navList.addEventListener('click', function (e) {
    var a = e.target.closest('a[data-id]');
    if (!a) return;
    if (doc.getAttribute('data-view') === 'single') {
      e.preventDefault();
      activate(a.getAttribute('data-id'));
      window.scrollTo(0, 0);
    }
  });

  /* ---------- 프레임 내부 데모 인터랙션 ----------
     와이어프레임이므로 실제 로직은 없다. 선택 상태가 어떻게 보이는지만 확인용. */

  grid.addEventListener('click', function (e) {
    // 라디오형 선택지: 같은 부모 안에서 하나만 on
    var opt = e.target.closest('.opt');
    if (opt && !opt.classList.contains('chk')) {
      Array.prototype.forEach.call(opt.parentNode.children, function (c) {
        if (c.classList && c.classList.contains('opt')) c.classList.remove('on');
      });
      opt.classList.add('on');
      return;
    }
    if (opt && opt.classList.contains('chk')) {
      opt.classList.toggle('on');
      return;
    }
    // 필터 칩 토글
    var chip = e.target.closest('.chip[data-toggle]');
    if (chip) { chip.classList.toggle('on'); return; }
    // 화면 간 이동 링크
    var go = e.target.closest('[data-goto]');
    if (go) {
      var target = document.getElementById(go.getAttribute('data-goto'));
      if (target) {
        if (doc.getAttribute('data-view') === 'single') { activate(go.getAttribute('data-goto')); window.scrollTo(0, 0); }
        else target.scrollIntoView({ behavior: 'smooth', block: 'start' });
      }
    }
  });

  render();
})();
