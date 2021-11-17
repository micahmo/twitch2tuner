using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Swan.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Core;
using TwitchLib.Api.Core.Exceptions;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace twitch2tuner
{
    public static class TwitchApiManager
    {
        private static readonly TwitchAPI TwitchApi = new TwitchAPI(settings: new ApiSettings
        {
            ClientId = Config.ClientId
        });

        /// <summary>
        /// Invoke the Twitch API
        /// </summary>
        /// <param name="action">The action to invoke on the current instance of the Twitch API client</param>
        /// <param name="tryRefreshToken">
        /// Whether or not this method is allowed to attempt to get a new access token if the current one is empty or expired.
        /// Set this to false when invoking this method recursively.
        /// </param>
        public static async Task<T> UseTwitchApi<T>(Func<TwitchAPI, Task<T>> action, bool tryRefreshToken = true)
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
                $"Encountered an error invoking the Twitch API: {ex}".Log(nameof(TwitchApiManager), LogLevel.Error);

                if ((ex is BadScopeException || ex is ClientIdAndOAuthTokenRequired) && tryRefreshToken)
                {
                    // These are the exceptions potentially caused by an expired access token. Try to get a new one.
                    // If it works, try to invoke this method again on behalf of the caller before giving up.
                    if (await RetrieveAccessToken())
                    {
                        return await UseTwitchApi(action, tryRefreshToken: false);
                    }
                }

                return default;
            }
        }

        private static async Task<bool> RetrieveAccessToken()
        {
            "Attempting to retrieve new access token using client credentials flow.".Log(nameof(UseTwitchApi), LogLevel.Info);

            HttpResponseMessage responseMessage = await new HttpClient().PostAsync(
                "https://id.twitch.tv/oauth2/token?" +
                $"client_id={Config.ClientId}&" +
                $"client_secret={Config.ClientSecret}&" +
                "grant_type=client_credentials&" +
                "scope=user:read:subscriptions", new HttpResponseMessage().Content);

            JsonElement jsonResponse = await responseMessage.Content.ReadFromJsonAsync<JsonElement>();
            jsonResponse.TryGetProperty("access_token", out var accessTokenJson);
            string accessToken = accessTokenJson.ToString();

            if (string.IsNullOrEmpty(accessToken))
            {
                $"There was an error retrieving new access token via client credentials flow. {jsonResponse}".Log(nameof(UseTwitchApi), LogLevel.Error);
                return false;
            }

            // Assign the token to the client
            TwitchApi.Settings.AccessToken = accessToken;

            // Do a simple call to see if the token is good
            // Set tryRefreshToken to false so we don't accidentally come back here
            User twitchUser = (await UseTwitchApi(twitchApi => twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> {Config.TwitchUsername}), tryRefreshToken: false))?.Users.FirstOrDefault();

            if (twitchUser is null)
            {
                $"Got new access token, but unable to retrieve user {Config.TwitchUsername}. New access token may be bad. {jsonResponse}".Log(nameof(RetrieveAccessToken), LogLevel.Error);
                return false;
            }

            $"Successfully got new access token and was able to retrieve user {Config.TwitchUsername}.".Log(nameof(RetrieveAccessToken), LogLevel.Info);
            return true;
        }
    }
}
