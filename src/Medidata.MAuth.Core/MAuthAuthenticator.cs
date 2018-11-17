﻿using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Org.BouncyCastle.Crypto;

namespace Medidata.MAuth.Core
{
    internal class MAuthAuthenticator
    {
        private readonly ILogger logger;
        private readonly MAuthOptionsBase options;
        private MAuthRequestRetrier retrier;

        public Guid ApplicationUuid => options.ApplicationUuid;

        public MAuthAuthenticator(MAuthOptionsBase options)
        {
            if (options.ApplicationUuid == default(Guid))
                throw new ArgumentException(nameof(options.ApplicationUuid));

            if (options.MAuthServiceUrl == null)
                throw new ArgumentNullException(nameof(options.MAuthServiceUrl));

            if (string.IsNullOrWhiteSpace(options.PrivateKey))
                throw new ArgumentNullException(nameof(options.PrivateKey));

            this.options = options;
            logger = options.Logger;

            retrier = new MAuthRequestRetrier(options);
        }

        public async Task<bool> AuthenticateRequest(HttpRequestMessage request)
        {
            try
            {
                var authInfo = request.GetAuthenticationInfo();
                var appInfo = await GetApplicationInfo(authInfo.ApplicationUuid);

                return authInfo.Payload.Verify(await request.GetSignature(authInfo), appInfo.PublicKey);
            }
            catch (ArgumentException ex)
            {
                logger.LogError(0, ex, "The request has invalid MAuth authentication headers.");
                throw new AuthenticationException("The request has invalid MAuth authentication headers.", ex);
            }
            catch (RetriedRequestException ex)
            {
                logger.LogError(0, ex, "Could not query the application information for the application from the MAuth server.");
                throw new AuthenticationException(
                    "Could not query the application information for the application from the MAuth server.", ex);
            }
            catch (InvalidCipherTextException ex)
            {
                logger.LogError(0, ex, "The request verification failed due to an invalid payload information.");
                throw new AuthenticationException(
                    "The request verification failed due to an invalid payload information.", ex);
            }
            catch (Exception ex)
            {
                logger.LogError(0, ex,
                    "An unexpected error occured during authentication. Please see the inner exception for details.");
                throw new AuthenticationException(
                    "An unexpected error occured during authentication. Please see the inner exception for details.",
                    ex
                );
            }
        }

        private async Task<ApplicationInfo> GetApplicationInfo(Guid applicationUuid) =>
            await (await retrier.GetSuccessfulResponse(applicationUuid, CreateRequest,
                requestAttempts: (int)options.MAuthServiceRetryPolicy + 1))
            .Content
            .FromResponse();

        private HttpRequestMessage CreateRequest(Guid applicationUuid) =>
            new HttpRequestMessage(HttpMethod.Get, new Uri(options.MAuthServiceUrl,
                $"{Constants.MAuthTokenRequestPath}{applicationUuid.ToHyphenString()}.json"));
    }
}
