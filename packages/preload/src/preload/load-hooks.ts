/**
 * Late-listener support for `load` and `DOMContentLoaded`.
 *
 * Pengu's preload IIFE runs synchronously inside OnContextCreated (via
 * cef_v8context_t::eval), so the IIFE itself is in place before any HTML
 * scripts execute. But user plugins are loaded via dynamic `import()` —
 * which is asynchronous. A slow-importing plugin can finish AFTER the
 * page already fired `load` / `DOMContentLoaded`. When such a plugin then
 * does `window.addEventListener('load', fn)`, the native dispatch is
 * already over — the listener would never fire.
 *
 * This module patches `addEventListener` on `window` and `document` to
 * detect that case and schedule the listener via `setTimeout(0)` with a
 * synthesized Event of the right type. From the plugin author's view,
 * `addEventListener` works as if their code had run before the event.
 *
 * Notes / known limitations:
 *  - Late listeners are invoked once on the next tick. They aren't
 *    registered with the real EventTarget, so `removeEventListener` on a
 *    re-routed listener has no cancellable handle.
 *  - The synthesized `Event` object has `currentTarget` / `target` set to
 *    null (we can't spoof them through normal Event construction). Most
 *    `load` / `DOMContentLoaded` listeners ignore those properties; if a
 *    plugin needs them, it should listen earlier or query `document` /
 *    `window` directly.
 *  - `capture` (boolean or `options.capture`) is ignored for late listeners
 *    — there's no DOM dispatch to capture against.
 */

const windowAddEventListener = window.addEventListener;
const documentAddEventListener = document.addEventListener;

// One-shot flag: did the native `load` event fire? Registered via the saved
// reference so our own patch (assigned below) doesn't intercept it.
let windowLoaded = false;
windowAddEventListener.call(window, 'load', () => { windowLoaded = true; }, { once: true });

function isDomReady(): boolean {
  // 'loading' is the only state where DOMContentLoaded hasn't fired yet.
  return document.readyState === 'interactive' || document.readyState === 'complete';
}

/**
 * Schedule a listener to fire on the next tick with a synthesized Event.
 * Honors `options.signal` both at schedule time and at fire time.
 */
function callLate(
  target: EventTarget,
  type: string,
  listener: EventListenerOrEventListenerObject,
  options?: boolean | AddEventListenerOptions,
) {
  const signal = (typeof options === 'object' && options) ? options.signal : undefined;
  if (signal?.aborted) return;

  setTimeout(() => {
    if (signal?.aborted) return;
    const ev = new Event(type);
    if (typeof listener === 'function') {
      listener.call(target, ev);
    } else if (listener && typeof listener.handleEvent === 'function') {
      listener.handleEvent(ev);
    }
  }, 0);
}

// `load` and `DOMContentLoaded` both bubble to window, so a plugin's late
// `window.addEventListener('DOMContentLoaded', ...)` is a real and common case.
window.addEventListener = function (
  type: string,
  listener: EventListenerOrEventListenerObject | null,
  options?: boolean | AddEventListenerOptions,
): void {
  // Spec: addEventListener with null callback is a no-op.
  if (!listener) return;

  if (type === 'load' && windowLoaded) {
    callLate(this, type, listener, options);
    return;
  }
  if (type === 'DOMContentLoaded' && isDomReady()) {
    callLate(this, type, listener, options);
    return;
  }
  windowAddEventListener.call(this, type, listener, options);
};

// `document` fires DOMContentLoaded directly (then it bubbles to window).
// `document` does not fire `load` — only window does — so we don't intercept it here.
document.addEventListener = function (
  type: string,
  listener: EventListenerOrEventListenerObject | null,
  options?: boolean | AddEventListenerOptions,
): void {
  if (!listener) return;
  if (type === 'DOMContentLoaded' && isDomReady()) {
    callLate(this, type, listener, options);
    return;
  }
  documentAddEventListener.call(this, type, listener, options);
};

export { };
