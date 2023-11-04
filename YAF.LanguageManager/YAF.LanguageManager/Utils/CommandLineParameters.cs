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

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;

/// <summary>
/// The command line parameters.
/// </summary>
public class CommandLineParameters : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CommandLineParameters"/> class.
    /// </summary>
    /// <param name="args">
    /// The args.
    /// </param>
    /// <param name="caseSensitive">
    /// The l case sensitive.
    /// </param>
    public CommandLineParameters(IEnumerable<string> args, bool caseSensitive)
    {
        var index1 = (string)null;

        foreach (var str in args.Select(t => t.Trim()))
        {
            if (str.StartsWith("/") || str.StartsWith("-"))
            {
                var key = str.Remove(0, 1);

                if (!caseSensitive)
                {
                    key = key.ToLower();
                }

                if (key.Contains(":"))
                {
                    var value = key[(key.IndexOf(":", StringComparison.Ordinal) +1)..];
                    key = key.Remove(key.IndexOf(":", StringComparison.Ordinal));

                    this.Switches.Add(key, value);
                }
                else
                {
                    this.Switches.Add(key, null);
                }

                index1 = key;
            }
            else
            {
                if (index1 != null)
                {
                    this.Switches[index1] = str;
                }
                else
                {
                    this.TextLines.Add(str);
                }

                index1 = null;
            }
        }
    }

    /// <summary>
    /// The text count.
    /// </summary>
    public int TextCount => this.TextLines.Count;

    /// <summary>
    /// Gets the switches.
    /// </summary>
    public StringDictionary Switches { get; } = new ();

    /// <summary>
    /// Gets the text lines.
    /// </summary>
    public StringCollection TextLines { get; } = new ();

    /// <summary>
    /// The this.
    /// </summary>
    /// <param name="switchName">
    /// The switch name.
    /// </param>
    /// <returns>
    /// The <see cref="bool"/>.
    /// </returns>
    public bool this[string switchName] => this.Switches.ContainsKey(switchName);

    /// <summary>
    /// The this.
    /// </summary>
    /// <param name="index">
    /// The index.
    /// </param>
    /// <returns>
    /// The <see cref="string"/>.
    /// </returns>
    public string this[int index]
    {
        get
        {
            if (index < this.TextLines.Count)
            {
                return this.TextLines[index];
            }

            throw new IndexOutOfRangeException();
        }
    }

    /// <summary>
    /// The dispose.
    /// </summary>
    public void Dispose()
    {
    }
}