namespace YAFNET.LanguageManager.Tests;

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

[Collection(NonParallelCollection.Name)]
public class ProgramTests : ProgramTestBase
{
    private string English(params (string Page, (string Tag, string Text)[] Resources)[] pages) =>
        this.WriteLanguage("english.json", Lang("English", "en", pages));

    [Fact]
    public async Task Main_WithoutArguments_ShowsUsage()
    {
        await Program.Main([]);

        Assert.Contains("JSON Language Synchronizer", this.Output);
        Assert.Contains("Usage: YAF.LanguageManager pathToLanguageFiles", this.Output);
        Assert.Contains("-translateGoogle", this.Output);
    }

    [Theory]
    [InlineData("-?")]
    [InlineData("/help")]
    public async Task Main_WithHelpSwitch_ShowsUsage(string helpSwitch)
    {
        await Program.Main([this.Folder, helpSwitch]);

        Assert.Contains("Usage: YAF.LanguageManager pathToLanguageFiles", this.Output);
    }

    [Fact]
    public async Task Main_WithEmptyPath_ReportsMissingPath()
    {
        await Program.Main(["  "]);

        Assert.Contains("Path to Language files not defined!", this.Output);
    }

    [Fact]
    public async Task Main_WithMissingFolder_ReportsError()
    {
        await Program.Main([Path.Combine(this.Folder, "does-not-exist"), "-sync"]);

        Assert.Contains("ERROR:", this.Output);
    }

    [Fact]
    public async Task Main_WithoutSwitches_LeavesFilesUntouched()
    {
        var path = this.English(("COMMON", [("OK", "Ok")]));
        var before = await File.ReadAllTextAsync(path);

        await Program.Main([this.Folder]);

        Assert.Equal(before, await File.ReadAllTextAsync(path));
    }

    [Fact]
    public async Task Main_WithAllSwitches_RunsEveryOperation()
    {
        this.English(("COMMON", [("OK", "Ok"), ("CANCEL", "Cancel")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Ok")])));

        await Program.Main([this.Folder, "-sync", "-translateGoogle", "-minify", "-uglify"]);

        var result = Program.LoadFile(german);
        Assert.Equal("[de] Cancel", Text(result, "COMMON", "CANCEL"));
        Assert.Equal("[de] Ok", Text(result, "COMMON", "OK"));
        Assert.Contains("All Languages Synced!", this.Output);
        Assert.Contains("Done!", this.Output);
        Assert.Contains("\n", await File.ReadAllTextAsync(german));
    }

    [Fact]
    public async Task Main_WithRelativePath_ResolvesAgainstCurrentDirectory()
    {
        this.English(("COMMON", [("OK", "Ok")]));
        var current = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(Path.GetDirectoryName(this.Folder)!);

            await Program.Main([Path.GetFileName(this.Folder), "-minify"]);
        }
        finally
        {
            Directory.SetCurrentDirectory(current);
        }

        Assert.DoesNotContain("\n", await File.ReadAllTextAsync(Path.Combine(this.Folder, "english.json")));
    }

    [Fact]
    public async Task Sync_AddsMissingPagesAndResources()
    {
        this.English(
            ("COMMON", [("OK", "Ok"), ("CANCEL", "Cancel")]),
            ("LOGIN", [("TITLE", "Login")]),
            ("TEMPLATES", [("MAIL", "<b>{0}</b>")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Okay")])));

        await Program.SyncLanguagesAsync(this.Folder, this.Languages());

        var result = Program.LoadFile(german);
        Assert.Equal("Okay", Text(result, "COMMON", "OK"));
        Assert.Equal("[de] Cancel", Text(result, "COMMON", "CANCEL"));
        Assert.Equal("[de] Login", Text(result, "LOGIN", "TITLE"));
        Assert.Equal("<b>{0}</b>", Text(result, "TEMPLATES", "MAIL"));
        Assert.Contains("Adding Missing Resource Page 'LOGIN'", this.Output);
        Assert.Contains("Adding Missing Resource 'CANCEL' ('COMMON')", this.Output);
        Assert.False(File.Exists($"{german}.tmp"));

        // template text is never sent to the translator
        Assert.DoesNotContain(this.Handler.Requests, r => r.Query.Contains("%7B0%7D"));
    }

    [Fact]
    public async Task Sync_WhenTranslationFails_FallsBackToSourceText()
    {
        this.UseHandler(FakeTranslationHandler.Failing());
        this.English(("COMMON", [("OK", "Ok"), ("CANCEL", "Cancel")]), ("LOGIN", [("TITLE", "Login")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Okay")])));

        await Program.SyncLanguagesAsync(this.Folder, this.Languages());

        var result = Program.LoadFile(german);
        Assert.Equal("Cancel", Text(result, "COMMON", "CANCEL"));
        Assert.Equal("Login", Text(result, "LOGIN", "TITLE"));
    }

    [Fact]
    public async Task Sync_RemovesLegacyPagesAndResources()
    {
        this.English(("COMMON", [("OK", "Ok")]));
        var german = this.WriteLanguage(
            "german.json",
            Lang("German", "de", ("COMMON", [("OK", "Okay"), ("OLD", "Alt")]), ("LEGACY", [("X", "Y")])));

        await Program.SyncLanguagesAsync(this.Folder, this.Languages());

        var result = Program.LoadFile(german);
        Assert.Single(result.Resources.Page);
        Assert.Equal("OK", Assert.Single(result.Resources.Page[0].Resource).Tag);
        Assert.Contains("Removed no longer used Resource Page 'LEGACY'", this.Output);
        Assert.Contains("Removed no longer used Resource 'OLD'", this.Output);
    }

    [Fact]
    public async Task Sync_WhenEverythingIsInSync_DoesNotRewriteFiles()
    {
        var english = this.English(("COMMON", [("OK", "Ok")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Okay")])));
        var englishBefore = await File.ReadAllTextAsync(english);
        var germanBefore = await File.ReadAllTextAsync(german);

        await Program.SyncLanguagesAsync(this.Folder, this.Languages());

        Assert.Equal(englishBefore, await File.ReadAllTextAsync(english));
        Assert.Equal(germanBefore, await File.ReadAllTextAsync(german));
        Assert.DoesNotContain("Writing Output File", this.Output);
        Assert.Contains("All Languages Synced!", this.Output);
    }

    [Fact]
    public async Task AutoTranslate_TranslatesOnlyUntranslatedResources()
    {
        var english = this.English(
            ("COMMON", [("OK", "Ok"), ("CANCEL", "Cancel"), ("COOKIES_TEXT", "Cookies"), ("SELECT_LOCALE_JS", "en-US"), ("NEW", "New")]),
            ("TEMPLATES", [("MAIL", "Mail")]),
            ("NOT_SYNCED", [("A", "A")]));
        var german = this.WriteLanguage(
            "german.json",
            Lang(
                "German",
                "de",
                ("COMMON", [("OK", "OK"), ("CANCEL", "Abbrechen"), ("COOKIES_TEXT", "Cookies"), ("SELECT_LOCALE_JS", "en-US")]),
                ("TEMPLATES", [("MAIL", "Mail")])));
        var englishBefore = await File.ReadAllTextAsync(english);

        await Program.AutoTranslateWithGoogleFreeAsync(this.Languages(), Program.LoadFile(english));

        var result = Program.LoadFile(german);
        Assert.Equal("[de] Ok", Text(result, "COMMON", "OK"));
        Assert.Equal("Abbrechen", Text(result, "COMMON", "CANCEL"));
        Assert.Equal("Cookies", Text(result, "COMMON", "COOKIES_TEXT"));
        Assert.Equal("en-US", Text(result, "COMMON", "SELECT_LOCALE_JS"));
        Assert.Equal("Mail", Text(result, "TEMPLATES", "MAIL"));
        Assert.Null(Text(result, "COMMON", "NEW"));
        Assert.Null(result.Resources.Page.Find(p => p.Name == "NOT_SYNCED"));
        Assert.Single(this.Handler.Requests);
        Assert.Equal(englishBefore, await File.ReadAllTextAsync(english));
    }

    [Fact]
    public async Task AutoTranslate_WhenTranslationFails_KeepsOriginalText()
    {
        this.UseHandler(FakeTranslationHandler.Failing());
        var english = this.English(("COMMON", [("OK", "Ok")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Ok")])));

        await Program.AutoTranslateWithGoogleFreeAsync(this.Languages(), Program.LoadFile(english));

        Assert.Equal("Ok", Text(Program.LoadFile(german), "COMMON", "OK"));
        Assert.Contains($"Writing Output File '{german}'", this.Output);
    }

    [Fact]
    public async Task AutoTranslate_WhenNothingToTranslate_DoesNotRewriteFile()
    {
        var english = this.English(("COMMON", [("OK", "Ok")]));
        var german = this.WriteLanguage("german.json", Lang("German", "de", ("COMMON", [("OK", "Okay")])));
        var before = await File.ReadAllTextAsync(german);

        await Program.AutoTranslateWithGoogleFreeAsync(this.Languages(), Program.LoadFile(english));

        Assert.Equal(before, await File.ReadAllTextAsync(german));
        Assert.Empty(this.Handler.Requests);
    }

    [Fact]
    public async Task Minify_WritesCompactJson()
    {
        var english = this.English(("COMMON", [("OK", "Ok")]));

        await Program.MinifyLanguagesAsync(this.Languages());

        var content = await File.ReadAllTextAsync(english);
        Assert.DoesNotContain("\n", content);
        Assert.Equal("Ok", Text(Program.LoadFile(english), "COMMON", "OK"));
        Assert.Contains("Done!", this.Output);
    }

    [Fact]
    public async Task Uglify_WritesIndentedJson()
    {
        var english = this.English(("COMMON", [("OK", "Ok")]));
        await Program.MinifyLanguagesAsync(this.Languages());

        await Program.UglifyLanguagesAsync(this.Languages());

        var content = await File.ReadAllTextAsync(english);
        Assert.Contains("\n", content);
        Assert.Equal("Ok", Text(Program.LoadFile(english), "COMMON", "OK"));
    }

    [Fact]
    public void LoadFile_NormalizesAndSortsPagesAndTags()
    {
        var path = this.WriteLanguage(
            "english.json",
            Lang("English", "en", ("zeta", [("b", "B"), ("a", "A")]), ("alpha", [("x", "X")])));

        var result = Program.LoadFile(path);

        Assert.Equal("English", result.Resources.Language);
        Assert.Equal("en", result.Resources.Code);
        Assert.Equal(["ALPHA", "ZETA"], result.Resources.Page.Select(p => p.Name));
        Assert.Equal(["A", "B"], result.Resources.Page[1].Resource.Select(r => r.Tag));
    }

    [Fact]
    public void LoadFile_AcceptsSingleResourceObject()
    {
        var path = this.WriteLanguage(
            "english.json",
            """
            {"Resources":{"@language":"English","@code":"en","page":[{"@name":"common","Resource":{"@tag":"ok","#text":"Ok"}}]}}
            """);

        var result = Program.LoadFile(path);

        var resource = Assert.Single(result.Resources.Page[0].Resource);
        Assert.Equal("OK", resource.Tag);
        Assert.Equal("Ok", resource.Text);
    }

    [Fact]
    public async Task WriteResourcesFile_ReplacesExistingFileAtomically()
    {
        var path = this.English(("COMMON", [("OK", "Ok")]));
        var file = Program.LoadFile(path);
        file.Resources.Page[0].Resource[0].Text = "Changed";

        await Program.WriteResourcesFileAsync(path, file, Formatting.None);

        Assert.Equal("Changed", Text(Program.LoadFile(path), "COMMON", "OK"));
        Assert.False(File.Exists($"{path}.tmp"));
    }

    [Fact]
    public async Task TranslateWithGoogle_ReturnsTranslatedTextAndEscapesInput()
    {
        var result = await Program.TranslateWithGoogleAsync("a&b c", "pt-BR");

        Assert.Equal("[pt-BR] a&b c", result);
        var uri = Assert.Single(this.Handler.Requests);
        Assert.Equal("translate.googleapis.com", uri.Host);
        Assert.Contains("tl=pt-BR", uri.Query);
        Assert.Contains("q=a%26b%20c", uri.OriginalString);
    }

    [Fact]
    public async Task TranslateWithGoogle_OnError_ReturnsNull()
    {
        this.UseHandler(FakeTranslationHandler.Failing());

        Assert.Null(await Program.TranslateWithGoogleAsync("Hello", "de"));
    }

    [Fact]
    public async Task TranslateWithGoogle_OnUnexpectedPayload_ReturnsNull()
    {
        this.UseHandler(new FakeTranslationHandler(_ => FakeTranslationHandler.Json("[]")));

        Assert.Null(await Program.TranslateWithGoogleAsync("Hello", "de"));
    }

    [Fact]
    public void CreateTranslationHttpClient_SetsUserAgent()
    {
        using var client = Program.CreateTranslationHttpClient();

        Assert.Equal("YAF.NET", client.DefaultRequestHeaders.UserAgent.ToString());
    }

    [Theory]
    [InlineData(0, false, false)]
    [InlineData(1, true, false)]
    [InlineData(2, false, true)]
    [InlineData(3, true, true)]
    public void ShowDivider_WritesExpectedLineBreaks(int showReturn, bool before, bool after)
    {
        var nl = System.Environment.NewLine;
        var blank = "\r\n" + nl;
        var expected = (before ? blank : string.Empty)
                       + "-------------------------------------------------------" + nl
                       + (after ? blank : string.Empty);

        Program.ShowDivider(showReturn);

        Assert.Equal(expected, this.Output);
    }
}
