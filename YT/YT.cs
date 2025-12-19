using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;


namespace ScriptStack
{
    public class YT : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        public YT()
        {
            if (exportedRoutines != null) return;
            List<Routine> routines = new List<Routine>();

            // so lassen wie bei dir (wenn ScriptStack das so erwartet)
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_info", (Type)null, "Zeige Informationen zu einem Video."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yt_get_manifest", (Type)null, "Zeige Informationen zu einem Video Manifest."));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters)
        {

            if (routine == "yt_get_info")
            {

                try
                {
                    // Wichtig: Task.Run um Deadlocks zu vermeiden
                    return System.Threading.Tasks.Task.Run(() => GetVideoInfo((string)parameters[0])).GetAwaiter().GetResult();

                }
                catch (Exception ex)
                {
                    throw; // ScriptStack soll den Fehler sehen
                }
            }

            if (routine == "yt_get_manifest")
            {
                return System.Threading.Tasks.Task.Run(() => GetVideoManifest((string)parameters[0])).GetAwaiter().GetResult();
            }

            return null;
        }

        private static async System.Threading.Tasks.Task<ScriptStack.Runtime.ArrayList> GetVideoInfo(string url)
        {

            using var youtube = new YoutubeClient();
            var videoUrl = url;

            var video = await youtube.Videos.GetAsync(videoUrl).ConfigureAwait(false);

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

        private static ScriptStack.Runtime.ArrayList BuildObjectOfStreams<T>(IEnumerable<T> streams)
        {
            var obj = new ScriptStack.Runtime.ArrayList();

            int i = 0;
            foreach (var s in streams)
            {
                // pro stream ein "objekt" (key/value)
                var item = new ScriptStack.Runtime.ArrayList();

                // YoutubeExplode v8+ (nach deinem Typ-Listing)
                dynamic ds = s!;
                item.Add("container", ds.Container.Name);
                item.Add("sizeBytes", (long)ds.Size.Bytes);
                item.Add("bitrateBps", (long)ds.Bitrate.BitsPerSecond);
                item.Add("url", (string)ds.Url);

                // Video-Felder existieren nur bei Video/Muxed
                TryAdd(item, "videoCodec", () => (string)ds.VideoCodec);
                TryAdd(item, "videoQuality", () => ds.VideoQuality.ToString());
                TryAdd(item, "width", () => (int)ds.VideoResolution.Width);
                TryAdd(item, "height", () => (int)ds.VideoResolution.Height);

                // Audio-Felder existieren nur bei Audio/Muxed
                TryAdd(item, "audioCodec", () => (string)ds.AudioCodec);
                TryAdd(item, "audioLanguage", () => ds.AudioLanguage?.ToString() ?? "");
                TryAdd(item, "audioLangDefault", () => (bool)(ds.IsAudioLanguageDefault ?? false));

                // optional itag aus URL
                item.Add("itag", ExtractItag((string)ds.Url));

                // IMPORTANT: als Objekt unter Key speichern, nicht als "Add(item)" (das wird bei dir flach)
                obj.Add(i.ToString(), item);
                i++;
            }

            return obj;
        }

        private static void TryAdd(ScriptStack.Runtime.ArrayList item, string key, Func<object> getter)
        {
            try { item.Add(key, getter()); } catch { /* Feld existiert nicht */ }
        }

        private static string ExtractItag(string streamUrl)
        {
            // YouTube stream URLs enthalten i.d.R. "...&itag=137&..."
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
