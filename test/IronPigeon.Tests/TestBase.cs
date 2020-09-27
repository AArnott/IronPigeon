// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IronPigeon;
using MessagePack;
using Nerdbank.Streams;
using Xunit.Abstractions;

public abstract class TestBase : IDisposable
{
    protected const int TestTimeout = 15000;

    /// <summary>
    /// The maximum length of time to wait for something that we expect will happen
    /// within the timeout.
    /// </summary>
    protected static readonly TimeSpan UnexpectedTimeout = Debugger.IsAttached ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(15);

    /// <summary>
    /// The maximum length of time to wait for something that we do not expect will happen
    /// within the timeout.
    /// </summary>
    protected static readonly TimeSpan ExpectedTimeout = TimeSpan.FromSeconds(2);

    public TestBase(ITestOutputHelper logger)
    {
        this.TraceSource = new TraceSource(this.GetType().Name)
        {
            Listeners =
                {
                    new XunitTraceListener(logger),
                },
        };
        this.Logger = logger;
    }

    public ITestOutputHelper Logger { get; }

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

    protected TraceSource TraceSource { get; }

    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected static T SerializeRoundTrip<T>(T value) => MessagePackSerializer.Deserialize<T>(MessagePackSerializer.Serialize(value, Utilities.MessagePackSerializerOptions), Utilities.MessagePackSerializerOptions);

    protected virtual void Dispose(bool disposing)
    {
    }

    protected async Task WriteChunkAsync(Stream target, byte[] buffer)
    {
        using (Substream substream = target.WriteSubstream())
        {
            await substream.WriteAsync(buffer, 0, buffer.Length, this.TimeoutToken);
        }
    }

    protected async Task<byte[]> ReadChunkAsync(Stream source)
    {
        using var sequence = new Sequence<byte>(ArrayPool<byte>.Shared);
        byte[] buffer = new byte[4096];
        using (Stream substream = source.ReadSubstream())
        {
            int bytesRead;
            do
            {
                bytesRead = await substream.ReadAsync(buffer, 0, buffer.Length, this.TimeoutToken);
                sequence.Write(buffer.AsSpan(0, bytesRead));
            }
            while (bytesRead > 0);
        }

        return sequence.AsReadOnlySequence.ToArray();
    }
}
