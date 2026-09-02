#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Tools > Toxic Heatmap > Bake Volume
public class HeatmapVolumeBaker : EditorWindow
{
    private bool useFolder = true;
    private string dataPath = "";       // single .xlsx file, if useFolder is false
    private string folderPath = "";     // folder of .xlsx files, if useFolder is true
    private int resolution = 48;
    private float idwPower = 2f;
    private float searchRadius = 1.5f;
    private Vector3 boundsMin = new Vector3(-5, 0, -5);
    private Vector3 boundsMax = new Vector3(5, 3, 5);
    private string outputAssetPath = "Assets/ToxicHeatmap/HeatmapVolume.asset";

    [MenuItem("Tools/Toxic Heatmap/Bake Volume")]
    public static void ShowWindow() => GetWindow<HeatmapVolumeBaker>("Toxic Heatmap Baker");

    void OnGUI()
    {
        GUILayout.Label("Toxic Air Heatmap Baker", EditorStyles.boldLabel);

        useFolder = EditorGUILayout.Toggle("Load Multiple Files (Folder)", useFolder);

        if (useFolder)
        {
            folderPath = EditorGUILayout.TextField("Folder Path", folderPath);
            if (GUILayout.Button("Browse Folder..."))
            {
                var p = EditorUtility.OpenFolderPanel("Select folder of .xlsx files", "", "");
                if (!string.IsNullOrEmpty(p)) folderPath = p;
            }
        }
        else
        {
            dataPath = EditorGUILayout.TextField("Excel File Path", dataPath);
            if (GUILayout.Button("Browse File..."))
            {
                var p = EditorUtility.OpenFilePanel("Select .xlsx", "", "xlsx");
                if (!string.IsNullOrEmpty(p)) dataPath = p;
            }
        }

        resolution = EditorGUILayout.IntSlider("Grid Resolution", resolution, 8, 128);
        idwPower = EditorGUILayout.FloatField("IDW Power", idwPower);
        searchRadius = EditorGUILayout.FloatField("Search Radius (m)", searchRadius);
        boundsMin = EditorGUILayout.Vector3Field("Room Bounds Min", boundsMin);
        boundsMax = EditorGUILayout.Vector3Field("Room Bounds Max", boundsMax);
        outputAssetPath = EditorGUILayout.TextField("Output Asset Path", outputAssetPath);

        EditorGUILayout.HelpBox(
            "Bounds should match the real-world room dimensions your samples were taken in. " +
            "All files in the folder are merged into one volume.",
            MessageType.Info);

        if (GUILayout.Button("Bake Texture3D")) Bake();
    }

    void Bake()
    {
        var samples = useFolder ? ExcelLoader.LoadFolder(folderPath) : ExcelLoader.Load(dataPath);
        if (samples.Count == 0) { Debug.LogError("No samples loaded."); return; }

        float cellSize = Mathf.Max(0.25f, searchRadius);
        var hash = new Dictionary<Vector3Int, List<int>>();
        Vector3Int KeyOf(Vector3 p) => new Vector3Int(
            Mathf.FloorToInt(p.x / cellSize),
            Mathf.FloorToInt(p.y / cellSize),
            Mathf.FloorToInt(p.z / cellSize));

        for (int i = 0; i < samples.Count; i++)
        {
            var key = KeyOf(samples[i].position);
            if (!hash.TryGetValue(key, out var list)) { list = new List<int>(); hash[key] = list; }
            list.Add(i);
        }

        float maxValue = 0f;
        foreach (var s in samples) maxValue = Mathf.Max(maxValue, s.value);

        int res = resolution;
        var colors = new Color[res * res * res];
        Vector3 size = boundsMax - boundsMin;

        for (int zi = 0; zi < res; zi++)
        for (int yi = 0; yi < res; yi++)
        for (int xi = 0; xi < res; xi++)
        {
            Vector3 worldPos = boundsMin + new Vector3(
                (xi + 0.5f) / res * size.x,
                (yi + 0.5f) / res * size.y,
                (zi + 0.5f) / res * size.z);

            float weightedSum = 0f, weightTotal = 0f;
            var cellKey = KeyOf(worldPos);

            for (int dz = -1; dz <= 1; dz++)
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                var k = cellKey + new Vector3Int(dx, dy, dz);
                if (!hash.TryGetValue(k, out var list)) continue;

                foreach (var idx in list)
                {
                    float dist = Vector3.Distance(worldPos, samples[idx].position);
                    if (dist > searchRadius) continue;
                    float w = 1f / Mathf.Pow(Mathf.Max(dist, 0.01f), idwPower);
                    weightedSum += w * samples[idx].value;
                    weightTotal += w;
                }
            }

            float value = weightTotal > 0f ? weightedSum / weightTotal : 0f;
            float normalized = maxValue > 0f ? value / maxValue : 0f;
            colors[xi + yi * res + zi * res * res] = new Color(normalized, 0, 0, normalized);
        }

        var tex = new Texture3D(res, res, res, TextureFormat.RGBA32, false);
        tex.SetPixels(colors);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(outputAssetPath));
        AssetDatabase.CreateAsset(tex, outputAssetPath);
        AssetDatabase.SaveAssets();
        Debug.Log($"Baked heatmap volume ({res}^3) to {outputAssetPath}. Max raw value = {maxValue}");
    }
}
#endif