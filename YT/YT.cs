using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using YoutubeExplode.Playlists;

namespace ScriptStack
{
    public class YT : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        public YT()
        {
            if (exportedRoutines != null) return;
            List<Routine> routines = new List<Routine>();

            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_info", (Type)null, "Zeige Informationen zu einem Video."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_manifest", (Type)null, "Zeige Informationen zu einem Video Manifest."));

            // NEU: Playlist
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_playlist_info", (Type)null, "Zeige Informationen zu einer Playlist (inkl. Videoliste). Optional: maxVideos."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_playlist_manifest", (Type)null, "Zeige Manifest-Infos für alle Videos einer Playlist. Optional: maxVideos."));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters)
        {
            if (routine == "yt_get_info")
            {
                return System.Threading.Tasks.Task.Run(() => GetVideoInfo((string)parameters[0]))
                    .GetAwaiter().GetResult();
            }

            if (routine == "yt_get_manifest")
            {
                return System.Threading.Tasks.Task.Run(() => GetVideoManifest((string)parameters[0]))
                    .GetAwaiter().GetResult();
            }

            // NEU: Playlist Info
            if (routine == "yt_get_playlist_info")
            {
                string url = (string)parameters[0];
                int maxVideos = GetOptionalInt(parameters, 1, 0);
                return System.Threading.Tasks.Task.Run(() => GetPlaylistInfo(url, maxVideos))
                    .GetAwaiter().GetResult();
            }

            // NEU: Playlist Manifest
            if (routine == "yt_get_playlist_manifest")
            {
                string url = (string)parameters[0];
                int maxVideos = GetOptionalInt(parameters, 1, 0);
                return System.Threading.Tasks.Task.Run(() => GetPlaylistManifest(url, maxVideos))
                    .GetAwaiter().GetResult();
            }

            return null;
        }

        private static int GetOptionalInt(List<object> parameters, int index, int defaultValue)
        {
            if (parameters == null || parameters.Count <= index || parameters[index] == null)
                return defaultValue;

            try
            {
                // ScriptStack kann int/long/double/string liefern – wir fangen gängig ab
                if (parameters[index] is int i) return i;
                if (parameters[index] is long l) return checked((int)l);
                if (parameters[index] is double d) return checked((int)d);
                if (parameters[index] is string s && int.TryParse(s, out var si)) return si;
            }
            catch { }

            return defaultValue;
        }

        private static async System.Threading.Tasks.Task<ScriptStack.Runtime.ArrayList> GetVideoInfo(string url)
        {
            using var youtube = new YoutubeClient();
            var video = await youtube.Videos.GetAsync(url).ConfigureAwait(false);

            var result = new ScriptStack.Runtime.ArrayList();
            result.Add("title", video.Title);
            result.Add("author", video.Author.ChannelTitle);
            result.Add("duration", video.Duration);

            return result;
        }

        private static async System.Threading.Tasks.Task<ScriptStack.Runtime.ArrayList> GetVideoManifest(string url)
        {
            using var youtube = new YoutubeClient();
            var manifest = await youtube.Videos.Streams.GetManifestAsync(url).ConfigureAwait(false);

            var result = new ScriptStack.Runtime.ArrayList();
            result.Add("videoUrl", url);

            result.Add("muxed", BuildObjectOfStreams(manifest.GetMuxedStreams()));
            result.Add("videoOnly", BuildObjectOfStreams(manifest.GetVideoOnlyStreams()));
            result.Add("audioOnly", BuildObjectOfStreams(manifest.GetAudioOnlyStreams()));

            return result;
        }

        // =========================
        // NEU: PLAYLIST INFO
        // =========================
        private static async System.Threading.Tasks.Task<ScriptStack.Runtime.ArrayList> GetPlaylistInfo(string url, int maxVideos = 0)
        {
            using var youtube = new YoutubeClient();

            var playlist = await youtube.Playlists.GetAsync(url).ConfigureAwait(false);

            var result = new ScriptStack.Runtime.ArrayList();
            result.Add("playlistUrl", url);

            // defensiv (je nach YoutubeExplode Version)
            TryAdd(result, "playlistId", () => playlist.Id.ToString());
            TryAdd(result, "title", () => playlist.Title);
            TryAdd(result, "author", () => playlist.Author.ChannelTitle);
            TryAdd(result, "videoCount", () => playlist.Count);

            // Videos
            var videosObj = new ScriptStack.Runtime.ArrayList();
            int i = 0;

            await foreach (var v in youtube.Playlists.GetVideosAsync(url))
            {
                if (maxVideos > 0 && i >= maxVideos) break;

                var item = new ScriptStack.Runtime.ArrayList();
                item.Add("index", i);
                item.Add("id", v.Id.ToString());
                item.Add("url", $"https://www.youtube.com/watch?v={v.Id}");
                item.Add("title", v.Title);
                item.Add("author", v.Author.ChannelTitle);
                item.Add("duration", v.Duration);

                videosObj.Add(i.ToString(), item);
                i++;
            }

            result.Add("videos", videosObj);
            result.Add("videosReturned", i);

            return result;
        }

        // =========================
        // NEU: PLAYLIST MANIFEST (pro Video)
        // =========================
        private static async System.Threading.Tasks.Task<ScriptStack.Runtime.ArrayList> GetPlaylistManifest(string url, int maxVideos = 0)
        {
            using var youtube = new YoutubeClient();

            var playlist = await youtube.Playlists.GetAsync(url).ConfigureAwait(false);

            var result = new ScriptStack.Runtime.ArrayList();
            result.Add("playlistUrl", url);
            TryAdd(result, "playlistId", () => playlist.Id.ToString());
            TryAdd(result, "title", () => playlist.Title);
            TryAdd(result, "author", () => playlist.Author.ChannelTitle);
            TryAdd(result, "videoCount", () => playlist.Count);

            var videosObj = new ScriptStack.Runtime.ArrayList();
            int i = 0;

            await foreach (var v in youtube.Playlists.GetVideosAsync(url))
            {
                if (maxVideos > 0 && i >= maxVideos) break;

                var videoBlock = new ScriptStack.Runtime.ArrayList();
                videoBlock.Add("index", i);
                videoBlock.Add("id", v.Id.ToString());
                videoBlock.Add("url", $"https://www.youtube.com/watch?v={v.Id}");
                videoBlock.Add("title", v.Title);
                videoBlock.Add("author", v.Author.ChannelTitle);
                videoBlock.Add("duration", v.Duration);

                // Manifest pro Video
                // Hinweis: Stream-URLs sind zeitlich begrenzt (normal bei YouTube).
                var manifest = await youtube.Videos.Streams.GetManifestAsync(v.Id).ConfigureAwait(false);

                videoBlock.Add("muxed", BuildObjectOfStreams(manifest.GetMuxedStreams()));
                videoBlock.Add("videoOnly", BuildObjectOfStreams(manifest.GetVideoOnlyStreams()));
                videoBlock.Add("audioOnly", BuildObjectOfStreams(manifest.GetAudioOnlyStreams()));

                videosObj.Add(i.ToString(), videoBlock);
                i++;
            }

            result.Add("videos", videosObj);
            result.Add("videosReturned", i);

            return result;
        }

        private static ScriptStack.Runtime.ArrayList BuildObjectOfStreams<T>(IEnumerable<T> streams)
        {
            var obj = new ScriptStack.Runtime.ArrayList();

            int i = 0;
            foreach (var s in streams)
            {
                var item = new ScriptStack.Runtime.ArrayList();

                dynamic ds = s!;
                item.Add("container", ds.Container.Name);
                item.Add("sizeBytes", (long)ds.Size.Bytes);
                item.Add("bitrateBps", (long)ds.Bitrate.BitsPerSecond);
                item.Add("url", (string)ds.Url);

                TryAdd(item, "videoCodec", () => (string)ds.VideoCodec);
                TryAdd(item, "videoQuality", () => ds.VideoQuality.ToString());
                TryAdd(item, "width", () => (int)ds.VideoResolution.Width);
                TryAdd(item, "height", () => (int)ds.VideoResolution.Height);

                TryAdd(item, "audioCodec", () => (string)ds.AudioCodec);
                TryAdd(item, "audioLanguage", () => ds.AudioLanguage?.ToString() ?? "");
                TryAdd(item, "audioLangDefault", () => (bool)(ds.IsAudioLanguageDefault ?? false));

                item.Add("itag", ExtractItag((string)ds.Url));

                obj.Add(i.ToString(), item);
                i++;
            }

            return obj;
        }

        private static void TryAdd(ScriptStack.Runtime.ArrayList item, string key, Func<object> getter)
        {
            try { item.Add(key, getter()); } catch { }
        }

        private static string ExtractItag(string streamUrl)
        {
            if (string.IsNullOrWhiteSpace(streamUrl))
                return "";

            try
            {
                var uri = new Uri(streamUrl);
                var q = uri.Query.TrimStart('?')
                    .Split('&', StringSplitOptions.RemoveEmptyEntries);

                foreach (var part in q)
                {
                    var kv = part.Split('=', 2);
                    if (kv.Length == 2 && kv[0] == "itag")
                        return Uri.UnescapeDataString(kv[1]);
                }
            }
            catch { }

            return "";
        }
    }
}
