using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json;
using Swan.Logging;
using TwitchLib.Api;
using TwitchLib.Api.Helix;
using TwitchLib.Api.Helix.Models.Streams.GetStreams;
using TwitchLib.Api.Helix.Models.Users.GetUsers;

namespace twitch2tuner
{
    /// <summary>
    /// A class that can provide Twitch channel information
    /// </summary>
    public class ChannelManager
    {
        /// <summary>
        /// Populates and returns channel information. Set <paramref name="updateChannelMode"/> to determine what kind of update is performed.
        /// Note that it is a flags enum, so pass all that apply.
        /// </summary>
        public static async Task<List<Channel>> UpdateChannels(UpdateChannelMode updateChannelMode)
        {
            if (updateChannelMode.HasFlag(UpdateChannelMode.Retrieve))
            {
                _channels = await RetrieveChannels();
            }

            if (updateChannelMode.HasFlag(UpdateChannelMode.Status))
            {
                // We were asked to update statuses. Let's make sure we actually have a list of channels to update
                if (_channels.Any() == false)
                {
                    "Trying to update channels' statuses, but no channels were found.".Log(nameof(UpdateChannels), LogLevel.Warning);
                }

                // Now update the status
                foreach (Channel channel in _channels)
                {
                    await UpdateLiveStatus(channel);
                }
            }

            return _channels;
        }

        /// <summary>
        /// Updates the live status (whether the streamer is live and other related data) for the given <paramref name="channel"/>.
        /// </summary>
        /// <param name="channel"></param>
        /// <returns></returns>
        public static async Task UpdateLiveStatus(Channel channel)
        {
            // See if the user is streaming
            Stream stream = (await TwitchApiManager.UseTwitchApi(twitchApi => twitchApi.Helix.Streams.GetStreamsAsync(userIds: new List<string> {channel.ChannelNumber}), nameof(TwitchAPI.Helix.Streams.GetStreamsAsync)))?.Streams.FirstOrDefault();

            channel.IsLive = stream is { };
            channel.LiveStreamId = stream?.Id;
            channel.UserName = stream?.UserLogin;
            channel.LiveGameName = stream?.GameName;
            channel.LiveStreamTitle = stream?.Title;
            channel.LiveStreamStartedDateTime = stream?.StartedAt;
            channel.GameIsJustChatting = stream?.GameId == JustChattingGameId;

            // If the user is streaming, get the game art
            if (stream is { })
            {
                var game = (await TwitchApiManager.UseTwitchApi(twitchApi => twitchApi.Helix.Games.GetGamesAsync(new List<string> {stream.GameId}), nameof(TwitchAPI.Helix.Games.GetGamesAsync)))?.Games.FirstOrDefault();
                channel.LiveGameArtUrl = game?.BoxArtUrl.Replace("{width}", "272").Replace("{height}", "380");
            }
        }

        private static async Task<List<Channel>> RetrieveChannels()
        {
            List<Channel> channels = new List<Channel>();

            List<string> followedLogins = new List<string>();
            if (Config.ChannelsFollowed?.Any() != true)
            {
                // Get the Twitch user
                User twitchUser = (await TwitchApiManager.UseTwitchApi(twitchApi => twitchApi.Helix.Users.GetUsersAsync(logins: new List<string> { Config.TwitchUsername }), nameof(TwitchAPI.Helix.Users.GetUsersAsync)))?.Users.FirstOrDefault();

                if (twitchUser is null)
                {
                    $"Unable to find Twitch user {Config.TwitchUsername}".Log(nameof(RetrieveChannels), LogLevel.Error);
                    return channels;
                }

                while (!Config.Scopes.Contains("user:read:follows"))
                {
                    // We must have a user token with this scope.
                    // Wait for the user to authenticate in the browser.

                    "No followed channels specified in CHANNELS_FOLLOWED. Need user token to retrieve user's followed channels via Twitch API. Please visit /authorize. Waiting 10 seconds.".Log(nameof(RetrieveChannels), LogLevel.Info);
                    await Task.Delay(TimeSpan.FromSeconds(10));
                }

                string page = string.Empty;

                while (page is not null)
                {
                    // TODO: Replace this with TwitchLib call when available
                    // https://github.com/TwitchLib/TwitchLib/issues/1112
                    var response = await (await "https://api.twitch.tv"
                        .AppendPathSegment("helix")
                        .AppendPathSegment("channels")
                        .AppendPathSegment("followed")
                        .SetQueryParam("user_id", twitchUser.Id)
                        .SetQueryParam("after", page)
                        .WithHeader("client-id", Config.ClientId)
                        .WithHeader("Authorization", $"Bearer {TwitchApiManager.TwitchApi.Settings.AccessToken}")
                        .AllowAnyHttpStatus()
                        .GetAsync()).GetJsonAsync();

                    try
                    {
                        page = response.pagination.cursor;
                    }
                    catch
                    {
                        page = null;
                    }

                    try
                    {
                        foreach (var follow in response.data)
                        {
                            followedLogins.Add(follow.broadcaster_login);
                        }
                    }
                    catch
                    {
                        $"Error retrieving follows: {JsonConvert.SerializeObject(response)}".Log(nameof(RetrieveChannels), LogLevel.Info);
                    }
                }

                $"Found that user {twitchUser.DisplayName} follows {followedLogins.Count} channels: {string.Join(", ", followedLogins)}".Log(nameof(RetrieveChannels), LogLevel.Info);
            }
            else
            {
                $"Followed channels were provided via config: {string.Join(", ", Config.ChannelsFollowed)}".Log(nameof(RetrieveChannels), LogLevel.Info);
                followedLogins.AddRange(Config.ChannelsFollowed);
            }

            // Translate those follows into Users, 100 at a time
            List<User> followedUsers = new List<User>();
            foreach (var subset in followedLogins.Chunk(100))
            {
                followedUsers.AddRange((await TwitchApiManager.UseTwitchApi(twitchApi => twitchApi.Helix.Users.GetUsersAsync(logins: subset.ToList()), nameof(Helix.Users.GetUsersAsync)))?.Users ?? Enumerable.Empty<User>());
            }

            if (followedUsers.Any())
            {
                $"Translated {followedLogins.Count} follows into {followedUsers.Count} users: {string.Join(", ", followedUsers.Select(u => u.DisplayName).ToArray())}".Log(nameof(RetrieveChannels), LogLevel.Info);

                // Translate those Users into Channels
                foreach (User followedUser in followedUsers)
                {
                    Channel channel = new Channel
                    {
                        DisplayName = followedUser.DisplayName,
                        ChannelNumber = followedUser.Id,
                        ProfileImageUrl = followedUser.ProfileImageUrl
                    };

                    channels.Add(channel);
                }
            }

            return channels;
        }

        private static List<Channel> _channels = new();

        private const string JustChattingGameId = "509658";
    }

    /// <summary>
    /// Describes the type of update that should be performed when calling <see cref="ChannelManager.UpdateChannels"/>.
    /// </summary>
    [Flags]
    public enum UpdateChannelMode
    {
        /// <summary>
        /// Do not perform any updates, simply return the prepopulated channels
        /// </summary>
        None,

        /// <summary>
        /// Retrieves all followed channels for a given user
        /// </summary>
        Retrieve,

        /// <summary>
        /// Updates the status (i.e., whether they are live streaming) for each channel
        /// </summary>
        Status
    }
}
