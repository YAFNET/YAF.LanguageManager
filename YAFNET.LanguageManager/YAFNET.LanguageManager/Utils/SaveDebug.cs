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

namespace YAFNET.LanguageManager.Utils;

using System;
using System.Diagnostics;
using System.IO;

/// <summary>
/// The save debug.
/// </summary>
public class SaveDebug : IDisposable
{
    /// <summary>
    /// The log file name.
    /// </summary>
    private readonly string _logFileName;

    /// <summary>
    /// The log stream.
    /// </summary>
    private readonly StringWriter _logStream;

    /// <summary>
    /// The debug listener.
    /// </summary>
    private readonly TextWriterTraceListener _debugListener;

    /// <summary>
    /// Initializes a new instance of the <see cref="SaveDebug"/> class.
    /// </summary>
    /// <param name="path">
    /// The path.
    /// </param>
    /// <param name="logName">
    /// The log name.
    /// </param>
    public SaveDebug(string path, string logName)
    {
        this._logFileName = $"{path}\\{logName}";

        try
        {
            this._logStream = new StringWriter();
            this._debugListener = new TextWriterTraceListener(this._logStream);
            Trace.Listeners.Add(this._debugListener);
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// The dispose.
    /// </summary>
    public void Dispose()
    {
        try
        {
            Trace.Flush();
            if (this._logStream.ToString().Length > 0)
            {
                var streamWriter = !File.Exists(this._logFileName)
                                       ? File.CreateText(this._logFileName)
                                       : File.AppendText(this._logFileName);
                streamWriter.Write(this._logStream.ToString());
                streamWriter.Flush();
                streamWriter.Close();
            }

            this._logStream.Close();
            Trace.Listeners.Remove(this._debugListener);
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}