// TouchControlsWeb.jslib
// ---------------------------------------------------------------------------
// The browser side of the touch controls (TouchControls.cs and
// TouchFullscreenButton.cs; only used in the Web build).
// ---------------------------------------------------------------------------
mergeInto(LibraryManager.library, {

  // ---- The phone's own keyboard ----
  // A phone only shows its keyboard for a text field, and only during a tap
  // (on an iPhone a script cannot open it by itself). So the page gets one
  // invisible text field. While the game wants typing (TouchControls says so
  // with SetKeyboardWanted), a tap on the page puts the cursor in that field
  // and the keyboard opens. Once the player closed it (HIDE KEYBOARD, or the
  // phone's own button), only TAP TO TYPE opens it again, so tapping a power
  // button does not bring it back. That button's taps are handled here, not in
  // Unity: Unity sees a tap a moment after it happened, too late for an iPhone
  // (TouchControls says where the button is with SetTypeButtonRect).
  // What arrives in the field goes to the game (Unity's SendMessage, to the
  // "Touch Controls" object):
  //   OnNativeText("abc")              typed characters (capitals, symbols, space)
  //   OnNativeKey("tap:Backspace")     the keyboard's delete key
  //   OnNativeKey("down:LeftArrow") / OnNativeKey("up:LeftArrow")
  //                                    keys of a real keyboard (an iPad's): the
  //                                    arrows, Escape, Enter, Backspace
  // The field always holds one character ("_") with the cursor after it, so
  // pressing delete with nothing typed still changes it and is noticed. After
  // each change it goes back to "_" (not while a word is being composed:
  // Android keyboards build a word letter by letter in the field, and each new
  // letter is sent).
  TouchControlsWeb_SetUpNativeKeyboard: function (objectNamePtr) {
    if (window.typeblastersKeyboard) {
      return;
    }
    var objectName = UTF8ToString(objectNamePtr);
    var BASE = "_";
    var simulated = /[?&]kbsim=([0-9.]+)/.exec(window.location.search); // tests: pretend a keyboard covers this much
    var input = document.createElement("input");
    input.type = "text";
    input.id = "typeblasters-keyboard";
    input.value = BASE;
    input.setAttribute("autocomplete", "off");
    input.setAttribute("autocorrect", "off");
    input.setAttribute("autocapitalize", "off");
    input.setAttribute("spellcheck", "false");
    input.setAttribute("enterkeyhint", "done");
    input.setAttribute("aria-hidden", "true");
    // Invisible but focusable, in the top-left corner (so the page never
    // scrolls to show it). 16px text: an iPhone zooms into smaller fields.
    // Text selection on: the page turns it off, and an iPhone cannot type
    // into a field without it.
    input.style.cssText = "position:fixed;left:0;top:0;width:1px;height:1px;padding:0;border:0;margin:0;"
      + "opacity:0;font-size:16px;color:transparent;background:transparent;caret-color:transparent;"
      + "pointer-events:none;z-index:-1;-webkit-user-select:text;user-select:text;";
    document.body.appendChild(input);

    var state = {
      input: input,
      wanted: false,          // the game wants typing (playing)
      closedByPlayer: false,  // the player closed the keyboard: only TAP TO TYPE opens it
      keyboardSeen: false,    // the keyboard has covered the page since the field got the cursor
      seenWidth: 0,           // the page width then (a new width = the phone turned)
      hiddenWhileFocused: false, // Android put the keyboard away but the cursor stayed in the field
      closeTimer: null,       // waiting to be sure the keyboard went away
      fullHeights: {},        // the page's full height (no keyboard) for each page width seen
      typeButton: null,       // TAP TO TYPE: { left, bottom, right, top } as fractions of the canvas
      typeTouch: null,        // the finger that went down on TAP TO TYPE
      typeMouse: false,       // the mouse button went down on it (computers, ?touch=1)
      openAtPress: false,     // the keyboard was open when that finger went down
      lastTouchTime: 0,       // a phone may copy a tap as mouse events afterwards: those are ignored
      last: BASE,
      composing: false
    };
    window.typeblastersKeyboard = state;

    function send(method, value) {
      try {
        SendMessage(objectName, method, value);
      } catch (error) {
        // the game is not running (page closing)
      }
    }
    function reset() {
      if (state.composing) {
        return;
      }
      input.value = BASE;
      state.last = BASE;
      try {
        input.setSelectionRange(BASE.length, BASE.length);
      } catch (error) {
      }
    }

    // How much of the canvas the keyboard covers from the bottom (0 to 1).
    // The visible part of the page is the "visual viewport".
    state.coveredBottom = function () {
      var canvas = Module["canvas"];
      if (!canvas || document.activeElement !== input) {
        return 0;
      }
      if (simulated) {
        return parseFloat(simulated[1]);
      }
      var view = window.visualViewport;
      if (!view) {
        return 0;
      }
      var rect = canvas.getBoundingClientRect();
      var visibleBottom = view.offsetTop + view.height;
      return rect.height > 0 ? Math.max(0, Math.min(1, (rect.bottom - visibleBottom) / rect.height)) : 0;
    };

    // Typed text: what changed between the field's old and new text. The part
    // both start with and the part both end with stayed the same; what was
    // between them was removed (the delete key) and replaced (typed).
    input.addEventListener("input", function () {
      state.hiddenWhileFocused = false; // typing: the keyboard is there (a floating one, say)
      var now = input.value;
      var last = state.last;
      var start = 0;
      while (start < now.length && start < last.length && now.charAt(start) === last.charAt(start)) {
        start++;
      }
      var end = 0;
      while (end < now.length - start && end < last.length - start
        && now.charAt(now.length - 1 - end) === last.charAt(last.length - 1 - end)) {
        end++;
      }
      for (var removed = last.length - start - end; removed > 0; removed--) {
        send("OnNativeKey", "tap:Backspace");
      }
      var added = now.substring(start, now.length - end);
      if (added.length > 0) {
        send("OnNativeText", added);
      }
      state.last = now;
      reset();
    });
    input.addEventListener("compositionstart", function () {
      state.composing = true;
    });
    input.addEventListener("compositionend", function () {
      state.composing = false;
      setTimeout(reset, 0);
    });

    // The cursor stays at the end of the field (a phone can move it: an
    // iPhone's space bar held down, a swipe on Android's space bar).
    document.addEventListener("selectionchange", function () {
      if (document.activeElement !== input || state.composing) {
        return;
      }
      var end = input.value.length;
      if (input.selectionStart !== end || input.selectionEnd !== end) {
        try {
          input.setSelectionRange(end, end);
        } catch (error) {
        }
      }
    });

    // Keys of a real keyboard that the game reads as keys, not as text.
    var KEYS = {
      "Backspace": "Backspace", "Escape": "Escape", "Enter": "Enter",
      "ArrowLeft": "LeftArrow", "ArrowRight": "RightArrow", "ArrowUp": "UpArrow", "ArrowDown": "DownArrow"
    };
    input.addEventListener("keydown", function (event) {
      var key = KEYS[event.key];
      if (!key) {
        return;
      }
      event.preventDefault();
      if (!event.repeat) {
        send("OnNativeKey", "down:" + key);
      }
    });
    input.addEventListener("keyup", function (event) {
      var key = KEYS[event.key];
      if (key) {
        event.preventDefault();
        send("OnNativeKey", "up:" + key);
      }
    });

    // The keyboard closed while the game still wants typing: the player
    // closed it. An iPhone may leave the page moved up: move it back.
    input.addEventListener("focus", function () {
      state.keyboardSeen = false;
      state.hiddenWhileFocused = false;
    });
    input.addEventListener("blur", function () {
      if (state.wanted) {
        state.closedByPlayer = true;
      }
      state.keyboardSeen = false;
      state.hiddenWhileFocused = false;
      clearTimeout(state.closeTimer);
      state.closeTimer = null;
      window.scrollTo(0, 0);
    });

    // Android puts its keyboard away (the Back button, its hide key) without
    // taking the cursor out of the field. Once the keyboard has been seen
    // covering the page, the page getting all its height back, and staying
    // so for a moment (the phone turning also moves it), means it went away:
    // the game then shows TAP TO TYPE (IsKeyboardOpen says 0), which brings
    // it back. The cursor stays in the field, because a floating keyboard
    // looks the same and must keep typing. An iPhone or iPad takes the
    // cursor out itself when its keyboard closes, so this is not needed there.
    // How much of the page the keyboard takes: what it covers, or (in the
    // browser built into an app) how much shorter the page itself is than
    // its full height at this width (learned while nobody types).
    function learnFullHeight() {
      if (document.activeElement !== input) {
        state.fullHeights[window.innerWidth] = window.innerHeight;
      }
    }
    function keyboardShare() {
      learnFullHeight();
      var full = state.fullHeights[window.innerWidth];
      var shorter = full ? Math.max(0, 1 - window.innerHeight / full) : 0;
      return Math.max(state.coveredBottom(), shorter);
    }
    function confirmHidden() {
      state.closeTimer = null;
      if (document.activeElement === input && state.keyboardSeen
        && window.innerWidth === state.seenWidth && keyboardShare() < 0.05) {
        state.keyboardSeen = false;
        state.hiddenWhileFocused = true;
      }
    }
    function watchKeyboard() {
      var share = keyboardShare();
      if (document.activeElement !== input) {
        return;
      }
      if (state.keyboardSeen && window.innerWidth !== state.seenWidth) {
        state.keyboardSeen = false; // the phone turned: measure again
      }
      if (share > 0.2) {
        state.keyboardSeen = true;
        state.seenWidth = window.innerWidth;
        state.hiddenWhileFocused = false;
        clearTimeout(state.closeTimer);
        state.closeTimer = null;
      } else if (state.keyboardSeen && share < 0.05 && state.closeTimer === null) {
        state.closeTimer = setTimeout(confirmHidden, 400);
      }
    }
    var apple = /iPhone|iPad|iPod/.test(navigator.userAgent)
      || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent));
    if (!apple && !simulated) {
      learnFullHeight();
      window.addEventListener("resize", watchKeyboard);
      if (window.visualViewport) {
        window.visualViewport.addEventListener("resize", watchKeyboard);
      }
      // Full screen, split screen or a pop-up window change the full heights.
      document.addEventListener("fullscreenchange", function () {
        state.fullHeights = {};
        learnFullHeight();
      });
      document.addEventListener("visibilitychange", function () {
        if (!document.hidden) {
          state.fullHeights = {};
          learnFullHeight();
        }
      });
    }

    function open() {
      if (document.activeElement !== input) {
        input.focus({ preventScroll: true });
        reset();
      }
    }
    // A tap while the game wants typing opens the keyboard (unless the player
    // closed it).
    function openOnTap() {
      if (state.wanted && !state.closedByPlayer) {
        open();
      }
    }
    state.open = openOnTap;

    // TAP TO TYPE / HIDE KEYBOARD, when the finger lifts: what it does depends
    // on whether the keyboard was open when the finger went down.
    function onTypeButton(clientX, clientY) {
      var canvas = Module["canvas"];
      var button = state.typeButton;
      if (!state.wanted || !button || !canvas) {
        return false;
      }
      var rect = canvas.getBoundingClientRect();
      if (rect.width <= 0 || rect.height <= 0) {
        return false;
      }
      var x = (clientX - rect.left) / rect.width;
      var y = 1 - (clientY - rect.top) / rect.height; // from the bottom, like Unity
      return x >= button.left && x <= button.right && y >= button.bottom && y <= button.top;
    }
    function toggle() {
      if (!state.wanted) {
        return;
      }
      if (state.openAtPress) {
        state.closedByPlayer = true;
        input.blur();
        return;
      }
      if (document.activeElement === input) {
        input.blur(); // Android put the keyboard away: taking the cursor out and back shows it again
      }
      state.closedByPlayer = false;
      open();
    }
    function keyboardShown() {
      return document.activeElement === input && !state.hiddenWhileFocused;
    }

    document.addEventListener("touchstart", function (event) {
      for (var i = 0; i < event.changedTouches.length; i++) {
        var touch = event.changedTouches[i];
        if (state.typeTouch === null && onTypeButton(touch.clientX, touch.clientY)) {
          state.typeTouch = touch.identifier;
          state.openAtPress = keyboardShown();
        }
      }
    }, true);
    document.addEventListener("touchend", function (event) {
      state.lastTouchTime = Date.now();
      var onButton = false;
      for (var i = 0; i < event.changedTouches.length; i++) {
        if (event.changedTouches[i].identifier === state.typeTouch) {
          onButton = true;
        }
      }
      if (onButton) {
        state.typeTouch = null;
        toggle();
      } else {
        openOnTap();
      }
    }, true);
    document.addEventListener("touchcancel", function (event) {
      for (var i = 0; i < event.changedTouches.length; i++) {
        if (event.changedTouches[i].identifier === state.typeTouch) {
          state.typeTouch = null;
        }
      }
    }, true);
    document.addEventListener("mousedown", function (event) {
      if (Date.now() - state.lastTouchTime < 1000) {
        return;
      }
      state.typeMouse = onTypeButton(event.clientX, event.clientY);
      state.openAtPress = keyboardShown();
    }, true);
    document.addEventListener("mouseup", function () {
      if (Date.now() - state.lastTouchTime < 1000) {
        return;
      }
      if (state.typeMouse) {
        state.typeMouse = false;
        toggle();
      } else {
        openOnTap();
      }
    }, true);
    var canvas = Module["canvas"];
    if (canvas) {
      canvas.addEventListener("mousedown", function (event) {
        if (state.wanted) {
          event.preventDefault(); // keep the cursor in the text field
        }
      });
    }
  },

  // The game wants typing (playing) or not (menus): not = close the keyboard.
  // Android can open the keyboard right after the tap that started play; an
  // iPhone or iPad waits for the next tap on the game (opening it outside a
  // tap would put the cursor in the field without showing the keyboard).
  TouchControlsWeb_SetKeyboardWanted: function (wanted) {
    var state = window.typeblastersKeyboard;
    if (!state) {
      return;
    }
    state.wanted = !!wanted;
    state.closedByPlayer = false;
    if (!state.wanted) {
      state.input.blur();
      return;
    }
    var apple = /iPhone|iPad|iPod/.test(navigator.userAgent)
      || (navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent));
    if (!apple && navigator.userActivation && navigator.userActivation.isActive) {
      state.open();
    }
  },

  // Where TAP TO TYPE is, as fractions of the canvas from its bottom-left
  // corner (all zeros: hidden).
  TouchControlsWeb_SetTypeButtonRect: function (left, bottom, right, top) {
    var state = window.typeblastersKeyboard;
    if (state) {
      state.typeButton = (right > left && top > bottom)
        ? { left: left, bottom: bottom, right: right, top: top } : null;
    }
  },

  // 1 while the phone's keyboard is up (the cursor is in the text field and
  // Android has not put the keyboard away).
  TouchControlsWeb_IsKeyboardOpen: function () {
    var state = window.typeblastersKeyboard;
    return state && document.activeElement === state.input && !state.hiddenWhileFocused ? 1 : 0;
  },

  // How much of the game canvas the phone's keyboard covers, as a fraction of
  // the canvas height: from the bottom (the keyboard) and from the top (when
  // the browser scrolled the page up). ?kbsim=0.4 pretends a keyboard covers
  // 40% (for tests).
  TouchControlsWeb_CoveredBottom: function () {
    var state = window.typeblastersKeyboard;
    return state ? state.coveredBottom() : 0;
  },

  TouchControlsWeb_CoveredTop: function () {
    var canvas = Module["canvas"];
    var state = window.typeblastersKeyboard;
    var view = window.visualViewport;
    if (!canvas || !state || !view || document.activeElement !== state.input || /[?&]kbsim=/.test(window.location.search)) {
      return 0;
    }
    var rect = canvas.getBoundingClientRect();
    return rect.height > 0 ? Math.max(0, Math.min(1, (view.offsetTop - rect.top) / rect.height)) : 0;
  },

  // The canvas height in CSS pixels (to size the buttons for a finger).
  TouchControlsWeb_CanvasCssHeight: function () {
    var canvas = Module["canvas"];
    return canvas ? canvas.getBoundingClientRect().height : 0;
  },

  // 1 if this is a phone or a tablet (the touch controls turn on), else 0.
  // The page address can force it: ?touch=1 (on) or ?touch=0 (off).
  // "pointer: coarse" means the main pointer is a finger. iPads ask for the
  // desktop version of pages and say "Macintosh", but they have touch points.
  TouchControlsWeb_IsTouchDevice: function () {
    var forced = /[?&]touch=([01])/.exec(window.location.search);
    if (forced) {
      return forced[1] === "1" ? 1 : 0;
    }
    var coarse = !!(window.matchMedia && window.matchMedia("(pointer: coarse)").matches);
    var phoneOrTablet = /iPhone|iPad|iPod|Android|Mobi/i.test(navigator.userAgent);
    var iPad = navigator.maxTouchPoints > 1 && /Macintosh/.test(navigator.userAgent);
    return (coarse || phoneOrTablet || iPad) ? 1 : 0;
  },

  // 1 if the page can be shown full screen (Android, iPad; not iPhone).
  TouchControlsWeb_CanGoFullscreen: function () {
    var root = document.documentElement;
    var enabled = document.fullscreenEnabled || document.webkitFullscreenEnabled;
    var canRequest = root.requestFullscreen || root.webkitRequestFullscreen;
    return (enabled && canRequest) ? 1 : 0;
  },

  // 1 while the page is full screen.
  TouchControlsWeb_IsFullscreen: function () {
    return (document.fullscreenElement || document.webkitFullscreenElement) ? 1 : 0;
  },

  // Browsers only allow full screen from inside a touch or click, and Unity
  // handles a touch one frame after it happened. If the browser still counts
  // that touch as "just happened" it switches at once; otherwise it waits for
  // the end of the touch that is going on (only one wait at a time; a cancelled
  // touch stops waiting). In full screen it also asks to stay sideways
  // (Android; elsewhere it is ignored).
  TouchControlsWeb_ToggleFullscreenOnRelease: function () {
    function stopWaiting() {
      document.removeEventListener("touchend", onRelease, true);
      document.removeEventListener("mouseup", onRelease, true);
      document.removeEventListener("touchcancel", stopWaiting, true);
      window.typeblastersFullscreenWaiting = false;
    }
    function onRelease() {
      stopWaiting();
      toggle();
    }
    function toggle() {
      var root = document.documentElement;
      if (document.fullscreenElement || document.webkitFullscreenElement) {
        if (document.exitFullscreen) {
          document.exitFullscreen();
        } else if (document.webkitExitFullscreen) {
          document.webkitExitFullscreen();
        }
        return;
      }
      var request = root.requestFullscreen || root.webkitRequestFullscreen;
      var result = request ? request.call(root) : null;
      if (result && result.then) {
        result.then(function () {
          if (screen.orientation && screen.orientation.lock) {
            screen.orientation.lock("landscape").catch(function () {});
          }
        }).catch(function () {});
      }
    }
    if (navigator.userActivation && navigator.userActivation.isActive) {
      toggle();
      return;
    }
    if (window.typeblastersFullscreenWaiting) {
      return;
    }
    window.typeblastersFullscreenWaiting = true;
    document.addEventListener("touchend", onRelease, true);
    document.addEventListener("mouseup", onRelease, true);
    document.addEventListener("touchcancel", stopWaiting, true);
  }
});
