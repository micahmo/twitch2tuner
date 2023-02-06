using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using EmbedIO;
using Swan.Logging;
using twitch2tuner.EPG;

namespace twitch2tuner
{
    /// <summary>
    /// This class defines the endpoints necessary to simulate an HDHomeRun tuner for Plex to call
    /// </summary>
    public class Tuner
    {
        /// <summary>
        /// Simulates an HDHomeRun tuner object.
        /// </summary>
        /// <remarks>
        /// HT: https://github.com/marklieberman/iptvtuner/blob/master/Handlers/DiscoveryHandler.cs
        /// </remarks>
        public static async Task Discover(IHttpContext context)
        {
            string publicAddress = context.Request.Url.AbsoluteUri.Replace(context.Request.Url.AbsolutePath, string.Empty);

            await context.SendDataAsync(new
            {
                FriendlyName = "Twitch Tuner",
                Manufacturer = "twitch2tuner",
                ModelNumber = "HDTC-2US",
                FirmwareName = "hdhomeruntc_atsc",
                FirmwareVersion = "20150826",
                TunerCount = 1,
                DeviceID = "12345678",
                DeviceAuth = "password",
                BaseURL = publicAddress,
                LineupURL = $"{publicAddress}/lineup.json"
            });
        }

        public static async Task LineupStatus(IHttpContext context)
        {
            await context.SendDataAsync(new
            {
                ScanInProgress = false,
                ScanPossible = true,
                Source = "Cable", // Do not change!
                SourceList = new[] { "Cable" }, // Do not change!
                Found = 1
            });
        }

        public static async Task Lineup(IHttpContext context)
        {
            string publicAddress = context.Request.Url.AbsoluteUri.Replace(context.Request.Url.AbsolutePath, string.Empty);

            List<HDHomeRunLineupItem> lineupItems = new List<HDHomeRunLineupItem>();

            foreach (Channel channel in await ChannelManager.UpdateChannels(UpdateChannelMode.Retrieve))
            {
                lineupItems.Add(new HDHomeRunLineupItem
                {
                    GuideName = channel.DisplayName,
                    GuideNumber = channel.ChannelNumber,
                    HD = true,
                    URL = $"{publicAddress}/getStream/{channel.ChannelNumber}"
                });
            }

            await context.SendDataAsync(lineupItems);
        }

        public static async Task Epg(IHttpContext context)
        {
            Tv tv = new Tv();

            foreach (Channel channel in await ChannelManager.UpdateChannels(UpdateChannelMode.Status))
            {
                tv.Channels.Add(new EPG.Channel
                {
                    Id = channel.DisplayName,
                    Lcn = channel.ChannelNumber,
                    DisplayName = channel.DisplayName,
                    Icon = new Icon {Source = channel.ProfileImageUrl}
                });

                string liveTitle = $"{(char)8226} {{0}} Playing {{1}}"; // 8226 is https://bytetool.web.app/en/ascii/code/0x95/
                string offlineTitle = "{0} Offline";

                tv.Programmes.Add(new Programme
                {
                    Channel = channel.DisplayName,
                    Start = (channel.LiveStreamStartedDateTime ?? DateTime.UtcNow.Subtract(TimeSpan.FromHours(1))).ToString("yyyyMMddHHmmss"),
                    Stop = DateTime.UtcNow.Add(TimeSpan.FromHours(24)).ToString("yyyyMMddHHmmss"),
                    Title = string.Format(channel.IsLive ? liveTitle : offlineTitle, channel.DisplayName, channel.LiveGameName),
                    Description = channel.LiveStreamTitle,
                    Icon = new Icon { Source = channel.GameIsJustChatting && Config.UseProfileAsJustChatting ? channel.ProfileImageUrl : channel.LiveGameArtUrl }
                });
            }

            await using MemoryStream memoryStream = new MemoryStream();
            XmlSerializer serializer = new XmlSerializer(typeof(Tv));
            serializer.Serialize(memoryStream, tv);

            string result = Encoding.UTF8.GetString(memoryStream.ToArray());

            await context.SendStringAsync(result, "text/xml", Encoding.UTF8);
        }

        public static async Task GetStream(IHttpContext context)
        {
            string id = context.RequestedPath.Replace("/", "");

            if ((await ChannelManager.UpdateChannels(UpdateChannelMode.None)).FirstOrDefault(c => c.ChannelNumber == id) is { } channel)
            {
                // Make sure we have the latest stream info for the channel.
                // This is especially important if the guide is out of date.
                // It allows watching a streamer who is really live, even if the guide says they are offline.
                await ChannelManager.UpdateLiveStatus(channel);

                // Grab the stream utility right off the bat, on the off chance that the env var changes.
                StreamUtility streamDiscoveryUtility = Config.StreamUtility;
                $"Using {streamDiscoveryUtility.Name} for stream discovery.".Log(nameof(GetStream), LogLevel.Info);

                // At this time, only youtube-dl (ffmpeg under the hood) can do the actual streaming for Plex.
                StreamUtility streamPlayingUtility = YoutubeDl.Instance; // Instead of Config.StreamUtility
                $"Using {streamPlayingUtility.Name} for stream playing.".Log(nameof(GetStream), LogLevel.Info);

                if (channel.IsLive)
                {
                    string streamUrl;

                    $"Searching for stream URL for streamer twitch.tv/{channel.UserName}".Log(nameof(GetStream), LogLevel.Info);

                    if (StreamUrlMap.TryGetValue(channel.LiveStreamId, out string cachedStreamUrl))
                    {
                        streamUrl = cachedStreamUrl;
                        $"Found cached stream URL for streamer {channel.DisplayName} with stream starting at {channel.LiveStreamStartedDateTime} and stream ID {channel.LiveStreamId}."
                            .Log(nameof(GetStream), LogLevel.Info);
                    }
                    else
                    {
                        // Get and save the URL for this streamer/stream start time
                        streamUrl = StreamUrlMap[channel.LiveStreamId] = streamDiscoveryUtility.GetStreamUrl($"twitch.tv/{channel.UserName}");

                        ($"No cached stream URL for streamer {channel.DisplayName} with stream starting at {channel.LiveStreamStartedDateTime} and stream ID {channel.LiveStreamId}. " +
                         $"Found new URL: {streamUrl}")
                            .Log(nameof(GetStream), LogLevel.Info);
                    }

                    // Now that we have the URL, we can stream it

                    context.Response.ContentType = "video/mp2t";
                    context.Response.SendChunked = true;
                    context.Response.Headers["Cache-Control"] = "no-cache";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
                    await context.Response.OutputStream.FlushAsync();

                    $"Starting stream of channel {channel.DisplayName}".Log(nameof(GetStream), LogLevel.Info);

                    Process streamProcess = streamPlayingUtility.StartStreamProcess(streamUrl);

                    try
                    {
                        await streamProcess.StandardOutput.BaseStream.CopyToAsync(context.Response.OutputStream);

                        // If we get here without throwing an exception, it probably means that the stream ended.
                        $"Channel {channel.DisplayName} stream ended.".Log(nameof(GetStream), LogLevel.Info);

                        // First, remove the stream id from the cache.
                        StreamUrlMap.Remove(channel.LiveStreamId);

                        // Next, wait for the stream playing process to end (with a timeout, in case it hangs)
                        if (streamProcess.WaitForExit((int)TimeSpan.FromSeconds(5).TotalMilliseconds))
                        {
                            $"Stream process {streamPlayingUtility.Name} exited gracefully is {streamProcess.HasExited}.".Log(nameof(GetStream), LogLevel.Info);
                        }
                        else
                        {
                            // The streaming process failed to exit gracefully, so kill it.
                            streamProcess.Kill();
                            await streamProcess.WaitForExitAsync();

                            $"Stream process {streamPlayingUtility.Name} exited successfully (ungracefully) is {streamProcess.HasExited}.".Log(nameof(GetStream), LogLevel.Info);
                        }

                        "Flushing output stream and returning.".Log(nameof(GetStream), LogLevel.Info);
                        await context.Response.OutputStream.FlushAsync();
                    }
                    catch
                    {
                        // Stream until there is an exception, which will occur when the client disconnects.
                        // Then we can kill the stream process process
                        streamProcess.Kill();
                        await streamProcess.WaitForExitAsync();

                        $"Client disconnected. Killing stream of channel {channel.DisplayName}. Stream process {streamPlayingUtility.Name} exited successfully is {streamProcess.HasExited}.".Log(nameof(GetStream), LogLevel.Info);
                    }
                }
            }
        }

        // Maps stream ID to stream URL to allow reusing URLs for the same stream
        private static readonly Dictionary<string, string> StreamUrlMap = new Dictionary<string, string>();
    }
}
