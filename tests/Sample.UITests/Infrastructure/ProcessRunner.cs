using System.Diagnostics;
using System.Text;

namespace Sample.UITests;

public sealed record ProcessResult(int ExitCode, string Output)
{
    public ProcessResult EnsureSuccess(string what)
    {
        if (ExitCode != 0)
            throw new InvalidOperationException($"{what} failed with exit code {ExitCode}:\n{Tail(Output, 60)}");
        return this;
    }

    static string Tail(string text, int lines)
    {
        var all = text.Split('\n');
        return String.Join('\n', all.Skip(Math.Max(0, all.Length - lines)));
    }
}

public static class ProcessRunner
{
    /// <summary>
    /// The dotnet that MAUI needs lives in the login shell's PATH on dev machines (a second SDK sits
    /// in /usr/local/share/dotnet without the workloads), so on Unix every dotnet invocation goes
    /// through a login shell. On Windows the PATH dotnet is the right one.
    /// </summary>
    public static Task<ProcessResult> DotNet(string arguments, TimeSpan? timeout = null)
        => OperatingSystem.IsWindows()
            ? Run("dotnet", arguments, timeout)
            : Run("/bin/zsh", ["-lc", "dotnet " + arguments], timeout);

    /// <summary>
    /// Runs with an argument vector. Use this whenever an argument holds shell quoting: .NET splits a
    /// flat Arguments string with Windows rules on every OS, so single quotes are not quotes to it.
    /// </summary>
    public static Task<ProcessResult> Run(string fileName, IReadOnlyList<string> arguments, TimeSpan? timeout = null)
        => Run(fileName, arguments, String.Join(' ', arguments), timeout, null, null);

    public static Task<ProcessResult> Run(
        string fileName,
        string arguments,
        TimeSpan? timeout = null,
        string? workingDirectory = null,
        IDictionary<string, string>? environment = null)
        => Run(fileName, null, arguments, timeout, workingDirectory, environment);

    static async Task<ProcessResult> Run(
        string fileName,
        IReadOnlyList<string>? argumentList,
        string arguments,
        TimeSpan? timeout,
        string? workingDirectory,
        IDictionary<string, string>? environment)
    {
        using var process = new Process();
        process.StartInfo = CreateStartInfo(fileName, argumentList == null ? arguments : "", workingDirectory, environment);
        if (argumentList != null)
        {
            foreach (var argument in argumentList)
                process.StartInfo.ArgumentList.Add(argument);
        }

        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        process.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var cts = new CancellationTokenSource(timeout ?? TimeSpan.FromMinutes(20));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException($"'{fileName} {arguments}' did not finish within {timeout}");
        }

        lock (output)
            return new ProcessResult(process.ExitCode, output.ToString());
    }

    /// <summary>Starts a long-running process (the app itself) with its output teed to a log file.</summary>
    public static Process StartLogged(
        string fileName,
        string arguments,
        string logFile,
        IDictionary<string, string>? environment = null)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logFile)!);

        // A relaunch opens this same path while the previous launch's writer may still be open:
        // Exited runs on a pool thread, so disposal races the next LaunchAsync. StreamWriter(string)
        // shares the file for reading only, which makes that an IOException on Windows and fails
        // every relaunch — the app is never brought back and every later test fails in its
        // BeforeTest hook. Sharing writes costs nothing here: the old process is already dead.
        var stream = new FileStream(logFile, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
        var writer = new StreamWriter(stream) { AutoFlush = true };
        var closed = false;

        void Write(string? data)
        {
            if (data == null)
                return;

            // Output can still arrive after Exited disposed the writer.
            lock (writer)
            {
                if (!closed)
                    writer.WriteLine(data);
            }
        }

        var process = new Process
        {
            StartInfo = CreateStartInfo(fileName, arguments, null, environment),
            EnableRaisingEvents = true
        };
        process.OutputDataReceived += (_, e) => Write(e.Data);
        process.ErrorDataReceived += (_, e) => Write(e.Data);
        process.Exited += (_, _) =>
        {
            lock (writer)
            {
                if (closed)
                    return;

                closed = true;
                writer.Dispose();
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    /// <summary>Double quotes: the one form both .NET's argument splitter and zsh -lc read the same way.</summary>
    public static string Quote(string value) => "\"" + value.Replace("\"", "\\\"") + "\"";

    static ProcessStartInfo CreateStartInfo(
        string fileName,
        string arguments,
        string? workingDirectory,
        IDictionary<string, string>? environment)
    {
        var info = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDirectory ?? UITestSettings.RepoRoot
        };
        if (environment != null)
        {
            foreach (var (key, value) in environment)
                info.Environment[key] = value;
        }
        return info;
    }
}
