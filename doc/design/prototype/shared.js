/* ============================================================
   ApixPress 原型共享交互脚本（仅演示用途，不承载真实业务逻辑）
   约定：
     [data-open="#id"]            打开抽屉/弹窗层（.drawer-layer / .modal-wrap）
     [data-close]                 关闭所在层
     [data-close-toast="文案"]    关闭所在层并弹出提示
     [data-toast="文案"]          点击弹出提示
     [data-href="url"]            点击跳转页面
     [data-msg="说明"]            打开层时把说明写入层内 [data-msg-text]
     [data-tabs="组名"]           视觉 Tab 组（.sub-tab/.mode-btn/.body-mode/.code-tab）
     [data-tabs-panel="组名"]     对应面板，data-v 与选中 Tab 一致时显示
   ============================================================ */
(function () {
  "use strict";

  /* 轻提示 */
  var toastTimer = null;
  function toast(msg) {
    var el = document.querySelector(".proto-toast");
    if (!el) {
      el = document.createElement("div");
      el.className = "proto-toast";
      document.body.appendChild(el);
    }
    el.textContent = msg;
    el.classList.add("show");
    clearTimeout(toastTimer);
    toastTimer = setTimeout(function () { el.classList.remove("show"); }, 2200);
  }

  /* Tab 组：视觉单选 + 面板切换 */
  function activateTab(tab) {
    var group = tab.closest("[data-tabs]");
    if (!group) return;
    var name = group.getAttribute("data-tabs");
    var value = tab.getAttribute("data-v");
    if (!value) return;
    Array.prototype.forEach.call(group.children, function (el) {
      el.classList.remove("active", "checked", "on");
    });
    if (tab.classList.contains("mode-btn")) tab.classList.add("checked");
    else if (tab.classList.contains("body-mode") || tab.classList.contains("code-tab")) tab.classList.add("on");
    else tab.classList.add("active");
    Array.prototype.forEach.call(document.querySelectorAll('[data-tabs-panel="' + name + '"]'), function (p) {
      p.classList.toggle("on", p.getAttribute("data-v") === value);
    });
  }

  function closeLayer(el) { if (el) el.classList.remove("open"); }

  document.addEventListener("click", function (e) {
    var t = e.target;

    /* 点击环境下拉以外区域时收起下拉 */
    if (!t.closest(".env-switch")) {
      document.querySelectorAll(".env-switch.open").forEach(function (el) {
        el.classList.remove("open");
      });
    }

    /* 环境下拉：展开/收起 */
    var envChip = t.closest(".env-chip");
    if (envChip && envChip.closest(".env-switch")) {
      envChip.closest(".env-switch").classList.toggle("open");
      return;
    }
    /* 环境下拉：选择环境 */
    var envItem = t.closest(".env-item");
    if (envItem) {
      var envSwitch = envItem.closest(".env-switch");
      envSwitch.querySelectorAll(".env-item").forEach(function (el) {
        el.classList.remove("active");
      });
      envItem.classList.add("active");
      var chipName = envSwitch.querySelector(".env-chip-name");
      if (chipName) chipName.textContent = envItem.getAttribute("data-name");
      envSwitch.classList.remove("open");
      toast("已切换到环境「" + envItem.getAttribute("data-name") + "」（演示）");
      return;
    }

    /* 勾选框开关 */
    var cb = t.closest(".checkbox");
    if (cb) { cb.classList.toggle("on"); return; }

    /* Tab 组 */
    var tab = t.closest(".sub-tab, .mode-btn, .body-mode, .code-tab");
    if (tab && tab.parentElement.hasAttribute("data-tabs")) { activateTab(tab); return; }

    /* 关闭层并提示 */
    var ct = t.closest("[data-close-toast]");
    if (ct) { closeLayer(ct.closest(".drawer-layer, .modal-wrap")); toast(ct.getAttribute("data-close-toast")); return; }

    /* 关闭层 */
    var closer = t.closest("[data-close]");
    if (closer) { closeLayer(closer.closest(".drawer-layer, .modal-wrap")); return; }

    /* 点击遮罩空白处关闭弹窗 */
    if (t.classList.contains("modal-wrap")) { closeLayer(t); return; }
    if (t.classList.contains("overlay-dim")) { closeLayer(t.closest(".drawer-layer, .modal-wrap")); return; }

    /* 打开层（可携带说明文案） */
    var opener = t.closest("[data-open]");
    if (opener) {
      var layer = document.querySelector(opener.getAttribute("data-open"));
      if (layer) {
        var msg = opener.getAttribute("data-msg");
        if (msg) {
          var titleEl = layer.querySelector("[data-msg-title]");
          var textEl = layer.querySelector("[data-msg-text]");
          if (titleEl) titleEl.textContent = opener.getAttribute("data-msg-title") || opener.textContent.trim();
          if (textEl) textEl.textContent = msg;
        }
        layer.classList.add("open");
      }
      return;
    }

    /* 页面跳转 */
    var jump = t.closest("[data-href]");
    if (jump) { location.href = jump.getAttribute("data-href"); return; }

    /* 演示提示 */
    var hint = t.closest("[data-toast]");
    if (hint) { toast(hint.getAttribute("data-toast")); return; }

    /* 目录树：展开/折叠 + 单选 */
    var treeRow = t.closest(".tree-row");
    if (treeRow) {
      // 行内「⋯」按钮只弹提示，不触发展开/选中
      if (t.closest(".row-more")) return;
      if (treeRow.hasAttribute("data-toggle")) {
        var node = treeRow.closest(".tree-node");
        if (node) {
          var children = node.querySelector(":scope > .tree-children");
          var caret = treeRow.querySelector(".caret");
          if (children) children.classList.toggle("open");
          if (caret) caret.classList.toggle("open");
        }
      }
      var tree = treeRow.closest(".tree");
      if (tree) {
        tree.querySelectorAll(".tree-row.selected").forEach(function (el) {
          el.classList.remove("selected");
        });
        treeRow.classList.add("selected");
      }
      return;
    }

    /* 左导航 / 历史条目：单选高亮 */
    var item = t.closest(".nav-item, .h-item");
    if (item && item.parentElement) {
      item.parentElement.querySelectorAll(".selected").forEach(function (el) {
        el.classList.remove("selected");
      });
      item.classList.add("selected");
      return;
    }
  });

  window.protoToast = toast;
})();
