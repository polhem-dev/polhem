using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace Polhem.Northwind.Blazor.Demo;

/// <summary>
/// Serves a published Avalonia WASM bundle as static files under a path prefix.
/// </summary>
public static class BrowserBundleExtensions
{
    private const string EncodingItemKey = "Northwind.BrowserBundle.Encoding";

    // The publish writes a Brotli and a gzip copy next to every file. Brotli is listed first: it is the smaller one.
    private static readonly (string Encoding, string Extension)[] s_precompressed =
    [
        ("br", ".br"),
        ("gzip", ".gz"),
    ];

    /// <summary>
    /// Serves the bundle in <paramref name="files"/> under <paramref name="path"/>, preferring the precompressed copy
    /// of each file the client accepts.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="path">The path prefix, without a trailing slash.</param>
    /// <param name="files">The bundle's <c>wwwroot</c> folder.</param>
    /// <remarks>
    /// The bundle is tens of megabytes uncompressed and about a quarter of that as Brotli. Sending the precompressed
    /// copies costs no CPU, which matters on a small host where compressing on the fly would not.
    /// </remarks>
    public static void UseBrowserBundle(this IApplicationBuilder app, PathString path, IFileProvider files)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(files);

        var contentTypes = new PrecompressedContentTypeProvider();

        app.UseWhen(context => context.Request.Path.StartsWithSegments(path), branch =>
        {
            // The page loads `./main.js` and `./_framework/...`, which resolve under the prefix only when the
            // address ends with a slash.
            branch.Use((context, next) =>
            {
                if (context.Request.Path == path)
                {
                    context.Response.Redirect(path + "/" + context.Request.QueryString);
                    return Task.CompletedTask;
                }
                return next(context);
            });
            branch.UseDefaultFiles(new DefaultFilesOptions { RequestPath = path, FileProvider = files });
            branch.Use((context, next) =>
            {
                SelectPrecompressed(context, path, files);
                return next(context);
            });
            branch.UseStaticFiles(new StaticFileOptions
            {
                RequestPath = path,
                FileProvider = files,
                ContentTypeProvider = contentTypes,
                OnPrepareResponse = static file =>
                {
                    var headers = file.Context.Response.Headers;
                    headers.Vary = "Accept-Encoding";
                    if (file.Context.Items[EncodingItemKey] is string encoding)
                        headers.ContentEncoding = encoding;
                },
            });
        });
    }

    private static void SelectPrecompressed(HttpContext context, PathString path, IFileProvider files)
    {
        if (!context.Request.Path.StartsWithSegments(path, out var relative) || !relative.HasValue)
            return;

        var accepted = context.Request.GetTypedHeaders().AcceptEncoding;
        foreach (var (encoding, extension) in s_precompressed)
        {
            bool isAccepted = accepted.Any(value =>
                string.Equals(value.Value.Value, encoding, StringComparison.OrdinalIgnoreCase)
                && value.Quality is not 0);
            if (!isAccepted || !files.GetFileInfo(relative.Value + extension).Exists)
                continue;

            context.Request.Path = path + relative + extension;
            context.Items[EncodingItemKey] = encoding;
            return;
        }
    }

    /// <summary>
    /// Names the content type of a precompressed copy after the file it was compressed from, and knows the ICU data
    /// files the default mapping lacks.
    /// </summary>
    private sealed class PrecompressedContentTypeProvider : IContentTypeProvider
    {
        private readonly FileExtensionContentTypeProvider _inner = new();

        public PrecompressedContentTypeProvider()
        {
            _inner.Mappings[".dat"] = "application/octet-stream";
        }

        public bool TryGetContentType(string subpath, [MaybeNullWhen(false)] out string contentType)
        {
            foreach (var (_, extension) in s_precompressed)
            {
                if (subpath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                {
                    subpath = subpath[..^extension.Length];
                    break;
                }
            }
            return _inner.TryGetContentType(subpath, out contentType);
        }
    }
}
