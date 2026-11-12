namespace MusselDiagram.E2ETests;

using System.Diagnostics;
using System.Text.RegularExpressions;

public sealed partial class AppFixture : IAsyncLifetime
{
#if DEBUG
    private const string Configuration = "Debug";
#else
    private const string Configuration = "Release";
#endif

    private static readonly string[] Channels = ["msedge", "chrome"];

    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(60);

    private static readonly HttpClient Client = new();

    private readonly List<string> output = [];

    private readonly string iconFolder = Directory.CreateTempSubdirectory("mussel-diagram-icons-").FullName;

    private readonly TaskCompletionSource<string> listening = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private Process? app;

    private IPlaywright? playwright;

    private IBrowser? browser;

    public string BaseAddress { get; private set; } = string.Empty;

    public async ValueTask InitializeAsync()
    {
        playwright = await Playwright.CreateAsync().ConfigureAwait(false);
        foreach (var channel in Channels)
        {
            try
            {
                browser = await playwright.Chromium.LaunchAsync(new() { Channel = channel }).ConfigureAwait(false);
                break;
            }
            catch (PlaywrightException)
            {
            }
        }

        if (browser is null)
        {
            return;
        }

        app = StartApp();
        BaseAddress = await WaitForAppAsync(app).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (browser is not null)
        {
            await browser.DisposeAsync().ConfigureAwait(false);
        }

        playwright?.Dispose();
        if (app is not null)
        {
            if (!app.HasExited)
            {
                app.Kill(entireProcessTree: true);
                await app.WaitForExitAsync().ConfigureAwait(false);
            }

            app.Dispose();
        }

        Directory.Delete(iconFolder, true);
    }

    public void ProvideAwsIcons(IEnumerable<(string Path, string Text)> files)
    {
        foreach (var (path, text) in files)
        {
            var file = Path.Combine(iconFolder, path);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, text);
        }
    }

    public void RemoveAwsIcons()
    {
        foreach (var directory in Directory.EnumerateDirectories(iconFolder))
        {
            Directory.Delete(directory, true);
        }

        foreach (var file in Directory.EnumerateFiles(iconFolder))
        {
            File.Delete(file);
        }
    }

    public async Task<DiagramPage> OpenAsync(string path, string mode, bool touch = false)
    {
        if (browser is null)
        {
            Assert.Skip("Neither Edge nor Chrome was found, so the E2E test is skipped.");
        }

        var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = 1280, Height = 900 }, HasTouch = touch }).ConfigureAwait(false);
        var page = new DiagramPage(context, await context.NewPageAsync().ConfigureAwait(false));
        var response = await page.Page.GotoAsync($"{BaseAddress}render-mode/{mode}?redirect={Uri.EscapeDataString($"/{path}")}").ConfigureAwait(false);
        page.InitialHtml = response is null ? string.Empty : await response.TextAsync().ConfigureAwait(false);
        await page.WaitForViewAsync().ConfigureAwait(false);
        return page;
    }

    private static string FindSolutionDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while ((directory is not null) && !File.Exists(Path.Combine(directory.FullName, "MusselDiagram.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("MusselDiagram.slnx was not found.");
    }

    [GeneratedRegex("Now listening on: (?<address>http://[^ ]+)")]
    private static partial Regex ListeningPattern();

    private Process StartApp()
    {
        var published = Environment.GetEnvironmentVariable("MUSSELDIAGRAM_E2E_PUBLISH_DIRECTORY");
        var project = Path.Combine(FindSolutionDirectory(), "Example");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = published ?? project,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(published is null ? Path.Combine(project, "bin", Configuration, "net10.0", "Example.dll") : Path.Combine(published, "Example.dll"));
        startInfo.ArgumentList.Add("--urls");
        startInfo.ArgumentList.Add("http://127.0.0.1:0");
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = published is null ? "Development" : "Production";
        startInfo.Environment["AwsIconFolder"] = iconFolder;

        var process = Process.Start(startInfo) ?? throw new InvalidOperationException("The app could not be started.");
        process.OutputDataReceived += (_, e) => Keep(e.Data);
        process.ErrorDataReceived += (_, e) => Keep(e.Data);
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        return process;
    }

    private async Task<string> WaitForAppAsync(Process process)
    {
        var watch = Stopwatch.StartNew();
        while (true)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException($"The app has exited:{Environment.NewLine}{Log()}");
            }

            if (listening.Task.IsCompleted)
            {
                var address = await listening.Task.ConfigureAwait(false) + "/";
                try
                {
                    using var response = await Client.GetAsync(new Uri(address)).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return address;
                    }
                }
                catch (HttpRequestException)
                {
                }
            }

            if (watch.Elapsed > StartTimeout)
            {
                throw new TimeoutException($"The app does not respond:{Environment.NewLine}{Log()}");
            }

            await Task.Delay(200).ConfigureAwait(false);
        }
    }

    private void Keep(string? line)
    {
        if (line is null)
        {
            return;
        }

        if (ListeningPattern().Match(line) is { Success: true } match)
        {
            listening.TrySetResult(match.Groups["address"].Value);
        }

        lock (output)
        {
            output.Add(line);
            if (output.Count > 50)
            {
                output.RemoveAt(0);
            }
        }
    }

    private string Log()
    {
        lock (output)
        {
            return string.Join(Environment.NewLine, output);
        }
    }
}
