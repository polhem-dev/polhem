using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Polhem.Templates.UnitTests;

/// <summary>
/// A generated site started with <c>dotnet run</c> on a free local port, and stopped when disposed.
/// </summary>
public sealed class RunningSite : IDisposable
{
    private static readonly TimeSpan s_startTimeout = TimeSpan.FromMinutes(2);

    private readonly Process _process;
    private readonly StringBuilder _output = new();

    private RunningSite(Process process, Uri address)
    {
        _process = process;
        Address = address;
        Client = new HttpClient { BaseAddress = address };
    }

    public Uri Address { get; }

    public HttpClient Client { get; }

    /// <summary>The output of the site so far, for a failure message.</summary>
    public string Output
    {
        get
        {
            lock (_output) return _output.ToString();
        }
    }

    /// <summary>
    /// Starts the built project in the Development environment, which serves the static assets of the referenced
    /// packages, and returns once <c>/</c> answers.
    /// </summary>
    public static async Task<RunningSite> StartAsync(TemplatePackageFixture fx, string workingDirectory, string project)
    {
        var address = new Uri($"http://127.0.0.1:{FreePort()}");
        var startInfo = fx.StartInfo(workingDirectory, "run", "--project", project, "--no-build", "--no-launch-profile");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_URLS"] = address.AbsoluteUri;

        var site = new RunningSite(Process.Start(startInfo)!, address);
        site._process.OutputDataReceived += (_, e) => site.Append(e.Data);
        site._process.ErrorDataReceived += (_, e) => site.Append(e.Data);
        site._process.BeginOutputReadLine();
        site._process.BeginErrorReadLine();

        var deadline = DateTime.UtcNow + s_startTimeout;
        while (DateTime.UtcNow < deadline)
        {
            if (site._process.HasExited)
            {
                var exitCode = site._process.ExitCode;
                site.Dispose();
                Assert.Fail($"The site exited with {exitCode} before it answered:\n{site.Output}");
            }
            try
            {
                using var response = await site.Client.GetAsync(new Uri("/", UriKind.Relative));
                if (response.StatusCode == HttpStatusCode.OK) return site;
            }
            catch (HttpRequestException)
            {
                // Not listening yet.
            }
            await Task.Delay(TimeSpan.FromMilliseconds(500));
        }
        site.Dispose();
        Assert.Fail($"The site did not answer at {address} within {s_startTimeout}:\n{site.Output}");
        return null!;
    }

    private void Append(string? line)
    {
        if (line is null) return;
        lock (_output) _output.AppendLine(line);
    }

    private static int FreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    public void Dispose()
    {
        Client.Dispose();
        if (!_process.HasExited) _process.Kill(entireProcessTree: true);
        _process.WaitForExit();
        _process.Dispose();
    }
}
