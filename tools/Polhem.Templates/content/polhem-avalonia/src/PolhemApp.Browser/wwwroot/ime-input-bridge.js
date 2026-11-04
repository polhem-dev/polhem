// Delivers input method text that Avalonia.Browser misses (https://github.com/polhem-dev/polhem/issues/310).
//
// Avalonia.Browser takes text from only two places: a `keydown` whose `key` is a single character, and the `data`
// of `compositionend`. It does not listen to `input` events. This script does two things.
// 1. Keys the input method is handling (`keyCode` 229) are not passed to Avalonia. Avalonia inserts any
//    single-character `key` as text, so the radicals of Boshiamy (OpenVanilla on macOS sends keys such as "i" and
//    "x") were typed into the field. After inserting them Avalonia rewrites the hidden input element, which
//    interrupts the composition the browser has in progress. The chosen character then arrives as `insertText`
//    with no `compositionend`, and it is dropped. With these keys stopped, OpenVanilla completes its composition
//    normally.
// 2. As a fallback, when an input method still sends text as `insertText` without a `compositionend`, the text is
//    forwarded as a synthetic `compositionend`. Avalonia then clears the radicals and inserts the text. With the
//    keys stopped, OpenVanilla no longer takes this path.
// Once Avalonia.Browser fixes this upstream, delete this file together with its import in `main.js`.

const INPUT_CLASS = 'avalonia-input-element';

const isAvaloniaInput = (target) =>
    target instanceof HTMLInputElement && target.classList.contains(INPUT_CLASS);

// The character the latest `keydown` already sent to Avalonia as text. An `input` event with the same `data` is a
// duplicate and is not forwarded.
let lastKeyChar = null;
// Set right after a genuine `compositionend`: that event already delivered this round's input.
// WARNING: Do not decide "composing" by the span between `compositionstart` and `compositionend`. The input methods
// described above send no `compositionend`, so that test would block every `insertText` they send. Whether a
// composition is in progress is decided only by the event's own `isComposing`.
let justComposed = false;

document.addEventListener('keydown', (e) => {
    if (!isAvaloniaInput(e.target)) return;
    // A `keyCode` of 229 means the input method handles this key, and its text arrives through composition or
    // `insertText`. Avalonia inserts any single-character `key` as text, so without this stop the radicals and the
    // space that picks a candidate would be typed into the field.
    if (e.keyCode === 229) {
        lastKeyChar = null;
        e.stopPropagation();
        return;
    }
    lastKeyChar = e.key.length === 1 && !e.ctrlKey && !e.metaKey ? e.key : null;
}, true);

document.addEventListener('compositionend', (e) => {
    if (!isAvaloniaInput(e.target) || !e.isTrusted) return;
    // NOTE: Safari sends the last `input` after `compositionend`, so every event of this round counts as delivered.
    justComposed = true;
    setTimeout(() => { justComposed = false; }, 0);
}, true);

document.addEventListener('input', (e) => {
    if (!isAvaloniaInput(e.target)) return;
    const keyChar = lastKeyChar;
    lastKeyChar = null;

    if (e.inputType !== 'insertText' || !e.data) return;
    if (e.isComposing || justComposed) return;
    if (e.data === keyChar) return;

    e.target.dispatchEvent(new CompositionEvent('compositionend', { data: e.data, bubbles: true }));
}, true);
