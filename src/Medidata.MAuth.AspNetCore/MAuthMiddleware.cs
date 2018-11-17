﻿using System.Net;
using System.Threading.Tasks;
using Medidata.MAuth.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Internal;
using Microsoft.Extensions.Logging;

namespace Medidata.MAuth.AspNetCore
{
    internal class MAuthMiddleware
    {
        private readonly MAuthMiddlewareOptions options;
        private readonly MAuthAuthenticator authenticator;
        private readonly RequestDelegate next;
        private readonly ILogger logger;

        public MAuthMiddleware(RequestDelegate next, MAuthMiddlewareOptions options)
        {
            this.next = next;
            this.options = options;
            this.logger = options.Logger;
            this.authenticator = new MAuthAuthenticator(options);
        }

        public async Task Invoke(HttpContext context)
        {
            context.Request.EnableRewind();

            if (!options.Bypass(context.Request) &&
                !await context.TryAuthenticate(authenticator, options.HideExceptionsAndReturnUnauthorized))
            {
                logger.LogDebug("MAuth Authentication failed");
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            context.Request.Body.Rewind();

            await next.Invoke(context);
        }
    }
}
