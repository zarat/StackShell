using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
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
            routines.Add(new Routine(typeof(string), "xml_string", (Type)null, "Gibt das XML (XDocument/XNode/XAttribute) als String zurück."));

            routines.Add(new Routine((Type)null, "xml_select", (Type)null, (Type)null, "Wählt einen(!) Knoten per XPath aus (XElement/XAttribute/XText) oder null."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "xml_select_all", (Type)null, (Type)null, "Wählt mehrere Knoten per XPath aus (ArrayList von Nodes)."));

            routines.Add(new Routine(typeof(string), "xml_value", (Type)null, "Liest den String-Value eines Nodes (XElement/XAttribute/XText)."));
            routines.Add(new Routine(typeof(string), "xml_attr", (Type)null, (Type)null, "Liest ein Attribut (Name) von einem Element (oder null)."));
            routines.Add(new Routine(typeof(bool), "xml_has", (Type)null, (Type)null, "Prüft ob ein XPath ein Ergebnis liefert."));

            routines.Add(new Routine((Type)null, "xml_set", (Type)null, (Type)null, (Type)null, "Setzt den Value eines Elements/Attributes per XPath (erstes Match)."));

            // type checks
            routines.Add(new Routine(typeof(bool), "xml_is_elem", (Type)null, "True wenn Node ein XElement ist."));
            routines.Add(new Routine(typeof(bool), "xml_is_attr", (Type)null, "True wenn Node ein XAttribute ist."));

            // iterators (alle 1 Parameter wie json_iter/yaml_iter)
            routines.Add(new Routine((Type)null, "xml_iter", (Type)null, "Iterator über Child-Nodes (XDocument/XElement/XNode oder XML-String)."));
            routines.Add(new Routine((Type)null, "xml_iter_all", (Type)null, "Iterator über Descendants (Unter-Elemente) eines Elements."));
            routines.Add(new Routine((Type)null, "xml_iter_attr", (Type)null, "Iterator über Attribute eines Elements."));

            routines.Add(new Routine(typeof(bool), "xml_next", (Type)null, "Iterator: nächstes Element. true wenn vorhanden."));
            routines.Add(new Routine(typeof(string), "xml_name", (Type)null, "Iterator: Name (Element/Attribut) oder null."));
            routines.Add(new Routine(typeof(int), "xml_index", (Type)null, "Iterator: Index (0..n-1) oder -1 wenn kein current."));
            routines.Add(new Routine((Type)null, "xml_node", (Type)null, "Iterator: aktueller Node (XElement/XAttribute/XText/XNode)."));
            routines.Add(new Routine(typeof(string), "xml_value_it", (Type)null, "Iterator: Value des aktuellen Nodes (wie xml_value)."));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines
        {
            get { return exportedRoutines; }
        }

        public object Invoke(string routine, List<object> parameters)
        {
            if (routine == "xml_parse")
            {
                string xml = (string)parameters[0];
                var doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
                return doc;
            }

            if (routine == "xml_string")
            {
                var node = parameters[0];
                if (node is XDocument d) return d.ToString(SaveOptions.None);
                if (node is XNode xn) return xn.ToString(SaveOptions.None);
                return XmlToString(parameters[0]);
            }

            if (routine == "xml_select")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                XNode xroot = ToXNode(root);
                if (xroot == null) return null;

                object eval = xroot.XPathEvaluate(xpath);

                if (eval is IEnumerable<object>)
                {
                    IEnumerable<object> en = (IEnumerable<object>)eval;
                    object first = en.Cast<object>().FirstOrDefault();
                    return UnwrapXPathObject(first);
                }

                return UnwrapXPathObject(eval);
            }

            if (routine == "xml_select_all")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                XNode xroot = ToXNode(root);
                var result = new ScriptStack.Runtime.ArrayList();
                if (xroot == null) return result;

                object eval = xroot.XPathEvaluate(xpath);

                if (eval is IEnumerable<object>)
                {
                    IEnumerable<object> en = (IEnumerable<object>)eval;
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
                return XmlNodeValue(parameters[0]);
            }

            if (routine == "xml_attr")
            {
                object node = parameters[0];
                string attrName = (string)parameters[1];

                XElement e = node as XElement;
                if (e != null) return e.Attribute(attrName)?.Value;

                XDocument d = node as XDocument;
                if (d != null) return d.Root?.Attribute(attrName)?.Value;

                return null;
            }

            if (routine == "xml_has")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                XNode xroot = ToXNode(root);
                if (xroot == null) return false;

                object eval = xroot.XPathEvaluate(xpath);

                if (eval is IEnumerable<object>)
                {
                    IEnumerable<object> en = (IEnumerable<object>)eval;
                    return en.Cast<object>().Any();
                }

                return eval != null;
            }

            if (routine == "xml_set")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];
                object valueObj = parameters[2];
                string value = valueObj != null ? valueObj.ToString() : "";

                if (root is XAttribute)
                    throw new InvalidOperationException("xml_set: root darf kein Attribut sein.");

                XNode xroot = root as XNode;
                if (xroot == null)
                    throw new InvalidOperationException("xml_set: erwartet XDocument oder XElement.");

                object eval = xroot.XPathEvaluate(xpath);

                object target = null;
                if (eval is IEnumerable<object>)
                {
                    IEnumerable<object> en = (IEnumerable<object>)eval;
                    target = en.Cast<object>().FirstOrDefault();
                }
                else
                {
                    target = eval;
                }

                target = UnwrapXPathObject(target);

                XElement te = target as XElement;
                if (te != null)
                {
                    te.Value = value;
                    return XmlToString(root);
                }

                XAttribute ta = target as XAttribute;
                if (ta != null)
                {
                    ta.Value = value;
                    return XmlToString(root);
                }

                XText tt = target as XText;
                if (tt != null)
                {
                    tt.Value = value;
                    return XmlToString(root);
                }

                return XmlToString(root);
            }

            if (routine == "xml_is_elem")
            {
                object n = parameters[0];
                XDocument d = n as XDocument;
                if (d != null) n = d.Root;
                return n is XElement;
            }

            if (routine == "xml_is_attr")
            {
                return parameters[0] is XAttribute;
            }

            // iterators (alle 1 Parameter)
            if (routine == "xml_iter")
            {
                object root = CoerceToXmlObject(parameters[0]);
                return new XmlIterator(root, XmlIterMode.Children);
            }

            if (routine == "xml_iter_all")
            {
                object root = CoerceToXmlObject(parameters[0]);
                return new XmlIterator(root, XmlIterMode.Descendants);
            }

            if (routine == "xml_iter_attr")
            {
                object root = CoerceToXmlObject(parameters[0]);
                return new XmlIterator(root, XmlIterMode.Attributes);
            }

            if (routine == "xml_next")
            {
                XmlIterator it = (XmlIterator)parameters[0];
                return it.MoveNext();
            }

            if (routine == "xml_name")
            {
                XmlIterator it = (XmlIterator)parameters[0];
                return it.CurrentName;
            }

            if (routine == "xml_index")
            {
                XmlIterator it = (XmlIterator)parameters[0];
                return it.CurrentIndex;
            }

            if (routine == "xml_node")
            {
                XmlIterator it = (XmlIterator)parameters[0];
                return it.CurrentNode;
            }

            if (routine == "xml_value_it")
            {
                XmlIterator it = (XmlIterator)parameters[0];
                return XmlNodeValue(it.CurrentNode);
            }

            return null;
        }

        // ---------- helpers ----------

        private static XNode ToXNode(object o)
        {
            if (o == null) return null;

            XDocument d = o as XDocument;
            if (d != null) return d;

            XElement e = o as XElement;
            if (e != null) return e;

            XNode n = o as XNode;
            if (n != null) return n;

            return null;
        }

        private static object CoerceToXmlObject(object o)
        {
            if (o == null) return null;

            if (o is XDocument || o is XElement || o is XAttribute || o is XText || o is XNode)
                return o;

            string s = o as string;
            if (s != null)
                return XDocument.Parse(s, LoadOptions.SetLineInfo);

            throw new InvalidOperationException("xml_iter erwartet XDocument/XElement/XAttribute/XText/XNode oder XML-String.");
        }

        private static string XmlToString(object node)
        {
            if (node == null) return null;

            XDocument d = node as XDocument;
            if (d != null) return d.ToString(SaveOptions.DisableFormatting);

            XNode xn = node as XNode;
            if (xn != null) return xn.ToString(SaveOptions.DisableFormatting);

            XAttribute xa = node as XAttribute;
            if (xa != null) return xa.ToString();

            return node.ToString();
        }

        private static string XmlNodeValue(object node)
        {
            if (node == null) return null;

            XDocument d = node as XDocument;
            if (d != null) return d.Root != null ? d.Root.Value : null;

            XElement e = node as XElement;
            if (e != null) return e.Value;

            XAttribute a = node as XAttribute;
            if (a != null) return a.Value;

            XText t = node as XText;
            if (t != null) return t.Value;

            XNode xn = node as XNode;
            if (xn != null) return xn.ToString(SaveOptions.DisableFormatting);

            return node.ToString();
        }

        private static object UnwrapXPathObject(object o)
        {
            if (o == null) return null;

            // LINQ to XML XPath liefert oft XElement/XAttribute/XText direkt.
            if (o is XElement || o is XAttribute || o is XText || o is XDocument) return o;

            // Scalar results from XPathEvaluate (bool/number/string) return as-is
            return o;
        }

        // ---------- iterator ----------

        private enum XmlIterMode
        {
            Children,
            Descendants,
            Attributes
        }

        private sealed class XmlIterator
        {
            private readonly object _root;
            private readonly XmlIterMode _mode;

            private IEnumerator<object> _enum;
            private bool _hasCurrent;
            private int _index;
            private object _current;

            public XmlIterator(object root, XmlIterMode mode)
            {
                _root = root ?? throw new ArgumentNullException(nameof(root));
                _mode = mode;
                Reset();
            }

            private void Reset()
            {
                _hasCurrent = false;
                _index = -1;
                _current = null;
                _enum = null;

                object r = _root;

                // XDocument -> Root (wenn vorhanden)
                XDocument d = r as XDocument;
                if (d != null)
                    r = d.Root != null ? (object)d.Root : (object)d;

                XElement e = r as XElement;
                if (e != null)
                {
                    if (_mode == XmlIterMode.Attributes)
                    {
                        _enum = e.Attributes().Cast<object>().GetEnumerator();
                        return;
                    }

                    if (_mode == XmlIterMode.Descendants)
                    {
                        // nur Elemente (wie "descendants" gemeint)
                        _enum = e.Descendants().Cast<object>().GetEnumerator();
                        return;
                    }

                    // children: Nodes() (Elemente + Text + Kommentare etc.)
                    _enum = e.Nodes().Cast<object>().GetEnumerator();
                    return;
                }

                // Attribut/Text/Node: iterator über "nur dieses Element"
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

            public int CurrentIndex
            {
                get { return _hasCurrent ? _index : -1; }
            }

            public string CurrentName
            {
                get
                {
                    if (!_hasCurrent) return null;

                    XElement e = _current as XElement;
                    if (e != null) return e.Name.LocalName;

                    XAttribute a = _current as XAttribute;
                    if (a != null) return a.Name.LocalName;

                    return null;
                }
            }

            public object CurrentNode
            {
                get { return _hasCurrent ? _current : null; }
            }
        }
    
    }

}
