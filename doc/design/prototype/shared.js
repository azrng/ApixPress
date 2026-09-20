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

  /* Body 模式 → 内容类型提示文案（与 body-modes 同行展示） */
  var bodyModeHints = {
    "none": "不发送请求体。",
    "form-data": "按字段组合为 multipart/form-data 请求体。",
    "urlencode": "按字段进行 URL 编码，适合传统表单接口。",
    "json": "以 application/json 发送原始 JSON 内容。",
    "xml": "以 application/xml 发送原始 XML 内容。",
    "raw": "以 text/plain 发送纯文本内容。",
    "binary": "选择单个文件作为请求体。"
  };

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
    if (name === "bd") {
      var hint = group.querySelector(".body-mode-hint");
      if (hint && bodyModeHints[value]) hint.textContent = bodyModeHints[value];
    }
  }

  function closeLayer(el) { if (el) el.classList.remove("open"); }

  /* 环境状态在各个原型页面之间保持一致，模拟工作区级环境上下文。 */
  var defaultEnvBaseUrls = {
    "开发": "http://localhost:5050",
    "测试": "https://test.example.com",
    "生产": "https://api.example.com"
  };

  function saveEnvironmentState(name, baseUrl) {
    try {
      sessionStorage.setItem("apixpress-prototype-env", JSON.stringify({ name: name, baseUrl: baseUrl || defaultEnvBaseUrls[name] || "" }));
    } catch (ignore) { }
  }

  function applyEnvironmentState(name, baseUrl) {
    document.querySelectorAll(".env-switch").forEach(function (envSwitch) {
      var item = envSwitch.querySelector('.env-item[data-name="' + name + '"]');
      if (!item) return;
      envSwitch.querySelectorAll(".env-item").forEach(function (el) { el.classList.remove("active"); });
      item.classList.add("active");
      var chipName = envSwitch.querySelector(".env-chip-name");
      if (chipName) chipName.textContent = name;
    });

    document.querySelectorAll(".env-chip-name").forEach(function (el) { el.textContent = name; });
    var legacyChipName = document.getElementById("envChipName");
    if (legacyChipName) legacyChipName.textContent = name;
    if (baseUrl) {
      document.querySelectorAll(".base-chip").forEach(function (el) { el.textContent = baseUrl; });
    }
  }

  function restoreEnvironmentState() {
    try {
      var saved = JSON.parse(sessionStorage.getItem("apixpress-prototype-env") || "null");
      if (saved && saved.name) applyEnvironmentState(saved.name, saved.baseUrl);
    } catch (ignore) { }
  }

  function restoreProjectState() {
    try {
      var queryProject = new URLSearchParams(location.search).get("project");
      var saved = JSON.parse(sessionStorage.getItem("apixpress-prototype-project") || "null");
      var projectName = queryProject || (saved && saved.name);
      if (!projectName) return;
      if (queryProject) {
        sessionStorage.setItem("apixpress-prototype-project", JSON.stringify({
          name: projectName,
          remark: saved && saved.remark ? saved.remark : ""
        }));
      }
      document.querySelectorAll(".tb-tab.active").forEach(function (tab) {
        if (tab.querySelector(".default-badge")) tab.childNodes[0].nodeValue = projectName + " ";
      });
    } catch (ignore) { }
  }

  /* 将项目级标签与项目内接口标签拆成两个层级。原型页面仍复用同一份静态标题栏数据。 */
  function splitWorkspaceTabs() {
    var titlebar = document.querySelector(".titlebar");
    var shell = document.querySelector(".shell");
    if (!titlebar || !shell) return;

    /* 项目内标签行只嵌入内容列顶部（对齐 Apifox：侧栏不被标签条压住）；
       首页 / 项目设置 / 导入导出等无 .main 的页面不展示该行 */
    var mainColumn = shell.querySelector(":scope > .main");
    if (!mainColumn) return;

    var tabs = Array.prototype.slice.call(titlebar.querySelectorAll(":scope > .tb-tab"));
    var innerTabs = tabs.filter(function (tab) {
      var scope = tab.getAttribute("data-tab-scope");
      var href = tab.getAttribute("data-href") || "";
      var text = tab.textContent.trim();
      return scope === "workspace"
        || (!scope && !tab.querySelector(".default-badge") && href !== "01-home.html" && text.indexOf("首页") !== 0);
    });

    tabs.forEach(function (tab) {
      if (tab.getAttribute("data-tab-scope") === "directory" && tab.querySelector(".default-badge")) {
        tab.classList.add("project-tab");
      }
    });

    if (innerTabs.length === 0) return;

    var innerTabbar = document.createElement("div");
    innerTabbar.className = "inner-tabbar";
    innerTabbar.setAttribute("aria-label", "项目内标签");

    if (!innerTabs.some(function (tab) { return tab.classList.contains("active"); })) {
      innerTabs[innerTabs.length - 1].classList.add("active");
    }

    innerTabs.forEach(function (tab) {
      tab.classList.add("inner-tab");
      innerTabbar.appendChild(tab);
    });

    var plus = titlebar.querySelector(":scope > .tb-plus");
    if (plus) innerTabbar.appendChild(plus);
    mainColumn.insertBefore(innerTabbar, mainColumn.firstChild);
  }

  window.protoSetEnvironment = function (name, baseUrl) {
    var resolvedBaseUrl = baseUrl || defaultEnvBaseUrls[name] || "";
    saveEnvironmentState(name, resolvedBaseUrl);
    applyEnvironmentState(name, resolvedBaseUrl);
  };

  restoreEnvironmentState();
  restoreProjectState();
  splitWorkspaceTabs();

  document.addEventListener("click", function (e) {
    var t = e.target;

    /* 点击下拉以外区域时收起环境下拉 */
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
      window.protoSetEnvironment(envItem.getAttribute("data-name"));
      toast("已切换到环境「" + envItem.getAttribute("data-name") + "」（演示）");
      return;
    }

    /* 单选项：导入冲突策略等互斥选项 */
    var radio = t.closest("[data-choice-group] .radio");
    if (radio) {
      var choiceGroup = radio.closest("[data-choice-group]");
      choiceGroup.querySelectorAll(".radio").forEach(function (el) { el.classList.remove("on"); });
      radio.classList.add("on");
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

  /* 保存用例后：把用例节点挂到树中选中的接口节点下（纯叶子行升级为可展开节点） */
  window.protoAttachCaseToSelectedTree = function (name) {
    var row = document.querySelector(".tree .tree-row.selected");
    if (!row) return;
    var titleEl = row.querySelector(".t");
    if (!titleEl) return;
    var text = titleEl.textContent;
    var m = text.match(/^(.*?)\s*\((\d+)\)$/);
    if (m) titleEl.textContent = m[1] + " (" + (parseInt(m[2], 10) + 1) + ")";
    else titleEl.textContent = text + " (1)";

    var node = row.closest(".tree-node");
    var children = node ? node.querySelector(":scope > .tree-children") : null;
    if (!children) {
      row.setAttribute("data-toggle", "");
      var caret = document.createElement("span");
      caret.className = "caret open";
      caret.textContent = "▸";
      row.insertBefore(caret, row.firstChild);
      children = document.createElement("div");
      children.className = "tree-children open";
      node.appendChild(children);
    }
    if (!row.querySelector(".status-dot")) {
      var dot = document.createElement("span");
      dot.className = "status-dot";
      dot.title = "该接口有用例";
      row.appendChild(dot);
    }
    var caseRow = document.createElement("div");
    caseRow.className = "tree-row case-node";
    caseRow.style.paddingLeft = (parseInt(row.style.paddingLeft, 10) + 24) + "px";
    var glyph = document.createElement("span");
    glyph.className = "case-glyph";
    glyph.textContent = "⎿";
    var t = document.createElement("span");
    t.className = "t";
    t.textContent = name;
    caseRow.appendChild(glyph);
    caseRow.appendChild(t);
    children.appendChild(caseRow);
  };

  window.protoToast = toast;
})();
