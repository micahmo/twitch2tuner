using System;

namespace twitch2dvr
{
    /// <summary>
    /// Describes a Twitch channel
    /// </summary>
    public class Channel
    {
        /// <summary>
        /// The name of the Twitch channel as seen on Twitch
        /// </summary>
        public string DisplayName { get; set; }

        /// <summary>
        /// The user name of the Twitch channel user
        /// </summary>
        public string UserName { get; set; }

        /// <summary>
        /// Whether the channel is currently live
        /// </summary>
        public bool IsLive { get; set; }

        /// <summary>
        /// If live, the name of the game being played on the channel (null if not live)
        /// </summary>
        public string LiveGameName { get; set; }

        /// <summary>
        /// If live, the artwork of the game being played on the channel (null if not live)
        /// </summary>
        public string LiveGameArtUrl { get; set; }

        /// <summary>
        /// If live, the date/time at which the stream started (null if not live)
        /// </summary>
        public DateTime? LiveStreamStartedDateTime { get; set; }

        /// <summary>
        /// A unique ID to identify the channel in a guide (use the Twitch channel's user ID to globally uniquely identify them)
        /// </summary>
        public string ChannelNumber { get; set; }

        /// <summary>
        /// The profile image for the channel
        /// </summary>
        public string ProfileImageUrl { get; set; }
    }
}
