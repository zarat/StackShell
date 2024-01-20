using System.Collections.ObjectModel;
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
            routines.Add(new Routine(typeof(bool), "readLine", "Eine Zeile von der Konsole lesen."));

            // Files
            routines.Add(new Routine(typeof(int), "fopen", typeof(string), typeof(string), "Eine Datei öffnen. (r, w, rw, a, rb, wb"));
            routines.Add(new Routine(typeof(string), "fread", typeof(int), "Eine geöffnete Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwrite", typeof(int), typeof(string), "In eine geöffnete Datei schreiben."));
            routines.Add(new Routine(typeof(int), "fclose", typeof(int), "Eine geöffnete Datei schliessen."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "freadb", typeof(int), "Binärdatei als Byte Array aus einer geöffneten Datei lesen."));
            routines.Add(new Routine(typeof(int), "fwriteb", typeof(int), typeof(ScriptStack.Runtime.ArrayList), "Binärdaten in eine geöffnete Datei schreiben."));

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

            if (strFunctionName == "readline")
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
                        // Fehlerbehandlung, falls ein Wert in der ArrayList keine Ganzzahl ist
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

            return null;

        }

        static byte[] ConvertListToByteArray(List<int> intList)
        {
            List<byte[]> byteArrays = new List<byte[]>();
            foreach (int intValue in intList)
            {
                byteArrays.Add(BitConverter.GetBytes(intValue));
            }
            byte[] resultByteArray = new byte[byteArrays.Sum(arr => arr.Length)];
            int offset = 0;
            foreach (byte[] byteArray in byteArrays)
            {
                Buffer.BlockCopy(byteArray, 0, resultByteArray, offset, byteArray.Length);
                offset += byteArray.Length;
            }
            return resultByteArray;
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
