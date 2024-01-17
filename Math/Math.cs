using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using ScriptStack.Runtime;

namespace ScriptStack {

    public class Math : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public Math()
        {

            if (exportedRoutines != null) return;
            List<Routine> routines = new List<Routine>();

            routines.Add(new Routine(typeof(float), "math_sin", typeof(float), "Berechne den Sinus."));
            routines.Add(new Routine(typeof(float), "math_cos", typeof(float), "Berechne den Cosinus."));
            routines.Add(new Routine(typeof(float), "math_tan", typeof(float), "Berechne den Tangens."));
            routines.Add(new Routine(typeof(float), "math_asin", typeof(float), "Berechne den Arc Sinus."));
            routines.Add(new Routine(typeof(float), "math_acos", typeof(float), "Berechne den Arc Cosinus."));
            routines.Add(new Routine(typeof(float), "math_atan", typeof(float), "Berechne den Arc Tangens."));
            routines.Add(new Routine(typeof(float), "math_atan2", typeof(float), typeof(float), "Berechne den Arc Tangens2."));
            routines.Add(new Routine(typeof(float), "math_sinh", typeof(float), "Berechne den Sinus Hyperbolicus."));
            routines.Add(new Routine(typeof(float), "math_cosh", typeof(float), "Berechne den Cosinus Hyperbolicus."));
            routines.Add(new Routine(typeof(float), "math_tanh", typeof(float), "Berechne den Tangens Hyperbolicus."));
            routines.Add(new Routine((Type)null, "math_abs", (Type)null));
            routines.Add(new Routine(typeof(float), "math_ceiling", typeof(float)));
            routines.Add(new Routine(typeof(float), "math_e"));
            routines.Add(new Routine(typeof(float), "math_floor", typeof(float)));
            routines.Add(new Routine(typeof(float), "math_log", typeof(float)));
            routines.Add(new Routine(typeof(float), "math_log2", typeof(float), typeof(float)));
            routines.Add(new Routine(typeof(float), "math_pi"));
            routines.Add(new Routine(typeof(int), "math_round", typeof(float)));
            routines.Add(new Routine(typeof(float), "math_round2", typeof(float), typeof(int)));           
            routines.Add(new Routine(typeof(float), "math_sqrt", typeof(float)));           
            routines.Add(new Routine(typeof(int), "math_rand", typeof(int)));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines
        {
            get { return exportedRoutines; }
        }

        public object Invoke(String strFunctionName,
            List<object> listParameters)
        {


            if (strFunctionName == "math_abs")
            {
                Type typeParameter = listParameters[0].GetType();
                if (typeParameter == typeof(int))
                    return System.Math.Abs((int)listParameters[0]);
                if (typeParameter == typeof(float))
                    return System.Math.Abs((float)listParameters[0]);
                throw new ScriptStackException("Unsupported parameter for function '" + strFunctionName + "'.");
            }
            else if (strFunctionName == "math_acos")
                return (float)System.Math.Acos((float)listParameters[0]);
            else if (strFunctionName == "math_asin")
                return (float)System.Math.Asin((float)listParameters[0]);
            else if (strFunctionName == "math_atan")
                return (float)System.Math.Atan((float)listParameters[0]);
            else if (strFunctionName == "math_atan2")
                return (float)System.Math.Atan2((float)listParameters[0], (float)listParameters[1]);
            else if (strFunctionName == "math_ceiling")
                return (float)System.Math.Ceiling((float)listParameters[0]);
            else if (strFunctionName == "math_cos")
                return (float)System.Math.Cos((float)listParameters[0]);
            else if (strFunctionName == "math_cosh")
                return (float)System.Math.Cosh((float)listParameters[0]);
            else if (strFunctionName == "math_e")
                return (float)System.Math.E;
            else if (strFunctionName == "math_floor")
                return (float)System.Math.Floor((float)listParameters[0]);
            else if (strFunctionName == "math_log")
                return (float)System.Math.Log((float)listParameters[0]);
            else if (strFunctionName == "math_log2")
                return (float)System.Math.Log((float)listParameters[0], (float)listParameters[1]);

            else if (strFunctionName == "math_pi")
                return (float)System.Math.PI;
            else if (strFunctionName == "math_round")
                return (int)System.Math.Round((float)listParameters[0]);
            else if (strFunctionName == "math_round2")
                return (float)System.Math.Round((float)listParameters[0], (int)listParameters[1]);

            else if (strFunctionName == "math_sin")
            {
                float f = (float)listParameters[0];

                return (float)System.Math.Sin(f);
            }
            else if (strFunctionName == "math_sinh")
                return (float)System.Math.Sinh((float)listParameters[0]);
            else if (strFunctionName == "math_sqrt")
                return (float)System.Math.Sqrt((float)listParameters[0]);
            else if (strFunctionName == "math_tan")
                return (float)System.Math.Tan((float)listParameters[0]);
            else if (strFunctionName == "math_tanh")
                return (float)System.Math.Tanh((float)listParameters[0]);
            //else if (strFunctionName == "math_rand")
                //return (int)s_random.Next((int)listParameters[0]);

            throw new ScriptStackException("Unimplemented function '" + strFunctionName + "'.");


        }

    }

}