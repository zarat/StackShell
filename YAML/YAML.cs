using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;

using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ScriptStack
{
    public class YAML : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        public YAML()
        {
            if (exportedRoutines != null) return;

            var routines = new List<Routine>();

            routines.Add(new Routine((Type)null, "yaml_parse", (Type)null, "Erstelle ein YAML Objekt (YamlNode) aus einem String."));
            routines.Add(new Routine((Type)null, "yaml_get", (Type)null, (Type)null, "Lese aus YAML per Path (a.b[0].c)."));
            routines.Add(new Routine((Type)null, "yaml_set", (Type)null, (Type)null, (Type)null, "Setze in YAML per Path (a.b[0].c)."));

            routines.Add(new Routine(typeof(int), "yaml_count", (Type)null, "Anzahl Elemente (Sequence) / Properties (Mapping)."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "yaml_keys", (Type)null, "Gibt alle Keys eines YAML-Mappings als array zurück."));
            routines.Add(new Routine(typeof(bool), "yaml_has", (Type)null, (Type)null, "Prüft ob ein YAML-Path existiert (und nicht null ist)."));

            routines.Add(new Routine((Type)null, "yaml_iter", (Type)null, "Iterator für YamlMapping/YamlSequence (oder raw yaml string)."));
            routines.Add(new Routine(typeof(bool), "yaml_next", (Type)null, "Iterator: nächstes Element. true wenn vorhanden."));
            routines.Add(new Routine(typeof(string), "yaml_key", (Type)null, "Iterator: aktueller Key (Mapping) oder null (Sequence)."));
            routines.Add(new Routine(typeof(int), "yaml_index", (Type)null, "Iterator: aktueller Index (Sequence) oder -1 (Mapping)."));
            routines.Add(new Routine((Type)null, "yaml_value", (Type)null, "Iterator: aktueller Wert (primitive oder YamlNode)."));

            routines.Add(new Routine((Type)null, "yaml_node", (Type)null, "Konvertiert Wert/ArrayList zu YamlNode (Mapping/Sequence/Scalar)."));
            routines.Add(new Routine(typeof(string), "yaml_string", (Type)null, "Konvertiert Wert/ArrayList/YamlNode zu YAML-String."));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters)
        {
            if (routine == "yaml_parse")
            {
                var root = ParseYaml(ToStr(parameters[0]));
                return root;
            }

            if (routine == "yaml_get")
            {
                var root = (YamlNode)parameters[0];
                string path = (string)parameters[1];

                var node = YamlPathNode.GetByPath(root, path);
                if (node is null) return null;

                if (node is YamlMappingNode || node is YamlSequenceNode) return node;
                return UnboxNode(node);
            }

            if (routine == "yaml_set")
            {
                var root = (YamlNode)parameters[0];
                string path = (string)parameters[1];
                object? value = parameters[2];

                var valueNode = CoerceToYamlNode(value);
                YamlPathNode.SetByPath(root, path, valueNode);

                return ToYamlString(root);
            }

            if (routine == "yaml_count")
            {
                object? p0 = parameters[0];
                if (p0 is null) return 0;

                if (p0 is YamlSequenceNode seq) return seq.Children.Count;
                if (p0 is YamlMappingNode map) return map.Children.Count;

                if (p0 is YamlNode) return 1;

                if (p0 is string s)
                {
                    var n = ParseYaml(s);
                    if (n is YamlSequenceNode a) return a.Children.Count;
                    if (n is YamlMappingNode o) return o.Children.Count;
                    return 1;
                }

                return 0;
            }

            if (routine == "yaml_keys")
            {
                object? p0 = parameters[0];
                if (p0 is null) return new ScriptStack.Runtime.ArrayList();

                YamlNode? node = p0 as YamlNode;
                if (node is null && p0 is string s)
                    node = ParseYaml(s);

                var result = new ScriptStack.Runtime.ArrayList();

                if (node is YamlMappingNode map)
                {
                    foreach (var kv in map.Children)
                    {
                        if (kv.Key is YamlScalarNode ks) result.Add(ks.Value);
                        else result.Add(kv.Key.ToString());
                    }
                }

                return result;
            }

            if (routine == "yaml_has")
            {
                var root = (YamlNode)parameters[0];
                string path = (string)parameters[1];

                var node = YamlPathNode.GetByPath(root, path);
                return node is not null;
            }

            if (routine == "yaml_iter")
            {
                var node = CoerceToNode(parameters[0]);
                return new YamlIterator(node);
            }

            if (routine == "yaml_next")
            {
                var it = (YamlIterator)parameters[0];
                return it.MoveNext();
            }

            if (routine == "yaml_key")
            {
                var it = (YamlIterator)parameters[0];
                return it.CurrentKey;
            }

            if (routine == "yaml_index")
            {
                var it = (YamlIterator)parameters[0];
                return it.CurrentIndex;
            }

            if (routine == "yaml_value")
            {
                var it = (YamlIterator)parameters[0];
                return UnboxValue(it.CurrentValue);
            }

            if (routine == "yaml_node")
            {
                return CoerceToYamlNode(parameters[0]);
            }

            if (routine == "yaml_string")
            {
                var node = CoerceToYamlNode(parameters[0]);
                return node is null ? "null\n" : ToYamlString(node);
            }

            return null;
        }

        // ---------------- YAML parse / stringify ----------------

        private static YamlNode ParseYaml(string yaml)
        {
            var stream = new YamlStream();
            using var sr = new StringReader(yaml);
            stream.Load(sr);

            if (stream.Documents.Count == 0)
                throw new InvalidOperationException("yaml_parse: Keine YAML Dokumente gefunden.");

            // (wie JSON) wir nehmen das erste Dokument
            return stream.Documents[0].RootNode;
        }

        private static string ToYamlString(YamlNode node)
        {
            var stream = new YamlStream(new YamlDocument(node));
            using var sw = new StringWriter(CultureInfo.InvariantCulture);

            // Save erzeugt i.d.R. YAML mit '...\n' am Ende; das ist okay.
            stream.Save(sw, assignAnchors: false);
            return sw.ToString();
        }

        // ---------------- Coercion ----------------

        private static string ToStr(object o) => o?.ToString() ?? "";

        private static YamlNode? CoerceToYamlNode(object? value)
        {
            if (value is null) return new YamlScalarNode((string?)null);

            if (value is YamlNode yn) return yn;

            // raw yaml string erlauben (heuristik: wenn es wie ein YAML-doc aussieht)
            if (value is string s)
            {
                var t = s.Trim();

                bool looksLikeDoc =
                    t.StartsWith("---") ||
                    t.Contains('\n') ||
                    t.Contains(": ") ||
                    (t.StartsWith("{") && t.EndsWith("}")) ||
                    (t.StartsWith("[") && t.EndsWith("]")) ||
                    t.StartsWith("- ");

                if (looksLikeDoc)
                {
                    try { return ParseYaml(s); }
                    catch { /* fall back to scalar */ }
                }

                // scalar string
                return new YamlScalarNode(s);
            }

            // ScriptStack ArrayList -> Mapping oder Sequence
            if (value is ScriptStack.Runtime.ArrayList arr)
                return ArrayListToYamlNode(arr);

            // primitive scalars
            if (value is bool b) return new YamlScalarNode(b ? "true" : "false");
            if (value is char ch) return new YamlScalarNode(ch.ToString());
            if (value is int or long or float or double or decimal)
                return new YamlScalarNode(Convert.ToString(value, CultureInfo.InvariantCulture));

            return new YamlScalarNode(value.ToString());
        }

        private static YamlNode ArrayListToYamlNode(ScriptStack.Runtime.ArrayList arr)
        {
            // Dense 0..n-1 => Sequence, sonst Mapping (keys->string)
            int n = arr.Count;
            bool isDense = true;
            for (int i = 0; i < n; i++)
            {
                if (!arr.ContainsKey(i)) { isDense = false; break; }
            }

            if (isDense)
            {
                var seq = new YamlSequenceNode();
                for (int i = 0; i < n; i++)
                    seq.Add(CoerceToYamlNode(arr[i]));
                return seq;
            }

            var map = new YamlMappingNode();
            foreach (var k in arr.Keys)
            {
                var keyStr = k?.ToString() ?? "null";
                map.Add(new YamlScalarNode(keyStr), CoerceToYamlNode(arr[k]) ?? new YamlScalarNode((string?)null));
            }
            return map;
        }

        private static object? UnboxValue(YamlNode? node)
        {
            if (node is null) return null;
            if (node is YamlMappingNode || node is YamlSequenceNode) return node;
            return UnboxNode(node);
        }

        private static object? UnboxNode(YamlNode node)
        {
            if (node is YamlScalarNode s)
            {
                var v = s.Value;

                if (v is null) return null;

                var t = v.Trim();

                // YAML null forms
                if (t.Equals("null", StringComparison.OrdinalIgnoreCase) ||
                    t.Equals("~", StringComparison.OrdinalIgnoreCase) ||
                    t.Length == 0)
                    return null;

                if (bool.TryParse(t, out var b)) return b;

                if (int.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i32)) return i32;
                if (long.TryParse(t, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i64)) return i64;

                if (decimal.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var dec)) return dec;
                if (double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out var dbl)) return dbl;

                return v;
            }

            // fallback: return node itself
            return node;
        }

        private static YamlNode CoerceToNode(object? p0)
        {
            if (p0 is YamlNode yn) return yn;

            if (p0 is string s)
            {
                return ParseYaml(s);
            }

            throw new InvalidOperationException("yaml_iter erwartet YamlNode oder YAML-String.");
        }

        // ---------------- Path helper (dot + [index]) ----------------

        public static class YamlPathNode
        {
            private sealed record Segment(string Name, List<int> Indices);

            public static YamlNode? GetByPath(YamlNode root, string path)
            {
                if (root is null) throw new ArgumentNullException(nameof(root));
                var segments = Parse(path);

                YamlNode? cur = root;

                foreach (var seg in segments)
                {
                    if (cur is null) return null;

                    if (!string.IsNullOrEmpty(seg.Name))
                    {
                        if (cur is not YamlMappingNode map) return null;

                        var key = new YamlScalarNode(seg.Name);
                        if (!map.Children.TryGetValue(key, out cur))
                            return null;
                    }

                    foreach (var idx in seg.Indices)
                    {
                        if (cur is not YamlSequenceNode seq) return null;
                        if (idx < 0 || idx >= seq.Children.Count) return null;
                        cur = seq.Children[idx];
                    }
                }

                return cur;
            }

            public static void SetByPath(YamlNode root, string path, YamlNode? valueNode)
            {
                if (root is null) throw new ArgumentNullException(nameof(root));

                var segments = Parse(path);
                if (segments.Count == 0) throw new ArgumentException("Path ist leer.", nameof(path));

                YamlNode cur = root;

                // bis zum Parent navigieren
                for (int i = 0; i < segments.Count - 1; i++)
                {
                    var seg = segments[i];
                    var next = segments[i + 1];
                    cur = EnsureSegment(cur, seg, next);
                }

                var last = segments[^1];

                if (!string.IsNullOrEmpty(last.Name))
                {
                    if (cur is not YamlMappingNode parentMap)
                        throw new InvalidOperationException($"Erwarte Mapping vor '{last.Name}'.");

                    if (last.Indices.Count == 0)
                    {
                        parentMap.Children[new YamlScalarNode(last.Name)] = valueNode ?? new YamlScalarNode((string?)null);
                        return;
                    }

                    // Property ist Sequence (ggf. anlegen)
                    var key = new YamlScalarNode(last.Name);
                    if (!parentMap.Children.TryGetValue(key, out var prop) || prop is not YamlSequenceNode)
                    {
                        prop = new YamlSequenceNode();
                        parentMap.Children[key] = prop;
                    }

                    SetIntoSequenceChain((YamlSequenceNode)prop, last.Indices, valueNode);
                    return;
                }
                else
                {
                    if (last.Indices.Count == 0)
                        throw new InvalidOperationException("Pfad endet ohne Property/Index.");

                    if (cur is not YamlSequenceNode seq)
                        throw new InvalidOperationException("Erwarte Sequence im Pfad-Ende.");

                    SetIntoSequenceChain(seq, last.Indices, valueNode);
                }
            }

            private static YamlNode EnsureSegment(YamlNode cur, Segment seg, Segment nextSeg)
            {
                // Property-Teil
                if (!string.IsNullOrEmpty(seg.Name))
                {
                    if (cur is not YamlMappingNode map)
                        throw new InvalidOperationException($"Erwarte Mapping vor '{seg.Name}'.");

                    var key = new YamlScalarNode(seg.Name);

                    if (!map.Children.TryGetValue(key, out var child) || child is null)
                    {
                        // erstellen abhängig davon, ob wir im selben Segment Indizes haben oder was als nächstes kommt
                        if (seg.Indices.Count > 0)
                            child = new YamlSequenceNode();
                        else if (!string.IsNullOrEmpty(nextSeg.Name))
                            child = new YamlMappingNode();
                        else if (nextSeg.Indices.Count > 0)
                            child = new YamlSequenceNode();
                        else
                            child = new YamlMappingNode();

                        map.Children[key] = child;
                    }

                    cur = child;
                }

                // Indizes im selben Segment
                if (seg.Indices.Count > 0)
                {
                    cur = EnsureSequenceChain(cur, seg.Indices, nextSeg);
                }

                return cur;
            }

            private static YamlNode EnsureSequenceChain(YamlNode cur, List<int> indices, Segment nextSeg)
            {
                YamlNode node = cur;

                for (int j = 0; j < indices.Count; j++)
                {
                    if (node is not YamlSequenceNode seq)
                        throw new InvalidOperationException("Erwarte Sequence im Pfad.");

                    int idx = indices[j];
                    if (idx < 0) throw new ArgumentOutOfRangeException(nameof(indices), "Index < 0");

                    while (seq.Children.Count <= idx) seq.Add(new YamlScalarNode((string?)null));

                    bool isLastIndexInThisSegment = (j == indices.Count - 1);

                    if (seq.Children[idx] is YamlScalarNode sc && sc.Value is null)
                    {
                        YamlNode created =
                            isLastIndexInThisSegment
                                ? (!string.IsNullOrEmpty(nextSeg.Name) ? new YamlMappingNode()
                                   : (nextSeg.Indices.Count > 0 ? new YamlSequenceNode() : new YamlMappingNode()))
                                : new YamlSequenceNode();

                        seq.Children[idx] = created;
                    }

                    node = seq.Children[idx];
                }

                return node;
            }

            private static void SetIntoSequenceChain(YamlSequenceNode seq, List<int> indices, YamlNode? value)
            {
                YamlNode node = seq;

                for (int j = 0; j < indices.Count; j++)
                {
                    if (node is not YamlSequenceNode s)
                        throw new InvalidOperationException("Erwarte Sequence im Pfad.");

                    int idx = indices[j];
                    if (idx < 0) throw new ArgumentOutOfRangeException(nameof(indices), "Index < 0");

                    while (s.Children.Count <= idx) s.Add(new YamlScalarNode((string?)null));

                    bool isLast = (j == indices.Count - 1);
                    if (isLast)
                    {
                        s.Children[idx] = value ?? new YamlScalarNode((string?)null);
                        return;
                    }

                    // Zwischenknoten: falls null-scalar -> neue Sequence
                    if (s.Children[idx] is YamlScalarNode sc && sc.Value is null)
                        s.Children[idx] = new YamlSequenceNode();

                    node = s.Children[idx];
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

                    int start = i;
                    while (i < path.Length && path[i] != '.' && path[i] != '[')
                        i++;
                    name = path[start..i];

                    var indices = new List<int>();

                    while (i < path.Length && path[i] == '[')
                    {
                        i++; // '['
                        int numStart = i;
                        while (i < path.Length && path[i] != ']') i++;
                        if (i >= path.Length) throw new FormatException("Fehlende ']'.");
                        var numStr = path[numStart..i];
                        if (!int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx))
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

        // ---------------- Iterator ----------------

        private sealed class YamlIterator
        {
            private readonly YamlNode _root;

            private IEnumerator<KeyValuePair<YamlNode, YamlNode>>? _mapEnum;
            private IEnumerator<YamlNode>? _seqEnum;

            private bool _hasCurrent;
            private string? _currentKey;
            private int _currentIndex;
            private YamlNode? _currentValue;

            public YamlIterator(YamlNode root)
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

                _mapEnum = null;
                _seqEnum = null;

                if (_root is YamlMappingNode map)
                    _mapEnum = map.Children.GetEnumerator();
                else if (_root is YamlSequenceNode seq)
                    _seqEnum = seq.Children.GetEnumerator();
                else
                    throw new InvalidOperationException("yaml_iter erwartet YamlMappingNode oder YamlSequenceNode.");
            }

            public bool MoveNext()
            {
                if (_mapEnum != null)
                {
                    if (!_mapEnum.MoveNext())
                    {
                        _hasCurrent = false;
                        return false;
                    }

                    _hasCurrent = true;

                    var k = _mapEnum.Current.Key;
                    _currentKey = k is YamlScalarNode ks ? ks.Value : k.ToString();

                    _currentIndex = -1;
                    _currentValue = _mapEnum.Current.Value;
                    return true;
                }

                if (_seqEnum != null)
                {
                    if (!_seqEnum.MoveNext())
                    {
                        _hasCurrent = false;
                        return false;
                    }

                    _hasCurrent = true;
                    _currentKey = null;
                    _currentIndex = _currentIndex < 0 ? 0 : _currentIndex + 1;
                    _currentValue = _seqEnum.Current;
                    return true;
                }

                _hasCurrent = false;
                return false;
            }

            public string? CurrentKey => _hasCurrent ? _currentKey : null;
            public int CurrentIndex => _hasCurrent ? _currentIndex : -1;
            public YamlNode? CurrentValue => _hasCurrent ? _currentValue : null;
        }
    }
}
