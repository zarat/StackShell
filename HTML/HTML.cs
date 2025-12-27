using HtmlAgilityPack;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace ScriptStack
{

    // Wrapper für ein geparstes HTML Dokument
    public class DomDocument
    {

        internal HtmlDocument Doc { get; }

        public DomDocument(string html)
        {
            Doc = new HtmlDocument()
            {
                OptionFixNestedTags = true,
                OptionAutoCloseOnEnd = true
            };
            Doc.LoadHtml(html ?? "");
        }

        public DomNode Root => new DomNode(Doc.DocumentNode);

        public string Html => Doc.DocumentNode?.OuterHtml ?? "";

    }

    // Wrapper für einen Knoten
    public class DomNode
    {

        internal HtmlNode Node { get; }

        internal DomNode(HtmlNode node)
        {
            Node = node;
        }

        public string Name => Node?.Name ?? "";
        public string Id => Node?.GetAttributeValue("id", "") ?? "";

        // Text lesen/setzen (setzen entitisiert)
        public string InnerText
        {
            get => Node?.InnerText ?? "";
            set
            {
                if (Node == null) return;
                Node.InnerHtml = HtmlEntity.Entitize(value ?? "");
            }
        }

        public string InnerHtml
        {
            get => Node?.InnerHtml ?? "";
            set
            {
                if (Node == null) return;
                Node.InnerHtml = value ?? "";
            }
        }

        public string OuterHtml => Node?.OuterHtml ?? "";

        public Dictionary<string, string> Attributes
        {
            get
            {
                var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (Node?.Attributes == null) return d;
                foreach (var a in Node.Attributes)
                    d[a.Name] = a.Value ?? "";
                return d;
            }
        }
    
    }

    public class DOM : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public DOM()
        {
            if (exportedRoutines != null)
                return;

            var r = new List<Routine>();

            r.Add(new Routine((Type)null, "dom_parse", (Type)null, "Parst HTML-Text und liefert ein DomDocument."));
            r.Add(new Routine((Type)null, "dom_select", (Type)null, (Type)null, "Selektiert Nodes via CSS (subset) oder XPath (wenn mit // oder .// beginnt). Rückgabe: List<DomNode>."));
            r.Add(new Routine((Type)null, "dom_first", (Type)null, "Wie dom_select, aber nur erstes Element (oder null)."));

            r.Add(new Routine((Type)null, "dom_html", (Type)null, "Gibt das komplette HTML des Dokuments zurück."));
            r.Add(new Routine((Type)null, "node_outer_html", (Type)null, "Gibt OuterHtml des Nodes zurück."));
            r.Add(new Routine((Type)null, "node_remove", (Type)null, "Entfernt den Node aus dem DOM."));

            r.Add(new Routine((Type)null, "node_get_attr", (Type)null, "Liest ein Attribut. (node, name) -> string"));
            r.Add(new Routine((Type)null, "node_set_attr", (Type)null, "Setzt ein Attribut. (node, name, value) -> void"));
            r.Add(new Routine((Type)null, "node_remove_attr", (Type)null, "Entfernt ein Attribut. (node, name) -> void"));

            r.Add(new Routine((Type)null, "node_set_text", (Type)null, "Setzt InnerText (entitisiert). (node, text) -> void"));
            r.Add(new Routine((Type)null, "node_set_html", (Type)null, "Setzt InnerHtml. (node, html) -> void"));

            r.Add(new Routine((Type)null, "node_append_html", (Type)null, "Hängt HTML ans Ende der Children. (node, htmlFragment) -> void"));
            r.Add(new Routine((Type)null, "node_prepend_html", (Type)null, "Hängt HTML an den Anfang der Children. (node, htmlFragment) -> void"));
            r.Add(new Routine((Type)null, "node_replace_with_html", (Type)null, "Ersetzt Node durch HTML. (node, htmlFragment) -> void"));

            exportedRoutines = r.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string fn, List<object> p)
        {
            try
            {
                switch (fn)
                {
                    case "dom_parse":
                        return new DomDocument(p.Count > 0 ? (p[0]?.ToString() ?? "") : "");

                    case "dom_select":
                        return SelectNodes(p[0], p[1]?.ToString() ?? "");

                    case "dom_first":
                        {
                            var list = SelectNodes(p[0], p[1]?.ToString() ?? "");
                            return (list.Count > 0) ? list[0] : null;
                        }

                    case "dom_html":
                        {
                            var doc = p[0] as DomDocument;
                            if (doc == null) throw new ScriptStackException("dom_html: Parameter ist kein DomDocument.");
                            return doc.Html;
                        }

                    case "node_outer_html":
                        return GetNode(p[0])?.OuterHtml ?? "";

                    case "node_remove":
                        {
                            var n = GetHtmlNode(p[0]);
                            n?.Remove();
                            return null;
                        }

                    case "node_get_attr":
                        {
                            var n = GetHtmlNode(p[0]);
                            var name = p[1]?.ToString() ?? "";
                            return (n == null || string.IsNullOrWhiteSpace(name)) ? "" : (n.GetAttributeValue(name, "") ?? "");
                        }

                    case "node_set_attr":
                        {
                            var n = GetHtmlNode(p[0]);
                            var name = p[1]?.ToString() ?? "";
                            var val = p[2]?.ToString() ?? "";
                            if (n != null && !string.IsNullOrWhiteSpace(name))
                                n.SetAttributeValue(name, val);
                            return null;
                        }

                    case "node_remove_attr":
                        {
                            var n = GetHtmlNode(p[0]);
                            var name = p[1]?.ToString() ?? "";
                            if (n != null && !string.IsNullOrWhiteSpace(name))
                                n.Attributes.Remove(name);
                            return null;
                        }

                    case "node_set_text":
                        {
                            var node = GetNode(p[0]);
                            node.InnerText = p[1]?.ToString() ?? "";
                            return null;
                        }

                    case "node_set_html":
                        {
                            var node = GetNode(p[0]);
                            node.InnerHtml = p[1]?.ToString() ?? "";
                            return null;
                        }

                    case "node_append_html":
                        {
                            var n = GetHtmlNode(p[0]);
                            var frag = p[1]?.ToString() ?? "";
                            AppendHtml(n, frag, prepend: false);
                            return null;
                        }

                    case "node_prepend_html":
                        {
                            var n = GetHtmlNode(p[0]);
                            var frag = p[1]?.ToString() ?? "";
                            AppendHtml(n, frag, prepend: true);
                            return null;
                        }

                    case "node_replace_with_html":
                        {
                            var n = GetHtmlNode(p[0]);
                            var frag = p[1]?.ToString() ?? "";
                            ReplaceWithHtml(n, frag);
                            return null;
                        }
                }

                return null;
            }
            catch (Exception e)
            {
                throw new ScriptStackException(e.Message);
            }
        }

        #region Internals

        private static DomNode GetNode(object o)
        {
            if (o is DomNode dn) return dn;
            throw new ScriptStackException("Parameter ist kein DomNode.");
        }

        private static HtmlNode GetHtmlNode(object o)
        {
            if (o is DomNode dn) return dn.Node;
            return null;
        }

        private static HtmlNode GetRoot(object docOrNode)
        {
            if (docOrNode is DomDocument d) return d.Doc.DocumentNode;
            if (docOrNode is DomNode n) return n.Node;
            throw new ScriptStackException("Root muss DomDocument oder DomNode sein.");
        }

        private static List<DomNode> SelectNodes(object docOrNode, string selectorOrXPath)
        {
            var root = GetRoot(docOrNode);
            selectorOrXPath ??= "";

            if (string.IsNullOrWhiteSpace(selectorOrXPath))
                return new List<DomNode>();

            // XPath, wenn offensichtlich
            if (selectorOrXPath.StartsWith("//") || selectorOrXPath.StartsWith(".//"))
            {
                var x = selectorOrXPath.StartsWith(".//") ? selectorOrXPath : "." + selectorOrXPath;
                var nodes = root.SelectNodes(x);
                return nodes == null ? new List<DomNode>() : nodes.Select(n => new DomNode(n)).ToList();
            }

            // CSS (subset) -> XPath
            var xpath = CssToXPath(selectorOrXPath);
            var found = root.SelectNodes(xpath);
            return found == null ? new List<DomNode>() : found.Select(n => new DomNode(n)).ToList();
        }

        // Sehr brauchbarer CSS-Subset:
        // - tag
        // - #id
        // - .class
        // - tag.class
        // - tag#id
        // - descendant via spaces: "div .item a"
        // - comma union: "h1, h2"
        private static string CssToXPath(string css)
        {
            css = (css ?? "").Trim();
            if (css == "*") return ".//*";

            // Union: "a, b, c"
            var parts = css.Split(',')
                           .Select(p => p.Trim())
                           .Where(p => p.Length > 0)
                           .ToArray();
            if (parts.Length > 1)
                return string.Join(" | ", parts.Select(CssToXPathSingle));

            return CssToXPathSingle(css);
        }

        private static string CssToXPathSingle(string css)
        {
            // descendant: "div .x a"
            var segs = Regex.Split(css.Trim(), @"\s+")
                            .Where(s => s.Length > 0)
                            .ToArray();

            var xpath = ".";
            foreach (var seg in segs)
            {
                xpath += "//" + CssSegmentToXPathNode(seg);
            }
            return xpath;
        }

        private static string CssSegmentToXPathNode(string seg)
        {
            // #id
            if (seg.StartsWith("#"))
            {
                var id = EscapeXPathLiteral(seg.Substring(1));
                return $"*[@id={id}]";
            }

            // .class
            if (seg.StartsWith("."))
            {
                var cls = EscapeXPathLiteral(seg.Substring(1));
                return $"*[contains(concat(' ', normalize-space(@class), ' '), concat(' ', {cls}, ' '))]";
            }

            // tag#id / tag.class / tag
            string tag = seg;
            string idPart = null;
            string classPart = null;

            int hash = seg.IndexOf('#');
            int dot = seg.IndexOf('.');

            if (hash >= 0)
            {
                tag = seg.Substring(0, hash);
                idPart = seg.Substring(hash + 1);
            }
            if (dot >= 0)
            {
                tag = seg.Substring(0, dot);
                classPart = seg.Substring(dot + 1);
                // tag könnte leer sein, dann *
                if (string.IsNullOrWhiteSpace(tag)) tag = "*";

                // Wenn sowohl # als auch . vorkommen, einfache Behandlung:
                // "tag#id.class" wird nicht perfekt geparst – keep simple.
                if (hash > dot)
                {
                    // ".class#id" selten – ignorieren wir hier.
                }
            }

            if (string.IsNullOrWhiteSpace(tag)) tag = "*";

            var conds = new List<string>();

            if (!string.IsNullOrWhiteSpace(idPart))
                conds.Add($"@id={EscapeXPathLiteral(idPart)}");

            if (!string.IsNullOrWhiteSpace(classPart))
                conds.Add($"contains(concat(' ', normalize-space(@class), ' '), concat(' ', {EscapeXPathLiteral(classPart)}, ' '))");

            if (conds.Count == 0) return tag;
            return $"{tag}[{string.Join(" and ", conds)}]";
        }

        private static string EscapeXPathLiteral(string s)
        {
            s ??= "";
            if (!s.Contains("'")) return $"'{s}'";
            if (!s.Contains("\"")) return $"\"{s}\"";
            // selten: enthält beide -> concat
            var parts = s.Split('\'');
            return "concat(" + string.Join(", \"'\", ", parts.Select(p => $"'{p}'")) + ")";
        }

        private static void AppendHtml(HtmlNode parent, string fragment, bool prepend)
        {
            if (parent == null) return;
            fragment ??= "";

            // HtmlAgilityPack kann Fragmente über CreateNode bauen
            var newNode = HtmlNode.CreateNode(fragment);

            if (prepend)
                parent.PrependChild(newNode);
            else
                parent.AppendChild(newNode);
        }

        private static void ReplaceWithHtml(HtmlNode node, string fragment)
        {
            if (node == null) return;
            fragment ??= "";

            var parent = node.ParentNode;
            if (parent == null)
            {
                // root replacement nicht sinnvoll -> set innerhtml
                node.InnerHtml = fragment;
                return;
            }

            var newNode = HtmlNode.CreateNode(fragment);
            parent.ReplaceChild(newNode, node);
        }

        #endregion
    
    }

}
