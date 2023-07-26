using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Swan.Logging;

namespace twitch2tuner
{
    /// <summary>
    /// Provides access to global configuration as defined by environment variables
    /// </summary>
    public static class Config
    {
        public static string Address => $"http://+:{Port}";

        public static string Port => "22708";

        public static string ClientId => Environment.GetEnvironmentVariable("CLIENT_ID");

        public static string ClientSecret => Environment.GetEnvironmentVariable("CLIENT_SECRET");

        public static string TwitchUsername => Environment.GetEnvironmentVariable("TWITCH_USERNAME");

        public static StreamUtility StreamUtility => Environment.GetEnvironmentVariable("STREAM_UTILITY") switch
        {
            "YOUTUBE_DL" => YoutubeDl.Instance,
            "YT_DLP" => YoutubeDl.Instance,
            "STREAMLINK" => Streamlink.Instance,
            _ => Streamlink.Instance,
        };

        public static bool UseProfileAsJustChatting =>
            bool.TryParse(Environment.GetEnvironmentVariable("USE_PROFILE_AS_JUST_CHATTING"), out bool useProfileAsJustChatting)
            && useProfileAsJustChatting;

        public static IEnumerable<string> ChannelsFollowed => Environment.GetEnvironmentVariable("CHANNELS_FOLLOWED")?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? Enumerable.Empty<string>();

        public static List<string> Scopes { get; } = new();

        public static string RefreshToken { get; set; }

        public static string CustomDomain => Environment.GetEnvironmentVariable("CUSTOM_DOMAIN");

        public static string RedirectUri => string.IsNullOrEmpty(CustomDomain) ? "https://google.com" : $"{CustomDomain}/redirect";

        /// <summary>
        /// Verifies that all required configuration values are available
        /// </summary>
        /// <param name="exitOnError">Whether this method should exiting the running process when there is an error</param>
        public static bool Verify(bool exitOnError = true)
        {
            bool result = true;

            // First, verify that we have everything we need.
            if (string.IsNullOrWhiteSpace(ClientId))
            {
                "Unable to load CLIENT_ID environment variable".Log(nameof(Program), LogLevel.Fatal);
                result = false;
            }

            if (string.IsNullOrWhiteSpace(ClientSecret))
            {
                "Unable to load CLIENT_SECRET environment variable".Log(nameof(Program), LogLevel.Fatal);
                result = false;
            }

            if (string.IsNullOrWhiteSpace(TwitchUsername))
            {
                "Unable to load TWITCH_USERNAME environment variable".Log(nameof(Program), LogLevel.Fatal);
                result = false;
            }

            if (exitOnError && !result)
            {
                // This is a hack to wait for logging to be flushed since it is totally asynchronous.
                // See https://github.com/unosquare/swan/issues/221
                Thread.Sleep(TimeSpan.FromSeconds(1));

                Environment.Exit(1);
            }

            return result;
        }
    }
}
