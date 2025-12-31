using Microsoft.Data.Analysis;
using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;

namespace ScriptStack
{

    /// <summary>
    /// ScriptStack Plugin für Microsoft.Data.Analysis DataFrames (pandas-ähnlich).
    /// Arbeitet mit Handles (int), ähnlich wie fopen/fclose.
    /// </summary>
    public class DataFrames : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        private readonly Dictionary<int, Microsoft.Data.Analysis.DataFrame> _frames = new Dictionary<int, Microsoft.Data.Analysis.DataFrame>();
        private int _nextHandle = 1;

        private readonly Dictionary<int, CsvFmtOptions> _csvFmtByHandle = new Dictionary<int, CsvFmtOptions>();

        // Global defaults (wenn für Handle nichts gesetzt ist)
        private readonly CsvFmtOptions _globalCsvFmt = new CsvFmtOptions
        {
            FloatFormat = "G9",
            DoubleFormat = "G17",
            DecimalFormat = "G29",
            QuoteAll = false,
            Culture = CultureInfo.InvariantCulture
        };

        public DataFrames()
        {
            if (exportedRoutines != null) return;

            var routines = new List<Routine>();

            // CSV I/O
            // dfLoadCsv(path, sep, headerFlag)
            routines.Add(new Routine(typeof(int), "df.LoadCsv", typeof(string), typeof(string), typeof(int), "Lade CSV in DataFrame. sep z.B. \",\" oder \";\". headerFlag: 1/0. -> handle"));
            routines.Add(new Routine(typeof(int), "df.ReadCsv", typeof(string), typeof(string), typeof(int), "Lade CSV in DataFrame. sep z.B. \",\" oder \";\". headerFlag: 1/0. -> handle"));

            // dfSaveCsv(handle, path, sep, headerFlag)
            List<Type> saveCSVParams = new List<Type>();
            saveCSVParams.Add(typeof(int));
            saveCSVParams.Add(typeof(string));
            saveCSVParams.Add(typeof(string));
            saveCSVParams.Add(typeof(int));
            routines.Add(new Routine(typeof(int), "df.SaveCsv", saveCSVParams, "Speichere DataFrame als CSV. -> 1 ok, -1 Fehler"));

            // Display/Info
            // dfHead(handle, n)
            routines.Add(new Routine(typeof(string), "df.Head", typeof(int), typeof(int), "Gibt die ersten n Zeilen als Texttabelle zurück."));

            // dfInfo(handle)
            routines.Add(new Routine(typeof(string), "df.Info", typeof(int), "Gibt Shape + Spalten (Name/Typ/NullCount) zurück."));

            // dfColumns(handle) -> ArrayList of strings
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "df.Columns", typeof(int), "Liste der Spaltennamen."));

            // dfShape(handle) -> ArrayList [rows, cols]
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "df.Shape", typeof(int), "Shape [rows, cols]."));

            // Cell get/set
            // dfGet(handle, rowIndex, col) col kann int (Index) oder string (Name) sein
            routines.Add(new Routine((Type)null, "df.Get", typeof(int), typeof(int), (Type)null, "Hole Zellenwert: dfGet(handle, row, colIndex|colName)."));

            List<Type> dfSetParams = new List<Type>();
            dfSetParams.Add(typeof(int));
            dfSetParams.Add(typeof(int));
            dfSetParams.Add((Type)null);
            dfSetParams.Add((Type)null);
            // dfSet(handle, rowIndex, col, value) -> 1 ok, -1
            routines.Add(new Routine(typeof(int), "df.Set", dfSetParams, "Setze Zellenwert: dfSet(handle, row, colIndex|colName, value)."));

            // Column as Array
            // dfCol(handle, col) -> ArrayList values
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "df.Col", typeof(int), (Type)null, "Hole eine Spalte als ArrayList: dfCol(handle, colIndex|colName)."));

            // Close handle
            routines.Add(new Routine(typeof(int), "df.Close", typeof(int), "Schließe DataFrame-Handle und gib Ressourcen frei."));


            // Row as Array
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "df.Row", typeof(int), typeof(int), "Hole eine Zeile als ArrayList: dfRow(handle, rowIndex)."));

            // Select columns -> new handle
            routines.Add(new Routine(typeof(int), "df.Select",
                typeof(int), typeof(ScriptStack.Runtime.ArrayList),
                "Wähle Spalten aus und erzeuge neuen DataFrame: dfSelect(handle, colsArray)."));

            // Filter eq -> new handle
            var filterParams = new List<Type> { typeof(int), (Type)null, (Type)null };
            routines.Add(new Routine(typeof(int), "df.FilterEq",
                filterParams,
                "Filtere Zeilen (col == value) und erzeuge neuen DataFrame: dfFilterEq(handle, colIndex|colName, value)."));

            // Describe -> string table
            var describeParams = new List<Type> { typeof(int), (Type)null };
            routines.Add(new Routine(typeof(string), "df.Describe",
                describeParams,
                "Describe für numerische Spalten. dfDescribe(handle, colName|colIndex|\"\")"));


            // Filter >
            var filterGtParams = new List<Type> { typeof(int), (Type)null, (Type)null };
            routines.Add(new Routine(typeof(int), "df.FilterGt", filterGtParams,
                "Filter: col > value. dfFilterGt(handle, colIndex|colName, value) -> new handle"));

            // Filter <
            var filterLtParams = new List<Type> { typeof(int), (Type)null, (Type)null };
            routines.Add(new Routine(typeof(int), "df.FilterLt", filterLtParams,
                "Filter: col < value. dfFilterLt(handle, colIndex|colName, value) -> new handle"));

            // Sort
            routines.Add(new Routine(typeof(int), "df.Sort",
                typeof(int), (Type)null, typeof(int),
                "Sortiere nach Spalte. dfSort(handle, colIndex|colName, ascFlag 1/0) -> new handle"));

            // ValueCounts
            routines.Add(new Routine(typeof(int), "df.ValueCounts",
                typeof(int), (Type)null, typeof(int),
                "ValueCounts. dfValueCounts(handle, colIndex|colName, ascFlag 1/0) -> new handle (Value,Count)"));


            // Filter >=
            var filterGeParams = new List<Type> { typeof(int), (Type)null, (Type)null };
            routines.Add(new Routine(typeof(int), "df.FilterGE", filterGeParams,
                "Filter: col >= value. dfFilterGE(handle, colIndex|colName, value) -> new handle"));

            // Filter <=
            var filterLeParams = new List<Type> { typeof(int), (Type)null, (Type)null };
            routines.Add(new Routine(typeof(int), "df.FilterLE", filterLeParams,
                "Filter: col <= value. dfFilterLE(handle, colIndex|colName, value) -> new handle"));

            // Multi-sort: colsArray + ascSpec (entweder int 1/0 für alle, oder ArrayList von 1/0)
            var sortByParams = new List<Type> { typeof(int), typeof(ScriptStack.Runtime.ArrayList), (Type)null };
            routines.Add(new Routine(typeof(int), "df.SortBy", sortByParams,
                "Multi-Sort: dfSortBy(handle, colsArray, ascSpec). ascSpec: 1/0 oder [1,0,1]. -> new handle"));

            var likeParams = new List<Type> { typeof(int), (Type)null, typeof(string), typeof(int) };
            routines.Add(new Routine(typeof(int), "df.FilterLike", likeParams,
                "Filter: LIKE pattern (% und _). dfFilterLike(handle, colIndex|colName, pattern, caseFlag 1/0) -> new handle"));

            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "df.SelectLike", likeParams, "Select: Werte aus Spalte, die LIKE matchen. -> ArrayList"));


            routines.Add(new Routine(typeof(int), "df.SetCsvFmt", typeof(int), typeof(string), "Set default CSV format options for handle. opts: \"k=v;k=v\""));

            routines.Add(new Routine(typeof(int), "df.ResetCsvFmt", typeof(int), "Reset CSV format options for handle (back to global defaults)."));
            routines.Add(new Routine((Type)null, "df.Ref", typeof(int), "Reference on the dataframe."));
            routines.Add(new Routine((Type)null, "df.Unref", (Type)null, "Dereference on the dataframe."));

            List<Type> saveCSVExParams = new List<Type>();
            saveCSVExParams.Add(typeof(int));
            saveCSVExParams.Add(typeof(string));
            saveCSVExParams.Add(typeof(string));
            saveCSVExParams.Add(typeof(int));
            saveCSVExParams.Add(typeof(string));
            routines.Add(new Routine(typeof(int), "df.SaveCsvEx", saveCSVExParams, "Save CSV with optional override opts: dfSaveCsvEx(handle,path,sep,header, \"k=v;...\")"));

            var toCsvParams = new List<Type> { typeof(int), typeof(string), typeof(int), typeof(int) };
            routines.Add(new Routine(typeof(string), "df.ToCsv", toCsvParams, "DataFrame -> CSV String. dfToCsv(handle, sep, headerFlag, quoteAllFlag 1/0)"));

            var toCsvExParams = new List<Type>
            {
                typeof(int),      // handle
                typeof(string),   // sep
                typeof(int),      // headerFlag
                typeof(string),   // opts "k=v;..."
                typeof(int)       // quoteAllFlag 1/0  (optional)
            };
            routines.Add(new Routine(typeof(string), "df.ToCsvEx", toCsvExParams, "CSV-String mit override opts: dfToCsvEx(handle, sep, headerFlag, \"k=v;...\", quoteAllFlag 1/0)"));

            exportedRoutines = routines.AsReadOnly();
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters)
        {
            try
            {
                switch (routine)
                {
                    case "df.LoadCsv":
                        return DfLoadCsv(parameters);
                    case "df.ReadCsv":
                        return DfReadCsv(parameters);

                    case "df.SaveCsv":
                        {
                            return DfSaveCsv(parameters);
                        }

                    case "df.ToCsv":
                        return DfToCsv(parameters);

                    case "df.ToCsvEx":
                        return DfToCsvEx(parameters);

                    case "df.Head":
                        return DfHead(parameters);

                    case "df.Info":
                        return DfInfo(parameters);

                    case "df.Columns":
                        return DfColumns(parameters);

                    case "df.Shape":
                        return DfShape(parameters);

                    case "df.Get":
                        return DfGet(parameters);

                    case "df.Set":
                        return DfSet(parameters);

                    case "df.Col":
                        return DfCol(parameters);

                    case "df.Close":
                        return DfClose(parameters);

                    case "df.Row":
                        return DfRow(parameters);

                    case "df.Select":
                        return DfSelect(parameters);

                    case "df.FilterEq":
                        return DfFilterEq(parameters);

                    case "df.Describe":
                        return DfDescribe(parameters);

                    case "df.FilterGt":
                        return DfFilterGt(parameters);

                    case "df.FilterLt":
                        return DfFilterLt(parameters);

                    case "df.Sort":
                        return DfSort(parameters);

                    case "df.ValueCounts":
                        return DfValueCounts(parameters);

                    case "df.FilterGE":
                        return DfFilterGE(parameters);

                    case "df.FilterLE":
                        return DfFilterLE(parameters);

                    case "df.SortBy":
                        return DfSortBy(parameters);

                    case "df.FilterLike":
                        return DfFilterLike(parameters);

                    case "df.SelectLike":
                        return DfSelectLike(parameters);

                    case "df.SetCsvFmt":
                        return DfSetCsvFmt(parameters);

                    case "df.ResetCsvFmt":
                        return DfResetCsvFmt(parameters);

                    case "df.SaveCsvEx":
                        return DfSaveCsvEx(parameters);

                    case "df.Ref":
                        {
                            if (!_frames.TryGetValue((int)parameters[0], out var df))
                                return -1;
                            return df;
                        }
                    case "df.Unref":
                        {
                            DataFrame df = (DataFrame)parameters[0];

                            for(int i = 0; i < _frames.Count(); i++)
                            {
                                if (!_frames.TryGetValue(i, out var _df))
                                    continue;
                                if(object.ReferenceEquals(_df, df))
                                {
                                    return i;
                                }
                            }
                            return null;
                        }
                }

                return null;
            }
            catch (Exception e)
            {
                // Alles in ScriptStackException wrappen, wie in Std
                throw new ScriptStackException(e.Message);
            }
        }

        // -------------------
        // Implementations
        // -------------------

        private int DfLoadCsv(List<object> p)
        {
            string path = (string)p[0];
            string sepStr = (string)p[1];
            int headerFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            if (!File.Exists(path))
                throw new ScriptStackException("CSV nicht gefunden: " + path);

            var df = Microsoft.Data.Analysis.DataFrame.LoadCsv(
                path,
                separator: sep,
                header: header,
                cultureInfo: CultureInfo.InvariantCulture // Wichtig wegen Tausendertrennzeichen
            );

            int handle = _nextHandle++;
            _frames[handle] = df;
            return handle;
        }

        private int DfReadCsv(List<object> p)
        {
            string csvText = (string)p[0];
            string sepStr = (string)p[1];
            int headerFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);

            if (csvText == null) csvText = "";

            // UTF-8 BOM entfernen!
            if (csvText.Length > 0 && csvText[0] == '\uFEFF') csvText = csvText.Substring(1);

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            // String -> UTF8 Bytes -> Stream
            byte[] bytes = Encoding.UTF8.GetBytes(csvText);

            using var ms = new MemoryStream(bytes, writable: false);

            // LoadCsv aus Stream
            var df = Microsoft.Data.Analysis.DataFrame.LoadCsv(
                ms,
                separator: sep,
                header: header,
                cultureInfo: CultureInfo.InvariantCulture
            );

            int handle = _nextHandle++;
            _frames[handle] = df;
            return handle;
        }

        /// <summary>
        /// Schreibt keine Quotes!
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        private int DfSaveCsv1(List<object> p)
        {

            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string path = (string)p[1];
            string sepStr = (string)p[2];
            int headerFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture);

            if (!_frames.TryGetValue(handle, out var df))
                return -1;

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            string fullPath = Path.GetFullPath(path);

            //Console.WriteLine("CWD  = " + Environment.CurrentDirectory);
            //Console.WriteLine("SAVE = " + fullPath);
            //Console.WriteLine($"DF   = rows={df.Rows.Count}, cols={df.Columns.Count}");

            // Stream-Overload benutzen (robuster als "string path" Overload)
            using (var fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Microsoft.Data.Analysis.DataFrame.SaveCsv(
                    df,
                    fs,
                    separator: sep,
                    header: header,
                    encoding: new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
                    cultureInfo: CultureInfo.InvariantCulture
                );

                fs.Flush(true);
            }

            long bytes = new FileInfo(fullPath).Length;

            return bytes > 0 ? 1 : -1;
        }

        private int DfSaveCsv(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string path = (string)p[1];
            string sepStr = (string)p[2];
            int headerFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture);

            if (!_frames.TryGetValue(handle, out var df))
                return -1;

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            var fmt = GetFmtForHandle(handle);      // <-- default pro handle
            SaveCsvManualFormatted(df, Path.GetFullPath(path), sep, header, fmt);
            return 1;
        }

        private string DfToCsv(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string sepStr = (string)p[1];
            int headerFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);
            int quoteAllFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture); // FIX: immer vorhanden

            if (!_frames.TryGetValue(handle, out var df))
                return "";

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            var fmt = GetFmtForHandle(handle).Clone();
            fmt.QuoteAll = (quoteAllFlag != 0); // FIX: keine Optionalität

            var sb = new StringBuilder();

            if (header)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(sep);
                    sb.Append(CsvEscape(df.Columns[c].Name, sep, fmt.QuoteAll));
                }
                sb.AppendLine();
            }

            for (long r = 0; r < df.Rows.Count; r++)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(sep);
                    string cell = FormatCell(df[r, c], fmt);
                    sb.Append(CsvEscape(cell, sep, fmt.QuoteAll));
                }
                if (r < df.Rows.Count - 1) sb.AppendLine();
            }

            return sb.ToString();
        }

        private string DfToCsvEx(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string sepStr = (string)p[1];
            int headerFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);
            string overrideOptsStr = (string)p[3];
            int quoteAllFlag = Convert.ToInt32(p[4], CultureInfo.InvariantCulture); // FIX: 5. Param

            if (!_frames.TryGetValue(handle, out var df))
                return "";

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            var baseFmt = GetFmtForHandle(handle);
            var fmt = ParseCsvFmtOptions(overrideOptsStr, baseOpts: baseFmt);

            // FIX: Flag soll eindeutig sein -> überschreibt opts
            fmt.QuoteAll = (quoteAllFlag != 0);

            var sb = new StringBuilder();

            if (header)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(sep);
                    sb.Append(CsvEscape(df.Columns[c].Name, sep, fmt.QuoteAll));
                }
                sb.AppendLine();
            }

            for (long r = 0; r < df.Rows.Count; r++)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(sep);
                    string cell = FormatCell(df[r, c], fmt);
                    sb.Append(CsvEscape(cell, sep, fmt.QuoteAll));
                }
                if (r < df.Rows.Count - 1) sb.AppendLine();
            }

            return sb.ToString();
        }

        private string DfHead(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            int n = Convert.ToInt32(p[1], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            if (n < 0) n = 0;

            long rows = df.Rows.Count;
            long take = Math.Min(rows, (long)n);

            return RenderTable(df, startRow: 0, rowCount: take, maxCellLen: 32);
        }

        private string DfInfo(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            var df = GetDf(handle);

            var sb = new StringBuilder();
            sb.AppendLine("DataFrame");
            sb.AppendLine($"Rows: {df.Rows.Count}");
            sb.AppendLine($"Cols: {df.Columns.Count}");
            sb.AppendLine("Columns:");

            for (int i = 0; i < df.Columns.Count; i++)
            {
                var c = df.Columns[i];
                sb.AppendLine($"  [{i}] {c.Name} : {c.DataType?.Name ?? "unknown"} (nulls={c.NullCount})");
            }

            return sb.ToString();
        }

        private ScriptStack.Runtime.ArrayList DfColumns(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            var df = GetDf(handle);

            var arr = new ScriptStack.Runtime.ArrayList();
            for (int i = 0; i < df.Columns.Count; i++)
                arr.Add(df.Columns[i].Name);

            return arr;
        }

        private ScriptStack.Runtime.ArrayList DfShape(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            var df = GetDf(handle);

            var arr = new ScriptStack.Runtime.ArrayList();
            arr.Add((int)df.Rows.Count);
            arr.Add(df.Columns.Count);
            return arr;
        }

        private object DfGet(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            long row = Convert.ToInt64(p[1], CultureInfo.InvariantCulture);
            object colSpec = p[2];

            var df = GetDf(handle);

            int colIndex = ResolveColumnIndex(df, colSpec);
            if (row < 0 || row >= df.Rows.Count) throw new ScriptStackException("row out of range");

            object val = df[row, colIndex];
            return NormalizeValue(val);
        }

        private int DfSet(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            long row = Convert.ToInt64(p[1], CultureInfo.InvariantCulture);
            object colSpec = p[2];
            object value = p[3];

            var df = GetDf(handle);

            int colIndex = ResolveColumnIndex(df, colSpec);
            if (row < 0 || row >= df.Rows.Count)
                throw new ScriptStackException("row out of range");

            var col = df.Columns[colIndex];
            var targetType = col.DataType;

            object coerced = CoerceForColumn(value, targetType);

            try
            {
                // besser als df[row, colIndex] = ... (klarer & column-typed)
                col[row] = coerced;
                return 1;
            }
            catch (Exception ex)
            {
                throw new ScriptStackException(
                    $"df.Set failed: col='{col.Name}' type='{targetType?.Name}', " +
                    $"value='{value}' ({value?.GetType().Name ?? "null"}): {ex.Message}"
                );
            }
        }

        private static object CoerceForColumn(object value, Type targetType)
        {
            if (value == null) return null;
            if (targetType == null) return value;

            // Falls Nullable<T> irgendwo auftauchen sollte (zur Sicherheit)
            targetType = Nullable.GetUnderlyingType(targetType) ?? targetType;

            // Strings
            if (targetType == typeof(string))
                return value.ToString();

            // Leere Strings -> null (praktisch bei CSV)
            if (value is string s)
            {
                if (string.IsNullOrWhiteSpace(s)) return null;
            }

            // DateTime
            if (targetType == typeof(DateTime))
            {
                if (value is DateTime dt) return dt;
                if (value is string ds &&
                    DateTime.TryParse(ds, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
                    return parsed;

                // letzter Versuch
                return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
            }

            // Bool
            if (targetType == typeof(bool))
            {
                if (value is bool b) return b;
                if (value is string bs)
                {
                    if (bs == "1") return true;
                    if (bs == "0") return false;
                }
                return Convert.ToBoolean(value, CultureInfo.InvariantCulture);
            }

            // Numerics/Enums/sonstiges (double -> long/int/etc.)
            try
            {
                return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            }
            catch
            {
                // Fallback: über String parsen (hilft bei exotischen Runtime-Number-Typen)
                var vs = value.ToString() ?? "";
                if (targetType == typeof(int)) return int.Parse(vs, CultureInfo.InvariantCulture);
                if (targetType == typeof(long)) return long.Parse(vs, CultureInfo.InvariantCulture);
                if (targetType == typeof(float)) return float.Parse(vs, CultureInfo.InvariantCulture);
                if (targetType == typeof(double)) return double.Parse(vs, CultureInfo.InvariantCulture);
                if (targetType == typeof(decimal)) return decimal.Parse(vs, CultureInfo.InvariantCulture);

                // wenn alles scheitert: original werfen lassen
                throw;
            }
        }

        private ScriptStack.Runtime.ArrayList DfCol(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var col = df.Columns[colIndex];
            var arr = new ScriptStack.Runtime.ArrayList();

            for (long i = 0; i < col.Length; i++)
            {
                arr.Add(NormalizeValue(col[i]));
            }

            return arr;
        }

        private int DfClose1(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            if (_frames.Remove(handle))
                return 1;
            return -1;
        }

        private int DfClose(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            _csvFmtByHandle.Remove(handle);

            if (_frames.Remove(handle))
                return 1;
            return -1;
        }

        private ScriptStack.Runtime.ArrayList DfRow(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            long row = Convert.ToInt64(p[1], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            if (row < 0 || row >= df.Rows.Count) throw new ScriptStackException("row out of range");

            var arr = new ScriptStack.Runtime.ArrayList();
            for (int c = 0; c < df.Columns.Count; c++)
                arr.Add(NormalizeValue(df[row, c]));

            return arr;
        }

        private int DfSelect(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            var colsList = (ScriptStack.Runtime.ArrayList)p[1];

            var df = GetDf(handle);
            var colSpecs = ScriptArrayListToOrderedValues(colsList);

            var cols = new List<DataFrameColumn>();
            foreach (var spec in colSpecs)
            {
                int idx = ResolveColumnIndex(df, spec);
                cols.Add(df.Columns[idx].Clone()); // wichtig: kopieren 
            }

            var newDf = new Microsoft.Data.Analysis.DataFrame(cols); // 
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfFilterEq(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            object value = p[2];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);
            var col = df.Columns[colIndex];

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                filter[r] = ValuesEqualLoose(cell, value, col.DataType);
            }

            var newDf = df.Filter(filter); // 
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private string DfDescribe(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p.Count > 1 ? p[1] : null;

            var df = GetDf(handle);
            var desc = df.Description(); // 

            // wenn keine Spalte angegeben: alle numerischen Spalten
            string colName = (colSpec == null) ? "" : (colSpec.ToString() ?? "");
            if (string.IsNullOrWhiteSpace(colName))
                return RenderTable(desc, 0, desc.Rows.Count, 32);

            int wantedIdx = ResolveColumnIndex(desc, colSpec);

            // optional: "Description"-Spalte (Row-Labels) mitnehmen, falls vorhanden
            int labelIdx = -1;
            for (int i = 0; i < desc.Columns.Count; i++)
            {
                if (string.Equals(desc.Columns[i].Name, "Description", StringComparison.OrdinalIgnoreCase))
                {
                    labelIdx = i;
                    break;
                }
            }

            var cols = new List<DataFrameColumn>();
            if (labelIdx >= 0) cols.Add(desc.Columns[labelIdx].Clone());
            cols.Add(desc.Columns[wantedIdx].Clone());

            var one = new Microsoft.Data.Analysis.DataFrame(cols);
            return RenderTable(one, 0, one.Rows.Count, 32);
        }

        private int DfFilterGt(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            object value = p[2];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                filter[r] = ValueCompareLoose(cell, value) > 0;
            }

            var newDf = df.Filter(filter);
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfFilterLt(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            object value = p[2];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                filter[r] = ValueCompareLoose(cell, value) < 0;
            }

            var newDf = df.Filter(filter);
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfSort(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            int ascFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);
            string colName = df.Columns[colIndex].Name;

            Microsoft.Data.Analysis.DataFrame sorted = (ascFlag != 0)
                ? df.OrderBy(colName)
                : df.OrderByDescending(colName);

            int newHandle = _nextHandle++;
            _frames[newHandle] = sorted;
            return newHandle;
        }

        private int DfValueCounts(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            int ascFlag = Convert.ToInt32(p[2], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);
            var col = df.Columns[colIndex];

            // counts
            var counts = new Dictionary<object, long>(new LooseObjectComparer());

            for (long i = 0; i < col.Length; i++)
            {
                object cell = col[i];
                object key = NormalizeKey(cell);

                if (counts.TryGetValue(key, out var c)) counts[key] = c + 1;
                else counts[key] = 1;
            }

            // materialize
            var values = counts.Keys.Select(k => k?.ToString() ?? "").ToList();
            var cnts = counts.Values.ToList();

            // build DataFrame
            var valueCol = DataFrameColumn.Create("Value", values);
            var countCol = DataFrameColumn.Create<long>("Count", cnts);

            var outDf = new Microsoft.Data.Analysis.DataFrame(new List<DataFrameColumn> { valueCol, countCol });

            // sort by Count
            outDf = (ascFlag != 0)
                ? outDf.OrderBy("Count")
                : outDf.OrderByDescending("Count");

            int newHandle = _nextHandle++;
            _frames[newHandle] = outDf;
            return newHandle;
        }

        private int DfFilterGE(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            object value = p[2];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                filter[r] = ValueCompareLoose(cell, value) >= 0;
            }

            var newDf = df.Filter(filter);
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfFilterLE(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            object value = p[2];

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                filter[r] = ValueCompareLoose(cell, value) <= 0;
            }

            var newDf = df.Filter(filter);
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfSortBy(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            var colsList = (ScriptStack.Runtime.ArrayList)p[1];
            object ascSpec = p[2]; // int 1/0 oder ArrayList

            var df = GetDf(handle);

            // cols in Reihenfolge (0..n) holen
            var colSpecs = ScriptArrayListToOrderedValues(colsList);
            if (colSpecs.Count == 0) throw new ScriptStackException("colsArray is empty");

            var sortCols = new List<int>(colSpecs.Count);
            foreach (var spec in colSpecs)
                sortCols.Add(ResolveColumnIndex(df, spec));

            var ascFlags = AscSpecToFlags(ascSpec, sortCols.Count);

            // Indices 0..rows-1 sortieren
            var idx = new List<long>((int)df.Rows.Count);
            for (long i = 0; i < df.Rows.Count; i++) idx.Add(i);

            idx.Sort((a, b) =>
            {
                for (int k = 0; k < sortCols.Count; k++)
                {
                    int c = sortCols[k];
                    int cmp = ValueCompareLoose(df[a, c], df[b, c]);
                    if (cmp != 0)
                        return ascFlags[k] ? cmp : -cmp;
                }
                return 0;
            });

            // mapIndices bauen (zum Umordnen per Clone(mapIndices))
            var map = new Int64DataFrameColumn("map", idx.Select(i => (long?)i));

            var newCols = new List<DataFrameColumn>(df.Columns.Count);
            for (int c = 0; c < df.Columns.Count; c++)
            {
                // Clone mit mapIndices -> Werte werden umgeordnet :contentReference[oaicite:1]{index=1}
                newCols.Add(df.Columns[c].Clone(map));
            }

            var sortedDf = new Microsoft.Data.Analysis.DataFrame(newCols);
            int newHandle = _nextHandle++;
            _frames[newHandle] = sortedDf;
            return newHandle;
        }

        private int DfFilterLike(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            string pattern = (string)p[2];
            int caseFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var rx = LikeToRegex(pattern, caseInsensitive: (caseFlag != 0));

            var filter = new BooleanDataFrameColumn("filter", df.Rows.Count);

            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                string s = cell?.ToString() ?? "";
                filter[r] = rx.IsMatch(s);
            }

            var newDf = df.Filter(filter);
            int newHandle = _nextHandle++;
            _frames[newHandle] = newDf;
            return newHandle;
        }

        private int DfSetCsvFmt(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string optsStr = (string)p[1];

            // Basis ist global, dann überschreiben mit opts
            var merged = ParseCsvFmtOptions(optsStr, baseOpts: _globalCsvFmt);
            _csvFmtByHandle[handle] = merged;
            return 1;
        }

        private int DfResetCsvFmt(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            _csvFmtByHandle.Remove(handle);
            return 1;
        }

        private int DfSaveCsvEx(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            string path = (string)p[1];
            string sepStr = (string)p[2];
            int headerFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture);
            string overrideOptsStr = (string)p[4];

            if (!_frames.TryGetValue(handle, out var df))
                return -1;

            if (string.IsNullOrEmpty(sepStr)) sepStr = ",";
            char sep = sepStr[0];
            bool header = headerFlag != 0;

            // Basis: handle-default (oder global), dann override drüber
            var baseFmt = GetFmtForHandle(handle);
            var merged = ParseCsvFmtOptions(overrideOptsStr, baseOpts: baseFmt);

            SaveCsvManualFormatted(df, Path.GetFullPath(path), sep, header, merged);
            return 1;
        }

        private ScriptStack.Runtime.ArrayList DfSelectLike(List<object> p)
        {
            int handle = Convert.ToInt32(p[0], CultureInfo.InvariantCulture);
            object colSpec = p[1];
            string pattern = (string)p[2];
            int caseFlag = Convert.ToInt32(p[3], CultureInfo.InvariantCulture);

            var df = GetDf(handle);
            int colIndex = ResolveColumnIndex(df, colSpec);

            var rx = LikeToRegex(pattern, caseInsensitive: (caseFlag != 0));

            var arr = new ScriptStack.Runtime.ArrayList();
            for (long r = 0; r < df.Rows.Count; r++)
            {
                object cell = df[r, colIndex];
                string s = cell?.ToString() ?? "";
                if (rx.IsMatch(s))
                    arr.Add(NormalizeValue(cell));
            }

            return arr;
        }

        // -------------------
        // Helpers
        // -------------------

        private Microsoft.Data.Analysis.DataFrame GetDf(int handle)
        {
            if (!_frames.TryGetValue(handle, out var df))
                throw new ScriptStackException("Ungültiger DataFrame-Handle: " + handle);
            return df;
        }

        private static int ResolveColumnIndex(Microsoft.Data.Analysis.DataFrame df, object colSpec)
        {
            if (colSpec is int ci) return ci;

            // manche Script-Werte kommen als double/float/decimal
            if (colSpec is double d) return (int)d;
            if (colSpec is float f) return (int)f;
            if (colSpec is decimal m) return (int)m;

            string name = colSpec?.ToString() ?? "";
            if (string.IsNullOrEmpty(name))
                throw new ScriptStackException("col is empty");

            // Name -> Index
            for (int i = 0; i < df.Columns.Count; i++)
                if (string.Equals(df.Columns[i].Name, name, StringComparison.Ordinal))
                    return i;

            throw new ScriptStackException("Unbekannte Spalte: " + name);
        }

        private static object NormalizeValue(object v)
        {
            if (v == null) return null;

            // keep simple primitives
            if (v is string || v is int || v is long || v is float || v is double || v is bool)
                return v;

            if (v is DateTime dt)
                return dt.ToString("o", CultureInfo.InvariantCulture);

            // fallback
            return v.ToString();
        }

        private static string RenderTable(Microsoft.Data.Analysis.DataFrame df, long startRow, long rowCount, int maxCellLen)
        {
            var sb = new StringBuilder();

            // header
            for (int c = 0; c < df.Columns.Count; c++)
            {
                if (c > 0) sb.Append(" | ");
                sb.Append(Trunc(df.Columns[c].Name, maxCellLen));
            }
            sb.AppendLine();

            sb.AppendLine(new string('-', Math.Min(200, df.Columns.Count * (maxCellLen + 3))));

            // rows
            for (long r = startRow; r < startRow + rowCount; r++)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sb.Append(" | ");
                    var v = NormalizeValue(df[r, c]);
                    sb.Append(Trunc(v?.ToString() ?? "", maxCellLen));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string Trunc(string s, int n)
        {
            if (s == null) return "";
            s = s.Replace("\r", " ").Replace("\n", " ");
            if (s.Length <= n) return s;
            if (n <= 1) return s.Substring(0, n);
            return s.Substring(0, n - 1) + "…";
        }

        private static List<object> ScriptArrayListToOrderedValues1(ScriptStack.Runtime.ArrayList list)
        {
            // ArrayList iteriert als KeyValuePair<object, object>
            // Wir versuchen nach numerischen Keys zu sortieren (0,1,2...) sonst Einfüge-Reihenfolge.
            var tmp = new List<(int idx, object val)>();
            int fallback = 0;

            foreach (KeyValuePair<object, object> kv in list)
            {
                int idx;
                if (kv.Key is int i) idx = i;
                else if (!int.TryParse(kv.Key?.ToString(), out idx)) idx = fallback;

                tmp.Add((idx, kv.Value));
                fallback++;
            }

            tmp.Sort((a, b) => a.idx.CompareTo(b.idx));
            return tmp.Select(x => x.val).ToList();
        }

        private static List<object> ScriptArrayListToOrderedValues(ScriptStack.Runtime.ArrayList list)
        {
            // ScriptStack.ArrayList iteriert als KeyValuePair<object, object>
            // Wir sortieren nach numerischen Keys (0,1,2,...) wenn möglich,
            // sonst nehmen wir Einfüge-Reihenfolge (fallback).
            var tmp = new List<(int idx, object val)>();
            int fallback = 0;

            foreach (KeyValuePair<object, object> kv in list)
            {
                int idx;

                if (kv.Key is int i)
                    idx = i;
                else if (kv.Key is long l)
                    idx = (int)l;
                else if (!int.TryParse(kv.Key?.ToString(), out idx))
                    idx = fallback;

                tmp.Add((idx, kv.Value));
                fallback++;
            }

            tmp.Sort((a, b) => a.idx.CompareTo(b.idx));
            return tmp.Select(x => x.val).ToList();
        }

        private static bool ValuesEqualLoose(object cell, object value, Type columnType)
        {
            // null-handling
            if (cell == null && value == null) return true;
            if (cell == null || value == null) return false;

            // string vs string
            if (cell is string cs && value is string vs)
                return string.Equals(cs, vs, StringComparison.Ordinal);

            // wenn value ein string ist, versuche es in den Spaltentyp zu konvertieren
            object v2 = value;
            if (value is string s && columnType != null && columnType != typeof(string))
            {
                try { v2 = Convert.ChangeType(s, columnType, CultureInfo.InvariantCulture); }
                catch { /* fallback unten */ }
            }

            // numerischer Vergleich (robust gegen int/float/double Mix)
            if (IsNumeric(cell) && IsNumeric(v2))
            {
                double a = Convert.ToDouble(cell, CultureInfo.InvariantCulture);
                double b = Convert.ToDouble(v2, CultureInfo.InvariantCulture);
                return a.Equals(b);
            }

            // bool
            if (cell is bool cb)
            {
                bool vb;
                if (v2 is bool b) vb = b;
                else
                {
                    try { vb = Convert.ToBoolean(v2, CultureInfo.InvariantCulture); }
                    catch { return false; }
                }
                return cb == vb;
            }

            return cell.Equals(v2);
        }

        private static bool IsNumeric(object o)
        {
            return o is byte || o is sbyte ||
                   o is short || o is ushort ||
                   o is int || o is uint ||
                   o is long || o is ulong ||
                   o is float || o is double || o is decimal;
        }

        private static int ValueCompareLoose(object a, object b)
        {
            // nulls: null ist "kleiner"
            if (a == null && b == null) return 0;
            if (a == null) return -1;
            if (b == null) return 1;

            // DateTime: wenn eins DateTime ist, versuch beide als DateTime
            if (a is DateTime || b is DateTime)
            {
                if (TryToDateTime(a, out var da) && TryToDateTime(b, out var db))
                    return da.CompareTo(db);

                // fallback: string compare
                return String.CompareOrdinal(a.ToString(), b.ToString());
            }

            // numeric: robust gegen int/float/double mix
            if (IsNumeric(a) && IsNumeric(b))
            {
                double da = Convert.ToDouble(a, CultureInfo.InvariantCulture);
                double db = Convert.ToDouble(b, CultureInfo.InvariantCulture);
                return da.CompareTo(db);
            }

            // bool
            if (a is bool ba && b is bool bb) return ba.CompareTo(bb);

            // string / fallback: ordinal
            return String.CompareOrdinal(a.ToString(), b.ToString());
        }

        private static bool TryToDateTime(object o, out DateTime dt)
        {
            if (o is DateTime d) { dt = d; return true; }
            if (o is string s)
                return DateTime.TryParse(s, CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out dt);

            try
            {
                dt = Convert.ToDateTime(o, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                dt = default;
                return false;
            }
        }

        private static object NormalizeKey(object v)
        {
            if (v == null) return null;

            // keep basic primitives stable
            if (v is string || v is bool) return v;

            // numeric to double to unify int/float mixes (optional, aber praktisch)
            if (IsNumeric(v)) return Convert.ToDouble(v, CultureInfo.InvariantCulture);

            if (v is DateTime dt) return dt.ToString("o", CultureInfo.InvariantCulture);

            return v.ToString();
        }

        private sealed class LooseObjectComparer : IEqualityComparer<object>
        {
            public new bool Equals(object x, object y)
            {
                if (x == null && y == null) return true;
                if (x == null || y == null) return false;

                // numeric normalized to double
                if (x is double dx && y is double dy) return dx.Equals(dy);

                return x.Equals(y);
            }

            public int GetHashCode(object obj)
            {
                if (obj == null) return 0;
                return obj.GetHashCode();
            }
        }

        private static List<bool> AscSpecToFlags(object ascSpec, int count)
        {
            // default: alles asc
            if (ascSpec == null) return Enumerable.Repeat(true, count).ToList();

            // int/double/float/etc: 1/0 für alle
            if (ascSpec is int ai) return Enumerable.Repeat(ai != 0, count).ToList();
            if (ascSpec is double ad) return Enumerable.Repeat(ad != 0.0, count).ToList();
            if (ascSpec is float af) return Enumerable.Repeat(af != 0.0f, count).ToList();
            if (ascSpec is decimal am) return Enumerable.Repeat(am != 0m, count).ToList();

            // ArrayList: pro Spalte 1/0
            if (ascSpec is ScriptStack.Runtime.ArrayList arr)
            {
                var vals = ScriptArrayListToOrderedValues(arr);
                var flags = new List<bool>(count);
                for (int i = 0; i < count; i++)
                {
                    if (i < vals.Count)
                    {
                        var v = vals[i];
                        int iv = 1;
                        if (v is int x) iv = x;
                        else if (v is double xd) iv = (int)xd;
                        else if (v is float xf) iv = (int)xf;
                        else if (v is decimal xm) iv = (int)xm;
                        else if (v is string s && int.TryParse(s, out var parsed)) iv = parsed;

                        flags.Add(iv != 0);
                    }
                    else
                    {
                        flags.Add(true);
                    }
                }
                return flags;
            }

            // Fallback
            return Enumerable.Repeat(true, count).ToList();
        }

        private static Regex LikeToRegex(string likePattern, bool caseInsensitive)
        {
            if (likePattern == null) likePattern = "";

            // SQL-LIKE: % = .*   _ = .
            // Escaping: \% \_ \\  (Backslash)
            var sb = new StringBuilder();
            sb.Append("^");

            bool escaping = false;
            foreach (char ch in likePattern)
            {
                if (escaping)
                {
                    sb.Append(Regex.Escape(ch.ToString()));
                    escaping = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escaping = true;
                    continue;
                }

                if (ch == '%')
                {
                    sb.Append(".*");
                }
                else if (ch == '_')
                {
                    sb.Append(".");
                }
                else
                {
                    sb.Append(Regex.Escape(ch.ToString()));
                }
            }

            if (escaping)
            {
                // Pattern endet mit "\" -> treat as literal "\"
                sb.Append(Regex.Escape("\\"));
            }

            sb.Append("$");

            var opts = RegexOptions.CultureInvariant;
            if (caseInsensitive) opts |= RegexOptions.IgnoreCase;

            return new Regex(sb.ToString(), opts);
        }

        private sealed class CsvFmtOptions
        {
            public string FloatFormat = "G9";    // float32: gute “schöne” Ausgabe ohne Müll
            public string DoubleFormat = "G17";  // double: round-trip
            public string DecimalFormat = "G29"; // decimal: maximal sinnvoll
            public bool QuoteAll = false;
            public CultureInfo Culture = CultureInfo.InvariantCulture;

            public CsvFmtOptions Clone() => new CsvFmtOptions
            {
                FloatFormat = this.FloatFormat,
                DoubleFormat = this.DoubleFormat,
                DecimalFormat = this.DecimalFormat,
                QuoteAll = this.QuoteAll,
                Culture = this.Culture
            };
        }

        private CsvFmtOptions GetFmtForHandle(int handle)
        {
            if (_csvFmtByHandle.TryGetValue(handle, out var fmt))
                return fmt;
            return _globalCsvFmt;
        }

        // optsString überschreibt nur Keys, die drin stehen.
        // baseOpts liefert Defaults für alles andere.
        private static CsvFmtOptions ParseCsvFmtOptions(string optsString, CsvFmtOptions baseOpts)
        {
            var o = (baseOpts ?? new CsvFmtOptions()).Clone();

            if (string.IsNullOrWhiteSpace(optsString))
                return o;

            var parts = optsString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                var p = raw.Trim();
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;

                var key = p.Substring(0, eq).Trim().ToLowerInvariant();
                var val = p.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "float": o.FloatFormat = val; break;
                    case "double": o.DoubleFormat = val; break;
                    case "dec":
                    case "decimal": o.DecimalFormat = val; break;

                    case "quoteall":
                    case "quote":
                        o.QuoteAll = (val == "1" || val.Equals("true", StringComparison.OrdinalIgnoreCase));
                        break;

                    case "culture":
                        if (string.IsNullOrWhiteSpace(val) || val.Equals("invariant", StringComparison.OrdinalIgnoreCase))
                            o.Culture = CultureInfo.InvariantCulture;
                        else
                            o.Culture = CultureInfo.GetCultureInfo(val);
                        break;
                }
            }

            return o;
        }

        private static void SaveCsvManualFormatted(DataFrame df, string fullPath, char sep, bool header, CsvFmtOptions fmt)
        {
            using var sw = new StreamWriter(fullPath, false, new UTF8Encoding(false));

            if (header)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sw.Write(sep);
                    sw.Write(CsvEscape(df.Columns[c].Name, sep, fmt.QuoteAll));
                }
                sw.WriteLine();
            }

            for (long r = 0; r < df.Rows.Count; r++)
            {
                for (int c = 0; c < df.Columns.Count; c++)
                {
                    if (c > 0) sw.Write(sep);

                    string cell = FormatCell(df[r, c], fmt);
                    sw.Write(CsvEscape(cell, sep, fmt.QuoteAll));
                }
                sw.WriteLine();
            }

            sw.Flush();
        }

        private static string FormatCell(object v, CsvFmtOptions fmt)
        {
            if (v == null) return "";

            // entscheidend: float/double so formatieren, dass 4.19999981 -> 4.2 wird
            if (v is float ff) return ff.ToString(fmt.FloatFormat, fmt.Culture);
            if (v is double dd) return dd.ToString(fmt.DoubleFormat, fmt.Culture);
            if (v is decimal dm) return dm.ToString(fmt.DecimalFormat, fmt.Culture);

            if (v is IFormattable f) return f.ToString(null, fmt.Culture);
            return v.ToString() ?? "";
        }

        private static string CsvEscape(string s, char sep, bool quoteAll)
        {
            s ??= "";
            bool mustQuote = quoteAll || s.Contains(sep) || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
            if (!mustQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

    }

}
