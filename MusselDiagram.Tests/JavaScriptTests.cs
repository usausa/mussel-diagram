namespace MusselDiagram;

using System.ComponentModel;
using System.Diagnostics;

public sealed class JavaScriptTests
{
    [Fact]
    public async Task ScriptFunctionsPassTheirTests()
    {
        var startInfo = new ProcessStartInfo("node")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = TestPaths.Root
        };
        startInfo.ArgumentList.Add("--test");
        startInfo.ArgumentList.Add(Path.Combine("MusselDiagram.Tests", "js", "MusselDiagram.test.mjs"));

        using var process = StartProcess(startInfo);
        if (process is null)
        {
            Assert.Skip("node が見つからないため、JS のテストを省略しました");
            return;
        }

        var token = Xunit.TestContext.Current.CancellationToken;
        var output = process.StandardOutput.ReadToEndAsync(token);
        var errors = process.StandardError.ReadToEndAsync(token);
        await process.WaitForExitAsync(token).ConfigureAwait(true);
        var log = await output.ConfigureAwait(true) + await errors.ConfigureAwait(true);

        Assert.True(process.ExitCode == 0, log);
    }

    private static Process? StartProcess(ProcessStartInfo startInfo)
    {
        try
        {
            return Process.Start(startInfo);
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
