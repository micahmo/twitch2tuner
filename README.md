![logo](https://raw.githubusercontent.com/micahmo/twitch2tuner/master/twitch2tuner-icon.png)

# twitch2tuner

[![GitHub Workflow Status](https://img.shields.io/github/actions/workflow/status/micahmo/twitch2tuner/docker-image.yml)](https://github.com/micahmo/twitch2tuner/pkgs/container/twitch2tuner)

Inspired by the likes of [locast2plex](https://github.com/tgorgdotcom/locast2plex) and [locast2tuner](https://github.com/wouterdebie/locast2tuner) (formerly [locast2dvr](https://github.com/wouterdebie/locast2dvr)) and the fact that there is no officially supported way to watch Twitch on a Roku device, twitch2tuner aims to present live Twitch streams as channels within Plex's [Live TV and DVR feature](https://support.plex.tv/articles/225877347-live-tv-dvr/).

The service acts as an HDHomeRun (m3u) tuner and also provides an XMLTV Electronic Program Guide (EPG). Together, it allows Plex to show Twitch streams as guide listings and play them on any supported Plex client with limited recording functionality. The channel listings are based on the Twitch channels followed by a particular user (set your username as an environment variable when running the container). Since stream end times (and future stream start times) are unknown, the guide is very imprecise. Live streams are listed as starting at the correct time in the past but ending in 24 hours. Offline streams are always shown as being offline for 24 hours. Of course, this data will be updated as the guide updates. See the [caveats section](https://github.com/micahmo/twitch2tuner#caveats-and-known-issues) for more info about the limitations of the guide. Other features of the guide include the game art as the show art, the current stream description as the show description, and a dot plus the name of the game as the show title for quickly identifying live streams in the guide.

![Guide Screenshot](https://user-images.githubusercontent.com/7417301/120251579-00b58380-c250-11eb-92dc-f06aca69cd40.png)

![What's On Screenshot](https://user-images.githubusercontent.com/7417301/120251580-014e1a00-c250-11eb-8a6a-4639025f7c1b.png)

# Setup

## Twitch API

The first requirement is to gain access to the Twitch API by creating a new application.

1. Go to https://dev.twitch.tv/console/apps.
2. Click `+ Register Your Application`.
3. Fill out Name and Category with anything you choose.
4. For the OAuth Redirect URL, use `https://google.com`.
5. Click Create.
6. Click Manage to get back into the application editor.
7. Copy and save the Client ID. Click New Secret to generate a new Client Secret. Copy and save that as well.

## Followed Channels

twitch2tuner uses the channels that you follow on Twitch as the basis for the guide. However, as of [Feb 2023](https://discuss.dev.twitch.tv/t/follows-endpoints-and-eventsub-subscription-type-are-now-available-in-open-beta/43322), Twitch is restricting access to user follow data. To populate the guide, there are now two options.

### Option 1: Manually Specify

You may specify the list of desired channels as a comma-delimited list in the `CHANNELS_FOLLOWED` environment variable. When this variable is set, it takes precedence.
* Be sure to use the channel's username (from their page's URL), not their display name. For https://www.twitch.tv/espnesports you would use `espnesports`, not `ESPN Esports`.
   ```
   -e CHANNELS_FOLLOWED=xqc,espnesports
   ```

### Option 2: Authorize App
When twitch2tuner starts and `CHANNELS_FOLLOWED` is not set, you will see this message: `No followed channels specified in CHANNELS_FOLLOWED. Need user token to retrieve user's followed channels via Twitch API. Please visit /authorize. Waiting 10 seconds.`.

Once you see this, you must authorize your instance of twitch2tuner to make Twitch API calls on your behalf. The instructions are slightly different depending whether you are accessing your instance directly (without TLS) under via a custom domain with TLS (e.g., behind a reverse proxy or ngrok).

#### No TLS

Start the service using the instructions below. Once you see the message, navigate to `http://IP:Port/authorize` (e.g., `http://192.168.1.2:22708/authorize`). There you will be redirected to Twitch where you can authorize your instance of twitch2tuner.

At this point, it will redirect back to `https://google.com`. Replace that with your `http://IP:Port/redirect`. (e.g., `http://192.168.1.2:22708/redirect/?code=...`). It should authorize, and you may close the tab. Check the logs to verify that it is able to load your follows.

#### TLS

If you are accessing your service via a custom domain with TLS, first, navigate back to the [dev console](https://dev.twitch.tv/console/apps), Manage your application, and add a new Redirect URL for `https://your.domain` followed by `/redirect` (e.g., `https://id.ngrok.io/redirect`).

Then, set the `CUSTOM_DOMAIN` environment variable to your domain name (without `/redirect`).
```
-e CUSTOM_DOMAIN=https://id.ngrok.io
```

Start the service using the instructions below. Once you see the message appear, navigate to `https://your.domain/authorize` (e.g., `https://id.ngrok.io/authorize`). There you will be redirected to Twitch where you can authorize your instance of twitch2tuner. It should redirect back to your domain, at which point you may close the tab. Check the logs to verify that it is able to load your follows.

---

You should not have to do this procedure again as long as the service is running (it handles refreshing your user token internally). However, you will have to do it again after restarting (as the token is not currently persisted outside of the service).

## Install

Now that you have access to the Twitch API, you can install twitch2tuner. It is intended to be run in a Docker container.
You can use the following docker run command, filling in the `CLIENT_ID` and `CLIENT_SECRET`, as retrieved above, and your Twitch `TWITCH_USERNAME`.
```
docker run -d --name=twitch2tuner -p 22708:22708 -e CLIENT_ID=... -e CLIENT_SECRET=... -e TWITCH_USERNAME=... ghcr.io/micahmo/twitch2tuner
```

### Other Options

* `STREAM_UTILITY`

  You can optionally pass a value for the `STREAM_UTILITY` environment variable. The accepted values are `YOUTBUE_DL` and `STREAMLINK` with any other value (or empty) defaulting to `STREAMLINK`. This determines the utility that is used to extract the stream URL from Twitch. This is configurable because occasionally the URLs from one utility or the other begin with an ad placeholder embedded at the beginning of the stream. (Fortunately, there is no ad, but it is annoying to wait for the placeholder to count down.) I've had more luck with the URLs obtained by Streamlink, which is why it is the default, but you can experiment to find the best for you.
  ```
  -e STREAM_UTILITY=YOUTUBE_DL
  ```

   If you'd like to locally test the exact command that is run for a given utility, see the code [here](https://github.com/micahmo/twitch2tuner/blob/cf30f3e12c4906e7e0eb422cf86e9acef384d52a/twitch2tuner/StreamUtility.cs#L71) and [here](https://github.com/micahmo/twitch2tuner/blob/cf30f3e12c4906e7e0eb422cf86e9acef384d52a/twitch2tuner/StreamUtility.cs#L49).


* `USE_PROFILE_AS_JUST_CHATTING`

  
  When a streamer is "Just Chatting", you may wish to see their profile picture, instead of the generic Twitch image, as the program artwork. If so, set `USE_PROFILE_AS_JUST_CHATTING` to `true`.
  ```
  -e USE_PROFILE_AS_JUST_CHATTING=true
  ```

### Unraid

To run the container on Unraid, you can use the Docker template from this repository.
* In Unraid, navigate to the Docker tab. At the bottom, add `https://github.com/micahmo/twitch2tuner` on a new line to the list of template repositories. Save.
* At the bottom, choose Add Container. From the template dropdown, choose `twitch2tuner`.
* Fill out the rest of the options as desired.

## Plex

Once the container is up and running, you can add the server as a Live TV and DVR in Plex.

In Plex, go to Settings > Live TV & DVR > Set Up Plex DVR.

If the server is not found automatically, click "Don't see your HDHomeRun device? Enter its network address manually" and enter the address and port that the server is running on, like `http://192.168.1.2:22708`. Click Connect. You should see that it has discovered a number of channels equaling the number of Twitch channels that are followed by the configured user.

Before allowing you to continue, Plex wants to discover the guide. There is a prompt to enter a ZIP code, but the Twitch guide is served by twitch2tuner, so click "Have an XMLTV guide on your server? Click here to use that instead." Then enter the address of the server, followed by `/epg.xml`, like `http://192.168.1.2:22708/epg.xml`. You may enter anything for the Guide Title. Click Continue.

Plex should load the Electronic Program Guide and match the listings to the channel lineup from the tuner. Click Continue.

Finally, Plex should download the guide, and it should be ready to use!

To keep the guide relatively up-to-date, you should go to Settings > Live TV & DVR > DVR Settings > and change Guide Refresh Interval to the lowest number (1 hour).

# Caveats and Known Issues

This is an imperfect solution, as Twitch is obviously not designed to be served in a traditional TV guide. Watch out for the following.
 - If the list of channels you follow on Twitch changes, you will have to "rescan" the tuner to discover new channels. In Plex, go to Settings > Live TV & DVR > where it says "X channels - X enabled" click the "enabled" part. This will bring up the list of channels from the tuner, where you can select Scan Channels to rescan.
 - Since traditional TV guides provide listings far in advance, Plex only updates the guide on a daily basis by default. Of course, Twitch streamers could go live at any time. You can decrease the interval between guide updates in Plex by going to Settings > Live TV & DVR > DVR Settings > and change Guide Refresh Interval to the desired amount. Unfortunately, it does not go any lower than 1 hour, so you may also want to refresh the guide manually. Press the three-dot menu next to Live TV & DVR, and choose Refresh Guide.
    - Note that if the guide has not updated, and a streamer who is currently live is shown as Offline, you may still select their channel and watch. Although the guide may be out of date, the latest info about the channel is retrieved whenever you attempt to watch.
 - This project is very much untested with regards to Plex's DVR feature. As with any project that is piecing together things that were not intended to work together, YMMV!
    - While DVR has has not been thoroughly tested, one thing that does work nicely is pausing and/or rewinding live streams.
 - While the ability to pause and rewind a live stream is nice, there is a small quirk where the stream will end immediately when the streamer goes offline even if you are not at the end. (In other words, it will not play till the end of the stream if you are watching behind live.) 
 - Playback does not seem to work in the Windows desktop app. It has been tested and works on Roku, Android, and in the Chrome web player.

# Misc

I've found that the guide often does not load correctly the first time on Roku devices. Pressing the back button once the guide is focused often fixes it. There is a Plex forum thread to track the issue [here](https://forums.plex.tv/t/bug-guide-is-blank-on-roku-until-pressing-back-button/707519).

# Contributing

While anyone is free to open new issues, be aware that there is a limit to the level of compatibility that this project will ever have, since it is essentially fitting a square peg in a round hole. In addition, much of the functionality is outside the control of this project (e.g., the Twitch API, Streamlink, yt-dlp, youtube-dl, Plex, etc.). There is no guarantee that any particular issue or quirk can be addressed. That said, contributions are more than welcome where appropriate.

# Credits

Thanks to the following projects which provided inspiration for this project.
* [locast2plex](https://github.com/tgorgdotcom/locast2plex)
* [locast2tuner](https://github.com/wouterdebie/locast2tuner)

Special credit to [IPTVTuner](https://github.com/marklieberman/iptvtuner) as a guide for emulating an HDHomeRun tuner.

Thanks to the following utilities which make it possible to stream from Twitch.
* [yt-dlp](https://github.com/yt-dlp/yt-dlp)
* [youtube-dl](https://github.com/ytdl-org/youtube-dl)
* [Streamlink](https://github.com/streamlink/streamlink)

[Icon](https://www.flaticon.com/free-icon/twitch_3845873) made by <a href="https://www.freepik.com" title="Freepik">Freepik</a> from <a href="https://www.flaticon.com/" title="Flaticon">www.flaticon.com.