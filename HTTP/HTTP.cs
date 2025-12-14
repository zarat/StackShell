using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Net.Http;
using System.Text;
using System.Threading;

using ScriptStack.Runtime;

namespace ScriptStack
{
    public class HTTP : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        private static readonly HttpClient _http = new HttpClient(new HttpClientHandler()
        {
            AllowAutoRedirect = true
        });

        public HTTP()
        {
            if (exportedRoutines != null) return;

            var r = new List<Routine>();

            // ============================================================
            // SIMPLE (Body only) - Rückgabe: string (Body)
            // ============================================================

            r.Add(new Routine(typeof(string), "http_get", typeof(string),
                "HTTP GET (Text). Parameter: url(string). Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_delete", typeof(string),
                "HTTP DELETE (Text). Parameter: url(string). Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_options", typeof(string),
                "HTTP OPTIONS (Text). Parameter: url(string). Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_post", typeof(string), typeof(string),
                "HTTP POST (Text). Parameter: url(string), body(string). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put", typeof(string), typeof(string),
                "HTTP PUT (Text). Parameter: url(string), body(string). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_patch", typeof(string), typeof(string),
                "HTTP PATCH (Text). Parameter: url(string), body(string). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            // ============================================================
            // SIMPLE + REQUEST HEADERS (ArrayList von Strings "Key: Value")
            // ============================================================

            r.Add(new Routine(typeof(string), "http_get_h", typeof(string), typeof(ArrayList),
                "HTTP GET (Text) mit Request-Headern. Parameter: url(string), headers(ArrayList von 'Key: Value'). Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_delete_h", typeof(string), typeof(ArrayList),
                "HTTP DELETE (Text) mit Request-Headern. Parameter: url(string), headers(ArrayList von 'Key: Value'). Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_post_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP POST (Text) mit Request-Headern. Parameter: url(string), body(string), headers(ArrayList 'Key: Value'). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP PUT (Text) mit Request-Headern. Parameter: url(string), body(string), headers(ArrayList 'Key: Value'). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_patch_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP PATCH (Text) mit Request-Headern. Parameter: url(string), body(string), headers(ArrayList 'Key: Value'). Default Content-Type: text/plain; charset=utf-8. Rückgabe: Response-Body als string."));

            // ============================================================
            // JSON (Body only) - Rückgabe: string (Body)
            // ============================================================

            r.Add(new Routine(typeof(string), "http_post_json", typeof(string), typeof(string),
                "HTTP POST (JSON). Parameter: url(string), json(string). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put_json", typeof(string), typeof(string),
                "HTTP PUT (JSON). Parameter: url(string), json(string). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_patch_json", typeof(string), typeof(string),
                "HTTP PATCH (JSON). Parameter: url(string), json(string). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_post_json_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP POST (JSON) mit Request-Headern. Parameter: url(string), json(string), headers(ArrayList 'Key: Value'). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put_json_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP PUT (JSON) mit Request-Headern. Parameter: url(string), json(string), headers(ArrayList 'Key: Value'). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_patch_json_h", typeof(string), typeof(string), typeof(ArrayList),
                "HTTP PATCH (JSON) mit Request-Headern. Parameter: url(string), json(string), headers(ArrayList 'Key: Value'). Content-Type: application/json; charset=utf-8. Rückgabe: Response-Body als string."));

            // ============================================================
            // STATUS ONLY - Rückgabe: int
            // ============================================================

            r.Add(new Routine(typeof(int), "http_status", typeof(string),
                "HTTP GET nur Statuscode. Parameter: url(string). Rückgabe: Statuscode int (z.B. 200)."));

            r.Add(new Routine(typeof(int), "http_status_h", typeof(string), typeof(ArrayList),
                "HTTP GET nur Statuscode mit Request-Headern. Parameter: url(string), headers(ArrayList 'Key: Value'). Rückgabe: Statuscode int."));

            // ============================================================
            // HEADERS ONLY (HEAD) - Rückgabe: ArrayList (strukturiert)
            // ============================================================
            // Rückgabe-Format:
            //   result['status']  = int
            //   result['headers'] = ArrayList von Strings "Key: Value"
            r.Add(new Routine(typeof(ArrayList), "http_head", typeof(string),
                "HTTP HEAD (Header only). Parameter: url(string). Rückgabe: ArrayList result mit Keys: "
                + "result['status']=int, result['headers']=ArrayList(\"Key: Value\")."));

            r.Add(new Routine(typeof(ArrayList), "http_head_h", typeof(string), typeof(ArrayList),
                "HTTP HEAD (Header only) mit Request-Headern. Parameter: url(string), headers(ArrayList 'Key: Value'). Rückgabe: ArrayList result mit Keys: "
                + "result['status']=int, result['headers']=ArrayList(\"Key: Value\")."));

            // ============================================================
            // FULL RESPONSE (TEXT) - Rückgabe: ArrayList (strukturiert)
            // ============================================================
            // Rückgabe-Format:
            //   result['status']  = int
            //   result['headers'] = ArrayList von Strings "Key: Value"
            //   result['body']    = string
            r.Add(new Routine(typeof(ArrayList), "http_get_resp", typeof(string),
                "HTTP GET (Full Response, Text). Parameter: url(string). Rückgabe: ArrayList result mit Keys: "
                + "status(int), headers(ArrayList 'Key: Value'), body(string)."));

            r.Add(new Routine(typeof(ArrayList), "http_get_resp_h", typeof(string), typeof(ArrayList),
                "HTTP GET (Full Response, Text) mit Request-Headern. Parameter: url(string), headers(ArrayList 'Key: Value'). Rückgabe: ArrayList result mit Keys: "
                + "status(int), headers(ArrayList 'Key: Value'), body(string)."));

            // ============================================================
            // FULL RESPONSE (BINARY) - Rückgabe: ArrayList (strukturiert)
            // ============================================================
            // Rückgabe-Format:
            //   result['status']  = int
            //   result['headers'] = ArrayList von Strings "Key: Value"
            //   result['bytes']   = ArrayList von int 0..255
            r.Add(new Routine(typeof(ArrayList), "http_get_resp_bin", typeof(string),
                "HTTP GET (Full Response, Binary). Parameter: url(string). Rückgabe: ArrayList result mit Keys: "
                + "status(int), headers(ArrayList 'Key: Value'), bytes(ArrayList int 0..255)."));

            r.Add(new Routine(typeof(ArrayList), "http_get_resp_bin_h", typeof(string), typeof(ArrayList),
                "HTTP GET (Full Response, Binary) mit Request-Headern. Parameter: url(string), headers(ArrayList 'Key: Value'). Rückgabe: ArrayList result mit Keys: "
                + "status(int), headers(ArrayList 'Key: Value'), bytes(ArrayList int 0..255)."));

            // ============================================================
            // BINARY (Body only) - Rückgabe: ArrayList bytes (int 0..255)
            // ============================================================

            r.Add(new Routine(typeof(ArrayList), "http_get_bin", typeof(string),
                "HTTP GET (Binary Download). Parameter: url(string). Rückgabe: ArrayList von int 0..255 (Response-Bytes)."));

            r.Add(new Routine(typeof(ArrayList), "http_get_bin_h", typeof(string), typeof(ArrayList),
                "HTTP GET (Binary Download) mit Request-Headern. Parameter: url(string), headers(ArrayList 'Key: Value'). Rückgabe: ArrayList int 0..255 (Response-Bytes)."));

            r.Add(new Routine(typeof(string), "http_post_bin", typeof(string), typeof(ArrayList),
                "HTTP POST (Binary Upload). Parameter: url(string), bytes(ArrayList int 0..255). Content-Type: application/octet-stream. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put_bin", typeof(string), typeof(ArrayList),
                "HTTP PUT (Binary Upload). Parameter: url(string), bytes(ArrayList int 0..255). Content-Type: application/octet-stream. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_post_bin_h", typeof(string), typeof(ArrayList), typeof(ArrayList),
                "HTTP POST (Binary Upload) mit Request-Headern. Parameter: url(string), bytes(ArrayList int 0..255), headers(ArrayList 'Key: Value'). Content-Type: application/octet-stream. Rückgabe: Response-Body als string."));

            r.Add(new Routine(typeof(string), "http_put_bin_h", typeof(string), typeof(ArrayList), typeof(ArrayList),
                "HTTP PUT (Binary Upload) mit Request-Headern. Parameter: url(string), bytes(ArrayList int 0..255), headers(ArrayList 'Key: Value'). Content-Type: application/octet-stream. Rückgabe: Response-Body als string."));

            // ============================================================
            // RAW REQUEST (Text) - Rückgabe: ArrayList (strukturiert)
            // ============================================================
            // Parameter:
            //   method(string), url(string), body(string), headers(ArrayList 'Key: Value'), contentType(string), timeoutMs(int)
            // Rückgabe:
            //   result['status'], result['headers'], result['body']
            /*
            r.Add(new Routine(typeof(ArrayList), "http_request_resp",
                typeof(string), typeof(string), typeof(string), typeof(ArrayList), typeof(string), typeof(int),
                "Universeller HTTP Request (Full Response, Text). Parameter: "
                + "method(string), url(string), body(string oder \"\"), headers(ArrayList 'Key: Value' oder null), "
                + "contentType(string z.B. 'application/json; charset=utf-8' oder ''), timeoutMs(int, <=0 = kein Timeout). "
                + "Rückgabe: ArrayList result mit Keys: status(int), headers(ArrayList 'Key: Value'), body(string)."));
            */

            exportedRoutines = r.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string fn, List<object> p)
        {
            try
            {
                // ===== SIMPLE TEXT =====
                if (fn == "http_get") return SendText("GET", (string)p[0], null, null, null, -1).body;
                if (fn == "http_delete") return SendText("DELETE", (string)p[0], null, null, null, -1).body;
                if (fn == "http_options") return SendText("OPTIONS", (string)p[0], null, null, null, -1).body;

                if (fn == "http_post") return SendText("POST", (string)p[0], (string)p[1], null, "text/plain; charset=utf-8", -1).body;
                if (fn == "http_put") return SendText("PUT", (string)p[0], (string)p[1], null, "text/plain; charset=utf-8", -1).body;
                if (fn == "http_patch") return SendText("PATCH", (string)p[0], (string)p[1], null, "text/plain; charset=utf-8", -1).body;

                // ===== SIMPLE TEXT + HEADERS =====
                if (fn == "http_get_h") return SendText("GET", (string)p[0], null, (ArrayList)p[1], null, -1).body;
                if (fn == "http_delete_h") return SendText("DELETE", (string)p[0], null, (ArrayList)p[1], null, -1).body;

                if (fn == "http_post_h") return SendText("POST", (string)p[0], (string)p[1], (ArrayList)p[2], "text/plain; charset=utf-8", -1).body;
                if (fn == "http_put_h") return SendText("PUT", (string)p[0], (string)p[1], (ArrayList)p[2], "text/plain; charset=utf-8", -1).body;
                if (fn == "http_patch_h") return SendText("PATCH", (string)p[0], (string)p[1], (ArrayList)p[2], "text/plain; charset=utf-8", -1).body;

                // ===== JSON =====
                if (fn == "http_post_json") return SendText("POST", (string)p[0], (string)p[1], null, "application/json; charset=utf-8", -1).body;
                if (fn == "http_put_json") return SendText("PUT", (string)p[0], (string)p[1], null, "application/json; charset=utf-8", -1).body;
                if (fn == "http_patch_json") return SendText("PATCH", (string)p[0], (string)p[1], null, "application/json; charset=utf-8", -1).body;

                if (fn == "http_post_json_h") return SendText("POST", (string)p[0], (string)p[1], (ArrayList)p[2], "application/json; charset=utf-8", -1).body;
                if (fn == "http_put_json_h") return SendText("PUT", (string)p[0], (string)p[1], (ArrayList)p[2], "application/json; charset=utf-8", -1).body;
                if (fn == "http_patch_json_h") return SendText("PATCH", (string)p[0], (string)p[1], (ArrayList)p[2], "application/json; charset=utf-8", -1).body;

                // ===== STATUS =====
                if (fn == "http_status") return SendText("GET", (string)p[0], null, null, null, -1).status;
                if (fn == "http_status_h") return SendText("GET", (string)p[0], null, (ArrayList)p[1], null, -1).status;

                // ===== HEAD (structured) =====
                if (fn == "http_head")
                {
                    var rr = SendText("HEAD", (string)p[0], null, null, null, -1);
                    return BuildHeadResult(rr.status, rr.headers);
                }
                if (fn == "http_head_h")
                {
                    var rr = SendText("HEAD", (string)p[0], null, (ArrayList)p[1], null, -1);
                    return BuildHeadResult(rr.status, rr.headers);
                }

                // ===== FULL RESPONSE (text, structured) =====
                if (fn == "http_get_resp")
                {
                    var rr = SendText("GET", (string)p[0], null, null, null, -1);
                    return BuildTextResponse(rr.status, rr.headers, rr.body);
                }
                if (fn == "http_get_resp_h")
                {
                    var rr = SendText("GET", (string)p[0], null, (ArrayList)p[1], null, -1);
                    return BuildTextResponse(rr.status, rr.headers, rr.body);
                }

                // ===== FULL RESPONSE (binary, structured) =====
                if (fn == "http_get_resp_bin")
                {
                    var rr = SendBytes("GET", (string)p[0], null, null, null, -1);
                    return BuildBinaryResponse(rr.status, rr.headers, rr.bytes);
                }
                if (fn == "http_get_resp_bin_h")
                {
                    var rr = SendBytes("GET", (string)p[0], null, (ArrayList)p[1], null, -1);
                    return BuildBinaryResponse(rr.status, rr.headers, rr.bytes);
                }

                // ===== BINARY (body only) =====
                if (fn == "http_get_bin")
                {
                    var rr = SendBytes("GET", (string)p[0], null, null, null, -1);
                    return BytesToArrayList(rr.bytes);
                }
                if (fn == "http_get_bin_h")
                {
                    var rr = SendBytes("GET", (string)p[0], null, (ArrayList)p[1], null, -1);
                    return BytesToArrayList(rr.bytes);
                }

                if (fn == "http_post_bin")
                {
                    byte[] data = ArrayListToBytes((ArrayList)p[1]);
                    return SendBytes("POST", (string)p[0], data, null, "application/octet-stream", -1).body;
                }
                if (fn == "http_put_bin")
                {
                    byte[] data = ArrayListToBytes((ArrayList)p[1]);
                    return SendBytes("PUT", (string)p[0], data, null, "application/octet-stream", -1).body;
                }
                if (fn == "http_post_bin_h")
                {
                    byte[] data = ArrayListToBytes((ArrayList)p[1]);
                    return SendBytes("POST", (string)p[0], data, (ArrayList)p[2], "application/octet-stream", -1).body;
                }
                if (fn == "http_put_bin_h")
                {
                    byte[] data = ArrayListToBytes((ArrayList)p[1]);
                    return SendBytes("PUT", (string)p[0], data, (ArrayList)p[2], "application/octet-stream", -1).body;
                }

                // ===== RAW REQUEST RESP =====
                if (fn == "http_request_resp")
                {
                    string method = (string)p[0];
                    string url = (string)p[1];
                    string body = (string)p[2];
                    ArrayList headers = (ArrayList)p[3];
                    string contentType = (string)p[4];
                    int timeoutMs = (int)p[5];

                    var rr = SendText(method, url, body, headers, contentType, timeoutMs);
                    return BuildTextResponse(rr.status, rr.headers, rr.body);
                }
            }
            catch (Exception e)
            {
                throw new ScriptStackException(e.Message);
            }

            return null;
        }

        // =====================================================================
        // Internals
        // =====================================================================

        private (int status, string body, Dictionary<string, string> headers) SendText(
            string method,
            string url,
            string body,
            ArrayList headers,
            string contentType,
            int timeoutMs)
        {
            using (var req = new HttpRequestMessage(new HttpMethod(method), url))
            {
                if (body != null)
                {
                    string ct = string.IsNullOrWhiteSpace(contentType) ? "text/plain; charset=utf-8" : contentType;
                    req.Content = new StringContent(body, Encoding.UTF8, ExtractMediaType(ct));
                    if (ct.Contains(";"))
                        req.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(ct);
                }

                ApplyHeaders(req, headers);

                HttpResponseMessage resp = SendWithOptionalTimeout(req, timeoutMs);

                string respBody = (resp.Content != null)
                    ? resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
                    : "";

                return ((int)resp.StatusCode, respBody ?? "", ReadHeaders(resp));
            }
        }

        private (int status, string body, byte[] bytes, Dictionary<string, string> headers) SendBytes(
            string method,
            string url,
            byte[] bodyBytes,
            ArrayList headers,
            string contentType,
            int timeoutMs)
        {
            using (var req = new HttpRequestMessage(new HttpMethod(method), url))
            {
                if (bodyBytes != null)
                {
                    req.Content = new ByteArrayContent(bodyBytes);
                    if (!string.IsNullOrWhiteSpace(contentType))
                        req.Content.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
                }

                ApplyHeaders(req, headers);

                HttpResponseMessage resp = SendWithOptionalTimeout(req, timeoutMs);

                byte[] respBytes = (resp.Content != null)
                    ? resp.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult()
                    : Array.Empty<byte>();

                string respText = "";
                if (resp.Content != null)
                {
                    try { respText = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult() ?? ""; }
                    catch { respText = ""; }
                }

                return ((int)resp.StatusCode, respText, respBytes ?? Array.Empty<byte>(), ReadHeaders(resp));
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

        // Request-Headers: ArrayList von Strings "Key: Value"
        // (Values iterieren; Keys egal - ArrayList ist Dictionary)
        private static void ApplyHeaders(HttpRequestMessage req, ArrayList headers)
        {
            if (headers == null) return;

            foreach (object v in headers.Values)
            {
                if (v == null) continue;

                string line = v.ToString();
                if (string.IsNullOrWhiteSpace(line)) continue;

                int idx = line.IndexOf(':');
                if (idx <= 0) continue;

                string key = line.Substring(0, idx).Trim();
                string val = line.Substring(idx + 1).Trim();
                if (key.Length == 0) continue;

                if (!req.Headers.TryAddWithoutValidation(key, val))
                {
                    if (req.Content == null)
                        req.Content = new StringContent("", Encoding.UTF8, "text/plain");

                    req.Content.Headers.TryAddWithoutValidation(key, val);
                }
            }
        }

        // ================== Structured return helpers ==================

        private static ArrayList BuildHeadResult(int status, Dictionary<string, string> headers)
        {
            var result = new ArrayList();
            result["status"] = status;
            result["headers"] = HeadersToArrayList(headers);
            return result;
        }

        private static ArrayList BuildTextResponse(int status, Dictionary<string, string> headers, string body)
        {
            var result = new ArrayList();
            result["status"] = status;
            result["headers"] = HeadersToArrayList(headers);
            result["body"] = body ?? "";
            return result;
        }

        private static ArrayList BuildBinaryResponse(int status, Dictionary<string, string> headers, byte[] bytes)
        {
            var result = new ArrayList();
            result["status"] = status;
            result["headers"] = HeadersToArrayList(headers);
            result["bytes"] = BytesToArrayList(bytes ?? Array.Empty<byte>());
            return result;
        }

        private static ArrayList HeadersToArrayList(Dictionary<string, string> headers)
        {
            var arr = new ArrayList();
            if (headers == null) return arr;

            int i = 0;
            foreach (var kv in headers)
                //arr[i++] = kv.Key + ": " + kv.Value;
                arr[kv.Key] = kv.Value;

            return arr;
        }

        private static ArrayList BytesToArrayList(byte[] data)
        {
            var arr = new ArrayList();
            if (data == null) return arr;

            for (int i = 0; i < data.Length; i++)
                arr[i] = (int)data[i];

            return arr;
        }

        private static byte[] ArrayListToBytes(ArrayList arr)
        {
            if (arr == null) return Array.Empty<byte>();

            var bytes = new List<byte>(arr.Count);

            foreach (object v in arr.Values)
            {
                if (v == null) throw new ScriptStackException("Byte ArrayList enthält null.");

                if (v is int iv)
                {
                    if (iv < 0 || iv > 255) throw new ScriptStackException("Byte außerhalb 0..255: " + iv);
                    bytes.Add((byte)iv);
                }
                else if (v is float fv)
                {
                    int iv2 = (int)fv;
                    if (iv2 < 0 || iv2 > 255) throw new ScriptStackException("Byte außerhalb 0..255: " + iv2);
                    bytes.Add((byte)iv2);
                }
                else
                {
                    if (!int.TryParse(v.ToString(), out int pv))
                        throw new ScriptStackException("Ungültiger Byte-Wert: " + v);

                    if (pv < 0 || pv > 255) throw new ScriptStackException("Byte außerhalb 0..255: " + pv);
                    bytes.Add((byte)pv);
                }
            }

            return bytes.ToArray();
        }
    }
}
