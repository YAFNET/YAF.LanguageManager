/* Yet Another Forum.NET
 * Copyright (C) 2003-2005 Bjørnar Henden
 * Copyright (C) 2006-2013 Jaben Cargman
 * Copyright (C) 2014-2023 Ingo Herbote
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

namespace YAF.LanguageManager.Utils;

using System.IO;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;

public static class JsonSerializerExtensions
{
    public static Task<T> DeserializeJson<T>(this string data, JsonSerializerSettings settings = null)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(data));
        return stream.DeserializeJson<T>(settings);
    }

    public static Task<T> DeserializeJson<T>(this Stream stream, JsonSerializerSettings settings = null)
    {
        return Task.Run(() =>
            {
                using (stream)
                using (var streamReader = new StreamReader(stream))
                using (var jsonReader = new JsonTextReader(streamReader))
                {
                    var serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.Create(settings);
                    return serializer.Deserialize<T>(jsonReader);
                }
            });
    }

    public static async Task<string> SerializeJson<T>(this T instance, JsonSerializerSettings settings = null)
    {
        using var stream = new MemoryStream();
        await instance.SerializeJson(stream, settings).ConfigureAwait(false);
        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static Task SerializeJson<T>(this T instance, Stream toStream, JsonSerializerSettings settings = null)
    {
        return Task.Run(() =>
        {
            using var streamWriter = new StreamWriter(toStream);
            var serializer = settings == null ? JsonSerializer.CreateDefault() : JsonSerializer.Create(settings);
            serializer.Serialize(streamWriter, instance);
        });
    }
}