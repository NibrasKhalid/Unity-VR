using UnityEngine;

/// <summary>
/// Spawns oxygen particles that travel a full path from a room Entrance
/// point to an Exit point, at a steady interval. Every particle gets the
/// same travelTime/pathBend/diffusion settings, so with
/// identicalPathForAllParticles enabled they all trace the exact same
/// curved route through the room - just offset in time, like a continuous
/// stream of oxygen entering at one point and leaving at another.
///
/// No prefab is required: if you don't assign one, the spawner builds a
/// small blue sphere with a TrailRenderer at runtime. Assign your own
/// prefab (with an OxygenParticleMotion component on it) if you want a
/// custom look instead.
/// </summary>
public class VentOxygenSpawner : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Optional: one or more CSV files, each with x,y,z columns (e.g. exported from a Room-Coordinates spreadsheet), each defining one full path in order. If any are assigned, this overrides Entrance/Waypoints/Exit below entirely - every new particle picks one of these paths to follow. Header rows are auto-skipped.")]
    public TextAsset[] pathCoordinatesFiles;

    [Tooltip("If true, cycles through Path Coordinates Files in order (1st particle uses file 0, 2nd uses file 1, etc). If false, each particle picks a random file from the list.")]
    public bool cyclePathFilesInOrder = true;

    [Tooltip("Where particles spawn from (e.g. the room's entrance/vent). Ignored if any Path Coordinates Files are assigned.")]
    public Transform entrance;

    [Tooltip("Optional: points the path passes through, in order, between entrance and exit - drag in existing scene objects (e.g. your A/B/C markers) to route around obstacles or add turns. Leave empty for a simple one-bend curve. Ignored if any Path Coordinates Files are assigned.")]
    public Transform[] waypoints;

    [Tooltip("Where particles are heading toward (e.g. the room's exit/outlet vent). Ignored if any Path Coordinates Files are assigned.")]
    public Transform exit;

    [Tooltip("Seconds to travel the full route.")]
    public float travelTime = 6f;

    [Tooltip("Only used when Waypoints is empty: how far the path bows off the straight entrance-exit line, in meters.")]
    public float pathBend = 0.6f;

    [Header("Spawning")]
    [Tooltip("Seconds between each new particle. Keep this a decent fraction of Travel Time so particles are spaced apart and read as individually moving, rather than chaining together into what looks like one continuous line.")]
    public float spawnInterval = 1.5f;

    [Tooltip("If true, every particle uses the exact same noise seed, so they all trace an identical route (just time-shifted). If false, each gets a random seed for slightly varied sweeps.")]
    public bool identicalPathForAllParticles = true;

    [Tooltip("Optional custom prefab. Must have an OxygenParticleMotion component. Leave empty to auto-build a simple blue sphere.")]
    public GameObject particlePrefab;

    [Header("Appearance (applied to every spawned particle)")]
    [Tooltip("Visual size in world units - independent of this spawner's or any parent's transform scale. NOTE: your Room-Scan object has a Transform Scale of 500, so 'world units' here are ~500x bigger than real meters - this default (75) targets roughly a 15cm ball at that scale.")]
    public float particleScale = 75f;
    public Color particleColor = Color.blue;

    [Header("Diffusion / turbulence")]
    public float diffusionStrength = 0.04f;
    public float diffusionFrequency = 1.5f;
    public float maxLifeTime = 20f;

    [Header("Performance")]
    [Tooltip("Hard cap on how many particles can exist at once. Spawning pauses above this cap so the scene can't fill up with particles indefinitely.")]
    public int maxActiveParticles = 40;

    private float _timer;
    private const int FixedSeed = 1337;
    private static Mesh _sharedSphereMesh;

    private System.Collections.Generic.Dictionary<TextAsset, Vector3[]> _fileRouteCache
        = new System.Collections.Generic.Dictionary<TextAsset, Vector3[]>();
    private int _nextFileIndex;

    private bool HasFileRoutes => pathCoordinatesFiles != null && pathCoordinatesFiles.Length > 0;

    private void Update()
    {
        bool hasTransformRoute = entrance != null && exit != null;
        if (!HasFileRoutes && !hasTransformRoute) return;

        _timer += Time.deltaTime;
        if (_timer >= spawnInterval)
        {
            _timer -= spawnInterval;
            if (OxygenParticleMotion.ActiveCount < maxActiveParticles)
            {
                SpawnOne();
            }
        }
    }

    private void SpawnOne()
    {
        Vector3[] route = BuildRoute();
        if (route == null || route.Length < 2) return;

        GameObject go = particlePrefab != null
            ? Instantiate(particlePrefab, route[0], Quaternion.identity)
            : BuildDefaultParticle();

        var motion = go.GetComponent<OxygenParticleMotion>();
        if (motion == null) motion = go.AddComponent<OxygenParticleMotion>();

        motion.travelTime = travelTime;
        motion.pathBend = pathBend;
        motion.diffusionStrength = diffusionStrength;
        motion.diffusionFrequency = diffusionFrequency;
        motion.maxLifeTime = maxLifeTime;
        motion.particleScale = particleScale;
        motion.particleColor = particleColor;

        int seed = identicalPathForAllParticles ? FixedSeed : Random.Range(0, 100000);
        motion.Initialize(route, seed);
    }

    private Vector3[] BuildRoute()
    {
        if (HasFileRoutes)
        {
            TextAsset chosen = PickPathFile();
            if (chosen == null) return null;

            if (!_fileRouteCache.TryGetValue(chosen, out Vector3[] route))
            {
                route = ParseCsvRoute(chosen);
                _fileRouteCache[chosen] = route;
            }
            return route;
        }

        if (entrance == null || exit == null) return null;

        int validWaypoints = 0;
        if (waypoints != null)
        {
            foreach (var w in waypoints) if (w != null) validWaypoints++;
        }

        var built = new Vector3[2 + validWaypoints];
        built[0] = entrance.position;
        int idx = 1;
        if (waypoints != null)
        {
            foreach (var w in waypoints)
            {
                if (w == null) continue;
                built[idx] = w.position;
                idx++;
            }
        }
        built[idx] = exit.position;
        return built;
    }

    /// <summary>Picks which of the assigned Path Coordinates Files this particle should follow.</summary>
    private TextAsset PickPathFile()
    {
        // Skip any empty slots in the array.
        int count = pathCoordinatesFiles.Length;
        if (cyclePathFilesInOrder)
        {
            for (int tries = 0; tries < count; tries++)
            {
                TextAsset candidate = pathCoordinatesFiles[_nextFileIndex % count];
                _nextFileIndex = (_nextFileIndex + 1) % count;
                if (candidate != null) return candidate;
            }
            return null;
        }
        else
        {
            int start = Random.Range(0, count);
            for (int tries = 0; tries < count; tries++)
            {
                TextAsset candidate = pathCoordinatesFiles[(start + tries) % count];
                if (candidate != null) return candidate;
            }
            return null;
        }
    }

    /// <summary>
    /// Parses a CSV TextAsset into a Vector3 route. Expects the first three
    /// columns of each row to be x,y,z (extra columns, like a Toxicity
    /// column, are ignored). Any row that doesn't parse as three numbers
    /// (e.g. a header row) is skipped automatically.
    /// </summary>
    private static Vector3[] ParseCsvRoute(TextAsset csv)
    {
        var points = new System.Collections.Generic.List<Vector3>();
        string[] lines = csv.text.Split('\n');
        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim().TrimEnd('\r');
            if (string.IsNullOrEmpty(line)) continue;

            string[] cols = line.Split(',');
            if (cols.Length < 3) continue;

            if (!float.TryParse(cols[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(cols[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(cols[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z))
            {
                continue; // skips header row ("x,y,z,Toxicity") and any bad row automatically
            }

            points.Add(new Vector3(x, y, z));
        }

        if (points.Count < 2)
        {
            Debug.LogWarning("VentOxygenSpawner: pathCoordinatesFile did not contain at least 2 valid x,y,z rows.");
        }

        return points.ToArray();
    }

    private GameObject BuildDefaultParticle()
    {
        if (_sharedSphereMesh == null)
        {
            // Build the reference mesh exactly once for the whole session,
            // instead of creating (and destroying) a full primitive + collider
            // every single spawn.
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sharedSphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        GameObject go = new GameObject("OxygenParticle");
        go.AddComponent<MeshFilter>().sharedMesh = _sharedSphereMesh;
        go.AddComponent<MeshRenderer>();
        go.AddComponent<TrailRenderer>();
        go.AddComponent<OxygenParticleMotion>();
        // No collider is ever added, so there's nothing to strip off - one
        // less object created/destroyed per spawn.
        return go;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (HasFileRoutes)
        {
            Color[] palette = { Color.blue, Color.cyan, Color.green, Color.yellow, Color.red, Color.magenta };
            for (int f = 0; f < pathCoordinatesFiles.Length; f++)
            {
                TextAsset file = pathCoordinatesFiles[f];
                if (file == null) continue;
                if (!_fileRouteCache.TryGetValue(file, out Vector3[] route))
                {
                    route = ParseCsvRoute(file);
                }
                DrawRouteGizmo(route, palette[f % palette.Length]);
            }
        }
        else
        {
            DrawRouteGizmo(BuildRoute(), Color.blue);
        }
    }

    private static void DrawRouteGizmo(Vector3[] route, Color pathColor)
    {
        if (route == null || route.Length < 2) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(route[0], 0.08f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(route[route.Length - 1], 0.08f);

        Gizmos.color = pathColor;
        for (int i = 0; i < route.Length - 1; i++)
        {
            Gizmos.DrawLine(route[i], route[i + 1]);
            if (i > 0) Gizmos.DrawWireSphere(route[i], 0.06f);
        }
    }
#endif
}