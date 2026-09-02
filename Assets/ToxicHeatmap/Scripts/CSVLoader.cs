using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Export your Excel file as CSV first (File > Save As > CSV in Excel).
// Expected header row: x,y,z,value
public static class CSVLoader
{
    public static List<ToxicSample> Load(string path)
    {
        var samples = new List<ToxicSample>();
        if (!File.Exists(path))
        {
            Debug.LogError($"CSV not found at {path}");
            return samples;
        }

        var lines = File.ReadAllLines(path);
        for (int i = 1; i < lines.Length; i++) // skip header row
        {
            var line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            var parts = line.Split(',');
            if (parts.Length < 4) continue;

            float x = float.Parse(parts[0], CultureInfo.InvariantCulture);
            float y = float.Parse(parts[1], CultureInfo.InvariantCulture);
            float z = float.Parse(parts[2], CultureInfo.InvariantCulture);
            float v = float.Parse(parts[3], CultureInfo.InvariantCulture);

            samples.Add(new ToxicSample(new Vector3(x, y, z), v));
        }

        Debug.Log($"Loaded {samples.Count} toxic air samples from {path}");
        return samples;
    }
}