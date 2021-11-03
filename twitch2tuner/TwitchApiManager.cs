using System;
using System.Threading.Tasks;
using Swan.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Core;

namespace twitch2tuner
{
    public static class TwitchApiManager
    {
        private static readonly TwitchAPI TwitchApi = new TwitchAPI(settings: new ApiSettings
        {
            ClientId = Config.ClientId,
            AccessToken = Config.AccessToken
        });

        /// <summary>
        /// Invoke the Twitch API
        /// </summary>
        public static Task<T> UseTwitchApi<T>(Func<TwitchAPI, Task<T>> action)
        {
            try
            {
                return action(TwitchApi);
            }
            catch (Exception ex)
            {
                $"Encountered an error invoking the Twitch API: {ex}".Log(nameof(TwitchApiManager), LogLevel.Error);
                return Task.FromResult(default(T));
            }
        }
    }
}
