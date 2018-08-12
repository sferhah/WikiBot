using NPOI.HSSF.Util;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace KabWikiBot
{
    public class XlsxSerializer
    {
        public static void Serialize<T>(T[] items, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            var exists = File.Exists(path);
            IWorkbook workbook = null;

            using (FileStream stream = new FileStream(path, FileMode.OpenOrCreate))
            {   
                workbook = Serialize(items, stream, exists);

                if(!exists)
                {
                    workbook.Write(stream);
                }
            }

            if(exists)
            {
                using (FileStream stream = new FileStream(path, FileMode.Create))
                {
                    workbook.Write(stream);                    
                }   
            }
        }
     
        public static IWorkbook Serialize<T>(T[] items, Stream stream, bool exists)
        {
            var workbook = exists ? new XSSFWorkbook(stream) : new XSSFWorkbook();
            Type t = typeof(T);
            
            ISheet sheet = workbook.GetSheet(t.Name) ?? workbook.CreateSheet(t.Name);            

            //Create columns
            if(!exists)
            {
                sheet.DefaultColumnWidth = 30;
                IRow excelRow = sheet.CreateRow(0);
                ICellStyle headerCellStyle = excelRow.Sheet.Workbook.CreateCellStyle();
                headerCellStyle.FillForegroundColor = HSSFColor.Grey25Percent.Index;
                headerCellStyle.BorderBottom = BorderStyle.Medium;
                headerCellStyle.FillPattern = FillPattern.SolidForeground;

                foreach (PropertyInfo prop in t.GetProperties())
                {
                    ICell cell = excelRow.CreateCell(excelRow.Cells.Count, CellType.String);
                    cell.SetCellValue(prop.Name);

                    ICellStyle style = cell.Row.Sheet.Workbook.CreateCellStyle();
                    style.FillBackgroundColor = HSSFColor.PaleBlue.Index;
                    cell.CellStyle = style;
                    cell.CellStyle = headerCellStyle;
                }
            }

            var getters = t.GetProperties().Select(x => x.GetGetMethod()).ToList();


            var date_style = workbook.CreateCellStyle();
            date_style.BorderBottom = BorderStyle.Thin;
            date_style.BorderLeft = BorderStyle.Thin;
            date_style.BorderTop = BorderStyle.Thin;
            date_style.BorderRight = BorderStyle.Thin;
            date_style.DataFormat = workbook.CreateDataFormat().GetFormat("yyyy-MM-dd HH:mm:ss");

            foreach (Object item in items)
            {
                IRow row = sheet.CreateRow(sheet.LastRowNum + 1);

                foreach (var val in getters.Select(x => x.Invoke(item, null)))
                {
                    var cell = row.CreateCell(row.Cells.Count);

                    if (val == null)
                    {
                        continue;
                    }

                    var valType = val.GetType();

                    if(valType == typeof(bool))
                    {
                        cell.SetCellValue((bool)val);
                    }
                    else if(valType == typeof(string))
                    {
                        cell.SetCellValue((string)val);
                    }
                    else if (valType == typeof(DateTime))
                    {   
                        cell.SetCellValue((DateTime)val);
                        cell.CellStyle = date_style;
                    }
                    else if (valType == typeof(double)
                        || valType == typeof(int)
                        || valType == typeof(float)
                        || valType == typeof(decimal))
                    {
                        cell.SetCellValue(Convert.ToDouble(val));
                    }
                }
            }

            return workbook;
        }

        
        public static T[] Deserialize<T>(string path)
        {
            if(!File.Exists(path))
            {
                return new T[0];
            }

            using (FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                return Deserialize<T>(file);
            }
        }
     

        public static T[] Deserialize<T>(Stream stream)
        {
            var workbook = new XSSFWorkbook(stream);
            Type t = typeof(T);

            var workbookSheet = workbook.GetSheet(t.Name);

            var sheet = GetDataTableFromExcel(workbookSheet);

            return ParseSheet<T>(sheet);
        }

        private static T[] ParseSheet<T>(DataTable sheet)
        {
            List<PropertyInfo> properties = new List<PropertyInfo>();

            foreach (var column in sheet.Columns)
            {
                PropertyInfo p =  typeof(T).GetProperty(column, BindingFlags.Instance | BindingFlags.IgnoreCase | BindingFlags.Public);

                if (p != null)
                {
                    properties.Add(p);
                }
            }

            List<T> ls = new List<T>();

            foreach (var row in sheet.Rows)
            {
                T instance = (T)Activator.CreateInstance<T>();

                foreach (PropertyInfo p in properties)
                {
                    row.TryGetValue(p.Name, out object o);
                    Type propertyType = p.PropertyType;
                    propertyType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
                    object safeValue = o == null ? GetDefault(propertyType) : Convert.ChangeType(o, propertyType);
                    p.SetValue(instance, safeValue);
                }

                ls.Add(instance);
            }

            return ls.ToArray();
        }

        public static object GetDefault(Type type)
        {
            if (type.IsValueType)
            {
                return Activator.CreateInstance(type);
            }
            return null;
        }      

        class DataTable
        {
            public List<string> Columns = new List<string>();
            public List<Dictionary<string, object>> Rows = new List<Dictionary<string, object>>();
        }

        private static DataTable GetDataTableFromExcel(ISheet sh)
        {
            DataTable dataTable = new DataTable();

            for (int j = 0; j < sh.GetRow(0).Cells.Count; j++)
            {
                var colName = sh.GetRow(0).GetCell(j).StringCellValue;
                dataTable.Columns.Add(colName);
            }

            int i = 0;

            while (sh.GetRow(i + 1) != null
                && sh.GetRow(i + 1).GetCell(0) != null)
            {
                dataTable.Rows.Add(new Dictionary<string, object>());

                // write row value
                for (int j = 0; j < dataTable.Columns.Count; j++)
                {
                    var cell = sh.GetRow(i + 1).GetCell(j);

                    if (cell != null)
                    {
                        object cell_value = null;

                        switch (cell.CellType)
                        {
                            case CellType.Boolean:
                                cell_value = cell.BooleanCellValue;
                                break;
                            case CellType.Numeric:
                                cell_value = DateUtil.IsCellDateFormatted(cell) ? (object)cell.DateCellValue : (object)cell.NumericCellValue;
                                break;
                            case CellType.String:
                                cell_value = cell.StringCellValue;
                                break;
                        }

                        dataTable.Rows[i][dataTable.Columns[j]] = cell_value;
                    }
                }

                i++;
            }

            return dataTable;
        }       
    }
}
