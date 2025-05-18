/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bjørnar Henden
 * Copyright (C) 2006-2013 Jaben Cargman
 * Copyright (C) 2014-2025 Ingo Herbote
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
using System.Web;

using DeepL.Model;

namespace YAFNET.LanguageManager;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using DeepL;

using Newtonsoft.Json;

using YAFNET.LanguageManager.Utils;

using Formatting = Newtonsoft.Json.Formatting;
using JsonSerializer = Newtonsoft.Json.JsonSerializer;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        using var debug = new SaveDebug(
                   Path.GetDirectoryName(typeof(Program).Module.FullyQualifiedName),
                   "LanguageSync.log");
        try
        {
            var commandLineParameters = new CommandLineParameters(args, false);

            ShowDivider(0);

            Console.WriteLine("YetAnotherForum.NET JSON Language Synchronizer v1.0.4");

            ShowDivider(2);

            if (commandLineParameters["?"] || commandLineParameters["help"]
                                           || commandLineParameters.TextCount < 1)
            {
                Console.WriteLine("Usage: YAF.LanguageManager pathToLanguageFiles\r\n");
                Console.WriteLine("Options:\r\n");
                Console.WriteLine("    -sync                                        Update and synchronize language files");
                Console.WriteLine("    -minify                                      Minify all language files");
                Console.WriteLine("    -uglify                                      Un-Minify all language files");
                Console.WriteLine("    -translateDeepL  -apiKey:123456              Automatic translation via DeepL");
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

                var currentFolder = Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location);

                var languageFolder = Path.GetFullPath(Path.Combine(currentFolder!, commandLineParameters.TextLines[0]));

                var languages = Directory.GetFiles(languageFolder, "*.json").ToList();

                var sourceResource = LoadFile(Path.Combine(languageFolder, "english.json"));

                if (commandLineParameters.Switches.ContainsKey("sync"))
                {
                    await SyncLanguagesAsync(languageFolder, languages).ConfigureAwait(true);
                }

                if (commandLineParameters.Switches.ContainsKey("translateDeepL"))
                {
                    var apiKey = commandLineParameters.Switches["apiKey"];

                    await AutoTranslateWithDeepLAsync(apiKey, languages, sourceResource).ConfigureAwait(true);
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

        var serializer = new JsonSerializer { Formatting = Formatting.Indented };

        // Add Missing Resources
        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            var updateFile = false;

            foreach (var sourcePage in sourceResources.Resources.Page)
            {
                foreach (var sourceResource in sourcePage.Resource)
                {
                    var translatePage = resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name);

                    // Add Missing pages in languages
                    if (translatePage == null)
                    {
                        updateFile = true;
                        DebugHelper.DisplayAndLogMessage($"Adding Missing Resource Page '{sourcePage.Name}' to the language file '{file}'.");

                        resourcesFile.Resources.Page.Add(sourcePage);
                    }
                    else
                    {
                        var translateResource = translatePage.Resource.Find(r => r.Tag == sourceResource.Tag);

                        if (translateResource != null)
                        {
                            continue;
                        }

                        updateFile = true;

                        DebugHelper.DisplayAndLogMessage($"Adding Missing Resource '{sourceResource.Tag}' ('{sourcePage.Name}') to the language file '{file}'.");

                        resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name)!.Resource.Add(sourceResource);
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            await using var sw = new StreamWriter(file);
            await using var writer = new JsonTextWriter(sw);

            serializer.Serialize(writer, resourcesFile);
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
                    foreach (var resource in resourcePage.Resource.Where(
                                 resource => sourcePage.Resource.TrueForAll(res => res.Tag != resource.Tag)))
                    {
                        updateFile = true;

                        DebugHelper.DisplayAndLogMessage(
                            $"Removed no longer used Resource '{resource.Tag}' from language file '{file}'.");

                        deleteResourceFile.Resources.Page.First(p => p.Name == resourcePage.Name).Resource
                            .RemoveAll(r => r.Tag == resource.Tag);
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            ShowDivider(0);

            await using var sw = new StreamWriter(file);
            await using JsonWriter writer = new JsonTextWriter(sw);

             serializer.Serialize(writer, deleteResourceFile);
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

            var serializer = new JsonSerializer
            {
                Formatting = Formatting.None
            };

            await using var sw = new StreamWriter(file);
            await using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, resourcesFile);
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

            var serializer = new JsonSerializer
                                 {
                                     Formatting = Formatting.Indented
                                 };

            await using var sw = new StreamWriter(file);
            await using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, resourcesFile);
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
        languageResource.Resources.Page.ForEach(p => p.Name = p.Name.ToUpper());
        languageResource.Resources.Page.ForEach(p => p.Resource.ForEach(i => i.Tag = i.Tag.ToUpper()));

        languageResource.Resources.Page = [.. languageResource.Resources.Page.OrderBy(p => p.Name)];

        languageResource.Resources.Page.ForEach(p => p.Resource = [.. p.Resource.OrderBy(r => r.Tag)]);

        return languageResource;
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

    /// <summary>Automatic translate languages via DeepL Api.</summary>
    /// <param name="apiKey">The Api Key</param>
    /// <param name="languages">The Language Files</param>
    /// <param name="sourceResources">The source resources.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    private static async Task AutoTranslateWithDeepLAsync(
        string apiKey,
        List<string> languages,
        ResourcesFile sourceResources)
    {
        if (string.IsNullOrEmpty(apiKey))
        {
            return;
        }

        var translator = new Translator(apiKey);

        var deepLanguageList = await translator.GetSourceLanguagesAsync().ConfigureAwait(true);

        foreach (var file in languages)
        {
            var resourcesFile = LoadFile(file);

            var updateFile = false;

            if (resourcesFile.Resources.Code == "en")
            {
                continue;
            }

            if (Array.TrueForAll(deepLanguageList, l => l.Code != resourcesFile.Resources.Code))
            {
                continue;
            }

            foreach (var sourcePage in sourceResources.Resources.Page)
            {
                if (sourcePage.Name.Equals("TEMPLATES"))
                {
                    continue;
                }

                foreach (var sourceResource in sourcePage.Resource)
                {
                    var translatePage = resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name);

                    var translateResource = translatePage!.Resource.Find(r => r.Tag == sourceResource.Tag);

                    if (!string.Equals(
                            sourceResource.Text,
                            translateResource.Text,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    if (translateResource.Tag.Equals("COOKIES_TEXT"))
                    {
                        continue;
                    }

                    DebugHelper.DisplayAndLogMessage(
                        $"Translate Page: '{translatePage.Name}': Tag: '{translateResource.Tag}'");

                    updateFile = true;

                    TextResult translatedText = null;

                    try
                    {
                        translatedText = await translator.TranslateTextAsync(
                            sourceResource.Text,
                            LanguageCode.English,
                            resourcesFile.Resources.Code == "pt"
                                ? LanguageCode.PortugueseEuropean
                                : resourcesFile.Resources.Code).ConfigureAwait(true);
                    }
                    finally
                    {
                        if (translatedText is not null)
                        {
                            translatePage.Resource.Find(r => r.Tag == sourceResource.Tag)!.Text = translatedText.Text;
                        }
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            ShowDivider(0);

            var serializer = new JsonSerializer {Formatting = Formatting.Indented};

            await using var sw = new StreamWriter(file);
            await using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, resourcesFile);
        }
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

                foreach (var sourceResource in sourcePage.Resource)
                {
                    var translatePage = resourcesFile.Resources.Page.Find(p => p.Name == sourcePage.Name);

                    var translateResource = translatePage.Resource.Find(r => r.Tag == sourceResource.Tag);

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


                    string result = null;

                    try
                    {
                        var client = new HttpClient(new HttpClientHandler());

                        client.DefaultRequestHeaders.UserAgent.ParseAdd("YAF.NET");

                        var url =
                            $"https://translate.googleapis.com/translate_a/single?client=gtx&sl=auto&tl={resourcesFile.Resources.Code}&dt=t&q={HttpUtility.HtmlEncode(sourceResource.Text)}";

                        var json = await client.GetFromJsonAsync<dynamic[]>(url);

                        result = Convert.ToString(json[0][0][0]);
                    }
                    finally
                    {
                        if (!string.IsNullOrEmpty(result))
                        {
                            translatePage.Resource.Find(r => r.Tag == sourceResource.Tag).Text = result;
                        }
                    }
                }
            }

            if (!updateFile)
            {
                continue;
            }

            DebugHelper.DisplayAndLogMessage($"Writing Output File '{file}'...");

            ShowDivider(0);

            var serializer = new JsonSerializer { Formatting = Formatting.Indented };

            await using var sw = new StreamWriter(file);
            await using JsonWriter writer = new JsonTextWriter(sw);
            serializer.Serialize(writer, resourcesFile);
        }
    }
}