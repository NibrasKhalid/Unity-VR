using UnityEngine;

/// Spawns oxygen particles that travel a full path from a room Entrance
/// point to an Exit point, at a steady interval. Every particle gets the
/// same travelTime/pathBend/diffusion settings, so with
/// identicalPathForAllParticles enabled they all trace the exact same
/// curved route through the room - just offset in time, like a continuous
/// stream of oxygen entering at one point and leaving at another.

/// No prefab is required: if you don't assign one, the spawner builds a
/// small blue sphere with a TrailRenderer at runtime. Assign your own
/// prefab (with an OxygenParticleMotion component on it) if you want a
/// custom look instead.

public class VentOxygenSpawner : MonoBehaviour
{
    [Header("Path")]
    [Tooltip("Optional: a CSV file with x,y,z columns (e.g. exported from your Room-Coordinates spreadsheet) defining the entire path in order. If assigned, this overrides Entrance/Waypoints/Exit below entirely. Header row (if any) is auto-skipped.")]
    public TextAsset pathCoordinatesFile;

    [Tooltip("Where particles spawn from (e.g. the room's entrance/vent). Ignored if Path Coordinates File is assigned.")]
    public Transform entrance;

    [Tooltip("Optional: points the path passes through, in order, between entrance and exit - drag in existing scene objects (e.g. your A/B/C markers) to route around obstacles or add turns. Leave empty for a simple one-bend curve. Ignored if Path Coordinates File is assigned.")]
    public Transform[] waypoints;

    [Tooltip("Where particles are heading toward (e.g. the room's exit/outlet vent). Ignored if Path Coordinates File is assigned.")]
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

    private Vector3[] _fileRoute; 

    private void Update()
    {
        bool hasFileRoute = pathCoordinatesFile != null;
        bool hasTransformRoute = entrance != null && exit != null;
        if (!hasFileRoute && !hasTransformRoute) return;

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
        if (pathCoordinatesFile != null)
        {
            if (_fileRoute == null)
            {
                _fileRoute = ParseCsvRoute(pathCoordinatesFile);
            }
            return _fileRoute;
        }

        if (entrance == null || exit == null) return null;

        int validWaypoints = 0;
        if (waypoints != null)
        {
            foreach (var w in waypoints) if (w != null) validWaypoints++;
        }

        var route = new Vector3[2 + validWaypoints];
        route[0] = entrance.position;
        int idx = 1;
        if (waypoints != null)
        {
            foreach (var w in waypoints)
            {
                if (w == null) continue;
                route[idx] = w.position;
                idx++;
            }
        }
        route[idx] = exit.position;
        return route;
    }

    /// Parses a CSV TextAsset into a Vector3 route. Expects the first three
    /// columns of each row to be x,y,z (extra columns, like a Toxicity
    /// column, are ignored). Any row that doesn't parse as three numbers
    /// (e.g. a header row) is skipped automatically.
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
                continue; 
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
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            _sharedSphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(temp);
        }

        GameObject go = new GameObject("OxygenParticle");
        go.AddComponent<MeshFilter>().sharedMesh = _sharedSphereMesh;
        go.AddComponent<MeshRenderer>();
        go.AddComponent<TrailRenderer>();
        go.AddComponent<OxygenParticleMotion>();
        return go;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3[] route = pathCoordinatesFile != null
            ? (_fileRoute ?? ParseCsvRoute(pathCoordinatesFile))
            : BuildRoute();

        if (route == null || route.Length < 2) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(route[0], 0.08f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(route[route.Length - 1], 0.08f);

        Gizmos.color = Color.blue;
        for (int i = 0; i < route.Length - 1; i++)
        {
            Gizmos.DrawLine(route[i], route[i + 1]);
            if (i > 0) Gizmos.DrawWireSphere(route[i], 0.06f);
        }
    }
#endif
}