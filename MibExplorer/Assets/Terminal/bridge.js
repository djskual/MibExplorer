"use strict";
// xterm.js lives entirely in the WebView2 renderer. Windows forwards only
// trusted terminal data / dimensions to the existing SSH.NET ShellStream.
(() => {
  const host = window.chrome?.webview;
  const root = document.getElementById("terminal");
  if (!host || !root || !window.Terminal || !window.FitAddon?.FitAddon) {
    root.textContent = "Missing local terminal library. Reinstall the complete MibExplorer package.";
    return;
  }

  const term = new window.Terminal({
    cursorBlink: true,
    cursorStyle: "block",
    allowTransparency: false,
    convertEol: false,
    scrollback: 10000,
    fontFamily: "Consolas, 'Cascadia Mono', monospace",
    fontSize: 13,
    lineHeight: 1.18,
    theme: {
      background: "#101820",
      foreground: "#dce6ed",
      cursor: "#ffffff",
      selectionBackground: "#506c83",
      black: "#101820",
      blue: "#80c7ff",
      green: "#91d9a0",
      red: "#ff9a90"
    }
  });
  const fit = new window.FitAddon.FitAddon();
  term.loadAddon(fit);
  term.open(root);
  const tell = (msg) => host.postMessage(msg);

  // Raw input. Sending newline for every key would break interactive apps.
  term.onData(data => tell({ type: "input", data }));
  term.onBinary(data => {
    let binary = "";
    for (let i = 0; i < data.length; i++) binary += String.fromCharCode(data.charCodeAt(i) & 0xff);
    tell({ type: "binary", data: btoa(binary) });
  });
  term.onResize(size => tell({ type: "resize", cols: size.cols, rows: size.rows }));

  // Clipboard shortcuts are owned by the Windows WPF host, not Chromium.
  // Copy when selected; otherwise Ctrl+C must still reach QNX as SIGINT.
  function copySelection(value = term.getSelection()) {
    if (value) tell({ type: "clipboardCopy", data: value });
  }

  function requestPaste() {
    tell({ type: "clipboardPaste" });
  }

  // Capture before xterm.js and Chromium clipboard accelerators. Keyboard
  // input that isn't a shortcut still goes through term.onData unchanged.
  document.addEventListener("keydown", event => {
    if (!root.contains(event.target) || event.isComposing || event.altKey || event.metaKey) return;
    const key = event.key.toLowerCase();
    const selected = term.hasSelection();
    const copy = (event.ctrlKey && key === "c" && (selected || event.shiftKey)) ||
                 (event.ctrlKey && key === "insert");
    const paste = (event.ctrlKey && key === "v") ||
                  (event.shiftKey && !event.ctrlKey && key === "insert");
    const clear = event.ctrlKey && !event.shiftKey && key === "l";

    if (copy || paste || clear) {
      event.preventDefault();
      event.stopImmediatePropagation();
      if (copy) copySelection();
      else if (paste) requestPaste();
      else term.clear();
    }
  }, true);

  // xterm.js has its own keyboard handler. Suppress its matching shortcuts,
  // but deliberately allow Ctrl+C without a selection to send 0x03 to QNX.
  term.attachCustomKeyEventHandler(event => {
    if (event.altKey || event.metaKey) return true;
    const key = event.key.toLowerCase();
    if (event.ctrlKey && key === "c" && (term.hasSelection() || event.shiftKey)) return false;
    if (event.ctrlKey && (key === "v" || key === "insert" || (key === "l" && !event.shiftKey))) return false;
    if (event.shiftKey && !event.ctrlKey && key === "insert") return false;
    return true;
  });

  // In-page menu is independent of Chromium's (disabled) context menu.
  const menu = document.getElementById("terminal-menu");
  const menuCopy = menu?.querySelector('[data-action="copy"]');
  let selectionAtRightClick = "";

  function closeMenu() {
    if (menu) menu.hidden = true;
  }

  root.addEventListener("contextmenu", event => {
    event.preventDefault();
    if (!menu) return;
    selectionAtRightClick = term.getSelection();
    if (menuCopy) menuCopy.disabled = !selectionAtRightClick;
    menu.hidden = false;
    const bounds = menu.getBoundingClientRect();
    menu.style.left = `${Math.max(0, Math.min(event.clientX, window.innerWidth - bounds.width))}px`;
    menu.style.top = `${Math.max(0, Math.min(event.clientY, window.innerHeight - bounds.height))}px`;
  });

  menu?.addEventListener("click", event => {
    const button = event.target.closest("button[data-action]");
    if (!button || button.disabled) return;
    switch (button.dataset.action) {
      case "copy": copySelection(selectionAtRightClick); break;
      case "paste": requestPaste(); break;
      case "selectAll": term.selectAll(); break;
    }
    closeMenu();
    term.focus();
  });

  document.addEventListener("pointerdown", event => {
    if (menu && !menu.contains(event.target)) closeMenu();
  }, true);
  document.addEventListener("keydown", event => {
    if (event.key === "Escape") closeMenu();
  }, true);
  window.addEventListener("blur", closeMenu);

  let resizePending = false;
  function resize() {
    if (resizePending) return;
    resizePending = true;
    requestAnimationFrame(() => {
      resizePending = false;
      try { fit.fit(); } catch (_) { /* no layout yet */ }
    });
  }
  new ResizeObserver(resize).observe(root);
  window.addEventListener("resize", resize);

  window.mibTerminal = {
    focus: () => term.focus(),
    selection: () => term.getSelection(),
    allText: () => {
      const lines = [];
      const buffer = term.buffer.active;
      for (let i = 0; i < buffer.length; i++) {
        const line = buffer.getLine(i);
        if (line) lines.push(line.translateToString(true));
      }
      while (lines.length && lines[lines.length - 1] === "") lines.pop();
      return lines.join("\n");
    }
  };

  host.addEventListener("message", event => {
    const msg = event.data;
    if (!msg || typeof msg.type !== "string") return;
    if (msg.type === "output" && typeof msg.data === "string") term.write(msg.data);
    else if (msg.type === "paste" && typeof msg.data === "string") term.paste(msg.data);
    else if (msg.type === "clear") term.clear();
  });

  resize();
  // WebView2 may expose the font metrics one layout frame after term.open().
  requestAnimationFrame(resize);
  document.fonts?.ready?.then(resize);
  tell({ type: "ready" });
  term.focus();
})();
