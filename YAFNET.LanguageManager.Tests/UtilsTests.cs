namespace YAFNET.LanguageManager.Tests;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Newtonsoft.Json;

using YAFNET.LanguageManager.Utils;

public class CommandLineParametersTests
{
    [Fact]
    public void ParsesTextLinesAndSwitches()
    {
        using var parameters = new CommandLineParameters([" C:\\Lang ", "-Sync", "/Minify", "-out:Value", "second"], false);

        Assert.Equal(2, parameters.TextCount);
        Assert.Equal("C:\\Lang", parameters.TextLines[0]);
        Assert.Equal("second", parameters[1]);
        Assert.True(parameters["sync"]);
        Assert.True(parameters["minify"]);
        Assert.False(parameters["uglify"]);
        Assert.Null(parameters.Switches["sync"]);
        Assert.Equal("value", parameters.Switches["out"]);
    }

    [Fact]
    public void CaseSensitiveMode_KeepsValueCase()
    {
        using var parameters = new CommandLineParameters(["-Out:Value"], true);

        Assert.Equal("Value", parameters.Switches["Out"]);
        Assert.Equal(0, parameters.TextCount);
    }

    [Fact]
    public void IntIndexer_OutOfRange_Throws()
    {
        using var parameters = new CommandLineParameters(["only"], false);

        Assert.Equal("only", parameters[0]);
        Assert.Throws<IndexOutOfRangeException>(() => parameters[1]);
    }
}

[Collection(NonParallelCollection.Name)]
public sealed class DebugHelperTests : IDisposable
{
    private readonly StringWriter trace = new();

    private readonly TextWriterTraceListener listener;

    private readonly TextWriter originalOut = Console.Out;

    private readonly StringWriter console = new();

    public DebugHelperTests()
    {
        this.listener = new TextWriterTraceListener(this.trace);
        Trace.Listeners.Add(this.listener);
        Console.SetOut(this.console);
    }

    [Fact]
    public void DisplayAndLogMessage_WritesToConsoleAndTrace()
    {
        DebugHelper.DisplayAndLogMessage("hello world");
        Trace.Flush();

        Assert.Contains("hello world", this.console.ToString());
        Assert.Contains("hello world", this.trace.ToString());
    }

    [Fact]
    public void DebugExceptionMessage_LogsWholeExceptionChain()
    {
        Exception exception;

        try
        {
            throw new InvalidOperationException("outer", new ArgumentException("inner"));
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        DebugHelper.DebugExceptionMessage(exception);
        Trace.Flush();

        var log = this.trace.ToString();
        Assert.Contains("outer", log);
        Assert.Contains("inner", log);
        Assert.Contains($"Machine: {Environment.MachineName}", log);
    }

    [Fact]
    public void DebugExceptionMessage_WithNull_DoesNothing()
    {
        DebugHelper.DebugExceptionMessage(null);
        Trace.Flush();

        Assert.Empty(this.trace.ToString());
    }

    public void Dispose()
    {
        Trace.Listeners.Remove(this.listener);
        Console.SetOut(this.originalOut);
        this.listener.Dispose();
    }
}

[Collection(NonParallelCollection.Name)]
public sealed class SaveDebugTests : IDisposable
{
    private readonly string folder = Path.Combine(Path.GetTempPath(), "YafLangMgrTests", Guid.NewGuid().ToString("N"));

    public SaveDebugTests() => Directory.CreateDirectory(this.folder);

    private string LogPath => Path.Combine(this.folder, "test.log");

    [Fact]
    public void Dispose_WritesTracedMessagesToLogFile()
    {
        using (new SaveDebug(this.folder, "test.log"))
        {
            Trace.TraceError("first message");
        }

        Assert.Contains("first message", File.ReadAllText(this.LogPath));
    }

    [Fact]
    public void Dispose_AppendsToExistingLogFile()
    {
        File.WriteAllText(this.LogPath, "existing" + Environment.NewLine);

        using (new SaveDebug(this.folder, "test.log"))
        {
            Trace.TraceError("appended message");
        }

        var content = File.ReadAllText(this.LogPath);
        Assert.StartsWith("existing", content);
        Assert.Contains("appended message", content);
    }

    [Fact]
    public void Dispose_WithoutMessages_DoesNotCreateLogFile()
    {
        using (new SaveDebug(this.folder, "test.log"))
        {
        }

        Assert.False(File.Exists(this.LogPath));
    }

    [Fact]
    public void Dispose_WhenLogCannotBeWritten_SwallowsException()
    {
        var debug = new SaveDebug(Path.Combine(this.folder, "missing"), "test.log");
        Trace.TraceError("lost message");

        var exception = Record.Exception(debug.Dispose);

        Assert.Null(exception);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(this.folder, true);
        }
        catch (IOException)
        {
            // best effort cleanup
        }
    }
}

public class LanguageFileTests
{
    [Fact]
    public void ResourceListConverter_CannotWrite()
    {
        var converter = new ResourceListConverter();

        Assert.False(converter.CanWrite);
        Assert.Throws<NotImplementedException>(
            () => converter.WriteJson(null, null, JsonSerializer.CreateDefault()));
    }

    [Theory]
    [InlineData(typeof(List<Resource>), true)]
    [InlineData(typeof(List<string>), true)]
    [InlineData(typeof(Dictionary<string, Resource>), false)]
    [InlineData(typeof(Resource), false)]
    public void ResourceListConverter_CanConvert(Type type, bool expected)
    {
        Assert.Equal(expected, new ResourceListConverter().CanConvert(type));
    }

    [Fact]
    public void Deserialize_ReadsArrayOfResources()
    {
        var page = JsonConvert.DeserializeObject<Page>(
            """{"@name":"COMMON","Resource":[{"@tag":"OK","#text":"Ok"},{"@tag":"NO","#text":"No"}]}""");

        Assert.Equal("COMMON", page.Name);
        Assert.Equal(2, page.Resource.Count);
        Assert.Equal("No", page.Resource[1].Text);
    }

    [Fact]
    public void Serialize_UsesYafPropertyNames()
    {
        var file = new ResourcesFile
        {
            Resources = new Resources
            {
                Language = "German",
                Code = "de",
                Page = [new Page { Name = "COMMON", Resource = [new Resource { Tag = "OK", Text = "Ok" }] }]
            }
        };

        var json = JsonConvert.SerializeObject(file);

        Assert.Equal(
            """{"Resources":{"@language":"German","@code":"de","page":[{"@name":"COMMON","Resource":[{"@tag":"OK","#text":"Ok"}]}]}}""",
            json);
    }
}
