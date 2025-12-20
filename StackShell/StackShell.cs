using ScriptStack;
using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace StackShell
{

    class MyScanner : Scanner 
    { 
    
        public List<string> Scan(string source)
        {

            var lines = new List<string>();

            foreach (var line in File.ReadLines(source))
            {
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

    class Program : Host
    {

        private Manager manager;
        private Script script;
        private Interpreter interpreter;

        public Program(string[] args)
        {

            try
            {

                manager = new Manager();

                manager.Optimize = true;
                manager.Debug = false;

                manager.Scanner = new MyScanner();

                manager.LexerFactory = lines =>
                {
                    var lx = new Lexer(lines);
                    lx.DefaultReal = Lexer.DefaultRealType.Float;
                    return lx;
                };

                manager.LoadComponents(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "plugins"));

                if (manager.IsRegistered("print"))
                    manager.UnRegister("print");

                manager.Register(new Routine((Type)null, "print", (Type)null));

                script = new Script(manager, args[0]);

                interpreter = new Interpreter(script);

                interpreter.Handler = this;

                while (!interpreter.Finished)
                    interpreter.Interpret(1);

            }
            catch (ScriptStackException e)
            {

                throw; //Console.WriteLine("Error: " + e.MessageTrace);

            }

        }

        public object Invoke(string routine, List<object> parameters)
        {

            if(routine == "print")
            {
                Console.WriteLine(parameters[0]);
            }
            return null;

        }

    }

}
