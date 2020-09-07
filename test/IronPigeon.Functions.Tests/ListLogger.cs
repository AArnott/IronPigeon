// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

internal class ListLogger : ILogger
{
    private readonly ITestOutputHelper xunitLogger;

    public ListLogger(ITestOutputHelper xunitLogger)
    {
        this.Logs = new List<string>();
        this.xunitLogger = xunitLogger;
    }

    public IList<string> Logs { get; set; }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => false;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
    {
        string message = formatter(state, exception);
        this.xunitLogger.WriteLine(message);
        this.Logs.Add(message);
    }
}
