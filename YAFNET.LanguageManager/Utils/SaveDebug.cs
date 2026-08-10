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
    private readonly string logFileName;

    /// <summary>
    /// The log stream.
    /// </summary>
    private readonly StringWriter logStream;

    /// <summary>
    /// The debug listener.
    /// </summary>
    private readonly TextWriterTraceListener debugListener;

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
        this.logFileName = $"{path}\\{logName}";

        try
        {
            this.logStream = new StringWriter();
            this.debugListener = new TextWriterTraceListener(this.logStream);
            Trace.Listeners.Add(this.debugListener);
        }
        catch
        {
            // Ignore
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Releases unmanaged and - optionally - managed resources.
    /// </summary>
    /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        try
        {
            Trace.Flush();
            if (this.logStream.ToString().Length > 0)
            {
                var streamWriter = !File.Exists(this.logFileName)
                    ? File.CreateText(this.logFileName)
                    : File.AppendText(this.logFileName);
                streamWriter.Write(this.logStream.ToString());
                streamWriter.Flush();
                streamWriter.Close();
            }

            this.logStream.Close();
            Trace.Listeners.Remove(this.debugListener);
        }
        catch (Exception)
        {
            // Ignore
        }
    }
}