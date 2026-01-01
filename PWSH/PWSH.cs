using ScriptStack.Runtime;
using System.Collections;
using System.Collections.ObjectModel;
using System.Reflection;
using SMA = System.Management.Automation;

namespace StackShell
{

    public class PWSH : Model
    {

        private static ReadOnlyCollection<Routine>? exportedRoutines;

        [ThreadStatic]
        private static ScriptStack.Runtime.ArrayList? _lastErrors;

        public PWSH()
        {

            if (exportedRoutines != null) 
                return;

            var routines = new List<Routine>
            {
                new Routine((Type)null, "ps.exec", typeof(string), "Powershell Befehl/Script ausführen, param: pscode"),
                new Routine((Type)null, "ps.prop", (Type)null, typeof(string)),
                new Routine((Type)null, "ps.list", (Type)null, typeof(string)),
                new Routine((Type)null, "ps.call", (Type)null, typeof(string), typeof(ScriptStack.Runtime.ArrayList)),
                new Routine((Type)null, "ps.base", (Type)null),
                new Routine((Type)null, "ps.type", typeof(string)),
                new Routine((Type)null, "ps.errors", (Type)null, "If there were errors. params psresult"),
            };

            exportedRoutines = routines.AsReadOnly();

        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines!;

        public object Invoke(string routine, List<object> parameters)
        {
            switch (routine)
            {
                case "ps.exec": return PsExec(parameters);
                case "ps.prop": return PsProp(parameters);
                case "ps.list": return PsList(parameters);
                case "ps.call": return PsCall(parameters);
                case "ps.base": return PsBase(parameters);
                case "ps.type": return PsType(parameters);
                case "ps.errors": return _lastErrors ?? new ScriptStack.Runtime.ArrayList();
                default: return null;
            }
        }

        private object PsExec(List<object> parameters)
        {
            _lastErrors = new ScriptStack.Runtime.ArrayList();

            var script = (parameters != null && parameters.Count > 0) ? parameters[0]?.ToString() : null;
            if (string.IsNullOrWhiteSpace(script))
            {
                _lastErrors[0] = "ps.exec expects 1 string parameter (script).";
                return new ScriptStack.Runtime.ArrayList();
            }

            try
            {
                using var ps = SMA.PowerShell.Create();
                ps.AddScript(script);

                var results = ps.Invoke(); // Collection<PSObject>

                var outArr = new ScriptStack.Runtime.ArrayList();
                for (int i = 0; i < results.Count; i++)
                    outArr[i] = results[i]; // PSObject behalten

                for (int i = 0; i < ps.Streams.Error.Count; i++)
                    _lastErrors[i] = ps.Streams.Error[i].ToString();

                return outArr;
            }
            catch (Exception ex)
            {
                _lastErrors[0] = ex.ToString();
                return new ScriptStack.Runtime.ArrayList();
            }
        }

        private object PsBase(List<object> parameters)
        {
            if (parameters == null || parameters.Count < 1) return null;
            var x = parameters[0];
            if (x == null || x is null) return null;

            if (x is SMA.PSObject pso)
                return pso.BaseObject ?? null;

            return x;
        }

        private object PsType(List<object> parameters)
        {
            if (parameters == null || parameters.Count < 1) return null;
            var name = parameters[0]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) return null;

            // 1) direct
            var t = Type.GetType(name, throwOnError: false, ignoreCase: true);
            if (t != null) return t;

            // 2) search loaded assemblies
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType(name, throwOnError: false, ignoreCase: true);
                if (t != null) return t;
            }

            return null;
        }

        private object PsProp(List<object> parameters)
        {
            if (parameters == null || parameters.Count < 2) return null;

            var target = parameters[0];
            var name = parameters[1]?.ToString();

            if (target == null || target is null) return null;
            if (string.IsNullOrWhiteSpace(name)) return null;

            try
            {
                return WrapForScript(ReadValue(target, name));
            }
            catch
            {
                return null;
            }
        }

        // NIE NULL zurückgeben -> verhindert PTR bei .size / foreach
        private object PsList(List<object> parameters)
        {
            if (parameters == null || parameters.Count < 2) return new ScriptStack.Runtime.ArrayList();

            var target = parameters[0];
            var name = parameters[1]?.ToString();

            if (target == null || target is null) return new ScriptStack.Runtime.ArrayList();
            if (string.IsNullOrWhiteSpace(name)) return new ScriptStack.Runtime.ArrayList();

            try
            {
                var v = ReadValue(target, name);

                // missing/null => empty list
                if (v == null || v is null) return new ScriptStack.Runtime.ArrayList();

                // IEnumerable => list
                if (v is IEnumerable en && v is not string)
                    return ToSSArray(en);

                // single => [v]
                var a = new ScriptStack.Runtime.ArrayList();
                a[0] = WrapForScript(v);
                return a;
            }
            catch
            {
                return new ScriptStack.Runtime.ArrayList();
            }
        }

        private object PsCall(List<object> parameters)
        {
            if (parameters == null || parameters.Count < 3) return null;

            var target = parameters[0];
            var method = parameters[1]?.ToString();
            var argsAl = parameters[2] as ScriptStack.Runtime.ArrayList;

            if (target == null || target is null) return null;
            if (string.IsNullOrWhiteSpace(method)) return null;
            if (argsAl == null) argsAl = new ScriptStack.Runtime.ArrayList();

            // default: wenn PSObject => auf BaseObject callen
            if (target is SMA.PSObject pso)
                target = pso.BaseObject ?? null;

            if (target == null || target is null) return null;

            try
            {
                var args = ExtractArgs(argsAl);

                var result = InvokeBestMethod(target, method, args);

                return WrapForScript(result);
            }
            catch (Exception ex)
            {
                _lastErrors = _lastErrors ?? new ScriptStack.Runtime.ArrayList();
                _lastErrors[0] = "ps.call ERROR: " + ex;
                return null;
            }
        }

        // ---------------- internals ----------------

        private static object? ReadValue(object target, string name)
        {
            // PSObject ETS zuerst
            if (target is SMA.PSObject pso)
            {
                var prop = pso.Properties[name];
                if (prop != null)
                {
                    try { return prop.Value; }
                    catch { return null; }
                }

                // fallback: CLR auf BaseObject
                if (pso.BaseObject != null)
                    return ReadClrMember(pso.BaseObject, name);

                return null;
            }

            // CLR
            return ReadClrMember(target, name);
        }

        private static object? ReadClrMember(object obj, string name)
        {
            var t = obj.GetType();

            var p = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (p != null && p.GetIndexParameters().Length == 0)
            {
                try { return p.GetValue(obj); } catch { return null; }
            }

            var f = t.GetField(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
            if (f != null)
            {
                try { return f.GetValue(obj); } catch { return null; }
            }

            return null;
        }

        private static object WrapForScript(object? v)
        {
            if (v == null) return null;
            if (v is null) return v;

            // IEnumerable => ScriptStack.ArrayList
            if (v is IEnumerable en && v is not string)
                return ToSSArray(en);

            return v; // PSObject/CLR bleibt CLR
        }

        private static ScriptStack.Runtime.ArrayList ToSSArray(IEnumerable en)
        {
            var a = new ScriptStack.Runtime.ArrayList();
            int i = 0;

            foreach (var item in en)
            {
                a[i++] = item ?? null;
                if (i > 10000) break;
            }

            return a;
        }

        private static object?[] ExtractArgs(ScriptStack.Runtime.ArrayList al)
        {
            // ArrayList ist Dictionary<object,object> mit int-Keys
            var keys = al.Keys.OfType<int>().Where(k => k >= 0).OrderBy(k => k).ToArray();
            if (keys.Length == 0) return Array.Empty<object?>();

            var max = keys.Max();
            var args = new object?[max + 1];

            for (int i = 0; i <= max; i++)
            {
                if (al.TryGetValue(i, out var v))
                    args[i] = (v is null) ? null : v;
                else
                    args[i] = null;
            }

            return args;
        }

        private static object? InvokeBestMethod(object target, string methodName, object?[] args)
        {
            var t = target.GetType();

            var candidates = t.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                              .Where(m => string.Equals(m.Name, methodName, StringComparison.OrdinalIgnoreCase))
                              .ToArray();

            // simplest: match by param count + convertibility
            foreach (var m in candidates.OrderBy(m => m.GetParameters().Length))
            {
                var ps = m.GetParameters();
                if (ps.Length != args.Length) continue;

                var converted = new object?[args.Length];
                bool ok = true;

                for (int i = 0; i < ps.Length; i++)
                {
                    var pt = ps[i].ParameterType;
                    var av = args[i];

                    if (av == null)
                    {
                        if (pt.IsValueType && Nullable.GetUnderlyingType(pt) == null) { ok = false; break; }
                        converted[i] = null;
                        continue;
                    }

                    if (pt.IsInstanceOfType(av))
                    {
                        converted[i] = av;
                        continue;
                    }

                    try
                    {
                        // bool/int/etc.
                        converted[i] = Convert.ChangeType(av, Nullable.GetUnderlyingType(pt) ?? pt);
                    }
                    catch
                    {
                        ok = false;
                        break;
                    }
                }

                if (!ok) continue;

                return m.Invoke(target, converted);
            }

            throw new MissingMethodException($"{t.FullName}.{methodName}({string.Join(",", args.Select(a => a?.GetType().Name ?? "null"))})");
        }
    
    }

}
