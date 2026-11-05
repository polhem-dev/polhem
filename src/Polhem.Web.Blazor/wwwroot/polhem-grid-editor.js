// Keys pressed before a cell's editor arrives are queued and replayed in order, because opening an editor takes a round
// trip on Blazor Server. Those keys would otherwise be lost, and a Tab would leave a cell whose editor is still on its way.
const TEXT_CELL = 'polhem-grid-cell__text';
// An editor that has not arrived by then never will (the form left edit mode, or the circuit is gone), so keys go back to
// the browser rather than wait for it.
const OPENING_TIMEOUT_MS = 10000;
const FOCUSABLE = 'a[href], button:not(:disabled), input:not(:disabled), select:not(:disabled), textarea:not(:disabled), [tabindex]:not(:disabled), [contenteditable]';
let queue = [];
let openingSince = 0;
// The cell Escape returned the focus to: it rests until a key opens it.
let restingCell = null;
// What holds the cell being opened. The render that brings the editor removes the text, so until script focuses the
// editor the focus is on the body, and only this still tells a click on that cell from a click elsewhere.
let openingSlot = null;

function isOpening() {
    if (openingSince === 0) return false;
    if (Date.now() - openingSince < OPENING_TIMEOUT_MS) return true;
    openingSince = 0;
    queue = [];
    return false;
}

function isTextCell(element) {
    return !!element && !!element.classList && element.classList.contains(TEXT_CELL);
}

document.addEventListener('focusin', event => {
    if (!isTextCell(event.target)) return;
    if (event.target === restingCell) {
        restingCell = null;
    } else {
        openingSince = Date.now();
        openingSlot = event.target.parentElement;
    }
}, true);

document.addEventListener('keydown', event => {
    const active = document.activeElement;
    const isResting = isTextCell(active) && !isOpening();
    if (!isOpening() && !isResting) return;
    if (event.key.length === 1 && !event.ctrlKey && !event.metaKey && !event.altKey) {
        queue.push({ text: event.key });
        event.preventDefault();
        if (isResting) {
            // The focus opens a cell, except the one Escape just returned it to. The second focus event opens that one.
            openingSince = Date.now();
            openingSlot = active.parentElement;
            active.dispatchEvent(new FocusEvent('focus'));
        }
    } else if (event.key === 'Tab' && isOpening()) {
        // Leaving now would strand what is queued and race the editor for the focus, so the move waits for the editor.
        queue.push({ tab: true, backwards: event.shiftKey });
        event.preventDefault();
    } else if (event.key === 'Enter' && isOpening()) {
        // Enter commits the editor, which is not there yet, so it waits with the keys typed before it.
        queue.push({ enter: true });
        event.preventDefault();
    } else if (event.key === 'Escape') {
        queue = [];
    }
}, true);

// A click elsewhere while keys are queued would give the next editor to another cell, and the keys would land there. The
// click waits until the keys reached the cell they were typed on, and leaving that cell has written them.
document.addEventListener('mousedown', event => {
    isOpening();
    if (queue.length === 0) return;
    event.preventDefault();
    event.stopPropagation();
    if (event.button !== 0 || isOnOpeningCell(event.target)) return;
    queue = queue.filter(item => !item.click);
    queue.push({ click: true, target: event.target, clickTarget: null });
}, true);

// WARNING: the browser fires `click` even when `mousedown` was stopped. A button such as Save must not run before the
// queued text is written, so it waits as well.
document.addEventListener('click', event => {
    isOpening();
    const pending = queue.find(item => item.click);
    if (!pending) return;
    pending.clickTarget = event.target;
    event.preventDefault();
    event.stopPropagation();
}, true);

// WARNING: the focused element alone is not enough. Between the render and the call that focuses the editor the focus is
// on the body, which contains everything, so a click elsewhere was stopped and never replayed: the editor kept the text
// and the value was not written. `LateFocus_TypeThenClickOutside_WritesTheText` holds that moment open.
function isOnOpeningCell(target) {
    const active = document.activeElement;
    if (active && active !== document.body && active.contains(target)) return true;
    return !!openingSlot && openingSlot.isConnected && openingSlot.contains(target);
}

function followClick(pending) {
    const target = pending.target.closest(FOCUSABLE);
    target && target.focus();
    if (!isTextCell(document.activeElement)) {
        // No editor is coming, so keys typed after the click go where it landed.
        const typed = queue.filter(item => item.text).map(item => item.text).join('');
        queue = [];
        if (typed.length > 0) document.execCommand('insertText', false, typed);
    }
    if (pending.clickTarget) {
        pending.clickTarget.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true, view: window }));
    }
}

function nextStop(from, backwards) {
    const table = from.closest('table');
    if (!table) return null;
    const stops = [...table.querySelectorAll(`.${TEXT_CELL}, .polhem-grid-cell__editor`)];
    return stops[stops.indexOf(from) + (backwards ? -1 : 1)] ?? null;
}

// The render that brings the text back can arrive after the call that closed the editor returned.
function focusTextWhenRendered(row, attempts) {
    const text = row && row.querySelector(`.${TEXT_CELL}`);
    if (text) {
        restingCell = text;
        text.focus();
    } else if (attempts > 0) {
        requestAnimationFrame(() => focusTextWhenRendered(row, attempts - 1));
    }
}

// An input can sit in a web component's open shadow root, where a selector from outside does not reach it.
function findInput(editor) {
    if (!editor) return null;
    const direct = editor.querySelector('input:not([type=checkbox]), textarea, select, input, [role=combobox]');
    if (direct) return direct;
    for (const element of editor.querySelectorAll('*')) {
        const inner = element.shadowRoot && element.shadowRoot.querySelector('input, textarea');
        if (inner) return inner;
    }
    return null;
}

export async function focusEditor(editor, cell, openPopupSelector, replayDelay) {
    // The user clicked on while this editor was on its way, and the cell now focused has its own editor coming.
    if (isTextCell(document.activeElement) && isOpening()) return;
    let input = findInput(editor);
    // A web component can add its input a moment after it renders.
    for (let frame = 0; !input && frame < 10; frame++) {
        await new Promise(requestAnimationFrame);
        input = findInput(editor);
    }
    if (!input) {
        openingSince = 0;
        const pendingClick = queue.find(item => item.click);
        queue = [];
        pendingClick && followClick(pendingClick);
        return;
    }
    const original = input.value;
    input.focus();
    input.select && input.select();

    const close = () => {
        const row = editor.closest('td');
        cell.invokeMethodAsync('CloseEditor').then(() => focusTextWhenRendered(row, 20));
    };
    // Leaving the input fires `change`, which writes the value, and that event reaches the server before the call that
    // closes the editor.
    const commitAndClose = () => {
        input.blur();
        close();
    };
    // Enter commits a typed value only. A multi-line input keeps it for a new line, and a drop-down (a button, or an input
    // it keeps read-only) keeps it for opening its list.
    const commitsOnEnter = input.tagName === 'INPUT' && input.type !== 'checkbox' && !input.readOnly;

    editor.addEventListener('keydown', event => {
        if (document.querySelector(openPopupSelector)) return;
        if (event.key === 'Escape') {
            // Back to the value on focus, so leaving fires no `change`, and whatever was typed is dropped.
            input.value = original;
            event.stopPropagation();
            close();
        } else if (event.key === 'Enter' && commitsOnEnter && !isOpening()
            && !event.shiftKey && !event.ctrlKey && !event.metaKey && !event.altKey) {
            // While keys are still being replayed the Enter waits in the queue behind them, and the replay commits.
            event.preventDefault();
            event.stopPropagation();
            commitAndClose();
        }
    }, true);

    const replay = () => {
        openingSince = 0;
        const leaveAt = queue.findIndex(item => item.tab || item.click || item.enter);
        const typed = (leaveAt < 0 ? queue : queue.slice(0, leaveAt)).map(item => item.text).join('');
        const pendingLeave = leaveAt < 0 ? null : queue[leaveAt];
        queue = leaveAt < 0 ? [] : queue.slice(leaveAt + 1);
        if (typed.length > 0 && !input.readOnly) {
            // WARNING: never set `value` here. A value set from script does not fire `change` when the input is left, so the
            // text would show and then be lost. Inserted text counts as typing.
            document.execCommand('insertText', false, typed);
        }
        if (pendingLeave && pendingLeave.enter && commitsOnEnter) {
            commitAndClose();
        } else if (pendingLeave && !pendingLeave.enter) {
            input.blur();
            if (pendingLeave.click) {
                followClick(pendingLeave);
            } else {
                const next = nextStop(editor, pendingLeave.backwards);
                next && next.focus();
            }
        }
    };
    // Keys pressed during the delay stay in the queue, because `openingSince` is still set.
    if (replayDelay > 0) {
        setTimeout(replay, replayDelay);
    } else {
        replay();
    }
}
