using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EmbedIO;
using EmbedIO.Actions;
using Swan.Logging;

namespace twitch2tuner
{
    class Program
    {
        static async Task Main()
        {
            // Verify the configuration.
            // This will kill the app if anything is wrong.
            Config.Verify();

            // Use pip (see Dockerfile) to install the latest version of youtube-dl and streamlink every time we start.
            // This command should download on first start, and upgrade on subsequent starts of the image.
            var pipProcess = Process.Start(new ProcessStartInfo
            {
                FileName = "pip3",
                Arguments = "install --upgrade youtube-dl streamlink",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });
            pipProcess?.StandardOutput.ReadToEnd().Log(nameof(Main), LogLevel.Info);

            StartServer();
            await ChannelManager.UpdateChannels(UpdateChannelMode.Retrieve | UpdateChannelMode.Status);
            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Starts the server and wires the tuner endpoints
        /// </summary>
        static void StartServer()
        {
            $"Starting server on {Config.Address}".Log(nameof(StartServer), LogLevel.Info);

            WebServer webServer = new WebServer(o => o
                    .WithUrlPrefix(Config.Address)
                    .WithMode(HttpListenerMode.EmbedIO))
                .WithLocalSessionManager()
                .WithModule(new ActionModule("/discover.json", HttpVerbs.Any, Tuner.Discover))
                .WithModule(new ActionModule("/lineup_status.json", HttpVerbs.Any, Tuner.LineupStatus))
                .WithModule(new ActionModule("/lineup.json", HttpVerbs.Any, Tuner.Lineup))
                .WithModule(new ActionModule("/lineup.post", HttpVerbs.Any, Tuner.Lineup))
                .WithModule(new ActionModule("/epg.xml", HttpVerbs.Any, Tuner.Epg))
                .WithModule(new ActionModule("/getStream", HttpVerbs.Any, Tuner.GetStream))
                .WithModule(new ActionModule("/authorize", HttpVerbs.Any, TwitchApiManager.AuthorizeUser))
                .WithModule(new ActionModule("/redirect", HttpVerbs.Any, TwitchApiManager.HandleAuthorizeUser))
                .WithModule(new ActionModule("/favicon.ico", HttpVerbs.Any, _ => Task.CompletedTask));

            // Important: Do not ignore write exceptions, but let them bubble up.
            // This allows us to see when a client disconnects, so that we can stop streaming.
            // (Otherwise we could stream to a disconnected client indefinitely.)
            webServer.Listener.IgnoreWriteExceptions = false;

            webServer.RunAsync();

            "Server is started and ready to receive connections.".Log(nameof(StartServer), LogLevel.Info);
        }
    }
}
