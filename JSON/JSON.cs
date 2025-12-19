using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ScriptStack
{

    public class JSON : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public JSON()
        {

            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();
            Routine routine = null;

            routines.Add(new Routine((Type)null, "json_parse", (Type)null, "Erstelle ein JSON Objekt aus einem String."));
            routines.Add(new Routine((Type)null, "json_get", (Type)null, (Type)null, "Lese ein JSON Objekt als JSON-Path."));
            routines.Add(new Routine((Type)null, "json_set", (Type)null, (Type)null, (Type)null, "Setze ein JSON Object als JSON-Path."));
            routines.Add(new Routine(typeof(int), "json_count", (Type)null, "Anzahl Elemente (Array) / Properties (Object)."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "json_keys", (Type)null, "Gibt alle Keys eines JSON-Objects als array zurück."));
            routines.Add(new Routine(typeof(bool), "json_has", (Type)null, (Type)null, "Prüft ob ein JSON-Path existiert (und nicht null ist)."));

            routines.Add(new Routine((Type)null, "json_iter", (Type)null, "Iterator für JsonObject/JsonArray (oder raw json string)."));
            routines.Add(new Routine(typeof(bool), "json_next", (Type)null, "Iterator: nächstes Element. true wenn vorhanden."));
            routines.Add(new Routine(typeof(string), "json_key", (Type)null, "Iterator: aktueller Key (Object) oder null (Array)."));
            routines.Add(new Routine(typeof(int), "json_index", (Type)null, "Iterator: aktueller Index (Array) oder -1 (Object)."));
            routines.Add(new Routine((Type)null, "json_value", (Type)null, "Iterator: aktueller Wert (primitive oder JsonNode)."));

            routines.Add(new Routine((Type)null, "json_node", (Type)null, "Konvertiert Wert/ArrayList zu JsonNode (JsonObject/JsonArray/JsonValue)."));
            routines.Add(new Routine(typeof(string), "json_string", (Type)null, "Konvertiert Wert/ArrayList zu JSON-String."));

            routines.Add(new Routine(typeof(bool), "json_is_obj", (Type)null, "True wenn Node ein JsonObject ist."));
            routines.Add(new Routine(typeof(bool), "json_is_arr", (Type)null, "True wenn Node ein JsonArray ist."));

            exportedRoutines = routines.AsReadOnly();

        }

        public object Invoke(String routine, List<object> parameters)
        {

            if (routine == "json_parse")
            {
                // editable tree (kein Dispose nötig)
                JsonNode? node = JsonNode.Parse((string)parameters[0]);
                return node!;
            }

            if (routine == "json_get")
            {
                JsonNode root = (JsonNode)parameters[0];
                string path = (string)parameters[1];

                JsonNode? node = JsonPathNode.GetByPath(root, path);
                if (node is null) return null;

                // Objekt/Array: als JsonNode zurückgeben (damit du es weiterverwenden kannst)
                if (node is JsonObject || node is JsonArray) return node;

                if (node is JsonValue v)
                {
                    // Häufig: JsonNode.Parse erzeugt JsonValue mit JsonElement als Backing
                    if (v.TryGetValue<JsonElement>(out var je))
                    {
                        switch (je.ValueKind)
                        {
                            case JsonValueKind.String: return je.GetString();
                            case JsonValueKind.True:
                            case JsonValueKind.False: return je.GetBoolean();
                            case JsonValueKind.Null: return null;

                            case JsonValueKind.Number:
                                // Heuristik: erst int, dann long, dann decimal, sonst double
                                //if(je.TryGetByte(out byte ch)) return ch;
                                if (je.TryGetInt32(out int i32)) return i32;
                                if (je.TryGetInt64(out long i64)) return i64;
                                if (je.TryGetDecimal(out decimal dec)) return dec;
                                if (je.TryGetSingle(out float f32)) return f32;
                                return je.GetDouble();
                        }
                    }

                    // Falls es NICHT JsonElement ist (z.B. aus JsonValue.Create),
                    // versuchen wir die gängigen Typen.
                    
                    if (v.TryGetValue<bool>(out var b)) return b;
                    if (v.TryGetValue<int>(out var i)) return i;
                    if (v.TryGetValue<long>(out var l)) return l;
                    if (v.TryGetValue<decimal>(out var d)) return d;
                    if (v.TryGetValue<double>(out var dbl)) return dbl;
                    if (v.TryGetValue<float>(out var f)) return f;
                    if (v.TryGetValue<string>(out var s)) return s;

                    // Fallback
                    return v.ToJsonString();

                }

                return null;
            }

            if (routine == "json_set")
            {
                JsonNode root = (JsonNode)parameters[0];
                string path = (string)parameters[1];
                object? value = parameters[2];

                JsonNode? valueNode = null;

                // Wenn value ein JSON-String ist (z.B. "{...}" oder "[...]"), als JsonNode setzen,
                if (value is string s)
                {
                    var t = s.Trim();

                    if (t.StartsWith("{") || t.StartsWith("["))
                        valueNode = JsonNode.Parse(s);
                    else if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                        valueNode = JsonValue.Create(i);
                    else if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        valueNode = JsonValue.Create(l);
                    else if (bool.TryParse(t, out var b))
                        valueNode = JsonValue.Create(b);
                    else if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
                        valueNode = JsonValue.Create(d);
                    else if (t.Equals("null", StringComparison.OrdinalIgnoreCase))
                        valueNode = null;
                    else
                        valueNode = JsonValue.Create(s);
                }
                else if (value is char c)
                {
                    valueNode = (int)c;
                }
                // sonst als "normalen" Wert (string/number/bool/null).
                else
                {
                    valueNode = JsonValue.Create(value);
                }

                JsonPathNode.SetByPath(root, path, valueNode);

                // zurückgeben: entweder das Root-Objekt selbst ODER JSON-String
                // (hier: JSON-String)
                return root.ToJsonString();
            }

            if (routine == "json_count")
            {
                object? p0 = parameters[0];
                if (p0 is null) return 0;

                if (p0 is JsonArray arr) return arr.Count;
                if (p0 is JsonObject obj) return obj.Count;

                if (p0 is JsonNode node)
                {
                    // JsonValue o.ä.
                    return 1;
                }

                // Falls jemand aus Versehen string (raw json) übergibt: versuchen zu parsen
                if (p0 is string s)
                {
                    var n = JsonNode.Parse(s);
                    if (n is null) return 0;
                    if (n is JsonArray a) return a.Count;
                    if (n is JsonObject o) return o.Count;
                    return 1;
                }

                return 0;
            }

            if (routine == "json_keys")
            {
                object? p0 = parameters[0];
                if (p0 is null) return new ScriptStack.Runtime.ArrayList();

                JsonNode? node = p0 as JsonNode;

                // optional: raw json string erlauben
                if (node is null && p0 is string s)
                    node = JsonNode.Parse(s);

                var result = new ScriptStack.Runtime.ArrayList();

                if (node is JsonObject obj)
                {
                    foreach (var kv in obj)
                        result.Add(kv.Key);
                }

                return result;
            }

            if (routine == "json_has")
            {
                JsonNode root = (JsonNode)parameters[0];
                string path = (string)parameters[1];

                JsonNode? node = JsonPathNode.GetByPath(root, path);

                // Variante A: "existiert" heißt: Pfad vorhanden UND Wert != null
                return node is not null;

                // Variante B (wenn du "existiert auch wenn null gesetzt" willst):
                // Dafür müsste GetByPath unterscheiden können zwischen "missing" und "present but null".
            }

            if (routine == "json_iter")
            {
                JsonNode node = CoerceToNode(parameters[0]);
                return new JsonIterator(node);
            }

            if (routine == "json_next")
            {
                var it = (JsonIterator)parameters[0];
                return it.MoveNext();
            }

            if (routine == "json_key")
            {
                var it = (JsonIterator)parameters[0];
                return it.CurrentKey; // bei array: null
            }

            if (routine == "json_index")
            {
                var it = (JsonIterator)parameters[0];
                return it.CurrentIndex; // bei object: -1
            }

            if (routine == "json_value")
            {
                var it = (JsonIterator)parameters[0];
                return UnboxNode(it.CurrentValue);
            }

            if (routine == "json_node")
            {
                return CoerceToJsonNode(parameters[0]);
            }

            if (routine == "json_string")
            {
                var node = CoerceToJsonNode(parameters[0]);
                return node is null ? "null" : node.ToJsonString();
            }

            if (routine == "json_is_obj")
            {
                var node = CoerceToJsonNode(parameters[0]);
                return node is JsonObject;
            }

            if (routine == "json_is_arr")
            {
                var node = CoerceToJsonNode(parameters[0]);
                return node is JsonArray;
            }

            return null;

        }

        public ReadOnlyCollection<Routine> Routines
        {
            get
            {
                return exportedRoutines;
            }
        }

        private static JsonNode? CoerceToJsonNode(object? value)
        {
            if (value is null) return null;

            // ScriptStack NullReference -> JSON null
            //if ( value is typeof(ScriptStack.Runtime.NullReference.Instance)) return null;

            // already JsonNode
            if (value is JsonNode jn) return jn;

            // raw json string allowed
            if (value is string s)
            {
                var t = s.Trim();
                if ((t.StartsWith("{") && t.EndsWith("}")) || (t.StartsWith("[") && t.EndsWith("]")))
                    return JsonNode.Parse(s);

                // otherwise treat as string primitive
                return JsonValue.Create(s);
            }

            // nested ArrayList
            if (value is ScriptStack.Runtime.ArrayList arr)
                return ArrayListToJsonNode(arr);

            // primitives
            if (value is bool b) return JsonValue.Create(b);
            if (value is int i32) return JsonValue.Create(i32);
            if (value is long i64) return JsonValue.Create(i64);
            if (value is float f32) return JsonValue.Create(f32);
            if (value is double f64) return JsonValue.Create(f64);
            if (value is decimal dec) return JsonValue.Create(dec);
            if (value is char ch) return JsonValue.Create(ch.ToString());

            // fallback: use string representation
            return JsonValue.Create(value.ToString());
        }

        private static JsonNode ArrayListToJsonNode(ScriptStack.Runtime.ArrayList arr)
        {
            // Decide: JSON array only if keys are exactly 0..n-1 (no gaps), all int
            int n = arr.Count;
            bool isDenseArray = true;
            for (int i = 0; i < n; i++)
            {
                if (!arr.ContainsKey(i)) { isDenseArray = false; break; }
            }

            if (isDenseArray)
            {
                var ja = new JsonArray();
                for (int i = 0; i < n; i++)
                {
                    var v = arr[i];
                    ja.Add(CoerceToJsonNode(v));
                }
                return ja;
            }

            // else: JSON object (keys -> string)
            var jo = new JsonObject();
            foreach (var k in arr.Keys)
            {
                //var keyString = k is null || ReferenceEquals(k, NullReference.Instance) ? "null" : k.ToString();
                var keyString = k.ToString();
                jo[keyString] = CoerceToJsonNode(arr[k]);
            }
            return jo;
        }
        
        public static class JsonPathNode
        {

            private sealed record Segment(string Name, List<int> Indices);

            public static JsonNode? GetByPath(JsonNode root, string path)
            {
                if (root is null) throw new ArgumentNullException(nameof(root));
                var segments = Parse(path);

                JsonNode? cur = root;

                foreach (var seg in segments)
                {
                    if (cur is null) return null;

                    if (!string.IsNullOrEmpty(seg.Name))
                    {
                        if (cur is not JsonObject obj) return null;
                        cur = obj[seg.Name];
                    }

                    foreach (var idx in seg.Indices)
                    {
                        if (cur is not JsonArray arr) return null;
                        if (idx < 0 || idx >= arr.Count) return null;
                        cur = arr[idx];
                    }
                }

                return cur;
            }

            public static void SetByPath(JsonNode root, string path, object? value)
                => SetByPath(root, path, value is JsonNode jn ? jn : JsonValue.Create(value));

            public static void SetByPath(JsonNode root, string path, JsonNode? valueNode)
            {
                if (root is null) throw new ArgumentNullException(nameof(root));

                var segments = Parse(path);
                if (segments.Count == 0) throw new ArgumentException("Path ist leer.", nameof(path));

                JsonNode cur = root;

                // bis zum Parent navigieren (alles außer letztes Segment)
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    var seg = segments[i];
                    var next = segments[i + 1];

                    cur = EnsureSegment(cur, seg, next);
                }

                // letztes Segment setzen
                var last = segments[^1];

                if (!string.IsNullOrEmpty(last.Name))
                {
                    if (cur is not JsonObject parentObj)
                        throw new InvalidOperationException($"Erwarte Objekt vor '{last.Name}'.");

                    if (last.Indices.Count == 0)
                    {
                        parentObj[last.Name] = valueNode;
                        return;
                    }

                    // Property ist ein Array (ggf. anlegen)
                    parentObj[last.Name] ??= new JsonArray();
                    JsonNode arrNode = parentObj[last.Name]!;
                    SetIntoArrayChain(arrNode, last.Indices, valueNode, nextSegment: null);
                    return;
                }
                else
                {
                    // Pfad endet in Array-Index auf dem aktuellen Node
                    if (last.Indices.Count == 0)
                        throw new InvalidOperationException("Pfad endet ohne Property/Index.");

                    SetIntoArrayChain(cur, last.Indices, valueNode, nextSegment: null);
                }
            }

            // --- intern helpers ---

            private static JsonNode EnsureSegment(JsonNode cur, Segment seg, Segment nextSeg)
            {
                // erst Property-Teil
                if (!string.IsNullOrEmpty(seg.Name))
                {
                    if (cur is not JsonObject obj)
                        throw new InvalidOperationException($"Erwarte Objekt vor '{seg.Name}'.");

                    obj[seg.Name] ??= seg.Indices.Count > 0 ? new JsonArray() : new JsonObject();
                    cur = obj[seg.Name]!;
                }

                // dann ggf. Indizes
                if (seg.Indices.Count > 0)
                {
                    cur = EnsureArrayChain(cur, seg.Indices, nextSeg);
                }
                else
                {
                    // wenn kein Index: cur bleibt wie es ist
                }

                // wenn wir nach diesem Segment in ein Property gehen, muss cur ein Objekt sein
                if (!string.IsNullOrEmpty(nextSeg.Name))
                {
                    cur ??= new JsonObject();
                    if (cur is not JsonObject) // falls es z.B. null war
                    {
                        // wenn hier was anderes steht, ist die Struktur inkompatibel
                    }
                }

                return cur;
            }

            private static JsonNode EnsureArrayChain(JsonNode cur, List<int> indices, Segment nextSeg)
            {
                JsonNode node = cur;

                for (int j = 0; j < indices.Count; j++)
                {
                    if (node is not JsonArray arr)
                        throw new InvalidOperationException("Erwarte Array im Pfad.");

                    int idx = indices[j];
                    if (idx < 0) throw new ArgumentOutOfRangeException(nameof(indices), "Index < 0");

                    while (arr.Count <= idx) arr.Add(null);

                    bool isLastIndexInThisSegment = (j == indices.Count - 1);

                    if (arr[idx] is null)
                    {
                        // Wenn nach dem letzten Index dieses Segments ein nächstes Segment kommt:
                        // - wenn nächstes Segment mit Property => Objekt
                        // - wenn nächstes Segment ohne Name aber mit Indices => Array
                        JsonNode created =
                            isLastIndexInThisSegment
                                ? (!string.IsNullOrEmpty(nextSeg.Name) ? new JsonObject()
                                  : (nextSeg.Indices.Count > 0 ? new JsonArray() : new JsonObject()))
                                : new JsonArray(); // weitere [][ ] innerhalb desselben Segments

                        arr[idx] = created;
                    }

                    node = arr[idx]!;
                }

                return node;
            }

            private static void SetIntoArrayChain(JsonNode cur, List<int> indices, JsonNode? value, Segment? nextSegment)
            {
                JsonNode node = cur;

                for (int j = 0; j < indices.Count; j++)
                {
                    if (node is not JsonArray arr)
                        throw new InvalidOperationException("Erwarte Array im Pfad.");

                    int idx = indices[j];
                    if (idx < 0) throw new ArgumentOutOfRangeException(nameof(indices), "Index < 0");

                    while (arr.Count <= idx) arr.Add(null);

                    bool isLast = (j == indices.Count - 1);

                    if (isLast)
                    {
                        arr[idx] = value;
                        return;
                    }

                    // Zwischenknoten: falls null → neues Array
                    arr[idx] ??= new JsonArray();
                    node = arr[idx]!;
                }
            }

            private static List<Segment> Parse(string path)
            {
                if (path is null) throw new ArgumentNullException(nameof(path));
                path = path.Trim();
                if (path.Length == 0) return new();

                var segments = new List<Segment>();
                int i = 0;

                while (i < path.Length)
                {
                    string name = "";

                    // Name lesen bis '.' oder '['
                    int start = i;
                    while (i < path.Length && path[i] != '.' && path[i] != '[')
                        i++;
                    name = path[start..i];

                    var indices = new List<int>();

                    // beliebig viele [n]
                    while (i < path.Length && path[i] == '[')
                    {
                        i++; // '['
                        int numStart = i;
                        while (i < path.Length && path[i] != ']') i++;
                        if (i >= path.Length) throw new FormatException("Fehlende ']'.");
                        var numStr = path[numStart..i];
                        if (!int.TryParse(numStr, out int idx))
                            throw new FormatException($"Ungültiger Index: {numStr}");
                        indices.Add(idx);
                        i++; // ']'
                    }

                    segments.Add(new Segment(name, indices));

                    if (i < path.Length)
                    {
                        if (path[i] != '.') throw new FormatException($"Unerwartetes Zeichen: '{path[i]}'");
                        i++; // '.'
                    }
                }

                return segments;
            }
        
        }

        private sealed class JsonIterator
        {
            private readonly JsonNode _root;
            private IEnumerator<KeyValuePair<string, JsonNode?>>? _objEnum;
            private IEnumerator<JsonNode?>? _arrEnum;

            private bool _hasCurrent;
            private string? _currentKey;
            private int _currentIndex;
            private JsonNode? _currentValue;

            public JsonIterator(JsonNode root)
            {
                _root = root ?? throw new ArgumentNullException(nameof(root));
                Reset();
            }

            public void Reset()
            {
                _hasCurrent = false;
                _currentKey = null;
                _currentIndex = -1;
                _currentValue = null;

                _objEnum = null;
                _arrEnum = null;

                if (_root is JsonObject obj)
                    _objEnum = obj.GetEnumerator();
                else if (_root is JsonArray arr)
                    _arrEnum = arr.GetEnumerator();
                else
                    throw new InvalidOperationException("json_iter erwartet JsonObject oder JsonArray.");
            }

            public bool MoveNext()
            {
                if (_objEnum != null)
                {
                    if (!_objEnum.MoveNext())
                    {
                        _hasCurrent = false;
                        return false;
                    }

                    _hasCurrent = true;
                    _currentKey = _objEnum.Current.Key;
                    _currentIndex = -1;
                    _currentValue = _objEnum.Current.Value;
                    return true;
                }

                if (_arrEnum != null)
                {
                    if (!_arrEnum.MoveNext())
                    {
                        _hasCurrent = false;
                        return false;
                    }

                    _hasCurrent = true;
                    _currentKey = null;
                    _currentIndex = _currentIndex < 0 ? 0 : _currentIndex + 1;
                    _currentValue = _arrEnum.Current;
                    return true;
                }

                _hasCurrent = false;
                return false;
            }

            public string? CurrentKey => _hasCurrent ? _currentKey : null;
            public int CurrentIndex => _hasCurrent ? _currentIndex : -1;
            public JsonNode? CurrentValue => _hasCurrent ? _currentValue : null;
        }

        private static object? UnboxNode(JsonNode? node)
        {
            if (node is null) return null;

            // Objekt/Array: als JsonNode zurück (damit Script weiter json_get/json_iter nutzen kann)
            if (node is JsonObject || node is JsonArray) return node;

            if (node is JsonValue v)
            {
                // häufig: Backing JsonElement
                if (v.TryGetValue<JsonElement>(out var je))
                {
                    switch (je.ValueKind)
                    {
                        case JsonValueKind.String: return je.GetString();
                        case JsonValueKind.True:
                        case JsonValueKind.False: return je.GetBoolean();
                        case JsonValueKind.Null: return null;

                        case JsonValueKind.Number:
                            if (je.TryGetInt32(out int i32)) return i32;
                            if (je.TryGetInt64(out long i64)) return i64;
                            if (je.TryGetDecimal(out decimal dec)) return dec;
                            if (je.TryGetSingle(out float f32)) return f32;
                            return je.GetDouble();
                    }
                }

                // sonst: direkte Typen
                if (v.TryGetValue<bool>(out var b)) return b;
                if (v.TryGetValue<int>(out var i)) return i;
                if (v.TryGetValue<long>(out var l)) return l;
                if (v.TryGetValue<decimal>(out var d)) return d;
                if (v.TryGetValue<double>(out var dbl)) return dbl;
                if (v.TryGetValue<float>(out var f)) return f;
                if (v.TryGetValue<string>(out var s)) return s;

                return v.ToJsonString();
            }

            return null;
        }

        private static JsonNode CoerceToNode(object? p0)
        {
            if (p0 is JsonNode jn) return jn;

            if (p0 is string s)
            {
                var n = JsonNode.Parse(s);
                if (n is null) throw new InvalidOperationException("json_iter: Konnte JSON nicht parsen.");
                return n;
            }

            throw new InvalidOperationException("json_iter erwartet JsonNode oder JSON-String.");
        }

    }

}
