using UnityEngine;

/// Drives a single oxygen-atom particle along a full, prescribed path through
/// a room using a scripted physics model instead of Unity's
/// Rigidbody/Physics.gravity.
///
/// The path is a smooth spline running through Entrance -> (optional
/// waypoints, in order) -> Exit - not a straight boring line, and not
/// limited to a single bulge either. Add waypoints on VentOxygenSpawner to
/// route the particle around obstacles or through a couple of turns; leave
/// them empty for a simple one-bend curve using pathBend/bendAxis as before.
/// Built from:
///   1. Base path   - a Catmull-Rom spline that passes smoothly through every
///                    point in order, the way a real air current curves
///                    around obstacles rather than beelining through walls.
///   2. Easing      - eased (smoothstep) progress along that path over
///                    travelTime, so it accelerates out of the vent and
///                    settles down near the exit, like real airflow does,
///                    rather than moving at a robotic constant speed.
///   3. Diffusion   - a small Perlin-noise-driven wobble layered on top,
///                    approximating Brownian-motion-like turbulence.
///
/// The route + the noise seed are fully deterministic per particle, so
/// spawning many particles with the same seed makes every one of them trace
/// the exact same route through the room, just offset in time.
///
/// The particle's visual size (particleScale) is applied directly in world
/// meters and is completely independent of any parent object's transform
/// scale - so you never need to type huge numbers to make it visible.
/// 
[RequireComponent(typeof(TrailRenderer))]
public class OxygenParticleMotion : MonoBehaviour
{
    [Header("Path (entrance -> [waypoints] -> exit)")]
    [Tooltip("Seconds to travel the full route.")]
    public float travelTime = 6f;

    [Tooltip("Only used when no waypoints are given: how far the path bows sideways/upward off the straight entrance-exit line, in meters. 0 = perfectly straight line.")]
    public float pathBend = 0.6f;

    [Tooltip("Only used when no waypoints are given: which world axis the single-bend path bends toward.")]
    public Vector3 bendAxis = Vector3.up;

    [Header("Diffusion / turbulence")]
    [Tooltip("Amplitude of the Brownian-like jitter, in meters.")]
    public float diffusionStrength = 0.04f;

    [Tooltip("How fast the turbulence pattern evolves over time.")]
    public float diffusionFrequency = 1.5f;

    [Tooltip("Deterministic seed for this particle's noise pattern and bend side. Give every particle the same seed to make them all trace an identical path.")]
    public int noiseSeed = 0;

    [Header("Appearance")]
    [Tooltip("Visual size in world units. Applied directly - independent of any parent's transform scale. NOTE: your Room-Scan object has a Transform Scale of 500, so 'world units' here are ~500x bigger than real meters - this default (75) targets roughly a 15cm ball at that scale, not 75 real meters.")]
    public float particleScale = 75f;
    public Color particleColor = Color.blue;
    public float trailTime = 0.6f;
    public float trailStartWidth = 75f;
    public float trailEndWidth = 0.0f;

    [Tooltip("Safety cap - particle is destroyed after this many seconds even if it hasn't reached the exit yet.")]
    public float maxLifeTime = 20f;

    private float _age;
    private Vector3[] _route; 
    private TrailRenderer _trail;
    private Renderer _renderer;

    private static Material _sharedTrailMaterial;
    private static Material _sharedSphereMaterial;

    /// How many oxygen particles currently exist in the scene. Used by VentOxygenSpawner to cap concurrent particles.
    public static int ActiveCount { get; private set; }

    private void Awake()
    {
        _trail = GetComponent<TrailRenderer>();
        _renderer = GetComponent<Renderer>();
        ActiveCount++;
    }

    private void OnDestroy()
    {
        ActiveCount--;
    }

    /// Call this right after Instantiate to configure the particle before it starts moving. routePoints must contain at least entrance and exit, in travel order.
    public void Initialize(Vector3[] routePoints, int seed)
    {
        // Make sure nothing else (a scaled parent, etc.) affects this particle.
        transform.SetParent(null, true);

        noiseSeed = seed;
        _age = 0f;

        if (routePoints == null || routePoints.Length < 2)
        {
            Debug.LogWarning("OxygenParticleMotion.Initialize needs at least an entrance and exit point.");
            routePoints = new Vector3[] { transform.position, transform.position };
        }

        if (routePoints.Length == 2)
        {
            // No waypoints given - keep the original single-bend curve so
            // existing setups behave exactly as before.
            Vector3 entrance = routePoints[0];
            Vector3 exit = routePoints[1];
            Vector3 mid = (entrance + exit) * 0.5f;
            Vector3 axis = bendAxis.sqrMagnitude > 0.0001f ? bendAxis.normalized : Vector3.up;
            Vector3 pathDir = (exit - entrance);
            Vector3 sideways = pathDir.sqrMagnitude > 0.0001f ? Vector3.Cross(pathDir.normalized, axis).normalized : Vector3.right;
            float side = (Mathf.PerlinNoise(seed * 0.37f, 0.5f) * 2f - 1f);
            Vector3 controlPoint = mid + axis * pathBend + sideways * pathBend * side;
            _route = new Vector3[] { entrance, controlPoint, exit };
        }
        else
        {
            // Waypoints given - the route passes through every point exactly in order.
            _route = routePoints;
        }

        transform.position = _route[0];
        transform.localScale = Vector3.one * particleScale;
        ApplyColor();

        if (_trail != null) _trail.Clear();
    }

    private void ApplyColor()
    {
        if (_trail != null)
        {
            // Trail disabled - only the sphere is shown.
            _trail.enabled = false;
        }
        if (_renderer != null)
        {
            // The sphere gets its OWN lit material (not the flat unlit one the
            // trail uses) so your scene's Directional Light actually shades
            // it - bright toward the light, darker away from it, plus a soft
            // specular highlight. That light/shadow gradient is what makes a
            // sphere read as a round 3D object instead of a flat, uniformly
            // colored disc.
            if (_sharedSphereMaterial == null)
            {
                _sharedSphereMaterial = BuildLitMaterial(particleColor);
            }
            _renderer.material = _sharedSphereMaterial;
        }
    }

    private static Material BuildUnlitMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        return mat;
    }

    private static Material BuildLitMaterial(Color c)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Diffuse"); // legacy render pipeline fallback
        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        // A bit of smoothness so it picks up a visible specular highlight too -
        // another strong "this is round" cue, on top of the diffuse shading.
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.4f);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.4f);
        return mat;
    }

    private void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        _age += dt;

        float rawT = travelTime > 0.01f ? Mathf.Clamp01(_age / travelTime) : 1f;
        // Smoothstep easing: eases out of the entrance and eases into the
        // exit, rather than constant robotic speed - closer to how a real
        // puff of air accelerates then settles.
        float t = rawT * rawT * (3f - 2f * rawT);

        Vector3 basePos = _route.Length == 3
            ? QuadraticBezier(_route[0], _route[1], _route[2], t)
            : CatmullRomSpline(_route, t);

        // Diffusion jitter layered on top of the base path.
        float nt = (Time.time + noiseSeed * 37.13f) * diffusionFrequency;
        float nx = Mathf.PerlinNoise(nt, noiseSeed * 0.11f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(nt, noiseSeed * 0.11f + 100f) * 2f - 1f;
        float nz = Mathf.PerlinNoise(nt, noiseSeed * 0.11f + 200f) * 2f - 1f;
        Vector3 jitter = new Vector3(nx, ny, nz) * diffusionStrength;

        transform.position = basePos + jitter;

        if (rawT >= 1f || _age >= maxLifeTime)
        {
            Destroy(gameObject);
        }
    }

    private static Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1f - t;
        return u * u * a + 2f * u * t * b + t * t * c;
    }

    /// Evaluates a point at parameter t (0-1) along a Catmull-Rom spline that passes exactly through every point in order.
    private static Vector3 CatmullRomSpline(Vector3[] pts, float t)
    {
        int segCount = pts.Length - 1;
        float scaled = Mathf.Clamp01(t) * segCount;
        int i = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, segCount - 1);
        float localT = scaled - i;

        Vector3 p0 = pts[Mathf.Max(i - 1, 0)];
        Vector3 p1 = pts[i];
        Vector3 p2 = pts[Mathf.Min(i + 1, pts.Length - 1)];
        Vector3 p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];

        float t2 = localT * localT;
        float t3 = t2 * localT;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * localT +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }
}