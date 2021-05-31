using System.Diagnostics;
using System.IO;
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

        protected virtual Process StartProcess(string filename, string arguments)
        {
            $"Executing: {filename} {arguments}".Log(GetType().Name, LogLevel.Info);

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
    }

    public class YoutubeDl : StreamUtility
    {
        /// <inheritdoc/>
        public override string Name => "youtube-dl";

        /// <inheritdoc/>
        public override string GetStreamUrl(string channelUrl)
        {
            Process getStreamUrlProcess = base.StartProcess("youtube-dl", $"-q --no-warnings {channelUrl} --get-url");
            return getStreamUrlProcess.StandardOutput.ReadToEnd();
        }

        /// <inheritdoc/>
        public override Process StartStreamProcess(string streamUrl)
        {
            Process youtubeDlProcess = base.StartProcess("youtube-dl", $"-q --no-warnings {streamUrl} -o -");
            return youtubeDlProcess;
        }

        public static StreamUtility Instance { get; set; } = new YoutubeDl();
    }

    public class Streamlink : StreamUtility
    {
        /// <inheritdoc/>
        public override string Name => "streamlink";

        /// <inheritdoc/>
        public override string GetStreamUrl(string channelUrl)
        {
            Process getStreamUrlProcess = base.StartProcess("streamlink", $"{channelUrl} best --quiet --stream-url");
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
