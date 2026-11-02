// Reports when the end of a card list comes near the viewport, so the next page reads before the user reaches it.
const MARGIN = '600px 0px';
const observers = new WeakMap();

export function watch(end, owner) {
    observers.get(end)?.disconnect();
    const observer = new IntersectionObserver(entries => {
        if (entries.some(entry => entry.isIntersecting)) owner.invokeMethodAsync('OnReachedEnd');
    }, { rootMargin: MARGIN });
    // A new observer reports the current state once, so an end still in view after a page arrived reads the next one.
    observer.observe(end);
    observers.set(end, observer);
}

export function stop(end) {
    observers.get(end)?.disconnect();
    observers.delete(end);
}
