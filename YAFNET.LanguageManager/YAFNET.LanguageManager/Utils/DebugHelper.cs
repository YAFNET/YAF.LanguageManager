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

/// <summary>
/// The debug helper.
/// </summary>
internal static class DebugHelper
{
    /// <summary>
    /// The display and log message.
    /// </summary>
    /// <param name="message">
    /// The message.
    /// </param>
    public static void DisplayAndLogMessage(string message)
    {
        Console.WriteLine(message);
        LogMessage(message);
    }

    /// <summary>
    /// The debug exception message.
    /// </summary>
    /// <param name="e">
    /// The e.
    /// </param>
    public static void DebugExceptionMessage(Exception e)
    {
        var message = string.Empty;

        for (; e != null; e = e.InnerException)
        {
            message =
                $"""
                 {message}{DateTime.Now:g} in {e.Source}\r\n
                                                    Machine: {Environment.MachineName}
                                                    User Name: {Environment.UserName}\r\n{e.Message}\r\n{e.StackTrace}\r\n-----------------------------\r\n
                 """;

            Trace.WriteLine(message);
        }
    }

    /// <summary>
    /// Logs the message.
    /// </summary>
    /// <param name="message">
    /// The message.
    /// </param>
    private static void LogMessage(string message)
    {
        Trace.WriteLine($"{DateTime.Now:G}: {message}");
    }
}