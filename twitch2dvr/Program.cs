using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Actions;
using Swan.Logging;

namespace twitch2dvr
{
    class Program
    {
        static async Task Main()
        {
            // Use pip (see Dockerfile) to install the latest version of youtube-dl and ffmpeg every time we start.
            // This command should download on first start, and upgrade on subsequent starts of the image.
            var pipProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "pip",
                Arguments = "install --upgrade youtube-dl",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            pipProcess?.StandardOutput.ReadToEnd().Log(nameof(Main), LogLevel.Info);

            await ChannelManager.UpdateChannels(UpdateChannelMode.Retrieve | UpdateChannelMode.Status);
            StartServer();
            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Starts the server and wires the tuner endpoints
        /// </summary>
        static void StartServer()
        {
            $"Starting server on {Config.Address}".Log(nameof(StartServer), LogLevel.Info);

            new WebServer(o => o
                    .WithUrlPrefix(Config.Address)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithModule(new ActionModule("/discover.json", HttpVerbs.Any, Tuner.Discover))
                .WithModule(new ActionModule("/lineup_status.json", HttpVerbs.Any, Tuner.LineupStatus))
                .WithModule(new ActionModule("/lineup.json", HttpVerbs.Any, Tuner.Lineup))
                .WithModule(new ActionModule("/lineup.post", HttpVerbs.Any, Tuner.Lineup))
                .WithModule(new ActionModule("/epg.xml", HttpVerbs.Any, Tuner.Epg))
                .WithModule(new ActionModule("/getStream", HttpVerbs.Any, Tuner.GetStream))
                .RunAsync();

            "Server is started and ready to receive connections.".Log(nameof(StartServer), LogLevel.Info);
        }
    }
}
