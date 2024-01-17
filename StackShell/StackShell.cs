using System;
using System.Collections.Generic;
using System.IO;

using ScriptStack;
using ScriptStack.Compiler;
using ScriptStack.Runtime;

namespace StackShell
{

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

                manager.LoadComponents(System.AppDomain.CurrentDomain.BaseDirectory);

                script = new Script(manager, args[0]);

                interpreter = new Interpreter(script);

                interpreter.Handler = this;

                while (!interpreter.Finished)
                    interpreter.Interpret(1);

            }
            catch (ScriptStackException e)
            {

                Console.WriteLine("Error: " + e.MessageTrace);

            }

        }

        public object Invoke(string routine, List<object> parameters)
        {

            return null;

        }

    }

}
