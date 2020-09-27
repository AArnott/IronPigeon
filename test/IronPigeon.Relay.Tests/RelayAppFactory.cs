// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

using IronPigeon.Relay;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

public class RelayAppFactory : WebApplicationFactory<Startup>
{
    internal ITestOutputHelper? Logger { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            if (this.Logger is object)
            {
                services.AddLogging(loggerBuilder =>
                {
                    loggerBuilder.AddTraceSource(new System.Diagnostics.SourceSwitch("Unit test") { Level = System.Diagnostics.SourceLevels.All }, new XunitTraceListener(this.Logger));
                });
            }
        });
    }
}
