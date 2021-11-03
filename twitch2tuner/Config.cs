using System;
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

        public static string AccessToken => Environment.GetEnvironmentVariable("ACCESS_TOKEN");

        public static string TwitchUsername => Environment.GetEnvironmentVariable("TWITCH_USERNAME");

        public static StreamUtility StreamUtility => Environment.GetEnvironmentVariable("STREAM_UTILITY") switch
        {
            "YOUTUBE_DL" => YoutubeDl.Instance,
            "STREAMLINK" => Streamlink.Instance,
            _ => Streamlink.Instance,
        };

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

            if (string.IsNullOrWhiteSpace(AccessToken))
            {
                "Unable to load ACCESS_TOKEN environment variable".Log(nameof(Program), LogLevel.Fatal);
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
