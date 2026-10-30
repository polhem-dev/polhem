import { dotnet } from './_framework/dotnet.js'

const is_browser = globalThis.window !== undefined;
if (!is_browser) throw new Error(`Expected to be running in a browser`);

// The demo site serves this bundle under /web/ (NorthwindDemo.BrowserPath in Polhem.Northwind.Blazor); the
// development server serves it at the root, where there is no other front end to link to.
if (globalThis.location.pathname.startsWith('/web/')) {
    document.getElementById('demo-switch').hidden = false;
}

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
