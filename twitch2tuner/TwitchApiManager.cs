using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EmbedIO;
using Newtonsoft.Json;
using Swan.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Auth;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Enums;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace twitch2tuner
{
    public static class TwitchApiManager
    {
        public static TwitchAPI TwitchApi { get; } = new(settings: new ApiSettings
        {
            ClientId = Config.ClientId,
            Secret = Config.ClientSecret
        });

        /// <summary>
        /// Invoke the Twitch API
        /// </summary>
        /// <param name="action">The action to invoke on the current instance of the Twitch API client</param>
        /// <param name="method">The name of the method being invoked (for logging purposes)</param>
        /// <param name="tryRefreshToken">
        /// Whether or not this method is allowed to attempt to get a new access token if the current one is empty or expired.
        /// Set this to false when invoking this method recursively.
        /// </param>
        public static async Task<T> UseTwitchApi<T>(Func<TwitchAPI, Task<T>> action, string method, bool tryRefreshToken = true)
        {
            if (string.IsNullOrEmpty(TwitchApi.Settings.AccessToken) && tryRefreshToken)
            {
                // First time, the access token will be null. Try to get it now.
                await RetrieveAccessToken();
            }

            try
            {
                // Have to await here, instead of just returning the Task,
                // otherwise we'll miss exceptions
                return await action(TwitchApi);
            }
            catch (Exception ex)
            {
                $"Encountered an error invoking method {method} on the Twitch API: {ex}".Log(nameof(TwitchApiManager), LogLevel.Error);

                if ((ex is BadScopeException || ex is ClientIdAndOAuthTokenRequired) && tryRefreshToken)
                {
                    // These are the exceptions potentially caused by an expired access token. Try to get a new one.
                    // If it works, try to invoke this method again on behalf of the caller before giving up.
                    if ((Config.Scopes.Any() && !string.IsNullOrEmpty(Config.RefreshToken) && await RefreshToken()) // User token refresh
                        || await RetrieveAccessToken()) // App token refresh
                    {
                        return await UseTwitchApi(action, method, tryRefreshToken: false);
                    }
                }

                return default;
            }
        }

        private static async Task<bool> RetrieveAccessToken()
        {
            "Attempting to retrieve new access token using client credentials flow.".Log(nameof(RetrieveAccessToken), LogLevel.Info);

            try
            {
                TwitchApi.Settings.AccessToken = await TwitchApi.Auth.GetAccessTokenAsync();
            }
            catch (Exception ex)
            {
                $"There was an error retrieving new app access token. {ex}".Log(nameof(RetrieveAccessToken), LogLevel.Error);
                return false;
            }

            // Do a simple call to see if the token is good
            // Set tryRefreshToken to false so we don't accidentally come back here
            User twitchUser = (await UseTwitchApi(twitchApi => twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> {Config.TwitchUsername}), nameof(TwitchAPI.Helix.Users.GetUsersAsync), tryRefreshToken: false))?.Users.FirstOrDefault();

            if (twitchUser is null)
            {
                $"Got new access token, but unable to retrieve user {Config.TwitchUsername}. New access token may be bad.".Log(nameof(RetrieveAccessToken), LogLevel.Error);
                return false;
            }

            $"Successfully got new access token and was able to retrieve user {JsonConvert.SerializeObject(twitchUser)}.".Log(nameof(RetrieveAccessToken), LogLevel.Info);
            return true;
        }

        private static async Task<bool> RefreshToken()
        {
            RefreshResponse response = await TwitchApi.Auth.RefreshAuthTokenAsync(Config.RefreshToken, Config.ClientSecret);

            if (!string.IsNullOrEmpty(response.AccessToken))
            {
                TwitchApi.Settings.AccessToken = response.AccessToken;
                Config.Scopes.Clear();
                Config.Scopes.AddRange(response.Scopes);
                Config.RefreshToken = response.RefreshToken;

                "Successfully refreshed user token".Log(nameof(RefreshToken), LogLevel.Info);

                return true;
            }

            "Failed to refresh user token".Log(nameof(RefreshToken), LogLevel.Error);

            return false;
        }

        public static Task AuthorizeUser(IHttpContext httpContext)
        {
            string authorizationUrl = TwitchApi.Auth.GetAuthorizationCodeUrl(Config.RedirectUri, new List<AuthScopes> { AuthScopes.Helix_User_Read_Follows });

            $"Redirecting user to authorization code flow URL: {authorizationUrl}".Log(nameof(AuthorizeUser), LogLevel.Info);

            httpContext.Redirect(authorizationUrl);

            return Task.CompletedTask;
        }

        public static async Task HandleAuthorizeUser(IHttpContext httpContext)
        {
            try
            {
                string code = httpContext.Request.QueryString["code"];
                
                // Exchange the authorization for a token
                AuthCodeResponse response = await TwitchApi.Auth.GetAccessTokenFromCodeAsync(code, Config.ClientSecret, Config.RedirectUri);

                TwitchApi.Settings.AccessToken = response.AccessToken;
                Config.Scopes.AddRange(response.Scopes.ToList());
                Config.RefreshToken = response.RefreshToken;

                $"Successfully converted auth code into user access token which expires in {TimeSpan.FromSeconds(response.ExpiresIn)}".Log(nameof(HandleAuthorizeUser), LogLevel.Info);
                await httpContext.SendStringAsync("Authorization successful. You may now close this page.", "text/html", Encoding.UTF8);
            }
            catch (Exception ex)
            {
                $"Error performing authorization: {ex}".Log(nameof(HandleAuthorizeUser), LogLevel.Error);
                await httpContext.SendStringAsync("Authorization failed. Please try again.", "text/html", Encoding.UTF8);
            }
        }
    }
}
