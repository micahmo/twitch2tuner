using System.Diagnostics;
using System.IO;
using System.Linq;
using Swan.Logging;

namespace twitch2tuner
{
    public abstract class StreamUtility
    {
        /// <summary>
        /// A user-friendly name of the stream process
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Extracts a stream url for a given <paramref name="channelUrl"/>.
        /// </summary>
        public abstract string GetStreamUrl(string channelUrl);

        /// <summary>
        /// Returns a <see cref="Stream"/> for the given <paramref name="streamUrl"/>.
        /// </summary>
        public abstract Process StartStreamProcess(string streamUrl);

        protected virtual Process StartProcess(string filename, string arguments, bool log = true)
        {
            if (log)
            {
                $"Executing: {filename} {arguments}".Log(GetType().Name, LogLevel.Info);
            }

            Process process = Process.Start(new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            return process;
        }

        protected virtual Process StartProcess(string filename, params StreamUtilityArgument[] arguments)
        {
            $"Executing: {filename} {string.Join(" ", arguments.Where(arg => arg.IsValid).Select(arg => arg.LogInstead ?? arg.Argument))}".Log(GetType().Name, LogLevel.Info);

            return StartProcess(filename, string.Join(" ", arguments.Where(arg => arg.IsValid).Select(arg => arg.Argument)), log: false);
        }
    }

    public record StreamUtilityArgument(string Argument, string LogInstead = null)
    {
        public bool IsValid => !string.IsNullOrEmpty(Argument);
    }

    public class YtDlp : StreamUtility
    {
        /// <inheritdoc/>
        public override string Name => "yt-dlp";

        /// <inheritdoc/>
        public override string GetStreamUrl(string channelUrl)
        {
            Process getStreamUrlProcess = base.StartProcess("yt-dlp", $"-q --no-warnings {channelUrl} --get-url --add-header \"X-Device-Id: twitch-web-wall-mason\" --add-header \"Device-ID: twitch-web-wall-mason\" --downloader ffmpeg");
            return getStreamUrlProcess.StandardOutput.ReadToEnd();
        }

        /// <inheritdoc/>
        public override Process StartStreamProcess(string streamUrl)
        {
            Process ytDlpProcess = base.StartProcess("yt-dlp", $"-q --no-warnings {streamUrl} --add-header \"X-Device-Id: twitch-web-wall-mason\" --add-header \"Device-ID: twitch-web-wall-mason\" --downloader ffmpeg -o -");
            return ytDlpProcess;
        }

        public static StreamUtility Instance { get; set; } = new YtDlp();
    }

    public class Streamlink : StreamUtility
    {
        /// <inheritdoc/>
        public override string Name => "streamlink";

        /// <inheritdoc/>
        public override string GetStreamUrl(string channelUrl)
        {
            string authorizationHeader = string.IsNullOrEmpty(Config.OauthToken) ? string.Empty : $"\"--http-header=Authorization=OAuth {Config.OauthToken}\"";
            Process getStreamUrlProcess = base.StartProcess("streamlink",
                new StreamUtilityArgument(channelUrl),
                new StreamUtilityArgument("best"),
                new StreamUtilityArgument(authorizationHeader, LogInstead: "[OAuth Token Redacted]"),
                new StreamUtilityArgument("--stream-url"),
                new StreamUtilityArgument("--twitch-disable-ads"));
            return getStreamUrlProcess.StandardOutput.ReadToEnd();
        }

        /// <inheritdoc/>
        /// <remarks>
        /// Does not work with Plex at this time.
        /// </remarks>
        public override Process StartStreamProcess(string streamUrl)
        {
            Process streamlinkProcess = base.StartProcess("streamlink", $"{streamUrl} best --stdout --quiet");
            return streamlinkProcess;
        }

        public static StreamUtility Instance { get; set; } = new Streamlink();
    }
}
