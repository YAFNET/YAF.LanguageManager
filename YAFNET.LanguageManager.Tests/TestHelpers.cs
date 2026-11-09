namespace YAFNET.LanguageManager.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

// Program uses global state (Console, Trace listeners, a static HttpClient), so tests must not run in parallel.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class NonParallelCollection
{
    public const string Name = "NonParallel";
}

/// <summary>
/// Fake HTTP handler that answers translation requests without touching the network.
/// </summary>
internal sealed class FakeTranslationHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<Uri> Requests { get; } = [];

    /// <summary>
    /// Returns a handler that "translates" every text to "[code] text".
    /// </summary>
    public static FakeTranslationHandler Echo() =>
        new(request =>
        {
            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            return Json(TranslationResponse($"[{query["tl"]}] {query["q"]}"));
        });

    public static FakeTranslationHandler Failing() =>
        new(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

    public static string TranslationResponse(string text) =>
        $"[[[{JsonConvert.ToString(text)},\"source\",null,null,1]],null,\"en\"]";

    public static HttpResponseMessage Json(string body) =>
        new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        this.Requests.Add(request.RequestUri);
        return Task.FromResult(responder(request));
    }
}

/// <summary>
/// Base class providing a temporary language folder, a fake translation client and captured console output.
/// </summary>
public abstract class ProgramTestBase : IDisposable
{
    private readonly HttpClient originalClient;

    private readonly TextWriter originalOut;

    private readonly StringWriter consoleOut = new();

    private FakeTranslationHandler handler;

    protected ProgramTestBase()
    {
        this.Folder = Path.Combine(Path.GetTempPath(), "YafLangMgrTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.Folder);

        this.originalClient = Program.TranslationHttpClient;
        this.UseHandler(FakeTranslationHandler.Echo());

        this.originalOut = Console.Out;
        Console.SetOut(this.consoleOut);
    }

    protected string Folder { get; }

    protected string Output => this.consoleOut.ToString();

    internal FakeTranslationHandler Handler => this.handler;

    internal void UseHandler(FakeTranslationHandler fake)
    {
        this.handler = fake;
        Program.TranslationHttpClient = new HttpClient(fake);
    }

    protected string WriteLanguage(string fileName, string json)
    {
        var path = Path.Combine(this.Folder, fileName);
        File.WriteAllText(path, json);
        return path;
    }

    protected List<string> Languages() => [.. Directory.GetFiles(this.Folder, "*.json")];

    internal ResourcesFile Load(string fileName) => Program.LoadFile(Path.Combine(this.Folder, fileName));

    internal static string Text(ResourcesFile file, string page, string tag) =>
        file.Resources.Page.Find(p => p.Name == page)?.Resource.Find(r => r.Tag == tag)?.Text;

    public void Dispose()
    {
        Console.SetOut(this.originalOut);
        Program.TranslationHttpClient = this.originalClient;

        try
        {
            Directory.Delete(this.Folder, true);
        }
        catch (IOException)
        {
            // best effort cleanup
        }

        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Builds a language file JSON string.
    /// </summary>
    protected static string Lang(string language, string code, params (string Page, (string Tag, string Text)[] Resources)[] pages)
    {
        var file = new
        {
            Resources = new Dictionary<string, object>
            {
                ["@language"] = language,
                ["@code"] = code,
                ["page"] = Array.ConvertAll(
                    pages,
                    p => new Dictionary<string, object>
                    {
                        ["@name"] = p.Page,
                        ["Resource"] = Array.ConvertAll(
                            p.Resources,
                            r => new Dictionary<string, string> { ["@tag"] = r.Tag, ["#text"] = r.Text })
                    })
            }
        };

        return JsonConvert.SerializeObject(file, Formatting.Indented);
    }
}
