using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

using ScriptStack.Runtime;

namespace ScriptStack
{

    public class Std : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        private Dictionary<int, FileStream> m_openFiles;
        private int c_openFiles;

        public Std()
        {

            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();
            Routine routine = null;

            // IO
            routines.Add(new Routine(typeof(bool), "print", (Type)null, "Ausgabe auf der Konsole erzeugen."));
            routines.Add(new Routine(typeof(int), "read", "Einen Tastenanschlag von der Konsole lesen."));
            routines.Add(new Routine(typeof(string), "readLine", "Eine Zeile von der Konsole lesen."));

            // Files
            routines.Add(new Routine(typeof(int), "fopen", typeof(string), typeof(string), "Eine Datei öffnen. (r, w, rw, a, rb, wb"));
            routines.Add(new Routine(typeof(string), "fread", typeof(int), "Eine geöffnete Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwrite", typeof(int), typeof(string), "In eine geöffnete Datei schreiben."));
            routines.Add(new Routine(typeof(int), "fclose", typeof(int), "Eine geöffnete Datei schliessen."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "freadb", typeof(int), "Binärdatei als Byte Array aus einer geöffneten Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwriteb", typeof(int), typeof(ScriptStack.Runtime.ArrayList), "Binärdaten in eine geöffnete Datei schreiben."));

            routines.Add(new Routine((Type)null, "readDir", typeof(string), "Liste Dateien in einem Verzeichnis."));

            routines.Add(new Routine(typeof(string), "popen", typeof(string), typeof(string), "Rufe einen Prozess mit Argumenten auf und speichere die Ausgabe in einem String."));

            routines.Add(new Routine(typeof(string), "typeof", typeof(void), "Erhalte den Typ einer Variable."));
            routines.Add(new Routine(typeof(void), "parse", typeof(string), typeof(string), "Erstelle einen Typ aus einem String. char, int, float"));

            // String
            routines.Add(new Routine((Type)null, "split", typeof(string), typeof(char), "Liste Dateien in einem Verzeichnis."));
            routines.Add(new Routine((Type)null, "join", typeof(ScriptStack.Runtime.ArrayList), typeof(string), "Erstelle einen String aus allen Elementen eines Array."));
            routines.Add(new Routine((Type)null, "startsWith", typeof(string), typeof(string), "Teste ob ein String mit einer bestimmten Zeichenfolge beginnt."));
            routines.Add(new Routine((Type)null, "endsWith", typeof(string), typeof(string), "Teste ob ein String mit einer bestimmten Zeichenfolge endet."));
            // regex, regexAll, regexReplace
            routines.Add(new Routine(typeof(bool), "match", typeof(string), typeof(string), "Prüfe ob eine Zeichenfolge in einem String enthalten ist. string, pattern."));
            routines.Add(new Routine(typeof(int), "matchAll", typeof(string), typeof(string), "Zähle die Vorkommen einer Zeichenfolge in einem String. string, pattern."));
            routines.Add(new Routine(typeof(void), "replace", typeof(string), typeof(string), typeof(string), "Ersetze alle Vorkommen einer Zeichenfolge in einem String. string, pattern, ersatz."));
            
            exportedRoutines = routines.AsReadOnly();

            m_openFiles = new Dictionary<int, FileStream>();
            c_openFiles = 0;

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

            if (strFunctionName == "readLine")
            {
                return Console.ReadLine();
            }

            if (strFunctionName == "fopen")
            {

                string fileName = (string)listParameters[0];
                string mode = (string)listParameters[1];
                int fileRef = -1;

                try
                {

                    fileRef = c_openFiles++;

                    switch (mode)
                    {
                        case "r":
                        case "rb":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Open, FileAccess.Read));
                            break;
                        case "w":
                        case "wb":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Create, FileAccess.Write));
                            break;
                        case "rw":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite));
                            break;
                        case "a":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Append, FileAccess.Write));
                            break;
                    }

                }
                catch (IOException e)
                {
                    throw new ScriptStackException(e.Message);
                }

                return fileRef;

            }

            if (strFunctionName == "fread")
            {

                int fileRef = (int)listParameters[0];
                string content = "";

                if (m_openFiles.ContainsKey(fileRef))
                {
                    FileStream fileStream = m_openFiles[fileRef];
                    StreamReader reader = new StreamReader(fileStream);
                    content = reader.ReadToEnd();
                    
                }

                return content;

            }

            if (strFunctionName == "fwrite")
            {

                int fileRef = (int)listParameters[0];
                string data = (string)listParameters[1];
                int res = -1;

                if (m_openFiles.ContainsKey(fileRef))
                {

                    FileStream fileStream = m_openFiles[fileRef];

                    using (StreamWriter writer = new StreamWriter(fileStream))
                    {
                        writer.Write(data);
                    }

                    res++;

                }

                return res;

            }

            if (strFunctionName == "fclose")
            {

                int fileRef = (int)listParameters[0];
                
                if (m_openFiles.ContainsKey(fileRef))
                {
                    FileStream fileStream = m_openFiles[fileRef];
                    fileStream.Close();
                    return 1;
                }

                return -1;

            }

            if (strFunctionName == "freadb")
            {
                int fileRef = (int)listParameters[0];
                List<int> content = new List<int>();

                if (m_openFiles.ContainsKey(fileRef))
                {
                    FileStream fileStream = m_openFiles[fileRef];
                    using (BinaryReader binaryReader = new BinaryReader(fileStream))
                    {
                        while (binaryReader.BaseStream.Position < binaryReader.BaseStream.Length)
                        {
                            content.Add(binaryReader.ReadByte());
                        }
                    }
                }

                ScriptStack.Runtime.ArrayList arr = new ScriptStack.Runtime.ArrayList();
                foreach (int value in content)
                {
                    arr.Add(value);
                }

                return arr;
            }

            if (strFunctionName == "fwriteb")
            {
                int fileRef = (int)listParameters[0];
                ScriptStack.Runtime.ArrayList intArrayList = (ScriptStack.Runtime.ArrayList)listParameters[1];

                List<byte> byteList = new List<byte>();
                foreach (KeyValuePair<object, object> value in intArrayList)
                {
                    if (value.Value is int intValue)
                    {
                        byteList.Add((byte)intValue);
                    }
                    else
                    {
                        throw new ScriptStackException("ArrayList enthält ungültigen Wert.");
                    }
                }

                byte[] byteArray = byteList.ToArray();

                if (m_openFiles.ContainsKey(fileRef))
                {
                    FileStream fileStream = m_openFiles[fileRef];
                    using (BinaryWriter binaryWriter = new BinaryWriter(fileStream))
                    {
                        binaryWriter.Write(byteArray);
                    }
                }
            }

            if(strFunctionName == "popen")
            {

                string executablePath = (string)listParameters[0];
                string arguments = (string)listParameters[1];

                using (Process process = new Process())
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = executablePath,
                        Arguments = arguments,
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    process.StartInfo = startInfo;
                    process.Start();
                    process.WaitForExit();

                    using (StreamReader reader = process.StandardOutput)
                    {
                        return reader.ReadToEnd();
                    }
                }
            }

            if(strFunctionName == "readDir")
            {
                // Setze den Pfad des Verzeichnisses, das du auflisten möchtest
                string directoryPath = @"" + (string)listParameters[0]; //@"C:\Dein\Verzeichnis\Pfad";

                // Überprüfe, ob das Verzeichnis existiert
                if (Directory.Exists(directoryPath))
                {
                    // Rufe die Dateien im Verzeichnis ab
                    string[] files = Directory.GetFiles(directoryPath);

                    Console.WriteLine("Dateien im Verzeichnis:");
                    foreach (string file in files)
                    {
                        Console.WriteLine(file);
                    }

                    // Rufe die Unterverzeichnisse im Verzeichnis ab
                    string[] subdirectories = Directory.GetDirectories(directoryPath);

                    Console.WriteLine("\nUnterverzeichnisse im Verzeichnis:");
                    foreach (string subdirectory in subdirectories)
                    {
                        Console.WriteLine(subdirectory);
                    }
                }
                else
                {
                    Console.WriteLine("Das angegebene Verzeichnis existiert nicht.");
                }
            }

            if(strFunctionName == "split")
            {

                // String Split
                string src = (string)listParameters[0];
                char delimiter = (char)listParameters[1];

                string[] arr = src.Split(delimiter);
                ScriptStack.Runtime.ArrayList result = new ScriptStack.Runtime.ArrayList();
                foreach(string s in arr)
                {
                    result.Add(s);
                }
                return result;

            }
            if (strFunctionName == "join")
            {
                ScriptStack.Runtime.ArrayList src = (ScriptStack.Runtime.ArrayList)listParameters[0]; 
                string delimiter = (string)listParameters[1];
                
                int len = src.Count;
                string result = "";
                int i = 1;
                foreach(KeyValuePair<object, object> pair in src)
                {
                    if (i < len)
                        result += pair.Value.ToString() + delimiter;
                    else
                        result += pair.Key.ToString();
                    i++;
                }

                return result;

            }
            if (strFunctionName == "startsWith")
            {
                string src = (string)listParameters[0];
                string test = (string)listParameters[1];
                return src.StartsWith(test);
            }
            if (strFunctionName == "endsWith")
            {
                string src = (string)listParameters[0];
                string test = (string)listParameters[1];
                return src.EndsWith(test);
            }

            if(strFunctionName == "typeof")
            {
                var o = listParameters[0];
                string result = "";
                if (o is char)
                {
                    result = "char";
                }
                else if (o is int)
                {
                    result = "int";
                }
                else if (o is string)
                {
                    result = "string";
                }
                else if (o is float)
                {
                    result = "float";
                }
                else if (o is ScriptStack.Runtime.ArrayList)
                {
                    result = "array";
                }
                else if(o is bool)
                {
                    result = "bool";
                }
                else
                    result = "undefined";

                return result;
            }
            if(strFunctionName == "parse")
            {
                string src = (string)listParameters[0];
                string target = (string)listParameters[1];

                switch(target)
                {
                    case "char":
                        return char.Parse(src);
                    case "int":
                        return Int32.Parse(src);
                    case "float":
                        return float.Parse(src);
                }
            }

            if (strFunctionName == "match")
            {
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];

                Regex regex = new Regex(pattern);
                Match match = regex.Match(input);

                if (match.Success)
                {
                    return true;
                }

                return false;
            }

            if (strFunctionName == "matchAll")
            {
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];

                Regex regex = new Regex(pattern);
                MatchCollection matches = regex.Matches(input);
                int i = 0;

                foreach (Match m in matches)
                {
                    i++;
                }

                return i;
            }

            if(strFunctionName == "replace")
            {
                
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];
                string replacement = (string)listParameters[2];
                string result = Regex.Replace(input, pattern, replacement);
                return result;

            }
            
            return null;

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
