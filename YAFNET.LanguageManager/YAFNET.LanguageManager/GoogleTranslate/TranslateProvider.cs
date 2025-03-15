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

namespace YAFNET.LanguageManager.GoogleTranslate;

using System.Threading;
using System.Threading.Tasks;

using Google.Cloud.Translate.V3;

using Nito.AsyncEx;

/// <summary>
/// Class TranslateProvider.
/// Implements the <see cref="ITranslateProvider" />
/// </summary>
/// <seealso cref="ITranslateProvider" />
public class TranslateProvider : ITranslateProvider
{
    private static readonly AsyncLazy<TranslationServiceClient> Client = new(
        () => TranslationServiceClient.CreateAsync());

    /// <summary>
    /// Execute as an asynchronous operation.
    /// </summary>
    /// <param name="projectId">The project Id</param>
    /// <param name="text">The text.</param>
    /// <param name="sourceLanguage">The source language.</param>
    /// <param name="targetLanguage">The target language.</param>
    /// <param name="cancellationToken">The cancellation token that can be used by other objects or threads to receive notice of cancellation.</param>
    /// <returns>A Task&lt;System.String&gt; representing the asynchronous operation.</returns>
    public async Task<string> ExecuteAsync(
        string projectId,
        string text,
        string sourceLanguage,
        string targetLanguage,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var client = await Client;

        var request = new TranslateTextRequest
                          {
                              Contents = {text},
                              TargetLanguageCode = targetLanguage,
                              SourceLanguageCode = sourceLanguage,
                              Parent = $"projects/{projectId}"
                          };

        var response = await client.TranslateTextAsync(request, cancellationToken);

        // Will always contain a single entry
        return response.Translations[0].TranslatedText;
    }
}