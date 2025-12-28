using ScriptStack.Runtime;
using System.Collections.ObjectModel;
using ClosedXML.Excel;

namespace SpreadSheets
{

    public class SpreadSheets : Model
    {
        private static ReadOnlyCollection<Routine> exportedRoutines;

        public SpreadSheets()
        {
            if (exportedRoutines != null) return;

            var routines = new List<Routine>();

            routines.Add(new Routine((Type)null, "xlsx_new"));
            routines.Add(new Routine((Type)null, "xlsx_load", (Type)null, "Loads an xlsx file from the specified path."));
            routines.Add(new Routine((Type)null, "xlsx_close", (Type)null, "close an open workbook."));

            routines.Add(new Routine((Type)null, "xlsx_add_ws", (Type)null, (Type)null, "add a worksheet to a workbook."));
            routines.Add(new Routine((Type)null, "xlsx_get_ws", (Type)null, (Type)null, "get a worksheet from a workbook."));
            routines.Add(new Routine((Type)null, "xlsx_remove_ws", (Type)null, (Type)null, "remove a worksheet from a workbook."));

            routines.Add(new Routine((Type)null, "xlsx_rows", (Type)null));
            routines.Add(new Routine((Type)null, "xlsx_cols", (Type)null));

            List<Type> cellParams = new List<Type>();
            cellParams.Add((Type)null);
            cellParams.Add(typeof(int));
            cellParams.Add(typeof(int));
            cellParams.Add((Type)null);
            routines.Add(new Routine((Type)null, "xlsx_set", cellParams));
            routines.Add(new Routine((Type)null, "xlsx_get", (Type)null, typeof(int), typeof(int)));
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
                string value = (string)parameters[3];
                sheet.Cell(row, col).Value = value;
                return null;
            }
            if(routine == "xlsx_rows")
            {
                var sheet = parameters[0] as IXLWorksheet;
                return sheet.RowsUsed().Count();
            }
            if(routine == "xlsx_cols")
            {
                var sheet = parameters[0] as IXLWorksheet;
                return sheet.ColumnsUsed().Count();
            }

            return null;

        }
    }

}
