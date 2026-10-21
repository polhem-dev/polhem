using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace Polhem.DefineEditor.Services;

/// <summary>
/// Runs an <see cref="ExternalCommand"/> as a child process and reports each line it writes as soon as it is written.
/// </summary>
public static class ProcessRunner
{
    /// <summary>
    /// Runs <paramref name="command"/> in its working directory, resolved against <paramref name="baseDirectory"/>.
    /// </summary>
    /// <remarks>
    /// Standard input is closed at once, so a program that asks a question reads end-of-file instead of waiting for
    /// an answer nobody can give. Lines from standard output and standard error arrive on a thread-pool thread, with
    /// password values already hidden by <see cref="SecretMasker"/>. Cancelling kills the whole process tree.
    /// </remarks>
    /// <param name="command">The command to run.</param>
    /// <param name="baseDirectory">The folder a relative working directory is resolved against.</param>
    /// <param name="onLine">Receives each line of output.</param>
    /// <param name="cancellationToken">Kills the process when cancelled.</param>
    public static async Task<ProcessRunResult> RunAsync(
        ExternalCommand command, string baseDirectory, Action<string> onLine, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(onLine);

        var workingDirectory = command.ResolveWorkingDirectory(baseDirectory);
        if (!Directory.Exists(workingDirectory))
            return new ProcessRunResult(null, $"The working directory does not exist: {workingDirectory}", false);

        var startInfo = new ProcessStartInfo(command.Command)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        foreach (var argument in command.Arguments) startInfo.ArgumentList.Add(argument);

        using var process = new Process { StartInfo = startInfo };
        process.OutputDataReceived += (_, e) => { if (e.Data is not null) onLine(SecretMasker.Hide(e.Data)); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) onLine(SecretMasker.Hide(e.Data)); };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new ProcessRunResult(null, ex.Message, false);
        }
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // The process exited between the cancellation and the kill.
            }
            catch (AggregateException)
            {
                // Some processes of the tree could not be killed; the wait below still ends with the main one.
            }
            catch (Win32Exception)
            {
                // The process could not be killed (already exiting, or not ours to end).
            }
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            return new ProcessRunResult(null, null, true);
        }

        // The parameterless wait returns only after the redirected streams have delivered their last line.
        process.WaitForExit();
        return new ProcessRunResult(process.ExitCode, null, false);
    }
}
