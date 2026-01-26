using ScriptStack;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System.Collections;
using System.Reflection;
using static System.Net.Mime.MediaTypeNames;

namespace StackShell
{

    class MyScanner : Scanner 
    { 
    
        public List<string> Scan(string source)
        {

            var lines = new List<string>();

            foreach (var line in File.ReadLines(source))
            {
                bool add = true;
                if (line.Trim().StartsWith("#region")) add = false;  
                if (line.Trim().StartsWith("#endregion")) add = false; 
                if(add)
                    lines.Add(line);
            }

            return lines;

        }

    }

    static class StackShell
    {

        static void Main(string[] args)
        {

            new Program(args);

        }

    }

    public sealed class MyClrPolicy : IClrPolicy
    {

        private static readonly HashSet<string> AllowedMembers = new(StringComparer.OrdinalIgnoreCase) { 
            "str", "i", "Method", "Url", "Body", "InnerText" 
        };

        private static readonly HashSet<string> AllowedMethods = new(StringComparer.OrdinalIgnoreCase) { 
            "GetString", "GetMember", "ToString", "WriteLine", "ReadLine", "ReadAllText" 
        };

        private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase) {
            "Zarat.HTTP.HttpRequest", "Zarat.HTTP.HttpResponse", "ScriptStack.DomNode", "System.Console", "ScriptStack.Runtime.ArrayList", "System.IO.File",
            "System.Collections.Generic.List`1[[ScriptStack.DomNode, HTML, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null]]"
        };

        public bool IsTypeAllowed(Type t)
        {

            // Nur dein Host-Objekt darf als "Target" per Interop verwendet werden
            //if (t == typeof(Test)) return true;

            if(t.FullName != null && AllowedTypes.Contains(t.FullName))
                return true;

            // Rückgaben/Parameter dürfen primitive "safe" sein
            if (t.IsPrimitive) return true;
            if (t == typeof(string)) return true;
            if (t == typeof(decimal)) return true;

            // Optional: Nullable<T>
            var u = Nullable.GetUnderlyingType(t);
            if (u != null) return IsTypeAllowed(u);

            // Optional: Arrays von safe types
            if (t.IsArray) return IsTypeAllowed(t.GetElementType()!);

            return false;

        }

        public bool IsMemberAllowed(MemberInfo m)
        {
            // Nur Member auf Test zulassen (Felder/Properties)
            //if (m.DeclaringType == typeof(Test)) return true;

            if ( AllowedTypes.Contains((m.DeclaringType.ToString())) && AllowedMembers.Contains(m.Name)) 
                return true;

            return false;

            return AllowedMembers.Contains(m.Name);
        }

        public bool IsCallAllowed(MethodInfo m)
        {
            // Nur Methoden auf Test zulassen
            //if (m.DeclaringType != typeof(Test)) return false;

            if (AllowedTypes.Contains((m.DeclaringType.ToString())) && AllowedMethods.Contains(m.Name))
                return true;

            return false;

            // Blocke "GetType" sicherheitshalber immer
            if (m.Name.Equals("GetType", StringComparison.OrdinalIgnoreCase))
                return false;

            if (!AllowedMethods.Contains(m.Name))
                return false;

            // Optional: Parameter-Typen einschränken (hier: nur safe)
            foreach (var p in m.GetParameters())
                if (!IsTypeAllowed(p.ParameterType))
                    return false;

            // Return-Type kannst du hier NICHT zu hart prüfen,
            // weil GetMember() bei dir object zurückgibt.
            // Die eigentliche Rückgabe wird unten über IsReturnValueAllowed geprüft.
            return true;
        }

        public bool IsReturnValueAllowed(object? value)
        {
            if (value == null) return true;
            //if (value is NullReference) return true; // schadet nicht

            return IsTypeAllowed(value.GetType());
        }

    }



class Program : Host
    {

        private Manager manager;
        private Script script;
        private Interpreter interpreter;

        public Program(string[] args)
        {

            if (args.Count() == 0)
            {
                Console.WriteLine("Usage: StackShell <script.ss> [args...]");
                return;
            }

            try
            {
                
                manager = new Manager();
                manager.Scanner = new MyScanner();

                manager.LoadComponents(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "plugins"));

               
                manager.LexerFactory = lines =>
                {
                    var lx = new Lexer(lines);
                    lx.DefaultReal = Lexer.DefaultRealType.Float;
                    return lx;
                };

                /*
                if (manager.IsRegistered("print"))
                    manager.UnRegister("print");
                if(manager.IsRegistered("println"))
                    manager.UnRegister("println");
                */

                
                manager.Register(new Routine((Type)null, "print", (Type)null));
                manager.Register(new Routine((Type)null, "println", (Type)null));
                manager.Register(new Routine((Type)null, "call", (Type)null, (Type)null));
                manager.Register(new Routine((Type)null, "clr", (Type)null, (Type)null));
                

                foreach (KeyValuePair<string, Routine> r in manager.Routines) 
                {
                    //Console.WriteLine(r.Value.Handler.ToString() + " => " + r.Value.ToString());
                }

                if (args[0] == "-h")
                {
                    foreach(Routine r in manager.Routines.Values)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(r.ToString());
                        Console.ResetColor();
                        Console.WriteLine(" => " + r.Description());
                    }
                    return;
                }

                script = new Script(manager, args[0]);

                //script.CompileBinary(args[0] + ".bin");
                //script = new Script(manager, args[0] + ".bin", true);

                script.ScriptMemory["args"] = args.ToList<string>();


                var options = new InterpreterOptions
                {
                    Clr = ClrInteropMode.Full
                };

                /*
                // 1) Lex + ParseTokens
                List<Token> stream = Standalone.Lex("var a;var b; function main() { a = 1; b = 2; if(b > a) std.print(b); }");
                Executable exec = Standalone.ParseTokens(stream, manager);

                // 2) WICHTIG: exec an Script hängen, sonst crasht Interpreter (Script.Executable == null)
                typeof(Script).GetField("executable", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(exec.Script, exec);

                // 3) Interpreter starten
                var interpreter = new Interpreter(exec.Script);
                */


                //var script = Standalone.CompileToScript("var a;var b; function main() { a = 1; b = 2; if(b > a) std.print(b); }", manager);
                //var interpreter = new Interpreter(script);

                interpreter = new Interpreter(script, options);
                interpreter.Handler = this;
                
                
                //foreach(Instruction i in script.Executable.Runnable) Console.WriteLine(i.ToString());
                
                

                while (!interpreter.Finished)
                    interpreter.Interpret(1);

            }
            
            catch (LexerException e)
            {

                Console.WriteLine("Lexer: " + e.MessageTrace);

            }
            catch (ParserException e)
            {

                Console.WriteLine("Parser: " + e.MessageTrace);

            }
            catch (ExecutionException e)
            {

                Console.WriteLine("Interpreter: " + e.MessageTrace);

            }
            catch (ScriptStackException e)
            {

                Console.WriteLine("ScriptStack: " + e.MessageTrace);

            }
            
            catch (Exception ex) {

                Console.WriteLine("System: " + ex.Message);

            }

            //manager.UnloadPlugins();

        }

        public object Invoke(string routine, List<object> parameters)
        {

            if (routine == "clr")
            {
                var fqn = (string)parameters[0];

                Type? type = Type.GetType(fqn);
                if (type == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(fqn);
                        if (type != null) break;
                    }
                }
                if (type == null) return null;

                // NEU: Wenn 2. Parameter bool false ist -> nur Type zurückgeben
                if (parameters.Count > 1 && parameters[1] is bool b && b == false)
                    return type;

                // static class oder enum -> Type zurückgeben
                if ((type.IsAbstract && type.IsSealed) || type.IsEnum)
                    return type;

                // ctor args
                if (parameters.Count > 1 && parameters[1] is ScriptStack.Runtime.ArrayList al)
                {
                    var ctorArgs = new object?[al.Count];
                    for (int i = 0; i < al.Count; i++)
                    {
                        var item = al[i];
                        ctorArgs[i] =
                            item is KeyValuePair<object, object> kv ? kv.Value :
                            item is DictionaryEntry de ? de.Value :
                            item;
                    }
                    return Activator.CreateInstance(type, ctorArgs);
                }

                return Activator.CreateInstance(type);
            }

            if (routine == "clr1")
            {
                var fqn = (string)parameters[0];

                Type? type = Type.GetType(fqn);
                if (type == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(fqn);
                        if (type != null) break;
                    }
                }
                if (type == null) return null;

                // static class oder enum -> Type zurückgeben
                if ((type.IsAbstract && type.IsSealed) || type.IsEnum)
                    return type;

                // Wenn parameters[1] eine ArrayList ist: ctor mit Params
                if (parameters[1] is ScriptStack.Runtime.ArrayList al)
                {
                    var ctorArgs = new object?[al.Count];
                    for (int i = 0; i < al.Count; i++)
                    {
                        var item = al[i];
                        ctorArgs[i] =
                            item is KeyValuePair<object, object> kv ? kv.Value :
                            item is DictionaryEntry de ? de.Value :
                            item;
                    }

                    return Activator.CreateInstance(type, ctorArgs);
                }

                // Sonst: ohne Parameter (Default-CTOR)
                return Activator.CreateInstance(type);
            }

            if (routine == "init1")
            {
                var fqn = (string)parameters[0];

                Type? type = Type.GetType(fqn);
                if (type == null)
                {
                    foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                    {
                        type = asm.GetType(fqn);
                        if (type != null) break;
                    }
                }
                if (type == null) return null;

                // static class ODER enum -> Type zurückgeben
                if ((type.IsAbstract && type.IsSealed) || type.IsEnum)
                    return type;


                // normale Klasse -> Instanz wenn Default-CTOR existiert
                if (type.GetConstructor(Type.EmptyTypes) != null)
                    return Activator.CreateInstance(type);
                    

                // sonst: Type zurückgeben
                return type;
            }

            if (routine == "print")
            {

                Console.Write(parameters[0]);

            }
            if (routine == "println")
            {

                Console.WriteLine(parameters[0]);

            }
            if(routine == "call")
            {

                string name = (string)parameters[0];

                /*
                List<object> _params = new List<object>();             
                foreach(KeyValuePair<object, object> o in (ArrayList)parameters[1])
                {
                    _params.Add(o.Value);
                }

                Routine r = manager.Routines[name];
                int parameterCount = r.ParameterTypes.Count;
                this.Invoke(name, _params);
                */

                List<object> _params = new List<object>();
                foreach (KeyValuePair<object, object> o in (ScriptStack.Runtime.ArrayList)parameters[1])
                {
                    _params.Add(o.Value);
                }
                Function f = script.Functions[name];

                Interpreter inter = new Interpreter(f, _params);
                inter.Handler = this;
                inter.Interpret();
                return inter.ParameterStack[0];
                
                

            }

            return null;

        }

    }

}
