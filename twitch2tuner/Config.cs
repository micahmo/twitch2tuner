using System;

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
    }
}
