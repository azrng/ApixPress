/* 本文件只操作模拟数据，展示交互修复的验收基线。 */
(function () {
  'use strict';
  function errorFor(input, message) {
    var id = (input.id || 'request-url') + '-error';
    var error = document.getElementById(id);
    if (!error) {
      error = document.createElement('div');
      error.id = id;
      error.className = 'interaction-error';
      error.setAttribute('role', 'alert');
      (input.closest('.url-row') || input).insertAdjacentElement('afterend', error);
    }
    error.textContent = message;
    input.classList.toggle('interaction-invalid', !!message);
    input.setAttribute('aria-invalid', String(!!message));
    input.setAttribute('aria-describedby', id);
    if (message) input.focus();
  }

  var closed = [];
  var bar = document.querySelector('.inner-tabbar');
  if (bar) {
    var menu = document.createElement('details');
    menu.className = 'interaction-tab-menu';
    menu.innerHTML = '<summary>所有标签</summary><div class="menu"><button class="btn btn-ghost" data-restore-tab>恢复关闭的标签 · Ctrl+Shift+T</button><div data-open-tabs></div></div>';
    bar.appendChild(menu);
    function updateTabs() {
      var list = menu.querySelector('[data-open-tabs]');
      list.replaceChildren();
      bar.querySelectorAll('.inner-tab').forEach(function (tab) {
        var button = document.createElement('button');
        button.className = 'btn btn-ghost';
        button.textContent = tab.textContent;
        button.onclick = function () { tab.scrollIntoView({block:'nearest', inline:'nearest'}); tab.classList.add('active'); menu.open = false; };
        list.appendChild(button);
      });
      menu.querySelector('[data-restore-tab]').disabled = closed.length === 0;
    }
    function restore() {
      var tab = closed.pop();
      if (!tab) return;
      bar.insertBefore(tab, menu);
      updateTabs();
      protoToast('已恢复标签及未保存内容（仅当前项目会话内保留最近 10 项）');
    }
    document.addEventListener('click', function (e) {
      var close = e.target.closest('.inner-tab .close');
      if (!close) return;
      e.preventDefault(); e.stopImmediatePropagation();
      var tab = close.closest('.inner-tab');
      closed.push(tab); if (closed.length > 10) closed.shift();
      tab.remove(); updateTabs();
      protoToast('标签已关闭，可在所有标签中恢复，或按 Ctrl+Shift+T');
    }, true);
    menu.querySelector('[data-restore-tab]').onclick = restore;
    document.addEventListener('keydown', function (e) {
      if (e.ctrlKey && e.shiftKey && e.key.toLowerCase() === 't') { e.preventDefault(); restore(); }
    });
    updateTabs();
  }

  document.querySelectorAll('.resp-search').forEach(function (input) {
    var card = input.closest('.resp-card');
    var body = card.querySelector('[data-tabs-panel="resp"][data-v="Body"] .resp-body');
    if (!body) return;
    var source = body.textContent, matches = [], current = -1;
    var nav = document.createElement('span');
    nav.className = 'interaction-search-nav';
    nav.innerHTML = '<span role="status"></span><button class="btn btn-ghost" title="上一个匹配 · Shift+Enter">↑</button><button class="btn btn-ghost" title="下一个匹配 · Enter">↓</button>';
    input.insertAdjacentElement('afterend', nav);
    input.placeholder = '搜索当前展示正文';
    function move(step) {
      if (!matches.length) return;
      current = (current + step + matches.length) % matches.length;
      matches.forEach(function (el, i) { el.classList.toggle('interaction-current', i === current); });
      matches[current].scrollIntoView({block:'nearest'});
      nav.firstChild.textContent = (current + 1) + ' / ' + matches.length;
    }
    input.oninput = function () {
      var term = input.value, cursor = 0, index;
      body.replaceChildren(); matches = []; current = -1;
      while (term && (index = source.toLowerCase().indexOf(term.toLowerCase(), cursor)) >= 0) {
        body.appendChild(document.createTextNode(source.slice(cursor, index)));
        var mark = document.createElement('mark'); mark.className = 'interaction-match'; mark.textContent = source.slice(index, index + term.length);
        body.appendChild(mark); matches.push(mark); cursor = index + term.length;
      }
      body.appendChild(document.createTextNode(source.slice(cursor)));
      nav.firstChild.textContent = term ? '未找到匹配' : '';
      nav.querySelectorAll('button').forEach(function (button) { button.disabled = !matches.length; });
      if (matches.length) move(1);
    };
    nav.querySelectorAll('button')[0].onclick = function () { move(-1); };
    nav.querySelectorAll('button')[1].onclick = function () { move(1); };
    input.onkeydown = function (e) { if (e.key === 'Enter') { e.preventDefault(); move(e.shiftKey ? -1 : 1); } };
  });

  document.querySelectorAll('.h-actions button').forEach(function (button) { button.setAttribute('aria-label', button.title); });
  var url = document.getElementById('urlInput');
  var name = document.getElementById('quickName');
  var savedName = '';
  if (url && name) {
    var save = document.querySelector('[data-open="#saveQuickModal"]');
    var saveAs = document.createElement('button');
    saveAs.className = 'btn btn-ghost'; saveAs.textContent = '另存为'; saveAs.hidden = true;
    save.insertAdjacentElement('afterend', saveAs);
    saveAs.onclick = function () { document.getElementById('saveQuickModal').classList.add('open'); name.focus(); };
    function validateUrl() {
      var valid = /^https?:\/\/[^\s/]+/i.test(url.value.trim());
      errorFor(url, valid ? '' : '请输入 http:// 或 https:// 开头的完整地址。'); return valid;
    }
    document.addEventListener('click', function (e) {
      if (e.target.closest('#sendBtn') && !validateUrl()) { e.preventDefault(); e.stopImmediatePropagation(); }
      if (e.target.closest('[data-open="#saveQuickModal"]')) {
        if (!validateUrl()) { e.preventDefault(); e.stopImmediatePropagation(); return; }
        if (savedName) { e.preventDefault(); e.stopImmediatePropagation(); protoToast('已更新快捷请求「' + savedName + '」'); }
      }
      if (e.target.closest('#quickSaveBtn')) {
        if (!name.value.trim()) { e.preventDefault(); e.stopImmediatePropagation(); errorFor(name, '请输入快捷请求名称。'); return; }
        errorFor(name, ''); savedName = name.value.trim(); saveAs.hidden = false;
      }
    }, true);
    url.addEventListener('input', function () { errorFor(url, ''); });
    name.addEventListener('input', function () { errorFor(name, ''); });
    document.addEventListener('keydown', function (e) {
      if (e.ctrlKey && e.key.toLowerCase() === 's') { e.preventDefault(); if (e.shiftKey) saveAs.click(); else save.click(); }
    });
  }
})();
