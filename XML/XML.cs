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

    }

}
