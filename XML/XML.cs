using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace ScriptStack
{

    public class XML : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public XML()
        {

            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();

            routines.Add(new Routine((Type)null, "xml_parse", (Type)null, "Erstelle ein XML Dokument (XDocument) aus einem String."));
            routines.Add(new Routine(typeof(string), "xml_string", (Type)null, "Gibt das XML als String zurück."));

            routines.Add(new Routine((Type)null, "xml_select", (Type)null, (Type)null, "Wählt einen(!) Knoten per XPath aus (gibt XElement/XAttribute/XText zurück oder null)."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "xml_select_all", (Type)null, (Type)null, "Wählt mehrere Knoten per XPath aus (gibt ArrayList von Nodes zurück)."));

            routines.Add(new Routine(typeof(string), "xml_value", (Type)null, "Liest den String-Value eines Nodes (XElement/XAttribute/XText)"));
            routines.Add(new Routine(typeof(string), "xml_attr", (Type)null, (Type)null, "Liest ein Attribut (Name) von einem Element (oder null)."));
            routines.Add(new Routine(typeof(bool), "xml_has", (Type)null, (Type)null, "Prüft ob ein XPath ein Ergebnis liefert."));

            routines.Add(new Routine((Type)null, "xml_set", (Type)null, (Type)null, (Type)null, "Setzt den Value eines Elements/Attributes per XPath (einfacher Setter)."));

            routines.Add(new Routine(typeof(bool), "xml_is_elem", (Type)null, "True wenn Node ein XElement ist."));
            routines.Add(new Routine(typeof(bool), "xml_is_attr", (Type)null, "True wenn Node ein XAttribute ist."));

            routines.Add(new Routine((Type)null, "xml_iter", (Type)null, "Iterator für XML Nodes (XDocument/XElement/XAttribute/XText/string xml)."));
            routines.Add(new Routine(typeof(bool), "xml_next", (Type)null, "Iterator: nächstes Element. true wenn vorhanden."));
            routines.Add(new Routine(typeof(string), "xml_name", (Type)null, "Iterator: Name (Element/Attribut) oder null."));
            routines.Add(new Routine(typeof(int), "xml_index", (Type)null, "Iterator: Index (nur wenn über eine Liste iteriert wird), sonst -1."));
            routines.Add(new Routine((Type)null, "xml_node", (Type)null, "Iterator: aktueller Node (XElement/XAttribute/XText)."));
            routines.Add(new Routine(typeof(string), "xml_value_it", (Type)null, "Iterator: Value des aktuellen Nodes (wie xml_value)."));

            exportedRoutines = routines.AsReadOnly();

        }

        public object Invoke(String routine, List<object> parameters)
        {

            if (routine == "xml_parse")
            {
                string xml = (string)parameters[0];
                // PreserveWhitespace=false (Default), damit "pretty" Ausgabe möglich bleibt.
                var doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
                return doc;
            }

            if (routine == "xml_string")
            {
                var node = parameters[0];
                if (node is null) return null;

                if (node is XDocument d) return d.ToString(SaveOptions.DisableFormatting);
                if (node is XNode xn) return xn.ToString(SaveOptions.DisableFormatting);
                if (node is XAttribute xa) return xa.ToString();

                return node.ToString();
            }

            if (routine == "xml_select")
            {
                var root = parameters[0];
                string xpath = (string)parameters[1];

                XNode? xroot = ToXNode(root);
                if (xroot is null) return null;

                object eval = xroot.XPathEvaluate(xpath);

                if (eval is IEnumerable<object> en)
                {
                    // first or null
                    var first = en.Cast<object>().FirstOrDefault();
                    return UnwrapXPathObject(first);
                }

                return UnwrapXPathObject(eval);
            }

            if (routine == "xml_select_all")
            {
                var root = parameters[0];
                string xpath = (string)parameters[1];

                XNode? xroot = ToXNode(root);
                if (xroot is null) return new ScriptStack.Runtime.ArrayList();

                object eval = xroot.XPathEvaluate(xpath);

                var result = new ScriptStack.Runtime.ArrayList();

                if (eval is IEnumerable<object> en)
                {
                    foreach (var item in en)
                        result.Add(UnwrapXPathObject(item));
                }
                else
                {
                    result.Add(UnwrapXPathObject(eval));
                }

                return result;
            }

            if (routine == "xml_value")
            {
                var node = parameters[0];
                if (node is null) return null;

                if (node is XDocument d) return d.Root?.Value;
                if (node is XElement e) return e.Value;
                if (node is XAttribute a) return a.Value;
                if (node is XText t) return t.Value;
                if (node is XNode xn) return xn.ToString();

                return node.ToString();
            }

            if (routine == "xml_attr")
            {
                var node = parameters[0];
                string attrName = (string)parameters[1];

                if (node is XElement e)
                    return e.Attribute(attrName)?.Value;

                // wenn ein Document kommt, nehmen wir Root
                if (node is XDocument d)
                    return d.Root?.Attribute(attrName)?.Value;

                return null;
            }

            if (routine == "xml_has")
            {
                var root = parameters[0];
                string xpath = (string)parameters[1];

                XNode? xroot = ToXNode(root);
                if (xroot is null) return false;

                object eval = xroot.XPathEvaluate(xpath);
                if (eval is IEnumerable<object> en)
                    return en.Cast<object>().Any();

                return eval != null;
            }

            if (routine == "xml_set")
            {
                // einfacher Setter: XPath muss auf Element oder Attribute zeigen.
                // value wird immer als string gesetzt (wie bei JSON plugin heuristics).
                var root = parameters[0];
                string xpath = (string)parameters[1];
                object? valueObj = parameters[2];
                string value = valueObj?.ToString() ?? "";

                // XDocument oder XElement als Mutations-Root
                XNode? xroot = root as XNode;
                if (root is XAttribute) throw new InvalidOperationException("xml_set: root darf kein Attribut sein.");
                if (xroot is null) throw new InvalidOperationException("xml_set: erwartet XDocument oder XElement.");

                // Evaluate liefert Iterator mit XObject
                object eval = xroot.XPathEvaluate(xpath);

                // Wir setzen nur das erste Ergebnis.
                object? target = null;
                if (eval is IEnumerable<object> en)
                    target = en.Cast<object>().FirstOrDefault();
                else
                    target = eval;

                target = UnwrapXPathObject(target);

                if (target is XElement te)
                {
                    te.Value = value;
                    return XmlToString(root);
                }

                if (target is XAttribute ta)
                {
                    ta.Value = value;
                    return XmlToString(root);
                }

                // falls XPath auf Text node zeigt
                if (target is XText tt)
                {
                    tt.Value = value;
                    return XmlToString(root);
                }

                return XmlToString(root);
            }

            if (routine == "xml_is_elem")
            {
                var n = parameters[0];
                if (n is XDocument d) n = d.Root;
                return n is XElement;
            }

            if (routine == "xml_is_attr")
            {
                return parameters[0] is XAttribute;
            }

            if (routine == "xml_iter")
            {
                // xml_iter(nodeOrXmlString, mode?)
                // mode optional: "children" (default), "attributes", "descendants"
                object p0 = parameters[0];
                string mode = parameters.Count >= 2 && parameters[1] != null ? parameters[1].ToString()! : "children";

                var root = CoerceToXmlObject(p0);
                return new XmlIterator(root, mode);
            }

            if (routine == "xml_next")
            {
                var it = (XmlIterator)parameters[0];
                return it.MoveNext();
            }

            if (routine == "xml_name")
            {
                var it = (XmlIterator)parameters[0];
                return it.CurrentName;
            }

            if (routine == "xml_index")
            {
                var it = (XmlIterator)parameters[0];
                return it.CurrentIndex;
            }

            if (routine == "xml_node")
            {
                var it = (XmlIterator)parameters[0];
                return it.CurrentNode;
            }

            if (routine == "xml_value_it")
            {
                var it = (XmlIterator)parameters[0];
                return XmlNodeValue(it.CurrentNode);
            }

            return null;

        }

        public ReadOnlyCollection<Routine> Routines
        {
            get { return exportedRoutines; }
        }

        // --- helpers ---

        private static XNode? ToXNode(object? o)
        {
            if (o is null) return null;
            if (o is XDocument d) return d;
            if (o is XElement e) return e;
            if (o is XNode n) return n;
            return null;
        }

        private static object? UnwrapXPathObject(object? o)
        {
            if (o is null) return null;

            // LINQ to XML XPath liefert oft XElement/XAttribute/XText direkt.
            if (o is XElement || o is XAttribute || o is XText || o is XDocument) return o;

            // manchmal kommt XPathNavigator
            if (o is XPathNavigator nav)
            {
                if (nav.NodeType == XPathNodeType.Attribute)
                {
                    // versuche als XAttribute zurückzugeben: nicht trivial -> gebe string value zurück
                    return nav.Value;
                }

                if (nav.NodeType == XPathNodeType.Text)
                    return nav.Value;

                // element
                return nav.OuterXml;
            }

            // boolean/number/string aus XPathEvaluate
            return o;
        }

        private static string XmlToString(object root)
        {
            if (root is XDocument d) return d.ToString(SaveOptions.DisableFormatting);
            if (root is XNode n) return n.ToString(SaveOptions.DisableFormatting);
            if (root is XAttribute a) return a.ToString();
            return root.ToString();
        }


        private static object CoerceToXmlObject(object o)
        {
            if (o is XDocument || o is XElement || o is XAttribute || o is XText || o is XNode)
                return o;

            if (o is string s)
            {
                // raw xml string -> parse to XDocument
                return XDocument.Parse(s, LoadOptions.SetLineInfo);
            }

            throw new InvalidOperationException("xml_iter erwartet XDocument/XElement/XAttribute/XText/XNode oder XML-String.");
        }

        private static string? XmlNodeValue(object? node)
        {
            if (node is null) return null;

            if (node is XDocument d) return d.Root?.Value;
            if (node is XElement e) return e.Value;
            if (node is XAttribute a) return a.Value;
            if (node is XText t) return t.Value;
            if (node is XNode xn) return xn.ToString(SaveOptions.DisableFormatting);

            return node.ToString();
        }

        private sealed class XmlIterator
        {
            private readonly object _root;
            private readonly string _mode;

            private IEnumerator<object>? _enum;
            private bool _hasCurrent;
            private int _index;

            private object? _current;

            public XmlIterator(object root, string mode)
            {
                _root = root ?? throw new ArgumentNullException(nameof(root));
                _mode = (mode ?? "children").Trim().ToLowerInvariant();
                Reset();
            }

            private void Reset()
            {
                _hasCurrent = false;
                _index = -1;
                _current = null;

                // normalize root
                object r = _root;

                XDocument d = r as XDocument;
                if (d != null)
                {
                    r = (d.Root != null) ? (object)d.Root : (object)d;
                }

                if (r is XElement e)
                {
                    if (_mode == "attributes")
                    {
                        _enum = e.Attributes().Cast<object>().GetEnumerator();
                        return;
                    }

                    if (_mode == "descendants")
                    {
                        _enum = e.Descendants().Cast<object>().GetEnumerator();
                        return;
                    }

                    // default: children nodes (elements + text nodes)
                    _enum = e.Nodes().Cast<object>().GetEnumerator();
                    return;
                }

                // Attribute/Text/Node: iterator über "nur dieses Element"
                if (r is XAttribute || r is XText || r is XNode)
                {
                    _enum = Single(r).GetEnumerator();
                    return;
                }

                throw new InvalidOperationException("xml_iter: Unbekannter Root-Typ.");
            }

            private static IEnumerable<object> Single(object x)
            {
                yield return x;
            }

            public bool MoveNext()
            {
                if (_enum == null) return false;

                if (!_enum.MoveNext())
                {
                    _hasCurrent = false;
                    return false;
                }

                _hasCurrent = true;
                _index = _index < 0 ? 0 : _index + 1;
                _current = _enum.Current;
                return true;
            }

            public int CurrentIndex => _hasCurrent ? _index : -1;

            public string? CurrentName
            {
                get
                {
                    if (!_hasCurrent) return null;

                    if (_current is XElement e) return e.Name.LocalName;
                    if (_current is XAttribute a) return a.Name.LocalName;

                    return null; // Text nodes haben keinen Namen
                }
            }

            public object? CurrentNode => _hasCurrent ? _current : null;
        }

    }

}
