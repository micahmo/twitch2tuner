# twitch2tuner

[![Docker Cloud Build Status](https://img.shields.io/docker/cloud/build/micahmo/twitch2tuner)](https://hub.docker.com/r/micahmo/twitch2tuner/builds)
[![Docker Pulls](https://img.shields.io/docker/pulls/micahmo/twitch2tuner)](https://hub.docker.com/r/micahmo/twitch2tuner)
[![Docker Image Version (tag latest semver)](https://img.shields.io/docker/v/micahmo/twitch2tuner/latest)](https://hub.docker.com/layers/micahmo/twitch2tuner/latest/images/sha256-3b013fc3e2930eb7806a40514390faab01fe773103d5d91c705aa18959c04b9d?context=explore)

Inspired by the likes of [locast2plex](https://github.com/tgorgdotcom/locast2plex) and [locast2tuner](https://github.com/wouterdebie/locast2tuner) (formerly [locast2dvr](https://github.com/wouterdebie/locast2dvr)) and the fact that there is no officially supported way to watch Twitch on a Roku device, twitch2tuner aims to present live Twitch streams as channels within Plex's [Live TV and DVR feature](https://support.plex.tv/articles/225877347-live-tv-dvr/).

The service acts as an HDHomeRun (m3u) tuner and also proves an XMLTV Electronic Program Guide (EPG). Together, it allows Plex to show Twitch streams as guide listings and play them on any supported Plex client with limited recording functionality. The channel listings are based on the Twitch channels followed by a particular user (set your username as an environment variable when running the container). Since stream end times (and future stream start times) are unknown, the guide is very imprecise. Live streams are listed as starting at the correct time in the past but ending in 24 hours. Offline streams are always shown as being offline for 24 hours. Of course, this data will be updated as the guide updates. See the [caveats section](https://github.com/micahmo/twitch2tuner#caveats-and-known-issues) for more info about the limitations of the guide. Other features of the guide include the game art as the show art, the current stream description as the show description, and a dot plus the name of the game as the show title for quickly identifying live streams in the guide.

![Guide Screenshot](https://user-images.githubusercontent.com/7417301/120251579-00b58380-c250-11eb-92dc-f06aca69cd40.png)

![What's On Screenshot](https://user-images.githubusercontent.com/7417301/120251580-014e1a00-c250-11eb-8a6a-4639025f7c1b.png)

# Setup

## Twitch API

The first requirement is to gain access to the Twitch API. This requires two components, a Client ID and an Access Token.

### Client ID
Go to https://dev.twitch.tv/console/apps and Click `+ Register Your Application`. Add a name, OAuth Redirect URL (which can be anything like `https://google.com`) and a Category. Remember the URL as it will be needed later. After the application is created, click Manage, and save the Client ID.

### Access Token
Modify the following URL so that `CLIENT_ID` is the id generated above, and `REDIRECT_URI` is the URL entered above. Then navigate to the URL in a browser. It will prompt you to authorize the client to access the Twitch API via your account. Currently it only asks for the `user:read:subscriptions` scope, although others may be needed. Available scopes can be seen here: https://dev.twitch.tv/docs/authentication/#scopes
```
https://id.twitch.tv/oauth2/authorize?client_id=CLIENT_ID&redirect_uri=REDIRECT_URI&response_type=token&scope=user:read:subscriptions
```
After you navigate to the link, it will redirect you back to the previously configured redirect URL. For example, if you entered `https://google.com`, it would redirect you to a link that looks like the following. Save the `ACCESS_TOKEN` part.
```
https://www.google.com/#access_token=ACCESS_TOKEN&scope=user%3Aread%3Asubscriptions&token_type=bearer
```

> Note: This token may eventually expire, which will require you to navigate to the oauth2 URL in order to obtain a new token. It is recommended to save the oauth2 URL with your `CLIENT_ID` and `REDIRECT_URL` for quick access in the future.

## Install

Now that you have access to the Twitch API, you can install twitch2tuner. It is intended to be run in a Docker container.
You can use the following docker run command, filling in the `CLIENT_ID`, `ACCESS_TOKEN`, and `USERNAME` as needed.
```
docker run -d --name=twitch2tuner -p 22708:22708 -e CLIENT_ID=... -e ACCESS_TOKEN=... -e USERNAME=... twitch2tuner
```
Alternatively, you can use the following Docker template to easily install the container in Unraid.

https://github.com/micahmo/docker-templates/blob/master/micahmo/twitch2tuner.xml

You can optionally pass a value for the `STREAM_UTILITY` environment variable. The accepted values are `YOUTBUE_DL` and `STREAMLINK` with any other value (or empty) defaulting to `STREAMLINK`. This determines the utility that is used to extract the stream URL from Twitch. This is configurable because occasionally the URLs from one utility or the other begin with an ad placeholder embedded at the beginning of the stream. (Fortunately, there is no ad, but it is annoying to wait for the placeholder to count down.) I've had more luck with the URLs obtained by Streamlink, which is why it is the default, but you can experiment to find the best for you.
```
... -e STREAM_UTILITY=YOUTUBE_DL ...
```

If you'd like to locally test the exact command that is run for a given utility, see the code [here](https://github.com/micahmo/twitch2tuner/blob/cf30f3e12c4906e7e0eb422cf86e9acef384d52a/twitch2tuner/StreamUtility.cs#L71) and [here](https://github.com/micahmo/twitch2tuner/blob/cf30f3e12c4906e7e0eb422cf86e9acef384d52a/twitch2tuner/StreamUtility.cs#L49).

## Plex

Once the container is up and running, you can add the server as a Live TV and DVR in Plex.

In Plex, go to Settings > Live TV & DVR > Set Up Plex DVR.

If the server is not found automatically, click "Don't see your HDHomeRun device? Enter its network address manually" and enter the address and port that the server is running on, like `http://192.168.1.2:22708`.

Once the device is found, click Next. It should detect a channel for each of your followed channels on Twitch. If not, click Scan Channels. Click Continue.

Next it will prompt for a ZIP code to download an electronic guide. In this case, the guide is served by twitch2tuner, so click "Have an XMLTV guide on your server? Click here to use that instead." Then enter the address of the server, followed by `/epg.xml`, like `http://192.168.1.2:22708/epg.xml`. Click Continue.

Plex should load the Electronic Program Guide and match the listings to the channel lineup from the tuner. Click Continue.

Finally, Plex should download the guide, and it should be ready to use!

# Caveats and Known Issues

This is an imperfect solution, as Twitch is obviously not designed to be served in a traditional TV guide. Watch out for the following.
 - If the list of channels you follow on Twitch changes, you will have to "rescan" the tuner to discover new channels. In Plex, go to Settings > Live TV & DVR > where it says "X channels - X enabled" click the "enabled" part. This will bring up the list of channels from the tuner, where you can select Scan Channels to rescan.
 - Since traditional TV guides provide listings far in advance, Plex only updates the guide on a daily basis by default. Of course, Twitch streamers could go live at any time. You can decrease the interval between guide updates in Plex by going to Settings > Live TV & DVR > DVR Settings > and change Guide Refresh Interval to the desired amount. Unfortunately, it does not go any lower than 1 hour, so you may also want to refresh the guide manually. Press the three-dot menu next to Live TV & DVR, and choose Refresh Guide.
    - Note that if the guide has not updated, and a streamer who is currently live is shown as Offline, you may still select their channel and watch. Although the guide may be out of date, the latest info about the channel is retrieved whenever you attempt to watch.
 - This project is very much untested with regards to Plex's DVR feature. As with any project that is piecing together things that were not intended to work together, YMMV!
    - While DVR has has not been thoroughly tested, one thing that does work nicely is pausing and/or rewinding live streams.
 - While the ability to pause and rewind a live stream is nice, there is a small quirk where the stream will end immediately when the streamer goes offline even if you are not at the end. (In other words, it will not play till the end of the stream if you are watching behind live.) 

# Misc

I've found that the guide often does not load correctly the first time on Roku devices. Pressing the back button once the guide is focused often fixes it. There is a Plex forum thread to track the issue [here](https://forums.plex.tv/t/bug-guide-is-blank-on-roku-until-pressing-back-button/707519).

# Credits

Thanks to the following projects which provided inspiration for this project.
* [locast2plex](https://github.com/tgorgdotcom/locast2plex)
* [locast2tuner](https://github.com/wouterdebie/locast2tuner)

Special credit to [IPTVTuner](https://github.com/marklieberman/iptvtuner) as a guide for emulating an HDHomeRun tuner.

Thanks to the following utilities which make it possible to stream from Twitch.
* [youtube-dl](https://github.com/ytdl-org/youtube-dl)
* [Streamlink](https://github.com/streamlink/streamlink)