// Hands an Escape pressed while the focus is on the body to the topmost MudBlazor dialog.
//
// WARNING: MudBlazor 9.14.0 handles a dialog's `CloseOnEscapeKey` with a key interceptor on the dialog's container, so it
// sees only a key pressed inside the dialog. When a render removes the focused element the focus falls back to the body,
// and Escape then did nothing, even on a dialog that should close. The forwarder dispatches the key again on the topmost
// `.mud-dialog`, where the interceptor finds it, and the dialog's own options decide what happens.
//
// It leaves alone a key something else has handled, a key aimed at an element, and any key while a popover other than a
// tooltip is open: a popover renders outside its dialog, so its Escape must not close the dialog too. A tooltip opens
// on hover and takes no keys (`MudDialogEscapeTests`).

let installed = false;

export function forwardEscape() {
    if (installed) return;
    installed = true;
    document.addEventListener('keydown', e => {
        if (e.key !== 'Escape' || !e.isTrusted || e.defaultPrevented || e.target !== document.body) return;
        if (document.querySelector('.mud-popover-open:not(.mud-tooltip)')) return;
        const dialogs = document.querySelectorAll('.mud-dialog-container .mud-dialog');
        const topmost = dialogs[dialogs.length - 1];
        topmost?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', code: 'Escape', bubbles: true, cancelable: true }));
    });
}
