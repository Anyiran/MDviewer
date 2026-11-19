/* ============================================================
   MDviewer — in-page runtime
   Handles theme, font scaling, outline sync, link routing.
   Communicates with the host app through window.chrome.webview.
   ============================================================ */
(function () {
  "use strict";

  var root = document.documentElement;
  var host = (window.chrome && window.chrome.webview) ? window.chrome.webview : null;
  var pendingScrollTarget = null;

  // ------------------------------------------------------------- messaging

  function post(msg) {
    if (host && host.postMessage) {
      try { host.postMessage(msg); } catch (e) { /* host gone */ }
    }
  }

  // Surface script errors to the host so they can be logged/diagnosed.
  window.addEventListener("error", function (e) {
    post({ type: "jsError", message: String(e.message || e.error || "unknown") });
  });

  // --------------------------------------------------------------- helpers

  function debounce(fn, wait) {
    var t = null;
    return function () {
      var args = arguments, self = this;
      if (t) clearTimeout(t);
      t = setTimeout(function () { t = null; fn.apply(self, args); }, wait);
    };
  }

  function throttle(fn, wait) {
    var last = 0, timer = null, lastArgs = null;
    return function () {
      var now = Date.now();
      lastArgs = arguments;
      if (now - last >= wait) {
        last = now;
        fn.apply(null, lastArgs);
      } else if (!timer) {
        timer = setTimeout(function () {
          timer = null;
          last = Date.now();
          fn.apply(null, lastArgs);
        }, wait - (now - last));
      }
    };
  }

  // ----------------------------------------------------------------- theme

  function setTheme(theme) {
    if (theme === "dark") {
      root.setAttribute("data-theme", "dark");
    } else {
      root.removeAttribute("data-theme");
    }
  }

  // ------------------------------------------------------------ font scale

  function setFontScale(scale) {
    var s = Number(scale);
    if (!isFinite(s) || s <= 0) s = 1;
    // Keep within a sane range.
    s = Math.min(3, Math.max(0.5, s));
    root.style.setProperty("--mdv-font-scale", String(s));
  }

  // --------------------------------------------------------------- outline

  var headingEls = [];

  function collectHeadings() {
    headingEls = Array.prototype.slice.call(
      document.querySelectorAll(".markdown-body h1[id], .markdown-body h2[id], " +
        ".markdown-body h3[id], .markdown-body h4[id], .markdown-body h5[id], " +
        ".markdown-body h6[id]")
    );
  }

  var lastActiveId = null;

  function reportActiveHeading() {
    if (headingEls.length === 0) {
      if (lastActiveId !== null) {
        lastActiveId = null;
        post({ type: "activeHeading", id: "" });
      }
      return;
    }

    // The heading closest to (but not far past) the top of the viewport wins.
    var offset = 24;
    var current = headingEls[0];
    for (var i = 0; i < headingEls.length; i++) {
      var top = headingEls[i].getBoundingClientRect().top;
      if (top - offset <= 0) {
        current = headingEls[i];
      } else {
        break;
      }
    }

    // Near the very bottom of the document, highlight the last heading.
    var doc = document.documentElement;
    if (window.innerHeight + window.scrollY >= doc.scrollHeight - 4) {
      current = headingEls[headingEls.length - 1];
    }

    var id = current ? current.id : "";
    if (id !== lastActiveId) {
      lastActiveId = id;
      post({ type: "activeHeading", id: id });
    }
  }

  var onScroll = throttle(reportActiveHeading, 90);

  // ------------------------------------------------------------ navigation

  function scrollToId(id) {
    if (!id) {
      window.scrollTo(0, 0);
      return;
    }
    var el = document.getElementById(id);
    if (!el) return;
    var y = el.getBoundingClientRect().top + window.scrollY - 16;
    window.scrollTo(0, Math.max(0, y));
  }

  // Intercept link clicks so the host can decide what to do with them.
  document.addEventListener("click", function (ev) {
    var a = ev.target;
    while (a && a !== document.body) {
      if (a.tagName === "A") break;
      a = a.parentNode;
    }
    if (!a || a.tagName !== "A") return;

    var href = a.getAttribute("href");
    if (!href) return;

    if (href.charAt(0) === "#") {
      ev.preventDefault();
      var id = decodeURIComponent(href.substring(1));
      scrollToId(id);
      if (history && history.replaceState) {
        try { history.replaceState(null, "", "#" + id); } catch (e) { /* ignore */ }
      }
      return;
    }

    ev.preventDefault();
    post({ type: "openLink", href: href });
  }, false);

  // ---------------------------------------------------- right-drag jump

  // Hold the right mouse button and drag up to jump to the top, or drag down to
  // jump to the bottom of the document. A normal right click still opens the
  // context menu because the gesture only fires after a clear vertical drag.
  var rightDrag = { active: false, startX: 0, startY: 0, fired: false };

  function scrollToEdge(top) {
    var target = top ? 0 : Math.max(
      document.body.scrollHeight, document.documentElement.scrollHeight);
    try {
      window.scrollTo({ top: target, behavior: "smooth" });
    } catch (e) {
      window.scrollTo(0, target);
    }
  }

  document.addEventListener("mousedown", function (ev) {
    if (ev.button !== 2) return;
    rightDrag.active = true;
    rightDrag.fired = false;
    rightDrag.startX = ev.clientX;
    rightDrag.startY = ev.clientY;
  }, true);

  document.addEventListener("mousemove", function (ev) {
    if (!rightDrag.active) return;
    if ((ev.buttons & 2) === 0) {
      rightDrag.active = false;
      return;
    }

    var dy = ev.clientY - rightDrag.startY;
    var dx = Math.abs(ev.clientX - rightDrag.startX);
    var threshold = Math.max(36, Math.round(window.innerHeight * 0.07));

    if (rightDrag.fired) return;
    if (dx > threshold * 1.6) {
      // Mostly horizontal: not an up/down gesture.
      rightDrag.active = false;
      return;
    }

    if (dy <= -threshold) {
      rightDrag.fired = true;
      scrollToEdge(true);
    } else if (dy >= threshold) {
      rightDrag.fired = true;
      scrollToEdge(false);
    }
  }, true);

  document.addEventListener("mouseup", function (ev) {
    if (ev.button !== 2) return;
    rightDrag.active = false;
    // Keep the flag until the contextmenu event has been suppressed.
    setTimeout(function () { rightDrag.fired = false; }, 0);
  }, true);

  window.addEventListener("contextmenu", function (ev) {
    if (rightDrag.fired) {
      ev.preventDefault();
      rightDrag.fired = false;
      return;
    }
  }, true);

  // ---------------------------------------------------------- keyboard shortcuts
  // The native host cannot see keys while Chromium has focus, so well-known
  // shortcuts are forwarded as messages for the host to execute.
  document.addEventListener("keydown", function (ev) {
    if (ev.altKey || ev.metaKey) return;
    var action = null;
    var key = ev.key || "";

    if (ev.ctrlKey) {
      switch (key.toLowerCase()) {
        case "o": action = "open"; break;
        case "b": action = "sidebar"; break;
        case "t": action = "theme"; break;
        case "f": action = "find"; break;
        case "0": action = "resetZoom"; break;
        case "+": case "=": action = "zoomIn"; break;
        case "-": action = "zoomOut"; break;
      }
    } else {
      if (key === "F5") action = "reload";
      else if (key === "F1") action = "about";
      else if (key === "Escape") action = "escape";
    }

    if (action) {
      ev.preventDefault();
      ev.stopPropagation();
      post({ type: "shortcut", action: action });
    }
  }, true);

  // Ctrl + wheel font scaling inside the document.
  document.addEventListener("wheel", function (ev) {
    if (!ev.ctrlKey) return;
    ev.preventDefault();
    post({ type: "zoomDelta", delta: ev.deltaY > 0 ? -1 : 1 });
  }, { passive: false });

  // Keep the outline in sync when the viewport changes.
  window.addEventListener("scroll", onScroll, { passive: true });
  window.addEventListener("resize", debounce(reportActiveHeading, 120));

  // ------------------------------------------------------------ host API

  var api = {
    setTheme: setTheme,
    setFontScale: setFontScale,
    scrollToId: scrollToId,
    scrollToTop: function () { window.scrollTo(0, 0); },
    findHighlight: highlightText,
    findClear: clearHighlight,
    findNext: jumpToMatch,

    // Called by the host after new HTML has been injected.
    afterRender: function () {
      collectHeadings();
      lastActiveId = null;

      // Restore a pending anchor (used for in-document jumps across reloads).
      if (pendingScrollTarget) {
        scrollToId(pendingScrollTarget);
        pendingScrollTarget = null;
      } else {
        window.scrollTo(0, 0);
      }

      reportActiveHeading();
      post({ type: "rendered", headings: headingEls.length });
    }
  };

  window.MDviewer = api;

  // ---------------------------------------------------------- host -> page

  if (host && host.addEventListener) {
    host.addEventListener("message", function (ev) {
      var msg = ev.data;
      if (!msg || !msg.type) return;
      switch (msg.type) {
        case "setTheme": setTheme(msg.theme); break;
        case "setFontScale": setFontScale(msg.scale); break;
        case "scrollToId": scrollToId(msg.id); break;
        case "scrollToTop": window.scrollTo(0, 0); break;
        case "afterRender": api.afterRender(); break;
        case "setPendingScroll":
          pendingScrollTarget = msg.id || null;
          break;
        case "findHighlight":
          highlightText(msg.text, msg.matchCase);
          break;
        case "findClear":
          clearHighlight();
          break;
        case "findNext":
          jumpToMatch(msg.direction || 1);
          break;
      }
    });
  }

  // -------------------------------------------------------------- find bar

  var matchNodes = [];
  var matchIndex = -1;

  function clearHighlight() {
    matchNodes = [];
    matchIndex = -1;
  }

  function highlightText(text, matchCase) {
    clearHighlight();
    if (!text) return;

    var body = document.querySelector(".markdown-body");
    if (!body) { post({ type: "findResult", count: 0, index: -1 }); return; }

    var flags = matchCase ? "g" : "gi";
    var re;
    try {
      re = new RegExp(text.replace(/[.*+?^${}()|[\]\\]/g, "\\$&"), flags);
    } catch (e) {
      post({ type: "findResult", count: 0, index: -1 });
      return;
    }

    var walker = document.createTreeWalker(body, NodeFilter.SHOW_TEXT, {
      acceptNode: function (node) {
        if (!node.nodeValue || !node.nodeValue.trim()) return NodeFilter.FILTER_REJECT;
        var p = node.parentNode;
        while (p && p !== body) {
          var tag = p.nodeName;
          if (tag === "SCRIPT" || tag === "STYLE" || tag === "MARK") return NodeFilter.FILTER_REJECT;
          p = p.parentNode;
        }
        return NodeFilter.FILTER_ACCEPT;
      }
    });

    var textNodes = [];
    var n;
    while ((n = walker.nextNode())) {
      if (re.test(n.nodeValue)) {
        textNodes.push(n);
      }
      re.lastIndex = 0;
    }

    textNodes.forEach(function (node) {
      var value = node.nodeValue;
      var frag = document.createDocumentFragment();
      var lastIndex = 0;
      var m;
      re.lastIndex = 0;
      while ((m = re.exec(value)) !== null) {
        if (m.index > lastIndex) {
          frag.appendChild(document.createTextNode(value.substring(lastIndex, m.index)));
        }
        var mark = document.createElement("mark");
        mark.className = "mdv-find-hit";
        mark.style.backgroundColor = "#ffd33d";
        mark.style.color = "#1f2328";
        mark.style.borderRadius = "2px";
        mark.textContent = m[0];
        frag.appendChild(mark);
        matchNodes.push(mark);
        lastIndex = m.index + m[0].length;
        if (m[0].length === 0) re.lastIndex++;
      }
      if (lastIndex < value.length) {
        frag.appendChild(document.createTextNode(value.substring(lastIndex)));
      }
      if (node.parentNode) node.parentNode.replaceChild(frag, node);
    });

    matchIndex = matchNodes.length > 0 ? 0 : -1;
    post({ type: "findResult", count: matchNodes.length, index: matchIndex });
    if (matchIndex >= 0) focusMatch(matchIndex);
  }

  var ACTIVE_COLOR = "#ff9632";

  function focusMatch(index) {
    for (var i = 0; i < matchNodes.length; i++) {
      matchNodes[i].style.backgroundColor = i === index ? ACTIVE_COLOR : "#ffd33d";
    }
    if (index < 0 || index >= matchNodes.length) return;
    var el = matchNodes[index];
    var rect = el.getBoundingClientRect();
    var targetY = rect.top + window.scrollY - window.innerHeight / 2 + rect.height / 2;
    window.scrollTo(0, Math.max(0, targetY));
  }

  function jumpToMatch(direction) {
    if (matchNodes.length === 0) return;
    matchIndex = (matchIndex + (direction || 1) + matchNodes.length) % matchNodes.length;
    focusMatch(matchIndex);
    post({ type: "findResult", count: matchNodes.length, index: matchIndex });
  }

  // Announce readiness so the host can push initial state.
  window.addEventListener("load", function () {
    post({ type: "ready" });
  });
})();
