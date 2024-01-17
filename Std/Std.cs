using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using ScriptStack.Runtime;

namespace ScriptStack
{

    public class Std : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public Std()
        {

            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();
            Routine routine = null;

            routine = new Routine(typeof(bool), "print", (Type)null, "Etwas auf der Konsole ausgeben.");
            routines.Add(routine);
            routine = new Routine(typeof(int), "read", "Einen einzelnen Tastenanschlag von der Konsole lesen.");
            routines.Add(routine);
            routine = new Routine(typeof(bool), "readline", "Eine Zeile von der Konsole lesen.");
            routines.Add(routine);

            exportedRoutines = routines.AsReadOnly();

        }

        public object Invoke(String strFunctionName, List<object> listParameters)
        {


            if (strFunctionName == "print")
            {

                Console.Write(listParameters[0]);
            }

            if (strFunctionName == "read")
            {
                return (int)Console.ReadKey().Key;
            }

            if (strFunctionName == "readline")
            {
                return Console.ReadLine();
            }

            return false;

        }

        public ReadOnlyCollection<Routine> Routines
        {
            get
            {
                return exportedRoutines;
            }
        }

    }

}