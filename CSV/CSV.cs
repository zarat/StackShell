using ScriptStack.Compiler;
using ScriptStack.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;

namespace ScriptStack
{
    public class CSV : Model
    {

        private static ReadOnlyCollection<Routine> exportedRoutines;

        public CSV()
        {
            if (exportedRoutines != null) return;

            List<Routine> routines = new List<Routine>();

            routines.Add(new Routine((Type)null, "csv_parse", (Type)null, "Erstelle eine CSV-Tabelle aus einem String."));
            routines.Add(new Routine((Type)null, "csv_get", (Type)null, (Type)null, "Lese einen CSV-Wert/Row per CSV-Path."));
            routines.Add(new Routine((Type)null, "csv_set", (Type)null, (Type)null, (Type)null, "Setze einen CSV-Wert per CSV-Path."));

            routines.Add(new Routine(typeof(int), "csv_row_count", (Type)null, "Anzahl Zeilen."));
            routines.Add(new Routine(typeof(int), "csv_col_count", (Type)null, "Anzahl Spalten (max)."));
            routines.Add(new Routine(typeof(ScriptStack.Runtime.ArrayList), "csv_headers", (Type)null, "Gibt Header als ArrayList zurück (leer wenn keine Header)."));
            routines.Add(new Routine(typeof(bool), "csv_has", (Type)null, (Type)null, "Prüft ob CSV-Path existiert (und Wert != null)."));

            routines.Add(new Routine((Type)null, "csv_iter", (Type)null, "Iterator über Zeilen (CsvTable oder raw csv string)."));
            routines.Add(new Routine(typeof(bool), "csv_next", (Type)null, "Iterator: nächste Zeile. true wenn vorhanden."));
            routines.Add(new Routine(typeof(int), "csv_index", (Type)null, "Iterator: aktueller Zeilenindex."));
            routines.Add(new Routine((Type)null, "csv_value", (Type)null, "Iterator: aktuelle Zeile (ArrayList)."));

            routines.Add(new Routine((Type)null, "csv_table", (Type)null, "Konvertiert ArrayList -> CsvTable (Rows oder Object-Style)."));
            routines.Add(new Routine(typeof(string), "csv_string", (Type)null, "Konvertiert CsvTable/ArrayList -> CSV-String."));

            exportedRoutines = routines.AsReadOnly();
        }

        public object Invoke(string routine, List<object> parameters)
        {
            if (routine == "csv_parse")
            {
                // csv_parse(csvString, [hasHeader=true], [delimiter=","], [culture="invariant"])
                string csv = (string)parameters[0];

                bool hasHeader = true;
                string delimiter = ";";
                string cultureName = "invariant";

                var culture = ParseCulture(cultureName);

                return CsvTable.Parse(csv, hasHeader, delimiter, culture);
            }

            if (routine == "csv_get")
            {
                var table = CoerceToTable(parameters[0]);
                string path = (string)parameters[1];

                var p = CsvPath.Parse(path);

                if (p.Row < 0 || p.Row >= table.Rows.Count) return null;

                // Row-only
                if (p.IsRowOnly)
                    return table.RowToArrayList(p.Row);

                // Cell
                if (p.ColIndex.HasValue)
                {
                    int ci = p.ColIndex.Value;
                    if (ci < 0) return null;
                    if (ci >= table.GetMaxColCount()) return null;
                    return table.GetCell(p.Row, ci);
                }

                if (p.ColName != null)
                {
                    int ci = table.GetOrFindHeaderIndex(p.ColName);
                    if (ci < 0) return null;
                    return table.GetCell(p.Row, ci);
                }

                return null;
            }

            if (routine == "csv_set")
            {
                var table = CoerceToTable(parameters[0]);
                string path = (string)parameters[1];
                object? value = parameters.Count > 2 ? parameters[2] : null;

                var p = CsvPath.Parse(path);

                // Ensure row exists
                table.EnsureRow(p.Row);

                // Row-only: value muss eine Row sein (ArrayList) oder JSON-artiges Objekt (ArrayList als map)
                if (p.IsRowOnly)
                {
                    table.SetRowFromValue(p.Row, value);
                    return table.ToCsvString();
                }

                string? s = value?.ToString();

                if (p.ColIndex.HasValue)
                {
                    int ci = p.ColIndex.Value;
                    table.EnsureCol(ci);
                    table.SetCell(p.Row, ci, s);
                    return table.ToCsvString();
                }

                if (p.ColName != null)
                {
                    int ci = table.EnsureHeader(p.ColName);
                    table.SetCell(p.Row, ci, s);
                    return table.ToCsvString();
                }

                return table.ToCsvString();
            }

            if (routine == "csv_row_count")
            {
                var table = CoerceToTable(parameters[0]);
                return table.Rows.Count;
            }

            if (routine == "csv_col_count")
            {
                var table = CoerceToTable(parameters[0]);
                return table.GetMaxColCount();
            }

            if (routine == "csv_headers")
            {
                var table = CoerceToTable(parameters[0]);
                var a = new ScriptStack.Runtime.ArrayList();
                foreach (var h in table.Headers) a.Add(h);
                return a;
            }

            if (routine == "csv_has")
            {
                var table = CoerceToTable(parameters[0]);
                string path = (string)parameters[1];

                var p = CsvPath.Parse(path);

                if (p.Row < 0 || p.Row >= table.Rows.Count) return false;

                if (p.IsRowOnly) return true;

                if (p.ColIndex.HasValue)
                {
                    int ci = p.ColIndex.Value;
                    if (ci < 0 || ci >= table.GetMaxColCount()) return false;
                    return table.GetCell(p.Row, ci) != null;
                }

                if (p.ColName != null)
                {
                    int ci = table.GetOrFindHeaderIndex(p.ColName);
                    if (ci < 0) return false;
                    return table.GetCell(p.Row, ci) != null;
                }

                return false;
            }

            if (routine == "csv_iter")
            {
                var table = CoerceToTable(parameters[0]);
                return new CsvIterator(table);
            }

            if (routine == "csv_next")
            {
                var it = (CsvIterator)parameters[0];
                return it.MoveNext();
            }

            if (routine == "csv_index")
            {
                var it = (CsvIterator)parameters[0];
                return it.CurrentIndex;
            }

            if (routine == "csv_value")
            {
                var it = (CsvIterator)parameters[0];
                return it.CurrentRow;
            }

            if (routine == "csv_table")
            {
                // csv_table(ArrayList rowsOrObject, [ArrayList headers], [delimiter], [culture], [hasHeader])
                object? p0 = parameters[0];

                ScriptStack.Runtime.ArrayList? headers = null;
                if (parameters.Count > 1 && parameters[1] is ScriptStack.Runtime.ArrayList h) headers = h;

                string delimiter = parameters.Count > 2 && parameters[2] is string d ? d : ",";
                string cultureName = parameters.Count > 3 && parameters[3] is string c ? c : "invariant";
                bool hasHeader = parameters.Count > 4 && parameters[4] is bool hb ? hb : (headers != null && headers.Count > 0);

                var culture = ParseCulture(cultureName);

                return CsvTable.FromArrayList(p0, headers, hasHeader, delimiter, culture);
            }

            if (routine == "csv_string")
            {
                // csv_string(CsvTable OR ArrayList, [headers], [delimiter], [culture], [hasHeader])
                object? p0 = parameters[0];

                if (p0 is CsvTable t0)
                    return t0.ToCsvString();

                // Sonst: über csv_table bauen
                ScriptStack.Runtime.ArrayList? headers = null;
                if (parameters.Count > 1 && parameters[1] is ScriptStack.Runtime.ArrayList h) headers = h;

                string delimiter = parameters.Count > 2 && parameters[2] is string d ? d : ",";
                string cultureName = parameters.Count > 3 && parameters[3] is string c ? c : "invariant";
                bool hasHeader = parameters.Count > 4 && parameters[4] is bool hb ? hb : (headers != null && headers.Count > 0);

                var culture = ParseCulture(cultureName);

                var table = CsvTable.FromArrayList(p0, headers, hasHeader, delimiter, culture);
                return table.ToCsvString();
            }

            return null;
        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        // ---------------------------
        // Internals
        // ---------------------------

        private static CultureInfo ParseCulture(string name)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Equals("invariant", StringComparison.OrdinalIgnoreCase))
                return CultureInfo.InvariantCulture;

            try { return CultureInfo.GetCultureInfo(name); }
            catch { return CultureInfo.InvariantCulture; }
        }

        private static CsvTable CoerceToTable(object? p0)
        {
            if (p0 is CsvTable t) return t;

            if (p0 is string s)
            {
                // Default: header=true, delimiter=","
                return CsvTable.Parse(s, hasHeader: true, delimiter: ",", culture: CultureInfo.InvariantCulture);
            }

            if (p0 is ScriptStack.Runtime.ArrayList a)
            {
                return CsvTable.FromArrayList(a, headers: null, hasHeader: false, delimiter: ",", culture: CultureInfo.InvariantCulture);
            }

            throw new InvalidOperationException("Erwarte CsvTable, CSV-String oder ArrayList.");
        }

        // ---------------------------
        // CSV-Path
        // ---------------------------

        private sealed record CsvPath(int Row, string? ColName, int? ColIndex, bool IsRowOnly)
        {
            // Unterstützt:
            //  "3" / "[3]"                            -> row only
            //  "3.Name" / "[3].Name"                  -> header cell
            //  "3[2]" / "[3][2]"                      -> index cell
            public static CsvPath Parse(string path)
            {
                if (path == null) throw new ArgumentNullException(nameof(path));
                path = path.Trim();
                if (path.Length == 0) throw new FormatException("CSV-Path ist leer.");

                // Row-only: "3" oder "[3]"
                var mRowOnly = Regex.Match(path, @"^\s*(?:\[\s*)?(?<r>\d+)(?:\s*\])?\s*$");
                if (mRowOnly.Success)
                {
                    int r = int.Parse(mRowOnly.Groups["r"].Value, CultureInfo.InvariantCulture);
                    return new CsvPath(r, null, null, true);
                }

                // "[3].Name" oder "3.Name"
                var mHeader = Regex.Match(path, @"^\s*(?:\[\s*)?(?<r>\d+)(?:\s*\])?\s*\.\s*(?<c>.+?)\s*$");
                if (mHeader.Success)
                {
                    int r = int.Parse(mHeader.Groups["r"].Value, CultureInfo.InvariantCulture);
                    string c = mHeader.Groups["c"].Value.Trim();
                    return new CsvPath(r, c, null, false);
                }

                // "[3][2]" oder "3[2]"
                var mIndex = Regex.Match(path, @"^\s*(?:\[\s*)?(?<r>\d+)(?:\s*\])?\s*\[\s*(?<c>\d+)\s*\]\s*$");
                if (mIndex.Success)
                {
                    int r = int.Parse(mIndex.Groups["r"].Value, CultureInfo.InvariantCulture);
                    int c = int.Parse(mIndex.Groups["c"].Value, CultureInfo.InvariantCulture);
                    return new CsvPath(r, null, c, false);
                }

                throw new FormatException($"Ungültiger CSV-Path: '{path}'");
            }
        }

        // ---------------------------
        // CsvTable
        // ---------------------------

        public sealed class CsvTable
        {
            public List<string> Headers { get; } = new List<string>();
            public List<List<string?>> Rows { get; } = new List<List<string?>>();

            public bool HasHeader { get; private set; }
            public string Delimiter { get; private set; } = ",";
            public CultureInfo Culture { get; private set; } = CultureInfo.InvariantCulture;

            private CsvTable() { }

            public static CsvTable Parse(string csv, bool hasHeader, string delimiter, CultureInfo culture)
            {
                var t = new CsvTable
                {
                    HasHeader = hasHeader,
                    Delimiter = delimiter ?? ",",
                    Culture = culture ?? CultureInfo.InvariantCulture
                };

                var cfg = new CsvConfiguration(t.Culture)
                {
                    HasHeaderRecord = hasHeader,
                    Delimiter = t.Delimiter,
                    BadDataFound = null,
                    MissingFieldFound = null,
                    HeaderValidated = null
                };

                using var sr = new StringReader(csv ?? "");
                using var reader = new CsvReader(sr, cfg);

                if (hasHeader)
                {
                    if (reader.Read())
                    {
                        reader.ReadHeader();
                        var headers = reader.HeaderRecord ?? Array.Empty<string>();
                        t.Headers.AddRange(headers);
                    }
                }

                while (reader.Read())
                {
                    var row = new List<string?>();
                    // Wenn Header: per FieldCount auslesen; wenn ohne Header: reader.Parser.Count
                    int count = Math.Max(t.Headers.Count, reader.Parser.Count);
                    for (int i = 0; i < count; i++)
                    {
                        string? val = null;
                        try { val = reader.GetField(i); }
                        catch { val = null; }
                        row.Add(val);
                    }
                    t.Rows.Add(row);
                }

                return t;
            }

            public static CsvTable FromArrayList(object? value, ScriptStack.Runtime.ArrayList? headers, bool hasHeader, string delimiter, CultureInfo culture)
            {
                var t = new CsvTable
                {
                    HasHeader = hasHeader,
                    Delimiter = delimiter ?? ",",
                    Culture = culture ?? CultureInfo.InvariantCulture
                };

                // Headers optional
                if (headers != null)
                {
                    for (int i = 0; i < headers.Count; i++)
                    {
                        var hv = headers[i];              // Value an Index i
                        if (hv != null)
                            t.Headers.Add(hv.ToString() ?? "");
                    }
                }

                // value kann sein:
                // - ArrayList mit dense numeric keys => array of rows
                // - ArrayList map/object => single row (obj)
                if (value is ScriptStack.Runtime.ArrayList arr)
                {
                    if (IsDenseArray(arr))
                    {
                        // rows
                        for (int i = 0; i < arr.Count; i++)
                        {
                            var rowVal = arr[i];
                            t.Rows.Add(ConvertRowValueToList(rowVal, t));
                        }
                    }
                    else
                    {
                        // object => one row
                        t.Rows.Add(ConvertRowValueToList(arr, t));
                    }
                }
                else
                {
                    // single primitive => 1x1
                    t.Rows.Add(new List<string?> { value?.ToString() });
                }

                // Wenn keine Headers und hasHeader=true -> generiere Col0..ColN-1
                if (t.HasHeader && t.Headers.Count == 0)
                {
                    int cols = t.GetMaxColCount();
                    for (int i = 0; i < cols; i++) t.Headers.Add($"Col{i}");
                }

                t.PadAllRowsToMax();
                return t;
            }

            private static List<string?> ConvertRowValueToList(object? rowVal, CsvTable t)
            {
                // rowVal kann sein:
                // - ArrayList dense => list by index
                // - ArrayList map => fill by headers (and create if missing)
                // - primitive => single cell
                if (rowVal is ScriptStack.Runtime.ArrayList ra)
                {
                    if (IsDenseArray(ra))
                    {
                        var row = new List<string?>();
                        for (int i = 0; i < ra.Count; i++)
                            row.Add(ra[i]?.ToString());
                        return row;
                    }
                    else
                    {
                        // map/object
                        // ensure headers include all keys if hasHeader
                        if (t.HasHeader)
                        {
                            foreach (var k in ra.Keys)
                            {
                                var key = k?.ToString() ?? "";
                                if (key.Length == 0) continue;
                                if (!t.Headers.Contains(key)) t.Headers.Add(key);
                            }

                            // now build row in header order
                            var row = new List<string?>(Enumerable.Repeat<string?>(null, t.Headers.Count));
                            for (int i = 0; i < t.Headers.Count; i++)
                            {
                                string h = t.Headers[i];
                                if (ra.ContainsKey(h))
                                    row[i] = ra[h]?.ToString();
                            }
                            return row;
                        }
                        else
                        {
                            // no headers: we can still try numeric keys -> sparse index
                            int max = -1;
                            foreach (var k in ra.Keys)
                            {
                                if (k is int ki) max = Math.Max(max, ki);
                                else if (int.TryParse(k?.ToString(), out int p)) max = Math.Max(max, p);
                            }

                            if (max >= 0)
                            {
                                var row = new List<string?>(Enumerable.Repeat<string?>(null, max + 1));
                                foreach (var k in ra.Keys)
                                {
                                    int idx = -1;
                                    if (k is int ki) idx = ki;
                                    else if (int.TryParse(k?.ToString(), out int p)) idx = p;
                                    if (idx >= 0 && idx < row.Count)
                                        row[idx] = ra[k]?.ToString();
                                }
                                return row;
                            }

                            // fallback: single cell JSON-like string
                            return new List<string?> { ra.ToString() };
                        }
                    }
                }

                return new List<string?> { rowVal?.ToString() };
            }

            private static bool IsDenseArray(ScriptStack.Runtime.ArrayList arr)
            {
                int n = arr.Count;
                for (int i = 0; i < n; i++)
                    if (!arr.ContainsKey(i)) return false;
                return true;
            }

            public int GetMaxColCount()
            {
                int max = Headers.Count;
                foreach (var r in Rows)
                    if (r.Count > max) max = r.Count;
                return max;
            }

            public int GetOrFindHeaderIndex(string header)
            {
                if (Headers.Count == 0) return -1;
                for (int i = 0; i < Headers.Count; i++)
                    if (string.Equals(Headers[i], header, StringComparison.Ordinal))
                        return i;
                return -1;
            }

            public int EnsureHeader(string header)
            {
                if (!HasHeader)
                    HasHeader = true;

                int idx = GetOrFindHeaderIndex(header);
                if (idx >= 0) return idx;

                Headers.Add(header);
                // Pad all rows to new col count
                foreach (var r in Rows)
                    while (r.Count < Headers.Count) r.Add(null);

                return Headers.Count - 1;
            }

            public void EnsureRow(int rowIndex)
            {
                if (rowIndex < 0) throw new ArgumentOutOfRangeException(nameof(rowIndex));
                int cols = GetMaxColCount();
                if (HasHeader) cols = Math.Max(cols, Headers.Count);

                while (Rows.Count <= rowIndex)
                {
                    var r = new List<string?>();
                    for (int i = 0; i < cols; i++) r.Add(null);
                    Rows.Add(r);
                }

                PadAllRowsToMax();
            }

            public void EnsureCol(int colIndex)
            {
                if (colIndex < 0) throw new ArgumentOutOfRangeException(nameof(colIndex));
                int need = colIndex + 1;

                if (HasHeader && Headers.Count < need)
                {
                    // generische Header erzeugen
                    while (Headers.Count < need)
                        Headers.Add($"Col{Headers.Count}");
                }

                foreach (var r in Rows)
                    while (r.Count < need) r.Add(null);
            }

            public string? GetCell(int rowIndex, int colIndex)
            {
                if (rowIndex < 0 || rowIndex >= Rows.Count) return null;
                var row = Rows[rowIndex];
                if (colIndex < 0 || colIndex >= row.Count) return null;
                return row[colIndex];
            }

            public void SetCell(int rowIndex, int colIndex, string? value)
            {
                EnsureRow(rowIndex);
                EnsureCol(colIndex);
                Rows[rowIndex][colIndex] = value;
            }

            public ScriptStack.Runtime.ArrayList RowToArrayList(int rowIndex)
            {
                var a = new ScriptStack.Runtime.ArrayList();
                if (rowIndex < 0 || rowIndex >= Rows.Count) return a;

                var row = Rows[rowIndex];

                if (HasHeader && Headers.Count > 0)
                {
                    // map: header -> value
                    for (int i = 0; i < Headers.Count; i++)
                    {
                        var key = Headers[i];
                        string? val = i < row.Count ? row[i] : null;
                        a[key] = val;
                    }
                }
                else
                {
                    // dense numeric
                    for (int i = 0; i < row.Count; i++)
                        a[i] = row[i];
                }

                return a;
            }

            public void SetRowFromValue(int rowIndex, object? value)
            {
                EnsureRow(rowIndex);

                if (value is ScriptStack.Runtime.ArrayList al)
                {
                    if (HasHeader && Headers.Count > 0 && !IsDenseArray(al))
                    {
                        // map into existing headers (create missing headers)
                        foreach (var k in al.Keys)
                        {
                            string key = k?.ToString() ?? "";
                            if (key.Length == 0) continue;
                            int ci = EnsureHeader(key);
                            SetCell(rowIndex, ci, al[k]?.ToString());
                        }
                        return;
                    }

                    // dense row
                    if (IsDenseArray(al))
                    {
                        int cols = al.Count;
                        EnsureCol(cols - 1);
                        for (int i = 0; i < cols; i++)
                            Rows[rowIndex][i] = al[i]?.ToString();

                        // Rest optional null lassen
                        return;
                    }
                }

                // primitive: erste Spalte setzen
                SetCell(rowIndex, 0, value?.ToString());
            }

            private void PadAllRowsToMax()
            {
                int max = GetMaxColCount();
                if (HasHeader) max = Math.Max(max, Headers.Count);
                foreach (var r in Rows)
                    while (r.Count < max) r.Add(null);
            }

            public string ToCsvString()
            {
                var cfg = new CsvConfiguration(Culture)
                {
                    HasHeaderRecord = HasHeader && Headers.Count > 0,
                    Delimiter = Delimiter,
                    BadDataFound = null
                };

                using var sw = new StringWriter(CultureInfo.InvariantCulture);
                using var writer = new CsvWriter(sw, cfg);

                int cols = GetMaxColCount();
                if (HasHeader && Headers.Count > 0)
                {
                    foreach (var h in Headers)
                        writer.WriteField(h);
                    writer.NextRecord();
                    cols = Math.Max(cols, Headers.Count);
                }

                foreach (var row in Rows)
                {
                    for (int i = 0; i < cols; i++)
                    {
                        string? v = i < row.Count ? row[i] : null;
                        writer.WriteField(v);
                    }
                    writer.NextRecord();
                }

                writer.Flush();
                return sw.ToString();
            }
        }

        // ---------------------------
        // Iterator
        // ---------------------------

        private sealed class CsvIterator
        {
            private readonly CsvTable _table;
            private int _idx = -1;

            public CsvIterator(CsvTable table)
            {
                _table = table ?? throw new ArgumentNullException(nameof(table));
            }

            public bool MoveNext()
            {
                _idx++;
                return _idx >= 0 && _idx < _table.Rows.Count;
            }

            public int CurrentIndex => (_idx >= 0 && _idx < _table.Rows.Count) ? _idx : -1;

            public ScriptStack.Runtime.ArrayList CurrentRow =>
                (_idx >= 0 && _idx < _table.Rows.Count) ? _table.RowToArrayList(_idx) : new ScriptStack.Runtime.ArrayList();
        }
    
    }

}
