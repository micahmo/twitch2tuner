using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using EmbedIO;
using twitch2dvr.EPG;

namespace twitch2dvr
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
                Manufacturer = "twitch2dvr",
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

                tv.Programmes.Add(new Programme
                {
                    Channel = channel.DisplayName,
                    Start = (channel.LiveStreamStartedDateTime ?? DateTime.Now.Subtract(TimeSpan.FromHours(1))).ToString("yyyyMMddHHmmss zzz"),
                    Stop = DateTime.Now.Add(TimeSpan.FromHours(24)).ToString("yyyyMMddHHmmss zzz"),
                    Title = $"{(channel.IsLive ? $"{(char)8226} " : string.Empty)}{channel.DisplayName} Playing {channel.LiveGameName ?? "Offline"}", // https://bytetool.web.app/en/ascii/code/0x95/
                    Icon = new Icon { Source = channel.LiveGameArtUrl }
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

                if (channel.IsLive)
                {
                    Process youtubeDlProcess = Process.Start(new ProcessStartInfo
                    {
                        FileName = "youtube-dl",
                        Arguments = $"-q --no-warnings twitch.tv/{channel.UserName} -o -",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    });

                    Stream stream = youtubeDlProcess.StandardOutput.BaseStream;

                    context.Response.ContentType = "video/mp2t";
                    context.Response.SendChunked = true;
                    context.Response.Headers["Cache-Control"] = "no-cache";
                    context.Response.Headers["Pragma"] = "no-cache";
                    context.Response.Headers["Expires"] = "0";
                    await context.Response.OutputStream.FlushAsync();

                    await stream.CopyToAsync(context.Response.OutputStream, 4096);
                    await context.Response.OutputStream.FlushAsync();
                }
            }
        }
    }
}
