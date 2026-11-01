// Reports when the viewport crosses the compact width threshold of a CompactViewportObserver. A media
// query list raises `change` only on a crossing, so resizing within either band costs no call to the server.
const watchers = new Map();

function watch(id, dotNetRef, threshold) {
    // The query ends 0.02px below the threshold, so a viewport exactly as wide as the threshold is wide, as
    // on the Avalonia head, and a fractional width cannot fall between the two bands.
    const query = window.matchMedia(`(max-width: ${threshold - 0.02}px)`);
    const listener = event => dotNetRef.invokeMethodAsync('OnCompactChanged', event.matches);
    query.addEventListener('change', listener);
    watchers.set(id, { dotNetRef, query, listener });
    return query.matches;
}

export function start(dotNetRef, id, threshold) {
    stop(id);
    return watch(id, dotNetRef, threshold);
}

export function update(id, threshold) {
    const watcher = watchers.get(id);
    if (!watcher) return false;
    stop(id);
    return watch(id, watcher.dotNetRef, threshold);
}

export function stop(id) {
    const watcher = watchers.get(id);
    if (!watcher) return;
    watcher.query.removeEventListener('change', watcher.listener);
    watchers.delete(id);
}
