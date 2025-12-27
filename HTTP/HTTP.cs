using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Threading;

namespace ScriptStack
{

    public class HttpRequest
    {
        public string Method { get; set; } = "GET";
        public string Url { get; set; } = "";
        /// <summary>
        /// Kann aus dem Script z.B. als Dictionary/Map gesetzt werden.
        /// Unterstützt intern:
        /// - Dictionary<string,string>
        /// - IDictionary (z.B. ScriptStack.Runtime.ArrayList wenn IDictionary)
        /// - IEnumerable<string> mit Zeilen "Key: Value"
        /// </summary>
        public object Headers { get; set; } = null;
        // Text-Body
        public string Body { get; set; } = null;
        // Binary-Body (Upload)
        public byte[] BodyBytes { get; set; } = null;
        // Wenn Body != null und ContentType leer => default text/plain; charset=utf-8
        public string ContentType { get; set; } = null;
        // <= 0 => kein Timeout
        public int TimeoutMs { get; set; } = -1;
        // Wenn true => Response als Bytes lesen (Download)
        public bool ReadResponseAsBytes { get; set; } = false;
    }

    public class HttpResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = "";
        public byte[] Bytes { get; set; } = Array.Empty<byte>();
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public bool IsSuccess => Status >= 200 && Status <= 299;
    }


    public class HTTP : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true
        });

        public HTTP()
        {

            if (exportedRoutines != null) 
                return;

            var r = new List<Routine>();

            r.Add(new Routine((Type)null, "http_request", "Erzeugt ein neues HttpRequest-Objekt."));

            r.Add(new Routine((Type)null, "http_send", (Type)null, "Sendet ein HttpRequest und liefert ein HttpResponse zurück."));


            r.Add(new Routine((Type)null, "http_open_stream", (Type)null, "Öffnet HTTP Response als Stream und liefert HttpStream."));
            r.Add(new Routine((Type)null, "http_stream_read_bytes", (Type)null, (Type)null, "Liest bis zu N Bytes. (stream, maxBytes) -> byte[] (leer = Ende)."));
            r.Add(new Routine((Type)null, "http_stream_read_line", (Type)null, "Liest eine Textzeile (UTF-8). -> string oder null bei Ende."));
            r.Add(new Routine((Type)null, "http_stream_close", (Type)null, "Schließt HttpStream."));


            exportedRoutines = r.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string fn, List<object> p)
        {
            try
            {
                if (fn == "http_request")
                    return new HttpRequest();

                if (fn == "http_send")
                    return Send((HttpRequest)p[0]);

                if (fn == "http_open_stream")
                    return OpenStream((HttpRequest)p[0]);

                if (fn == "http_stream_read_bytes")
                    return StreamReadBytes((HttpStream)p[0], Convert.ToInt32(p[1]));

                if (fn == "http_stream_read_line")
                    return StreamReadLine((HttpStream)p[0]);

                if (fn == "http_stream_close")
                {
                    CloseStream((HttpStream)p[0]);
                    return null;
                }


            }
            catch (Exception e)
            {
                throw new ScriptStackException(e.Message);
            }

            return null;
        }

        # region Internals

        private HttpResponse Send(HttpRequest req)
        {
            if (req == null) throw new ScriptStackException("Request ist null.");
            if (string.IsNullOrWhiteSpace(req.Url)) throw new ScriptStackException("Request.Url ist leer.");
            if (string.IsNullOrWhiteSpace(req.Method)) req.Method = "GET";

            using (var msg = new HttpRequestMessage(new HttpMethod(req.Method), req.Url))
            {
                // Content setzen (Bytes bevorzugen)
                if (req.BodyBytes != null)
                {
                    msg.Content = new ByteArrayContent(req.BodyBytes);

                    if (!string.IsNullOrWhiteSpace(req.ContentType))
                        msg.Content.Headers.ContentType =
                            System.Net.Http.Headers.MediaTypeHeaderValue.Parse(req.ContentType);
                }
                else if (req.Body != null)
                {
                    string ct = string.IsNullOrWhiteSpace(req.ContentType)
                        ? "text/plain; charset=utf-8"
                        : req.ContentType;

                    msg.Content = new StringContent(req.Body, Encoding.UTF8, ExtractMediaType(ct));

                    if (ct.Contains(";"))
                        msg.Content.Headers.ContentType =
                            System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct);
                }

                ApplyHeaders(msg, req.Headers);

                using var resp = SendWithOptionalTimeout(msg, req.TimeoutMs);

                var result = new HttpResponse
                {
                    Status = (int)resp.StatusCode,
                    Headers = ReadHeaders(resp)
                };

                if (req.ReadResponseAsBytes)
                {
                    result.Bytes = (resp.Content != null)
                        ? resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult() ?? Array.Empty<byte>()
                        : Array.Empty<byte>();
                }
                else
                {
                    result.Body = (resp.Content != null)
                        ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? ""
                        : "";
                }

                return result;
            }
        }

        private static HttpResponseMessage SendWithOptionalTimeout(HttpRequestMessage req, int timeoutMs)
        {
            if (timeoutMs <= 0)
                return _http.SendAsync(req).GetAwaiter().GetResult();

            using (var cts = new CancellationTokenSource(timeoutMs))
            {
                return _http.SendAsync(req, cts.Token).GetAwaiter().GetResult();
            }
        }

        private static Dictionary<string, string> ReadHeaders(HttpResponseMessage resp)
        {
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var h in resp.Headers)
                dict[h.Key] = string.Join(", ", h.Value);

            if (resp.Content != null)
            {
                foreach (var h in resp.Content.Headers)
                    dict[h.Key] = string.Join(", ", h.Value);
            }

            return dict;
        }

        private static string ExtractMediaType(string contentType)
        {
            int idx = contentType.IndexOf(';');
            if (idx < 0) return contentType.Trim();
            return contentType.Substring(0, idx).Trim();
        }

        /// <summary>
        /// Unterstützt flexibel:
        /// - Dictionary<string,string>
        /// - IDictionary (z.B. ScriptStack Runtime Map/ArrayList falls es IDictionary ist)
        /// - IEnumerable<string> mit "Key: Value"
        /// </summary>
        private static void ApplyHeaders(HttpRequestMessage msg, object headersObj)
        {
            if (headersObj == null) 
                return;

            // 1) Dictionary<string,string>
            if (headersObj is IDictionary<string, string> dictSS)
            {
                foreach (var kv in dictSS)
                    AddHeader(msg, kv.Key, kv.Value);
                return;
            }

            // 2) Allgemeines IDictionary (z.B. Script-Map)
            if (headersObj is System.Collections.IDictionary dict)
            {
                foreach (System.Collections.DictionaryEntry de in dict)
                {
                    string key = de.Key?.ToString();
                    string val = de.Value?.ToString() ?? "";
                    AddHeader(msg, key, val);
                }
                return;
            }

            // 3) IEnumerable<string> von "Key: Value"
            if (headersObj is IEnumerable<string> lines)
            {
                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    int idx = line.IndexOf(':');
                    if (idx <= 0) continue;

                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();
                    AddHeader(msg, key, val);
                }
                return;
            }

            // 4) Fallback: einzelner String "Key: Value"
            if (headersObj is string oneLine)
            {
                int idx = oneLine.IndexOf(':');
                if (idx > 0)
                {
                    string key = oneLine.Substring(0, idx).Trim();
                    string val = oneLine.Substring(idx + 1).Trim();
                    AddHeader(msg, key, val);
                }
            }
        }

        private static void AddHeader(HttpRequestMessage msg, string key, string val)
        {
            key = key?.Trim();
            if (string.IsNullOrEmpty(key)) return;

            val ??= "";

            // Erst versuchen als Request-Header
            if (msg.Headers.TryAddWithoutValidation(key, val))
                return;

            // Dann als Content-Header, aber nur wenn Content existiert
            if (msg.Content != null)
            {
                msg.Content.Headers.TryAddWithoutValidation(key, val);
            }
        }

        public class HttpStream : IDisposable
        {
            internal System.Net.Http.HttpRequestMessage Request;
            internal System.Net.Http.HttpResponseMessage Response;
            internal Stream Stream;
            internal StreamReader Reader;
            internal System.Threading.CancellationTokenSource Cts;

            public int Status { get; internal set; }
            public Dictionary<string, string> Headers { get; internal set; } =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            public bool IsSuccess => Status >= 200 && Status <= 299;
            public bool IsClosed { get; internal set; } = false;

            public void Dispose()
            {
                if (IsClosed) return;
                IsClosed = true;

                try { Reader?.Dispose(); } catch { }
                try { Stream?.Dispose(); } catch { }
                try { Response?.Dispose(); } catch { }
                try { Request?.Dispose(); } catch { }
                try { Cts?.Dispose(); } catch { }
            }

            ~HttpStream()
            {
                // Safety net – aber im Script bitte immer http_stream_close() nutzen!
                try { Dispose(); } catch { }
            }
        }

        private HttpStream OpenStream(HttpRequest req)
        {
            if (req == null) throw new ScriptStackException("Request ist null.");
            if (string.IsNullOrWhiteSpace(req.Url)) throw new ScriptStackException("Request.Url ist leer.");
            if (string.IsNullOrWhiteSpace(req.Method)) req.Method = "GET";

            var msg = new HttpRequestMessage(new HttpMethod(req.Method), req.Url);

            // Body setzen (wie bei Send)
            if (req.BodyBytes != null)
            {
                msg.Content = new ByteArrayContent(req.BodyBytes);
                if (!string.IsNullOrWhiteSpace(req.ContentType))
                    msg.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(req.ContentType);
            }
            else if (req.Body != null)
            {
                string ct = string.IsNullOrWhiteSpace(req.ContentType)
                    ? "text/plain; charset=utf-8"
                    : req.ContentType;

                msg.Content = new StringContent(req.Body, Encoding.UTF8, ExtractMediaType(ct));

                if (ct.Contains(";"))
                    msg.Content.Headers.ContentType =
                        System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct);
            }

            ApplyHeaders(msg, req.Headers);

            CancellationTokenSource cts = null;
            CancellationToken token = default;

            if (req.TimeoutMs > 0)
            {
                cts = new CancellationTokenSource(req.TimeoutMs);
                token = cts.Token;
            }

            // WICHTIG: ResponseHeadersRead -> kein komplettes Buffering
            var resp = _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, token)
                            .GetAwaiter().GetResult();

            var hs = new HttpStream
            {
                Request = msg,
                Response = resp,
                Cts = cts,
                Status = (int)resp.StatusCode,
                Headers = ReadHeaders(resp),
            };

            hs.Stream = resp.Content.ReadAsStreamAsync().GetAwaiter().GetResult();

            // Reader nur für line-based APIs; Bytes lesen geht direkt über hs.Stream
            hs.Reader = new StreamReader(hs.Stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 8192, leaveOpen: true);

            return hs;
        }

        private static byte[] StreamReadBytes(HttpStream hs, int maxBytes)
        {
            if (hs == null || hs.IsClosed || hs.Stream == null) return Array.Empty<byte>();
            if (maxBytes <= 0) maxBytes = 8192;

            var buf = new byte[maxBytes];
            int read = hs.Stream.Read(buf, 0, buf.Length);
            if (read <= 0) return Array.Empty<byte>();

            if (read == buf.Length) return buf;

            var trimmed = new byte[read];
            Buffer.BlockCopy(buf, 0, trimmed, 0, read);
            return trimmed;
        }

        private static string StreamReadLine(HttpStream hs)
        {
            if (hs == null || hs.IsClosed || hs.Reader == null) return null;
            return hs.Reader.ReadLine(); // null bei EOF
        }

        private static void CloseStream(HttpStream hs)
        {
            hs?.Dispose();
        }

        #endregion

    }

}
