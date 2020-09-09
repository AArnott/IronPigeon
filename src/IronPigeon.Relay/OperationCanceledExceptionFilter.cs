// Copyright (c) Andrew Arnott. All rights reserved.
// Licensed under the Microsoft Reciprocal License (Ms-RL) license. See LICENSE file in the project root for full license information.

namespace IronPigeon.Relay
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Mvc.Filters;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// An ASP.NET controller filter to better report cancelled handling.
    /// </summary>
    /// <remarks>
    /// Inspired by <see href="https://andrewlock.net/using-cancellationtokens-in-asp-net-core-mvc-controllers/">this blog post</see>.
    /// </remarks>
    public class OperationCanceledExceptionFilter : ExceptionFilterAttribute
    {
        private readonly ILogger logger;

        public OperationCanceledExceptionFilter(ILoggerFactory loggerFactory)
        {
            this.logger = loggerFactory.CreateLogger<OperationCanceledExceptionFilter>();
        }

        public override void OnException(ExceptionContext context)
        {
            if (context.Exception is OperationCanceledException)
            {
                this.logger.LogInformation("Request was cancelled.");
                context.ExceptionHandled = true;
                context.Result = new StatusCodeResult(499); // Client Closed Request (nginx)
            }
        }
    }
}
