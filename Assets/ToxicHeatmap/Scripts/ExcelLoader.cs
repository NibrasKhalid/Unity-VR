// using System;
// using System.Collections.Generic;
// using System.Data;
// using System.IO;
// using ExcelDataReader;
// using UnityEngine;

// public static class ExcelLoader
// {
//     // Expects each sheet's first row to be a header: x,y,z,value (in that column order)
//     public static List<ToxicSample> Load(string path)
//     {
//         var samples = new List<ToxicSample>();
//         if (!File.Exists(path))
//         {
//             Debug.LogError($"Excel file not found at {path}");
//             return samples;
//         }

//         // Needed once per run for legacy encodings; harmless if called repeatedly.
//         // System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

//         using (var stream = File.Open(path, FileMode.Open, FileAccess.Read))
//         using (var reader = ExcelReaderFactory.CreateReader(stream))
//         {
//             var result = reader.AsDataSet(new ExcelDataSetConfiguration
//             {
//                 ConfigureDataTable = _ => new ExcelDataTableConfiguration { UseHeaderRow = true }
//             });

//             var table = result.Tables[0];
//             foreach (DataRow row in table.Rows)
//             {
//                 if (row.ItemArray.Length < 4) continue;
//                 try
//                 {
//                     float x = Convert.ToSingle(row[0]);
//                     float y = Convert.ToSingle(row[1]);
//                     float z = Convert.ToSingle(row[2]);
//                     float v = Convert.ToSingle(row[3]);
//                     samples.Add(new ToxicSample(new Vector3(x, y, z), v));
//                 }
//                 catch
//                 {
//                     continue; // skip malformed/blank rows
//                 }
//             }
//         }

//         Debug.Log($"Loaded {samples.Count} samples from {Path.GetFileName(path)}");
//         return samples;
//     }

//     // Loads and merges every .xlsx file in a folder — this is your "multiple files" entry point.
//     public static List<ToxicSample> LoadFolder(string folderPath)
//     {
//         var all = new List<ToxicSample>();
//         if (!Directory.Exists(folderPath))
//         {
//             Debug.LogError($"Folder not found at {folderPath}");
//             return all;
//         }

//         var files = Directory.GetFiles(folderPath, "*.xlsx");
//         foreach (var file in files)
//             all.AddRange(Load(file));

//         Debug.Log($"Loaded {all.Count} total samples from {files.Length} Excel files in {folderPath}");
//         return all;
//     }
// }

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using UnityEngine;
using ExcelDataReader;

// Reads .xlsx files with a header row containing X, Y, Z, Toxicity columns
// (column order doesn't matter, headers are matched by name, case-insensitive)
// and returns a flat list of ToxicSample.
//
// Requires the ExcelDataReader package — see install notes at the bottom.
public static class ExcelLoader
{
    public static List<ToxicSample> Load(string filePath)
    {
        var samples = new List<ToxicSample>();

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Debug.LogError($"ExcelLoader: file not found at '{filePath}'");
            return samples;
        }

        // Note: no need to register CodePagesEncodingProvider here — that's only
        // required for legacy .xls binary files, and pulls in an extra assembly
        // (System.Text.Encoding.CodePages) that Unity doesn't reference by default.
        // Since this loader only reads .xlsx, we can skip it.

        try
        {
            using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read))
            using (var reader = ExcelReaderFactory.CreateReader(stream))
            {
                var dataSet = reader.AsDataSet(new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                });

                samples.AddRange(ParseTable(dataSet.Tables[0], filePath));
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"ExcelLoader: failed to read '{filePath}' — {e.Message}");
        }

        return samples;
    }

    public static List<ToxicSample> LoadFolder(string folderPath)
    {
        var samples = new List<ToxicSample>();

        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            Debug.LogError($"ExcelLoader: folder not found at '{folderPath}'");
            return samples;
        }

        var files = Directory.GetFiles(folderPath, "*.xlsx")
            .Where(f => !Path.GetFileName(f).StartsWith("~$")) // skip Excel's temp lock files
            .ToArray();

        if (files.Length == 0)
        {
            Debug.LogWarning($"ExcelLoader: no .xlsx files found in '{folderPath}'");
            return samples;
        }

        foreach (var file in files)
            samples.AddRange(Load(file));

        return samples;
    }

    private static List<ToxicSample> ParseTable(DataTable table, string sourceName)
    {
        var samples = new List<ToxicSample>();

        int xCol = FindColumn(table, "X");
        int yCol = FindColumn(table, "Y");
        int zCol = FindColumn(table, "Z");
        int valCol = FindColumn(table, "Toxicity");

        if (xCol < 0 || yCol < 0 || zCol < 0 || valCol < 0)
        {
            var found = string.Join(", ", table.Columns.Cast<DataColumn>().Select(c => c.ColumnName));
            Debug.LogError($"ExcelLoader: '{sourceName}' is missing an expected column (need X, Y, Z, Toxicity). Found headers: {found}");
            return samples;
        }

        for (int row = 0; row < table.Rows.Count; row++)
        {
            var dr = table.Rows[row];

            if (!TryParseFloat(dr[xCol], out float x) ||
                !TryParseFloat(dr[yCol], out float y) ||
                !TryParseFloat(dr[zCol], out float z) ||
                !TryParseFloat(dr[valCol], out float val))
            {
                Debug.LogWarning($"ExcelLoader: skipping row {row + 2} in '{sourceName}' — non-numeric value.");
                continue;
            }

            samples.Add(new ToxicSample
            {
                position = new Vector3(x, y, z),
                value = val
            });
        }

        Debug.Log($"ExcelLoader: parsed {samples.Count} sample(s) from '{Path.GetFileName(sourceName)}'.");
        return samples;
    }

    private static int FindColumn(DataTable table, string headerName)
    {
        for (int i = 0; i < table.Columns.Count; i++)
        {
            if (string.Equals(table.Columns[i].ColumnName.Trim(), headerName, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return -1;
    }

    private static bool TryParseFloat(object cellValue, out float result)
    {
        result = 0f;
        if (cellValue == null || cellValue == DBNull.Value) return false;

        switch (cellValue)
        {
            case double d: result = (float)d; return true;
            case float f: result = f; return true;
            case int i: result = i; return true;
        }

        return float.TryParse(
            cellValue.ToString(),
            System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
    }
}

// ---------------------------------------------------------------------------
// SETUP (one-time):
// ExcelDataReader isn't built into Unity, so you need to add it:
//
// Option A (recommended) — NuGetForUnity:
//   1. Install the "NuGetForUnity" package (via Package Manager > Add package from git URL:
//      https://github.com/GlitchEnzo/NuGetForUnity.git?path=/src/NuGetForUnity)
//   2. NuGet > Manage NuGet Packages, search "ExcelDataReader", install it.
//
// Option B (manual):
//   1. Download ExcelDataReader + ExcelDataReader.DataSet .dll files (netstandard2.0 build)
//      from https://www.nuget.org/packages/ExcelDataReader
//   2. Drop both .dll files into an Assets/Plugins folder in your project.
//
// After either option, this script should compile without errors.
// ---------------------------------------------------------------------------