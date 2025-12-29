using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using ScriptStack.Runtime;
using System.Collections.ObjectModel;
using System.Text;

namespace Excel
{

    public class Excel : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        public Excel()
        {
            if (exportedRoutines != null) return;

            var routines = new List<Routine>();

            routines.Add(new Routine((Type)null, "xlsx_new"));
            routines.Add(new Routine((Type)null, "xlsx_load", (Type)null, "Loads an xlsx file from the specified path."));
            routines.Add(new Routine((Type)null, "xlsx_close", (Type)null, "close an open workbook."));

            routines.Add(new Routine((Type)null, "xlsx_add_ws", (Type)null, (Type)null, "add a worksheet to a workbook."));
            routines.Add(new Routine((Type)null, "xlsx_get_ws", (Type)null, (Type)null, "get a worksheet from a workbook."));
            routines.Add(new Routine((Type)null, "xlsx_remove_ws", (Type)null, (Type)null, "remove a worksheet from a workbook."));
            routines.Add(new Routine((Type)null, "xlsx_list_ws", (Type)null, "list all the worksheets from a workbook."));

            routines.Add(new Routine((Type)null, "xlsx_rows", (Type)null));
            routines.Add(new Routine((Type)null, "xlsx_cols", (Type)null));

            List<Type> cellParams = new List<Type>();
            cellParams.Add((Type)null);
            cellParams.Add(typeof(int));
            cellParams.Add(typeof(int));
            cellParams.Add((Type)null);
            routines.Add(new Routine((Type)null, "xlsx_set", cellParams));
            routines.Add(new Routine((Type)null, "xlsx_set_formula", cellParams));
            routines.Add(new Routine((Type)null, "xlsx_get", (Type)null, typeof(int), typeof(int)));
            routines.Add(new Routine((Type)null, "xlsx_get_formula", (Type)null, typeof(int), typeof(int)));

            List<Type> csvParams = new List<Type>();
            csvParams.Add((Type)null);        // sheet (IXLWorksheet)
            csvParams.Add(typeof(string));    // separator, z.B. ";", ",", "\t"
            csvParams.Add(typeof(bool));      // quoteFields (always quote)
            routines.Add(new Routine((Type)null, "xlsx_tocsv", csvParams, "params: sheet, separator, quoteFields"));

            exportedRoutines = routines.AsReadOnly();

        }

        public ReadOnlyCollection<Routine> Routines => exportedRoutines;

        public object Invoke(string routine, List<object> parameters) {

            if(routine == "xlsx_new")
            {
                return new XLWorkbook();
            }
            if(routine == "xlsx_load")
            {
                string path = (string)parameters[0];
                XLWorkbook wb = new XLWorkbook(path);
                return wb;
            }
            if(routine == "xlsx_close")
            {
                XLWorkbook wb = parameters[0] as XLWorkbook;
                wb.Dispose();
                return null;
            }

            if (routine == "xlsx_add_ws")
            {
                XLWorkbook wb = parameters[0] as XLWorkbook;            
                var sheet = wb.Worksheets.Add((string)parameters[1]);
                return sheet;
            }
            if (routine == "xlsx_get_ws")
            {
                XLWorkbook wb = (XLWorkbook)parameters[0];
                try
                {
                    var sheet = wb.Worksheet((string)parameters[1]);
                    return sheet;
                }
                catch
                {
                    return null;
                }
            }
            if(routine == "xlsx_remove_ws")
            {
                XLWorkbook wb = parameters[0] as XLWorkbook;
                wb.Worksheets.Delete((string)parameters[1]);
                return null;
            }
            if(routine == "xlsx_list_ws")
            {
                XLWorkbook wb = parameters[0] as XLWorkbook;
                List<string> sheetNames = new List<string>();
                foreach(var sheet in wb.Worksheets)
                {
                    sheetNames.Add(sheet.Name);
                }
                return sheetNames;
            }


            if (routine == "xlsx_get")
            {
                var sheet = parameters[0] as IXLWorksheet;
                int row = (int)parameters[1];
                int col = (int)parameters[2];
                return sheet.Cell(row, col).Value;
            }
            if (routine == "xlsx_set")
            {
                var sheet = parameters[0] as IXLWorksheet;
                int row = (int)parameters[1];
                int col = (int)parameters[2];

                switch(parameters[3].GetType().ToString())
                {

                    case "System.Char":
                        {
                            sheet.Cell(row, col).Value = (char)parameters[3];
                            break;
                        }
                    case "System.Int32":
                        {
                            sheet.Cell(row, col).Value = (int)parameters[3];
                            break;
                        }
                    case "System.Single":
                        {
                            sheet.Cell(row, col).Value = (float)parameters[3];
                            break;
                        }
                    case "System.Double":
                        {
                            sheet.Cell(row, col).Value = (double)parameters[3];
                            break;
                        }
                    case "System.Decimal":
                        {
                            sheet.Cell(row, col).Value = (decimal)parameters[3];
                            break;
                        }
                    case "System.String":
                    default:
                        {
                            sheet.Cell(row, col).Value = (string)parameters[3];
                            break;
                        }

                }

                return null;
            }
            if(routine == "xlsx_set_formula")
            {
                var sheet = parameters[0] as IXLWorksheet;
                int row = (int)parameters[1];
                int col = (int)parameters[2];
                string formula = (string)parameters[3];
                sheet.Cell(row, col).FormulaA1 = formula;
                return null;
            }
            if(routine == "xlsx_get_formula")
            {
                var sheet = parameters[0] as IXLWorksheet;
                int row = (int)parameters[1];
                int col = (int)parameters[2];
                return sheet.Cell(row, col).FormulaA1;
            }
            if (routine == "xlsx_rows")
            {
                var sheet = parameters[0] as IXLWorksheet;
                return sheet.RowsUsed().Count();
            }
            if(routine == "xlsx_cols")
            {
                var sheet = parameters[0] as IXLWorksheet;
                return sheet.ColumnsUsed().Count();
            }

            if (routine == "xlsx_tocsv")
            {
                var sheet = parameters[0] as IXLWorksheet;
                if (sheet == null) return "";

                string sepStr = (parameters.Count > 1 && parameters[1] != null) ? (string)parameters[1] : ";";
                char sep = string.IsNullOrEmpty(sepStr) ? ';' : sepStr[0];

                bool alwaysQuote = (parameters.Count > 2 && parameters[2] != null) && (bool)parameters[2];

                var range = sheet.RangeUsed();
                if (range == null) return ""; // leeres Sheet

                int firstRow = range.RangeAddress.FirstAddress.RowNumber;
                int lastRow = range.RangeAddress.LastAddress.RowNumber;
                int firstCol = range.RangeAddress.FirstAddress.ColumnNumber;
                int lastCol = range.RangeAddress.LastAddress.ColumnNumber;

                var sb = new StringBuilder();

                for (int r = firstRow; r <= lastRow; r++)
                {
                    for (int c = firstCol; c <= lastCol; c++)
                    {
                        if (c > firstCol) sb.Append(sep);

                        var cell = sheet.Cell(r, c);

                        // GetFormattedString() entspricht eher dem, was Excel anzeigt (Zahlen/Datum-Format)
                        string text = cell.IsEmpty() ? "" : cell.GetFormattedString();

                        sb.Append(EscapeCsv(text, sep, alwaysQuote));
                    }

                    if (r < lastRow) sb.AppendLine();
                }

                return sb.ToString();
            }

            return null;

        }

        private static string EscapeCsv(string value, char sep, bool alwaysQuote)
        {
            value ??= "";

            bool mustQuote =
                alwaysQuote ||
                value.IndexOf(sep) >= 0 ||
                value.IndexOf('"') >= 0 ||
                value.IndexOf('\r') >= 0 ||
                value.IndexOf('\n') >= 0;

            if (!mustQuote) return value;

            // Quotes verdoppeln
            value = value.Replace("\"", "\"\"");
            return $"\"{value}\"";
        }

    }

}
