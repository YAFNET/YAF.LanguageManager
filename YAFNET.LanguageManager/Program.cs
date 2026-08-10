/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bjørnar Henden
 * Copyright (C) 2006-2013 Jaben Cargman
 * Copyright (C) 2014-2026 Ingo Herbote
 * http://www.yetanotherforum.net/
 *
 * Licensed to the Apache Software Foundation (ASF) under one
 * or more contributor license agreements.  See the NOTICE file
 * distributed with this work for additional information
 * regarding copyright ownership.  The ASF licenses this file
 * to you under the Apache License, Version 2.0 (the
 * "License"); you may not use this file except in compliance
 * with the License.  You may obtain a copy of the License at

 * http://www.apache.org/licenses/LICENSE-2.0

 * Unless required by applicable law or agreed to in writing,
 * software distributed under the License is distributed on an
 * "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
 * KIND, either express or implied.  See the License for the
 * specific language governing permissions and limitations
 * under the License.
 */

using System.Net.Http;
using System.Net.Http.Json;

namespace YAFNET.LanguageManager;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Newtonsoft.Json;

using YAFNET.LanguageManager.Utils;

using Formatting = Newtonsoft.Json.Formatting;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

internal static class Program
{
    /// <summary>
    /// Shared HTTP client for translation requests, reused across all calls to avoid socket exhaustion.
    /// </summary>
    private static readonly HttpClient TranslationHttpClient = CreateTranslationHttpClient();

    private static async Task Main(string[] args)
    {
        using var debug = new SaveDebug(
                   Path.GetDirectoryName(typeof(Program).Module.FullyQualifiedName),
                   "LanguageSync.log");
        try
        {
            var commandLineParameters = new CommandLineParameters(args, false);

            ShowDivider(0);

            var version = Assembly.GetExecutingAssembly().GetName().Version;

            Console.WriteLine($"YetAnotherForum.NET JSON Language Synchronizer v{version}");

            ShowDivider(2);

            if (commandLineParameters["?"] || commandLineParameters["help"]
                                           || commandLineParameters.TextCount < 1)
            {
                Console.WriteLine("Usage: YAF.LanguageManager pathToLanguageFiles\r\n");
                Console.WriteLine("Options:\r\n");
                Console.WriteLine("    -sync                                        Update and synchronize language files");
                Console.WriteLine("    -minify                                      Minify all language files");
                Console.WriteLine("    -uglify                                      Un-Minify all language files");
                Console.WriteLine("    -translateGoogle                             Automatic translation via Google API");
                ShowDivider(1);
            }
            else
            {
                if (string.IsNullOrEmpty(commandLineParameters.TextLines[0]))
                {
                    Console.WriteLine("Path to Language files not defined!");
                    return;
                }

                var languageFolder = Path.GetFullPath(commandLineParameters.TextLines[0], Directory.GetCurrentDirectory());

                var languages = Directory.GetFiles(languageFolder, "*.json").ToList();

                var sourceResource = LoadFile(Path.Combine(languageFolder, "english.json"));

                if (commandLineParameters.Switches.ContainsKey("sync"))
                {
                    await SyncLanguagesAsync(languageFolder, languages).ConfigureAwait(true);
                }

                if (commandLineParameters.Switches.ContainsKey("translateGoogle"))
                {
                    await AutoTranslateWithGoogleFreeAsync(languages, sourceResource).ConfigureAwait(true);
                }

                if (commandLineParameters.Switches.ContainsKey("minify"))
                {
                    await MinifyLanguagesAsync(languages).ConfigureAwait(true);
                }

                if (commandLineParameters.Switches.ContainsKey("uglify"))
                {
                    await UglifyLanguagesAsync(languages).ConfigureAwait(true);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR: {ex.Message}");
            DebugHelper.DebugExceptionMessage(ex);
        }
    }

    /// <summary>
    /// Synchronizes languages.
    /// </summary>
    /// <param name="languageFolder">The language folder.</param>
    /// <param name="languages">The languages.</param>
    private static async Task SyncLanguagesAsync(string languageFolder, List<string> languages)
    {
        DebugHelper.DisplayAndLogMessage(
                   $"Reading Languages Folder {languageFolder} ...");

        var sourceResources = LoadFile(Path.Combine(languageFolder, "english.json"));

        // Resolves the text to use for a new resource: template content is copied verbatim
        // (it may contain HTML/placeholders that machine translation would corrupt), everything
        // else is auto-translated, falling back to the source text if translation fails.
        async Task<string> ResolveTextAsync(string pageName, string sourceText, string targetLanguageCode)
        {
            if (pageName.Equals("TEMPLATES"))
            {
                return sourceText;
            }

            var result = await TranslateWithGoogleAsync(sourceText, targetLanguageCode);

            return !string.IsNullOrEmpty(result) ? result : sourceText;
        }

        // Add Missing Resources
        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            var updateFile = false;

            foreach (var sourcePage in sourceResources.Resources.Page)
            {
                var translatePage = resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name);

                // Add Missing pages in languages
                if (translatePage == null)
                {
                    updateFile = true;
                    DebugHelper.DisplayAndLogMessage($"Adding Missing Resource Page '{sourcePage.Name}' to the language file '{file}'.");

                    var translatedPage = new Page() { Name = sourcePage.Name, Resource = [] };

                    // translate page
                    foreach (var resource in sourcePage.Resource)
                    {
                        var text = await ResolveTextAsync(sourcePage.Name, resource.Text, resourcesFile.Resources.Code);

                        translatedPage.Resource.Add(new Resource
                            { Tag = resource.Tag, Text = text });
                    }

                    resourcesFile.Resources.Page.Add(translatedPage);
                }
                else
                {
                    foreach (var sourceResource in sourcePage.Resource)
                    {
                        var translateResource = translatePage.Resource.Find(r => r.Tag == sourceResource.Tag);

                        if (translateResource != null)
                        {
                            continue;
                        }

                        updateFile = true;

                        DebugHelper.DisplayAndLogMessage($"Adding Missing Resource '{sourceResource.Tag}' ('{sourcePage.Name}') to the language file '{file}'.");

                        var text = await ResolveTextAsync(sourcePage.Name, sourceResource.Text, resourcesFile.Resources.Code);

                        translatePage.Resource.Add(new Resource
                            { Tag = sourceResource.Tag, Text = text });
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            await WriteResourcesFileAsync(file, resourcesFile, Formatting.Indented);
        }

        // Remove legacy Resources
        foreach (var file in languages)
        {
            var updateFile = false;

            var resourcesFile = LoadFile(file);

            if (resourcesFile.Resources.Code == "en")
            {
                continue;
            }

            var deleteResourceFile = LoadFile(file);

            foreach (var resourcePage in resourcesFile.Resources.Page)
            {
                var sourcePage = sourceResources.Resources.Page.Find(p => p.Name == resourcePage.Name);

                if (sourcePage == null)
                {
                    updateFile = true;

                    DebugHelper.DisplayAndLogMessage(
                        $"Removed no longer used Resource Page '{resourcePage.Name}' from language file '{file}'.");

                    deleteResourceFile.Resources.Page.RemoveAll(p => p.Name == resourcePage.Name);
                }
                else
                {
                    foreach (var tag in resourcePage.Resource.Where(
                                 resource => sourcePage.Resource.TrueForAll(res => res.Tag != resource.Tag)).Select(resource => resource.Tag))
                    {
                        updateFile = true;

                        DebugHelper.DisplayAndLogMessage(
                            $"Removed no longer used Resource '{tag}' from language file '{file}'.");

                        deleteResourceFile.Resources.Page.First(p => p.Name == resourcePage.Name).Resource
                            .RemoveAll(r => r.Tag == tag);
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            ShowDivider(0);

            await WriteResourcesFileAsync(file, deleteResourceFile, Formatting.Indented);
        }

        DebugHelper.DisplayAndLogMessage("All Languages Synced!");
    }

    /// <summary>
    /// Minify all languages.
    /// </summary>
    /// <param name="languages">The languages.</param>
    private static async Task MinifyLanguagesAsync(IEnumerable<string> languages)
    {
        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            await WriteResourcesFileAsync(file, resourcesFile, Formatting.None);
        }

        Console.WriteLine("Done!");
    }

    /// <summary>
    /// Un-Minify all languages.
    /// </summary>
    /// <param name="languages">The languages.</param>
    private static async Task UglifyLanguagesAsync(IEnumerable<string> languages)
    {
        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            await WriteResourcesFileAsync(file, resourcesFile, Formatting.Indented);
        }

        Console.WriteLine("Done!");
    }

    /// <summary>
    /// Loads the Resource JSON file.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns>ResourcesFile.</returns>
    private static ResourcesFile LoadFile(string filePath)
    {
        using var file = File.OpenText(filePath);
        using var reader = new JsonTextReader(file);
        var serializer = new JsonSerializer();
        var languageResource = serializer.Deserialize<ResourcesFile>(reader);

        // transform the page and tag name ToUpper...
        languageResource.Resources.Page.ForEach(p => p.Name = p.Name.ToUpperInvariant());
        languageResource.Resources.Page.ForEach(p => p.Resource.ForEach(i => i.Tag = i.Tag.ToUpperInvariant()));

        languageResource.Resources.Page = [.. languageResource.Resources.Page.OrderBy(p => p.Name)];

        languageResource.Resources.Page.ForEach(p => p.Resource = [.. p.Resource.OrderBy(r => r.Tag)]);

        return languageResource;
    }

    /// <summary>
    /// Serializes and writes the resources file atomically: the new content is written to a temp
    /// file first and only swapped in on success, so a failure mid-write can't leave a truncated
    /// or corrupted language file on disk.
    /// </summary>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="resourcesFile">The resources file to write.</param>
    /// <param name="formatting">The JSON formatting to use.</param>
    private static async Task WriteResourcesFileAsync(string filePath, ResourcesFile resourcesFile, Formatting formatting)
    {
        var tempFilePath = $"{filePath}.tmp";

        var serializer = new JsonSerializer { Formatting = formatting };

        await using (var sw = new StreamWriter(tempFilePath))
        await using (JsonWriter writer = new JsonTextWriter(sw))
        {
            serializer.Serialize(writer, resourcesFile);
        }

        File.Move(tempFilePath, filePath, true);
    }

    /// <summary>
    /// The show divider.
    /// </summary>
    /// <param name="showReturn">
    /// The show return.
    /// </param>
    private static void ShowDivider(int showReturn)
    {
        if ((showReturn & 1) == 1)
        {
            Console.WriteLine("\r\n");
        }

        Console.WriteLine("-------------------------------------------------------");

        if ((showReturn & 2) != 2)
        {
            return;
        }

        Console.WriteLine("\r\n");
    }

    /// <summary>
    /// Automatic translate languages via google translate Api.
    /// </summary>
    /// <param name="languages">The Language Files</param>
    /// <param name="sourceResources">The source resources.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private static async Task AutoTranslateWithGoogleFreeAsync(
        List<string> languages,
        ResourcesFile sourceResources)
    {
        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            var updateFile = false;

            if (resourcesFile.Resources.Code == "en")
            {
                continue;
            }

            foreach (var sourcePage in sourceResources.Resources.Page)
            {
                if (sourcePage.Name.Equals("TEMPLATES"))
                {
                    continue;
                }

                var translatePage = resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name);

                // Skip pages the target file hasn't been synced with yet; run -sync first.
                if (translatePage == null)
                {
                    continue;
                }

                foreach (var sourceResource in sourcePage.Resource)
                {
                    var translateResource = translatePage.Resource.Find(r => r.Tag == sourceResource.Tag);

                    // Skip resources the target file hasn't been synced with yet; run -sync first.
                    if (translateResource == null)
                    {
                        continue;
                    }

                    if (!string.Equals(
                            sourceResource.Text,
                            translateResource.Text,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    switch (translateResource.Tag)
                    {
                        case "COOKIES_TEXT":
                        case "SELECT_LOCALE_JS":
                            continue;
                    }

                    DebugHelper.DisplayAndLogMessage(
                        $"Translate Page: '{translatePage.Name}': Tag: '{translateResource.Tag}'");

                    updateFile = true;

                    var result = await TranslateWithGoogleAsync(sourceResource.Text, resourcesFile.Resources.Code);

                    if (!string.IsNullOrEmpty(result))
                    {
                        translateResource.Text = result;
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            ShowDivider(0);

            await WriteResourcesFileAsync(file, resourcesFile, Formatting.Indented);
        }
    }

    /// <summary>
    /// Translates the with google asynchronous.
    /// </summary>
    /// <param name="inputToTranslate">The input to translate.</param>
    /// <param name="targetLanguageCode">The target language code.</param>
    /// <returns>System.Threading.Tasks.Task&lt;System.String&gt;.</returns>
    private static async Task<string> TranslateWithGoogleAsync(string inputToTranslate, string targetLanguageCode)
    {
        string result;

        try
        {
            var url =
                $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={Uri.EscapeDataString(targetLanguageCode)}&dt=t&q={Uri.EscapeDataString(inputToTranslate)}";

            var json = await TranslationHttpClient.GetFromJsonAsync<dynamic[]>(url);

            result = Convert.ToString(json[0][0][0]);
        }
        catch (Exception)
        {
            result = null;
        }

        return result;
    }

    /// <summary>
    /// Creates the shared, reusable <see cref="HttpClient"/> used for translation requests.
    /// </summary>
    private static HttpClient CreateTranslationHttpClient()
    {
        var client = new HttpClient(new HttpClientHandler());

        client.DefaultRequestHeaders.UserAgent.ParseAdd("YAF.NET");

        return client;
    }
}