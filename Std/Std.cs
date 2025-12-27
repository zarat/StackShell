using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
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

            if (exportedRoutines != null) 
                return;

            List<Routine> routines = new List<Routine>();

            // conversions
            routines.Add(new Routine(typeof(char), "char", (Type)null, "Konvertiere einen Wert zu char."));
            routines.Add(new Routine(typeof(int), "int", (Type)null, "Konvertiere einen Wert zu int."));
            routines.Add(new Routine(typeof(float), "float", (Type)null, "Konvertiere einen Wert zu float."));
            routines.Add(new Routine(typeof(double), "double", (Type)null, "Konvertiere einen Wert zu double."));
            routines.Add(new Routine(typeof(decimal), "decimal", (Type)null, "Konvertiere einen Wert zu decimal."));
            routines.Add(new Routine(typeof(string), "string", (Type)null, "Konvertiere einen Wert zu string."));
            routines.Add(new Routine(typeof(bool), "bool", (Type)null, "Konvertiere einen Wert zu bool."));


            // IO
            routines.Add(new Routine(typeof(bool), "print", (Type)null, "Ausgabe auf der Konsole erzeugen."));
            routines.Add(new Routine(typeof(int), "read", "Einen Tastenanschlag von der Konsole lesen."));
            routines.Add(new Routine(typeof(string), "readLine", "Eine Zeile von der Konsole lesen."));

            // Files
            routines.Add(new Routine(typeof(int), "fopen", typeof(string), typeof(string), "Eine Datei öffnen. (r, w, rw, a, rb, wb)"));
            routines.Add(new Routine(typeof(string), "fread", typeof(int), "Eine geöffnete Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwrite", typeof(int), typeof(string), "In eine geöffnete Datei schreiben."));
            routines.Add(new Routine(typeof(int), "fclose", typeof(int), "Eine geöffnete Datei schliessen."));
            routines.Add(new Routine(typeof(ArrayList), "freadb", typeof(int), "Binärdatei als Byte Array aus einer geöffneten Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwriteb", typeof(int), typeof(ArrayList), "Binärdaten (ArrayList 0..255) in eine geöffnete Datei schreiben."));
            routines.Add(new Routine(typeof(int), "fwritebytes", typeof(int), typeof(byte[]), "Binärdaten (byte[]) in eine geöffnete Datei schreiben."));

            routines.Add(new Routine((Type)null, "readDir", typeof(string), "Liste Dateien in einem Verzeichnis."));
            routines.Add(new Routine(typeof(string), "popen", typeof(string), typeof(string), "Rufe einen Prozess mit Argumenten auf und speichere die Ausgabe in einem String."));

            routines.Add(new Routine(typeof(string), "typeof", typeof(void), "Erhalte den Typ einer Variable."));
            routines.Add(new Routine(typeof(void), "parse", typeof(string), typeof(string), "Erstelle einen Typ aus einem String. char, int, float"));

            // String
            routines.Add(new Routine(typeof(string), "trim", typeof(string), ""));
            routines.Add(new Routine(typeof(string), "ltrim", typeof(string), ""));
            routines.Add(new Routine(typeof(string), "rtrim", typeof(string), ""));
            routines.Add(new Routine((Type)null, "split", typeof(string), typeof(char), "Splitte String mit Delimiter."));
            routines.Add(new Routine((Type)null, "join", typeof(ArrayList), typeof(string), "Erstelle einen String aus allen Elementen eines Array."));
            routines.Add(new Routine((Type)null, "startsWith", typeof(string), typeof(string), "Teste ob ein String mit einer bestimmten Zeichenfolge beginnt."));
            routines.Add(new Routine((Type)null, "endsWith", typeof(string), typeof(string), "Teste ob ein String mit einer bestimmten Zeichenfolge endet."));

            // regex
            routines.Add(new Routine(typeof(bool), "match", typeof(string), typeof(string), "Prüfe ob eine Zeichenfolge in einem String enthalten ist. string, pattern."));
            routines.Add(new Routine(typeof(int), "matchAll", typeof(string), typeof(string), "Zähle die Vorkommen einer Zeichenfolge in einem String. string, pattern."));
            routines.Add(new Routine(typeof(void), "replace", typeof(string), typeof(string), typeof(string), "Ersetze alle Vorkommen einer Zeichenfolge in einem String. string, pattern, ersatz."));

            exportedRoutines = routines.AsReadOnly();

            m_openFiles = new Dictionary<int, FileStream>();
            c_openFiles = 0;
        }

        public object Invoke(String strFunctionName, List<object> listParameters)
        {

            // ------------------------------------------------------------
            // conversions
            // ------------------------------------------------------------
            if(strFunctionName == "char")
                return Convert.ToChar(listParameters[0]);
            if(strFunctionName == "int")
                return Convert.ToInt32(listParameters[0]);
            if(strFunctionName == "float")
                return Convert.ToSingle(listParameters[0]);
            if(strFunctionName == "double")
                return Convert.ToDouble(listParameters[0]);
            if(strFunctionName == "decimal")
                return Convert.ToDecimal(listParameters[0]);
            if(strFunctionName == "string")
                return Convert.ToString(listParameters[0]);

            if(strFunctionName == "trim")
                return ((string)listParameters[0]).Trim();
            if(strFunctionName == "ltrim")
                return ((string)listParameters[0]).TrimStart();
            if(strFunctionName == "rtrim")
                return ((string)listParameters[0]).TrimEnd();


            // ------------------------------------------------------------
            // print
            // ------------------------------------------------------------
            if (strFunctionName == "print")
            {
                Console.Write(listParameters[0]);
                return true;
            }

            if (strFunctionName == "read")
                return (int)Console.ReadKey().Key;

            if (strFunctionName == "readLine")
                return Console.ReadLine();

            // ------------------------------------------------------------
            // fopen
            // ------------------------------------------------------------
            if (strFunctionName == "fopen")
            {
                string fileName = (string)listParameters[0];
                string mode = (string)listParameters[1];

                try
                {
                    int fileRef = c_openFiles++;

                    switch (mode)
                    {
                        case "r":
                        case "rb":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read));
                            break;

                        case "w":
                        case "wb":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read));
                            break;

                        case "rw":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read));
                            break;

                        case "a":
                            m_openFiles.Add(fileRef, new FileStream(fileName, FileMode.Append, FileAccess.Write, FileShare.Read));
                            break;

                        default:
                            throw new ScriptStackException("Ungültiger fopen-Mode: " + mode);
                    }

                    return fileRef;
                }
                catch (IOException e)
                {
                    throw new ScriptStackException(e.Message);
                }
            }

            // ------------------------------------------------------------
            // fread
            // ------------------------------------------------------------
            if (strFunctionName == "fread")
            {
                int fileRef = (int)listParameters[0];

                if (!m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                    return "";

                // StreamReader NICHT schließen -> leaveOpen:true
                using (var reader = new StreamReader(fileStream, Encoding.UTF8, true, 1024, leaveOpen: true))
                {
                    return reader.ReadToEnd();
                }
            }

            // ------------------------------------------------------------
            // fwrite
            // ------------------------------------------------------------
            if (strFunctionName == "fwrite")
            {
                int fileRef = (int)listParameters[0];
                string data = (string)listParameters[1];

                if (!m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                    return -1;

                using (var writer = new StreamWriter(fileStream, Encoding.UTF8, 1024, leaveOpen: true))
                {
                    writer.Write(data);
                    writer.Flush();
                }

                return data?.Length ?? 0;
            }

            // ------------------------------------------------------------
            // fclose
            // ------------------------------------------------------------
            if (strFunctionName == "fclose")
            {
                int fileRef = (int)listParameters[0];

                if (m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                {
                    fileStream.Flush();
                    fileStream.Close();
                    m_openFiles.Remove(fileRef);
                    return 1;
                }

                return -1;
            }

            // ------------------------------------------------------------
            // freadb
            // ------------------------------------------------------------
            if (strFunctionName == "freadb")
            {
                int fileRef = (int)listParameters[0];

                if (!m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                    return new ArrayList();

                // BinaryReader NICHT schließen -> leaveOpen:true
                var content = new List<int>();
                using (var br = new BinaryReader(fileStream, Encoding.UTF8, leaveOpen: true))
                {
                    while (br.BaseStream.Position < br.BaseStream.Length)
                        content.Add(br.ReadByte());
                }

                ArrayList arr = new ArrayList();
                foreach (int v in content)
                    arr.Add(v);

                return arr;
            }

            // ------------------------------------------------------------
            // fwriteb (FIXED)
            // ------------------------------------------------------------
            if (strFunctionName == "fwriteb")
            {
                int fileRef = (int)listParameters[0];
                ArrayList intArrayList = (ArrayList)listParameters[1];

                if (!m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                    return -1;

                if (intArrayList == null)
                    return 0;

                // WICHTIG: über Values iterieren (zuverlässig bei deiner ArrayList)
                var byteList = new List<byte>(intArrayList.Count);

                foreach (object v in intArrayList.Values)
                {
                    if (v == null) throw new ScriptStackException("ArrayList enthält null.");

                    int iv;

                    if (v is byte bv) iv = bv;
                    else if (v is int i) iv = i;
                    else if (v is float f) iv = (int)f;
                    else if (v is double d) iv = (int)d;
                    else if (!int.TryParse(v.ToString(), out iv))
                        throw new ScriptStackException("ArrayList enthält ungültigen Wert: " + v);

                    if (iv < 0 || iv > 255)
                        throw new ScriptStackException("Byte außerhalb 0..255: " + iv);

                    byteList.Add((byte)iv);
                }

                byte[] byteArray = byteList.ToArray();

                // Direkt in den Stream schreiben, NICHT schließen – nur flushen
                fileStream.Write(byteArray, 0, byteArray.Length);
                fileStream.Flush();

                return byteArray.Length;
            }

            // ------------------------------------------------------------
            // fwritebytes (NEU)
            // ------------------------------------------------------------
            if (strFunctionName == "fwritebytes")
            {
                int fileRef = (int)listParameters[0];
                byte[] data = (byte[])listParameters[1];

                if (!m_openFiles.TryGetValue(fileRef, out FileStream fileStream))
                    return -1;

                if (data == null) return 0;

                fileStream.Write(data, 0, data.Length);
                fileStream.Flush();

                return data.Length;
            }

            // ------------------------------------------------------------
            // popen
            // ------------------------------------------------------------
            if (strFunctionName == "popen")
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
                        return reader.ReadToEnd();
                }
            }

            // ------------------------------------------------------------
            // readDir
            // ------------------------------------------------------------
            if (strFunctionName == "readDir")
            {
                string directoryPath = @"" + (string)listParameters[0];

                if (Directory.Exists(directoryPath))
                {
                    string[] files = Directory.GetFiles(directoryPath);
                    Console.WriteLine("Dateien im Verzeichnis:");
                    foreach (string file in files) Console.WriteLine(file);

                    string[] subdirectories = Directory.GetDirectories(directoryPath);
                    Console.WriteLine("\nUnterverzeichnisse im Verzeichnis:");
                    foreach (string subdirectory in subdirectories) Console.WriteLine(subdirectory);
                }
                else
                {
                    Console.WriteLine("Das angegebene Verzeichnis existiert nicht.");
                }
                return null;
            }

            // ------------------------------------------------------------
            // split/join
            // ------------------------------------------------------------
            if (strFunctionName == "split")
            {
                string src = (string)listParameters[0];
                char delimiter = (char)listParameters[1];

                string[] arr = src.Split(delimiter);
                ArrayList result = new ArrayList();
                foreach (string s in arr) result.Add(s);
                return result;
            }

            if (strFunctionName == "join")
            {
                ArrayList src = (ArrayList)listParameters[0];
                string delimiter = (string)listParameters[1];

                int len = src.Count;
                int i = 1;
                string result = "";

                foreach (KeyValuePair<object, object> pair in src)
                {
                    result += pair.Value?.ToString() ?? "";
                    if (i < len) result += delimiter;
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

            // ------------------------------------------------------------
            // typeof / parse
            // ------------------------------------------------------------
            if (strFunctionName == "typeof")
            {
                var o = listParameters[0];
                return o == null ? "null" : o.GetType().Name;
            }

            if (strFunctionName == "parse")
            {
                string src = (string)listParameters[0];
                string target = (string)listParameters[1];

                switch (target)
                {
                    case "char": return char.Parse(src);
                    case "int": return Int32.Parse(src);
                    case "float": return float.Parse(src);
                }

                return null;
            }

            // ------------------------------------------------------------
            // regex helpers
            // ------------------------------------------------------------
            if (strFunctionName == "match")
            {
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];

                Regex regex = new Regex(pattern);
                return regex.IsMatch(input);
            }

            if (strFunctionName == "matchAll")
            {
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];

                Regex regex = new Regex(pattern);
                return regex.Matches(input).Count;
            }

            if (strFunctionName == "replace")
            {
                string input = (string)listParameters[0];
                string pattern = (string)listParameters[1];
                string replacement = (string)listParameters[2];
                return Regex.Replace(input, pattern, replacement);
            }

            return null;
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;
    
    }

}
