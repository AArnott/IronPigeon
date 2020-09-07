// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using IronPigeon.Functions;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;

public class TestBase
{
    protected const int TestTimeout = 5000;

    /// <summary>
    /// The maximum length of time to wait for something that we expect will happen
    /// within the timeout.
    /// </summary>
    protected static readonly TimeSpan UnexpectedTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(5);

    /// <summary>
    /// The maximum length of time to wait for something that we do not expect will happen
    /// within the timeout.
    /// </summary>
    protected static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(2);

    public TestBase(ITestOutputHelper logger)
    {
        this.Logger = new ListLogger(logger);
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string>
        {
            { "AzureWebJobsStorage", "UseDevelopmentStorage=true" },
        });
        this.AzureStorage = new AzureStorage(configBuilder.Build());
    }

    /// <summary>
    /// Gets or sets the source of <see cref="TimeoutToken"/> that influences
    /// when tests consider themselves to be timed out.
    /// </summary>
    protected CancellationTokenSource TimeoutTokenSource { get; set; } = new CancellationTokenSource(UnexpectedTimeout);

    /// <summary>
    /// Gets a token that is canceled when the test times out,
    /// per the policy set by <see cref="TimeoutTokenSource"/>.
    /// </summary>
    protected CancellationToken TimeoutToken => this.TimeoutTokenSource.Token;

    protected AzureStorage AzureStorage { get; }

    private protected ListLogger Logger { get; }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
    }
}
