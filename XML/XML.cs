using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

            var routines = new List<Routine>();

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

            // iterators
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

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters)
        {
            if (routine == "xml_parse")
            {
                string xml = (string)parameters[0];
                return XDocument.Parse(xml, LoadOptions.SetLineInfo);
            }

            if (routine == "xml_string")
            {
                var node = parameters[0];
                if (node is XDocument d) return d.ToString(SaveOptions.None);
                if (node is XNode xn) return xn.ToString(SaveOptions.None);
                return XmlToString(node);
            }

            if (routine == "xml_select")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                // !!! WICHTIG !!!
                // Für XPath arbeiten wir IMMER auf dem Root-Element (bei XDocument => d.Root),
                // weil es Umgebungen gibt, wo absolute XPaths auf dem XDocument-Kontext “komisch” laufen.
                XElement ctxElem = ToXPathContextElement(root);
                if (ctxElem == null) return null;

                var nsmgr = BuildNamespaceResolver(ctxElem);
                object eval = ctxElem.XPathEvaluate(xpath, nsmgr);

                object first = FirstXPathResult(eval);
                return UnwrapXPathObject(first);
            }

            if (routine == "xml_select_all")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                var result = new ScriptStack.Runtime.ArrayList();

                XElement ctxElem = ToXPathContextElement(root);
                if (ctxElem == null) return result;

                var nsmgr = BuildNamespaceResolver(ctxElem);
                object eval = ctxElem.XPathEvaluate(xpath, nsmgr);

                if (IsEnumerableXPathResult(eval))
                {
                    foreach (var item in (IEnumerable)eval)
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

                if (node is XElement e) return e.Attribute(attrName)?.Value;
                if (node is XDocument d) return d.Root?.Attribute(attrName)?.Value;

                return null;
            }

            if (routine == "xml_has")
            {
                object root = parameters[0];
                string xpath = (string)parameters[1];

                XElement ctxElem = ToXPathContextElement(root);
                if (ctxElem == null) return false;

                var nsmgr = BuildNamespaceResolver(ctxElem);
                object eval = ctxElem.XPathEvaluate(xpath, nsmgr);

                if (IsEnumerableXPathResult(eval))
                {
                    foreach (var _ in (IEnumerable)eval) return true;
                    return false;
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

                // Kontext fürs XPath: Root-Element
                XElement ctxElem = ToXPathContextElement(root);
                if (ctxElem == null)
                    throw new InvalidOperationException("xml_set: erwartet XDocument oder XElement (mit Root).");

                var nsmgr = BuildNamespaceResolver(ctxElem);
                object eval = ctxElem.XPathEvaluate(xpath, nsmgr);

                object target = UnwrapXPathObject(FirstXPathResult(eval));

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

                if (target is XText tt)
                {
                    tt.Value = value;
                    return XmlToString(root);
                }

                return XmlToString(root);
            }

            if (routine == "xml_is_elem")
            {
                object n = parameters[0];
                if (n is XDocument d) n = d.Root;
                return n is XElement;
            }

            if (routine == "xml_is_attr")
            {
                return parameters[0] is XAttribute;
            }

            // iterators
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

        /// <summary>
        /// XPath-Kontext immer als XElement (Root-Element).
        /// </summary>
        private static XElement ToXPathContextElement(object o)
        {
            if (o == null) return null;

            if (o is XDocument d) return d.Root;
            if (o is XElement e) return e;

            // Falls jemand ein XNode (Element) direkt übergibt:
            if (o is XNode xn && xn is XElement xe) return xe;

            // Optional: wenn jemand XDocument als XNode gecastet hat
            if (o is XNode n && n is XDocument xd) return xd.Root;

            return null;
        }

        private static object CoerceToXmlObject(object o)
        {
            if (o == null) return null;

            if (o is XDocument || o is XElement || o is XAttribute || o is XText || o is XNode)
                return o;

            if (o is string s)
                return XDocument.Parse(s, LoadOptions.SetLineInfo);

            throw new InvalidOperationException("xml_iter erwartet XDocument/XElement/XAttribute/XText/XNode oder XML-String.");
        }

        private static string XmlToString(object node)
        {
            if (node == null) return null;

            if (node is XDocument d) return d.ToString(SaveOptions.DisableFormatting);
            if (node is XNode xn) return xn.ToString(SaveOptions.DisableFormatting);
            if (node is XAttribute xa) return xa.ToString();

            return node.ToString();
        }

        private static string XmlNodeValue(object node)
        {
            if (node == null) return null;

            if (node is XDocument d) return d.Root != null ? d.Root.Value : null;
            if (node is XElement e) return e.Value;
            if (node is XAttribute a) return a.Value;
            if (node is XText t) return t.Value;
            if (node is XNode xn) return xn.ToString(SaveOptions.DisableFormatting);

            return node.ToString();
        }

        private static object UnwrapXPathObject(object o)
        {
            if (o == null) return null;

            if (o is XElement || o is XAttribute || o is XText || o is XDocument) return o;
            return o; // scalar (bool/number/string)
        }

        private static bool IsEnumerableXPathResult(object eval)
        {
            // string ist IEnumerable<char> -> NICHT als nodeset behandeln
            if (eval == null) return false;
            if (eval is string) return false;
            return eval is IEnumerable;
        }

        private static object FirstXPathResult(object eval)
        {
            if (!IsEnumerableXPathResult(eval))
                return eval;

            foreach (var item in (IEnumerable)eval)
                return item;

            return null;
        }

        /// <summary>
        /// IXmlNamespaceResolver für XPath:
        /// - Default-Namespace (xmlns="...") wird als Prefix "d" registriert.
        /// - Alle xmlns:foo="..." Deklarationen vom Root werden ebenfalls registriert.
        /// </summary>
        private static IXmlNamespaceResolver BuildNamespaceResolver(XElement root)
        {
            var nt = new NameTable();
            var nsmgr = new XmlNamespaceManager(nt);

            if (root == null)
                return nsmgr;

            // Default-NS der Elemente
            string defNs = root.Name.NamespaceName;
            if (!string.IsNullOrEmpty(defNs))
                nsmgr.AddNamespace("d", defNs);

            // weitere deklarierte Prefixe (xmlns:foo="...")
            foreach (var a in root.Attributes().Where(a => a.IsNamespaceDeclaration))
            {
                // default decl (xmlns="...") ignorieren, weil wir es schon auf "d" mappen
                if (a.Name.LocalName == "xmlns") continue;

                string prefix = a.Name.LocalName;
                string uri = a.Value;

                if (!string.IsNullOrEmpty(prefix) && !string.IsNullOrEmpty(uri))
                    nsmgr.AddNamespace(prefix, uri);
            }

            return nsmgr;
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
                if (r is XDocument d)
                    r = d.Root != null ? (object)d.Root : (object)d;

                if (r is XElement e)
                {
                    if (_mode == XmlIterMode.Attributes)
                    {
                        _enum = e.Attributes().Cast<object>().GetEnumerator();
                        return;
                    }

                    if (_mode == XmlIterMode.Descendants)
                    {
                        _enum = e.Descendants().Cast<object>().GetEnumerator();
                        return;
                    }

                    _enum = e.Nodes().Cast<object>().GetEnumerator();
                    return;
                }

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

            public string CurrentName
            {
                get
                {
                    if (!_hasCurrent) return null;

                    if (_current is XElement e) return e.Name.LocalName;
                    if (_current is XAttribute a) return a.Name.LocalName;

                    return null;
                }
            }

            public object CurrentNode => _hasCurrent ? _current : null;
        }
    }
}
