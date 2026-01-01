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

namespace YAFNET.LanguageManager;

using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

/// <summary>
/// Class Page.
/// </summary>
public class Page
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    /// <value>The name.</value>
    [JsonProperty("@name")]
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the resource.
    /// </summary>
    /// <value>The resource.</value>
    [JsonConverter(typeof(ResourceListConverter))]
    public List<Resource> Resource { get; set; }
}

internal class ResourceListConverter : JsonConverter
{
    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        var list = new List<Resource>();

        if (token.Type == JTokenType.Array)
        {
            var array = (JArray)token;

            return array.ToObject<List<Resource>>();
        }

        var item = (JObject)token;

        list.Add(item.ToObject<Resource>());

        return list;
    }

    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
    {
        throw new NotImplementedException();
    }

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
    {
        return objectType.IsGenericType && objectType.GetGenericTypeDefinition() == typeof(List<>);
    }
}

/// <summary>
/// Class Resource.
/// </summary>
[JsonObject]
public class Resource
{
    [JsonProperty("@tag")]
    public string Tag { get; set; }

    [JsonProperty("#text")]
    public string Text { get; set; }
}

public class Resources
{
    [JsonProperty("@language")]
    public string Language { get; set; }

    [JsonProperty("@code")]
    public string Code { get; set; }

    [JsonProperty("page")]
    public List<Page> Page { get; set; }
}

[JsonObject]
public class ResourcesFile
{
    public Resources Resources { get; set; }
}