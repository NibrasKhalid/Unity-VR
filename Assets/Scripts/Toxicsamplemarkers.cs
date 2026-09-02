// ============================================================================
// WHAT THIS FILE DOES (read this first!)
// ============================================================================
// This script's job is to:
//   1. Read your Excel file of toxicity measurements (x, y, z position + a
//      toxicity number for each spot that was measured).
//   2. Create a soft, glowing, semi-transparent colored "blob" at each of
//      those spots — green for low toxicity, yellow for medium, red for
//      high — like a real heatmap, instead of a solid hard-edged ball.
//      Because the blobs are soft and see-through, ones that are close
//      together blend into each other, giving a smooth "cloud of color"
//      look rather than a scatter of separate marbles.
//   3. Optionally put a floating number above each blob showing its exact
//      toxicity reading.
//   4. Hide most of those blobs, and only show the ones close to the player
//      and roughly in front of them — like a flashlight beam, or "fog of
//      war" in a video game — so the player has to actually walk around and
//      explore to see the readings, instead of seeing all 240 at once.
//
// You do NOT need to understand every line below to use this script. The
// important settings you'll actually touch live in the Unity Inspector
// (the panel on the right when you click this object), and each one has a
// plain-English explanation as a comment right above it in the code.
//
// TECH NOTE FOR ANYONE CURIOUS: the blobs are drawn using a SpriteRenderer
// (the same component 2D games use), NOT a 3D sphere/quad with a custom
// material. Sprites automatically support transparency and per-blob color
// tinting in every version of Unity with zero extra setup — which sidesteps
// a whole category of "why is this rendering as a plain white square"
// problems that hand-built 3D materials can run into depending on your
// exact Unity/URP version.
// ============================================================================

using System.Collections.Generic; // Gives us "List<>", a resizable list/array we can add items to.
using TMPro;                       // Gives us access to TextMeshPro, the text/label system this project uses.
using UnityEngine;                 // Gives us access to all of Unity's built-in tools (GameObject, Vector3, etc).

// ----------------------------------------------------------------------------
// IMPORTANT SETUP NOTE:
// This script should be attached to an EMPTY GameObject that is positioned
// at (0,0,0) with a scale of (1,1,1) — i.e. it should NOT be moved, rotated,
// or resized in the Inspector. Why? Because every measurement's x/y/z
// position is treated as being relative to THIS object. If you scale or move
// this object, every single measurement point will also shift or scale by
// the same amount.
//
// (If your SampleMarkers object is intentionally NOT at (0,0,0)/(1,1,1)
// right now and everything looks the way you want — that's fine! Just keep
// in mind that "Marker Radius" below ends up multiplied by whatever scale
// this parent object has, so if you ever change the parent's scale, you'll
// likely need to re-tune Marker Radius to compensate.)
//
// Do NOT attach this same script to "ToxicVolume" — that object is
// intentionally scaled up 500x for a completely different visual effect (a
// smooth glowing dome), and is not meant to represent real-world distances.
// ----------------------------------------------------------------------------

// "public class ToxicSampleMarkers : MonoBehaviour" simply means:
// "This is a script named ToxicSampleMarkers, and it can be attached to
// any object in the scene (because it's a MonoBehaviour)."
public class ToxicSampleMarkers : MonoBehaviour
{
    // ========================================================================
    // SECTION 1: WHERE DOES THE DATA COME FROM?
    // These fields show up as fields in the Inspector. "public" just means
    // "this value can be set/edited from the Unity Editor, not just code."
    // ========================================================================

    [Header("Data Source")]
    // If this checkbox is ON: the script will look inside "Folder Path" and
    // load EVERY .xlsx Excel file it finds there, combining all of them.
    // If this checkbox is OFF: the script loads just the ONE file you type
    // into "File Path" below.
    [Tooltip("If true, loads every .xlsx in folderPath and merges them. If false, loads a single file at filePath.")]
    public bool useFolder = true;

    // The folder to search when "Use Folder" is checked, e.g.
    // "C:\Users\you\Documents\VR\ToxicData\"
    public string folderPath;

    // The exact path to a single Excel file, used when "Use Folder" is
    // unchecked, e.g. "C:\Users\you\Documents\VR\dummy_toxic_air_data.xlsx"
    public string filePath;

    // ========================================================================
    // SECTION 2: WHAT DO THE HEATMAP BLOBS LOOK LIKE?
    // ========================================================================

    [Header("Heatmap Blob Appearance")]
    // How big each glowing blob is, in Unity units (usually meters), BEFORE
    // any scaling from a parent object. Since blobs are soft and meant to
    // overlap/blend into a heatmap cloud, bigger values look more like a
    // continuous heatmap; smaller values look like distinct dots.
    [Tooltip("Radius of each glowing heatmap blob. Bigger values overlap more and look more like a continuous heatmap.")]
    public float markerRadius = 0.35f;

    // A "Gradient" is a smooth blend between colors, like a rainbow slider.
    // We use it to decide what color a blob should be based on how toxic
    // that spot is (low toxicity = one color, high toxicity = another).
    // You can leave this empty in the Inspector — if you do, the script
    // automatically uses a green -> yellow -> red gradient (see the
    // DefaultGradient() function near the bottom of this file), which is
    // the classic "traffic light" heatmap coloring: green = safe, yellow =
    // caution, red = dangerous.
    [Tooltip("Leave empty to use the default green -> yellow -> red gradient.")]
    public Gradient valueGradient;

    // Controls how "soft" the edge of each blob is, from 0 (a hard-edged
    // flat disc, almost no fade) to 1 (a very soft, wide glow that fades
    // out gradually). Higher values blend better with neighboring blobs to
    // look like a smooth heatmap; lower values look more like distinct
    // colored dots.
    [Tooltip("0 = hard-edged disc, 1 = very soft glowing fade. Higher blends better with neighbors.")]
    [Range(0f, 1f)]
    public float blobSoftness = 0.65f;

    // If checked (recommended), each blob constantly rotates to always
    // face the camera — like a flat sticker that's always turned towards
    // you — so it always reads as a soft circular glow no matter which
    // direction you're looking at it from. This is the same "billboard"
    // trick used for the floating number labels below.
    // If unchecked, blobs instead lie FLAT on the ground (like a painted
    // heatmap decal on the floor) and do not rotate.
    [Tooltip("If true, blobs always face the camera (glowing cloud look). If false, blobs lie flat on the ground (floor decal look).")]
    public bool billboardMarkers = true;

    // OPTIONAL: if you want a fancier look (e.g. an additive "glow" shader
    // instead of normal blending) you can drag a custom Sprite-compatible
    // material in here. You do NOT need this for normal use — plain
    // colored, properly transparent blobs work automatically with no
    // material assigned at all, since SpriteRenderer handles that itself.
    [Tooltip("Optional: a custom material for the blobs (e.g. an additive glow shader). Leave empty to use Unity's default sprite rendering — this already handles transparency and color correctly with zero setup.")]
    public Material blobMaterialOverride;

    // ========================================================================
    // SECTION 3: SHOULD WE SHOW THE ACTUAL NUMBER ABOVE EACH BLOB?
    // ========================================================================

    [Header("Value Labels (optional)")]
    // If checked, a small floating text label (like "45.2") appears above
    // each blob showing its exact toxicity reading.
    public bool showValueLabels = true;

    // Drag a "3D Text" prefab into this slot in the Inspector. This is the
    // template used to create each floating number label. IMPORTANT: this
    // must be a real 3D World-Space TextMeshPro object, NOT a UI Canvas
    // Text — UI text only renders when it's a child of a Canvas, so if you
    // use the wrong kind here, the numbers will silently fail to appear.
    [Tooltip("A world-space TMP text prefab, e.g. the same pfLabel used by Labels.cs.")]
    public GameObject labelPrefab;

    // How far above the blob (in Unity units) the number label floats.
    public float labelHeight = 0.15f;

    // How big the label text is. Because these labels live in real 3D
    // space (not on a flat screen UI), this number is usually tiny, like
    // 0.002 to 0.01 — a bit of trial and error is normal here.
    public float labelScale = 0.002f;

    // If checked, each label will constantly rotate to always face the
    // camera, like a name tag that follows you with its eyes — this keeps
    // the text readable no matter which direction the player is looking
    // from.
    public bool billboardLabels = true;

    // ========================================================================
    // SECTION 4: THE "REVEAL AS YOU WALK" / FOG-OF-WAR SETTINGS
    // This is the part that hides most blobs and only shows the ones
    // near the player, so the player has to explore instead of seeing
    // everything at once.
    // ========================================================================

    [Header("Reveal-As-You-Walk")]
    // Drag your Player object here. Every frame, the script measures the
    // distance (and direction) from THIS object to every blob, to decide
    // which ones should currently be visible.
    // NOTE: if you leave this empty, the fog-of-war effect is turned OFF
    // completely and every blob just stays visible all the time (the old
    // behavior, before this feature was added).
    [Tooltip("The player transform. Blobs only reveal near this transform. Leave empty to disable filtering (show everything, old behavior).")]
    public Transform player;

    // How close (in Unity units) the player needs to be to a blob before
    // it becomes visible. Think of this as "how far the flashlight beam
    // reaches." A bigger number reveals more area around the player.
    [Tooltip("How far ahead of the player a blob becomes visible.")]
    public float revealRadius = 3f;

    // If checked: blobs only reveal when they're roughly IN FRONT of the
    // player (within the cone described below) — blobs directly behind
    // the player's back stay hidden even if they're close, which encourages
    // the player to physically turn and look around.
    // If unchecked: any blob within "Reveal Radius" shows up, regardless
    // of which way the player is facing (a simple circle around the
    // player instead of a cone).
    [Tooltip("If true, only blobs roughly in front of the player reveal (a cone), not ones behind. If false, revealRadius applies in all directions.")]
    public bool onlyRevealAhead = true;

    // The width of that "flashlight cone" in degrees. Imagine standing at
    // the tip of a pizza slice, looking straight ahead — this number is how
    // wide that pizza slice is.
    //   - A small number (e.g. 30) = a narrow, focused beam directly ahead.
    //   - A big number (e.g. 180) = a wide half-circle in front of you.
    //   - 360 = every direction, same as if "Only Reveal Ahead" were off.
    [Tooltip("Full width of the forward cone in degrees. 360 = same as all-directions.")]
    [Range(1f, 360f)] // This just makes it show up as a nice slider in the Inspector, limited between 1 and 360.
    public float revealConeAngle = 110f;

    // ========================================================================
    // SECTION 5: INTERNAL "BACKSTAGE" VARIABLES
    // Everything below is marked "private", which means it's only used
    // internally by this script and does NOT show up in the Inspector.
    // Think of these as the script's own private notes/memory, not
    // settings for you to change.
    // ========================================================================

    // The full list of measurements loaded from the Excel file. Each item
    // in this list has a position (x,y,z) and a toxicity value.
    private List<ToxicSample> samples;

    // The single highest toxicity value found across all samples. We use
    // this to figure out how "red" a given blob should be — e.g. a sample
    // with the maximum value gets the most extreme color in the gradient.
    private float maxValue;

    // A reference to the main camera, used for making blobs/labels always
    // face the player (the "billboard" effect).
    private Camera cam;

    // The one shared soft-circle texture used by every single blob. We
    // only need to generate this ONCE and reuse it for every sample,
    // rather than making a brand new texture per marker — much cheaper on
    // memory and performance.
    private Texture2D blobTexture;

    // A "Sprite" is Unity's wrapper around a texture that lets a
    // SpriteRenderer actually draw it — think of blobTexture as the raw
    // image data, and blobSprite as "that image, ready to be displayed."
    // Just like the texture, we only need to build this once and every
    // blob can reuse the same one.
    private Sprite blobSprite;

    // ------------------------------------------------------------------
    // This is a small custom "container" — think of it like a labeled
    // folder that holds a few related things together: the blob, its
    // floating number label, and whether it's currently being shown or
    // hidden. We keep one of these per measurement, so that every frame
    // we can quickly check "is this one close enough to the player to
    // show?" and toggle both its blob AND its label together in one step.
    // ------------------------------------------------------------------
    private class MarkerEntry
    {
        public GameObject marker;       // The glowing blob itself.
        public GameObject label;        // The floating number above it (can be empty/null if labels are off).
        public bool currentlyVisible;   // True if this marker is switched ON right now, false if hidden.
    }

    // This is the actual list of all 240 (or however many) MarkerEntry
    // "folders" described above — one per measurement point.
    private readonly List<MarkerEntry> entries = new List<MarkerEntry>();

    // ========================================================================
    // Start() is a special Unity function that automatically runs ONCE,
    // right when the game begins playing. This is where we do all the
    // "setup" work: loading the Excel data and creating all the blobs.
    // ========================================================================
    void Start()
    {
        // Camera.main automatically finds the camera tagged "MainCamera"
        // in the scene, so we don't have to manually drag it in.
        cam = Camera.main;

        // Build the shared soft-circle texture and sprite that every blob
        // will use. We do this once, before spawning anything, since
        // every blob needs it.
        PrepareBlobVisuals();

        // This line says: "If Use Folder is checked, load every Excel file
        // in the folder. Otherwise, load just the one file." The "? :"
        // pattern here is called a "ternary" — it's just a shorthand way
        // of writing a simple if/else in one line.
        samples = useFolder ? ExcelLoader.LoadFolder(folderPath) : ExcelLoader.Load(filePath);

        // Safety check: if nothing was loaded (empty file, wrong path,
        // typo in column names, etc), warn in the console and stop here
        // instead of continuing on and causing more confusing errors later.
        if (samples == null || samples.Count == 0)
        {
            Debug.LogWarning("ToxicSampleMarkers: no samples loaded — nothing to show.");
            return; // "return" here means "stop running this function early."
        }

        // Go through every single sample we loaded, and remember the
        // single highest toxicity value seen. We need this later to scale
        // colors correctly (e.g. so the WORST spot is always fully red).
        maxValue = 0f;
        foreach (var s in samples) maxValue = Mathf.Max(maxValue, s.value);
        // "foreach" means: "for every item in this list, one at a time, do
        // the following." Mathf.Max just returns whichever of the two
        // numbers is bigger.

        // If you didn't set up a custom color gradient in the Inspector,
        // use our simple built-in green -> yellow -> red one instead.
        if (valueGradient == null || valueGradient.colorKeys.Length == 0)
            valueGradient = DefaultGradient();

        // Now actually create a blob (and optional label) for every single
        // sample we loaded. SpawnMarker() is a function defined further
        // down in this file — we call it once per sample.
        foreach (var sample in samples)
            SpawnMarker(sample);

        // Print a friendly confirmation message to the Console window so
        // you can see at a glance that everything worked, and how many
        // blobs got created.
        Debug.Log($"ToxicSampleMarkers: spawned {samples.Count} heatmap blobs (max value = {maxValue:0.0}).");

        // If a Player has been assigned, we want the fog-of-war effect to
        // be active — which means ALL blobs should start out hidden, and
        // only reveal themselves once the player gets close. Without
        // this, you'd see a brief flash of all 240 blobs for one frame
        // before the hiding logic (in Update, below) has a chance to run.
        if (player != null)
        {
            foreach (var e in entries)
            {
                e.marker.SetActive(false);                          // Turn the blob off.
                if (e.label != null) e.label.SetActive(false);      // Turn its number label off too, if it has one.
                e.currentlyVisible = false;                          // Remember that it's currently hidden.
            }
        }
    }

    // ------------------------------------------------------------------
    // Builds the one shared soft-circle texture (and the Sprite wrapper
    // around it) that every blob will reuse. Doing this once here
    // (instead of once per blob) is both faster and uses far less memory
    // than building something new per marker.
    // ------------------------------------------------------------------
    void PrepareBlobVisuals()
    {
        // Generate the soft circular gradient (white in the middle, fading
        // to fully see-through at the edges) that gives every blob its
        // soft "glow" look instead of a hard-edged shape.
        blobTexture = GenerateSoftCircleTexture(64, blobSoftness);

        // Wrap that texture in a Sprite so a SpriteRenderer can display
        // it. A few notes on the numbers below:
        //   - The Rect covers the FULL texture (0,0) to (width,height).
        //   - The pivot (0.5, 0.5) means "the center of the image is the
        //     object's origin point," so it's centered on the sample's
        //     exact position rather than offset to one side.
        //   - "pixelsPerUnit" is set to the texture's own width, which
        //     means "at a transform scale of 1, this sprite covers
        //     exactly 1x1 Unity unit." That makes the math in SpawnMarker
        //     below simple: we just scale the object's transform to the
        //     diameter we actually want.
        blobSprite = Sprite.Create(
            blobTexture,
            new Rect(0f, 0f, blobTexture.width, blobTexture.height),
            new Vector2(0.5f, 0.5f),
            blobTexture.width);
    }

    // ------------------------------------------------------------------
    // Creates a small square texture containing a soft, glowing white
    // circle — fully solid in the middle, gradually fading to fully
    // see-through by the edges. This is the "shape" every blob is drawn
    // with; we then tint it a different color (green/yellow/red) per
    // sample by setting each SpriteRenderer's own .color in SpawnMarker().
    //
    // size     = how many pixels wide/tall the texture is (bigger = smoother
    //            but slightly more memory; 64 is plenty for a soft blur).
    // softness = 0 means a hard-edged solid circle with barely any fade;
    //            1 means a very gradual, wide, cloud-like fade.
    // ------------------------------------------------------------------
    Texture2D GenerateSoftCircleTexture(int size, float softness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp; // Don't repeat/tile at the edges.
        tex.filterMode = FilterMode.Bilinear; // Smooth out the pixels instead of looking blocky.

        // "innerRadius" is the distance from the center (as a fraction,
        // where 0 = dead center and 1 = the very edge of the texture)
        // within which the blob is still fully solid/opaque. Beyond that,
        // it starts fading out. A higher "softness" setting shrinks this
        // inner solid area, making more of the blob a soft fade.
        float innerRadius = 1f - softness;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                // Convert this pixel's coordinate into a -1..1 range,
                // where (0,0) is the exact center of the texture.
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;

                // Plain old distance-from-center formula (Pythagoras).
                float dist = Mathf.Sqrt(nx * nx + ny * ny);

                // How far between "still solid" (innerRadius) and "fully
                // faded out" (the edge, at distance 1) is this pixel?
                // InverseLerp gives us that as a 0-to-1 fraction.
                float t = Mathf.InverseLerp(innerRadius, 1f, dist);

                // SmoothStep gives a nice gentle S-curve fade rather than
                // a straight linear one, which looks softer/more natural.
                float alpha = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));

                // Anything past the outer edge (a perfect circle) should
                // be fully invisible, even in the texture's corners.
                if (dist > 1f) alpha = 0f;

                // We only care about the alpha (see-through-ness) here —
                // the actual color/tint gets applied later, per-instance,
                // via each SpriteRenderer's own .color. So we just store
                // plain white with our calculated alpha.
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(); // Upload all those pixel changes to the GPU.
        return tex;
    }

    // ========================================================================
    // Update() is another special Unity function — unlike Start(), this one
    // runs automatically, over and over, roughly 60+ times per second,
    // for as long as the game is running. This is where we do things that
    // need to keep happening continuously, like checking the player's
    // position every frame to decide what should currently be visible.
    // ========================================================================
    void Update()
    {
        UpdateBillboards();  // Keep blobs/labels facing the camera (if enabled).
        UpdateVisibility();  // Decide which blobs should be shown/hidden this frame.
    }

    // ------------------------------------------------------------------
    // Makes every visible blob and/or label rotate to always face the
    // camera, like a flat sticker that spins to follow you. This is what
    // keeps a flat, 2D soft circle looking like a glowing round blob no
    // matter which direction the player views it from — and keeps the
    // floating numbers readable too.
    // ------------------------------------------------------------------
    void UpdateBillboards()
    {
        if (cam == null) return;

        foreach (var e in entries)
        {
            // Rotate the blob to face the camera, but only if the
            // "Billboard Markers" option is on, and only if this blob is
            // actually currently visible (no point doing math on
            // something invisible).
            if (billboardMarkers && e.marker != null && e.marker.activeSelf)
            {
                Vector3 dirToCam = cam.transform.position - e.marker.transform.position;
                // The "-dirToCam" flip makes sure the FRONT of the flat
                // blob (not its invisible back side) faces the camera.
                e.marker.transform.rotation = Quaternion.LookRotation(-dirToCam, Vector3.up);
            }

            // Same idea, but for the floating number label.
            if (billboardLabels && e.label != null && e.label.activeSelf)
            {
                Vector3 dirToCam = cam.transform.position - e.label.transform.position;
                e.label.transform.rotation = Quaternion.LookRotation(-dirToCam, Vector3.up);
            }
        }
    }

    // ------------------------------------------------------------------
    // This is the heart of the "fog of war" / reveal-as-you-walk effect.
    // Every frame, for every single blob, we ask two yes/no questions:
    //   1. Is this blob close enough to the player? (inside the circle)
    //   2. Is this blob roughly in front of the player? (inside the cone)
    // If the answer to both is "yes" (or question 2 is skipped because
    // "Only Reveal Ahead" is off), the blob gets shown. Otherwise, it
    // gets hidden.
    // ------------------------------------------------------------------
    void UpdateVisibility()
    {
        // If no Player has been assigned in the Inspector, we have no way
        // to measure distance from anything — so just leave everything
        // visible all the time (this preserves the original, simpler
        // behavior for anyone who doesn't want the fog-of-war effect).
        if (player == null) return;

        // Where is the player right now, this exact frame?
        Vector3 playerPos = player.position;

        // Which direction is the player currently facing? "forward" gives
        // us a direction arrow pointing out of the front of the player.
        Vector3 flatForward = player.forward;

        // We zero out the up/down (Y) component here, because we only
        // care about which way the player is facing along the FLOOR
        // (left/right/forward/back) — we don't want looking up or down
        // with your head to affect which blobs reveal.
        flatForward.y = 0f;

        // "Normalize" just means "make this arrow exactly 1 unit long,
        // while keeping it pointing the same direction." This is
        // necessary for the angle math below to work correctly. We only
        // do this if the arrow isn't basically zero-length already
        // (which could happen in rare edge cases and would cause an
        // error otherwise).
        if (flatForward.sqrMagnitude > 0.0001f) flatForward.Normalize();

        // Half of the total cone width — because the cone is centered on
        // "straight ahead," we check "is this within X degrees to the
        // LEFT or RIGHT of straight ahead," which is half the total cone
        // angle on each side.
        float halfCone = revealConeAngle * 0.5f;

        // Now check every single blob, one at a time.
        foreach (var e in entries)
        {
            if (e.marker == null) continue; // Safety check — skip if somehow missing.

            // Work out the straight-line direction/distance from the
            // player to this particular blob.
            Vector3 toMarker = e.marker.transform.position - playerPos;
            toMarker.y = 0f; // Again, ignore height differences — only compare positions as if flattened onto the floor.

            // "magnitude" of a direction arrow just means "how long is
            // it" — i.e. the distance between the player and this blob.
            float dist = toMarker.magnitude;

            // QUESTION 1: Is the blob close enough?
            bool inRange = dist <= revealRadius;

            // QUESTION 2: Is the blob roughly in front of the player?
            // We start by assuming "yes" by default...
            bool inCone = true;

            // ...and only actually check the angle if we need to (no
            // point calculating angles for something that already failed
            // the distance check, or if the "Only Reveal Ahead" option is
            // turned off).
            if (inRange && onlyRevealAhead && dist > 0.001f)
            {
                // Vector3.Angle tells us the angle (in degrees) between
                // two directions. Here: "how many degrees away is the
                // direction to this blob, compared to straight ahead?"
                float angle = Vector3.Angle(flatForward, toMarker);

                // If that angle is small enough to fit inside our cone,
                // it counts as "in front."
                inCone = angle <= halfCone;
            }

            // The blob should be visible only if BOTH checks passed.
            bool visible = inRange && inCone;

            // Performance tip: only actually turn things on/off if the
            // visibility state has actually CHANGED since last frame.
            // Constantly calling SetActive() on something that's already
            // in the state you want is wasted effort.
            if (visible != e.currentlyVisible)
            {
                e.marker.SetActive(visible); // Show/hide the blob.

                // Show/hide the label too — but only turn it ON if BOTH
                // this blob is visible AND the global "Show Value
                // Labels" checkbox is turned on.
                if (e.label != null) e.label.SetActive(visible && showValueLabels);

                e.currentlyVisible = visible; // Remember the new state for next frame's comparison.
            }
        }
    }

    // ------------------------------------------------------------------
    // Creates one glowing heatmap blob (and, optionally, one floating
    // number label) for a single measurement. This function is called
    // once per sample, back in Start().
    // ------------------------------------------------------------------
    void SpawnMarker(ToxicSample sample)
    {
        // Create a brand new, completely empty GameObject to hold our
        // blob. We build it from scratch (rather than using Unity's
        // built-in "create a Quad/Sphere" shortcuts) specifically so it
        // has NO extra components we don't need — no 3D mesh, no
        // collider — just the SpriteRenderer we're about to add.
        GameObject marker = new GameObject($"Blob_{sample.value:0.0}");

        // Make this blob a "child" of this script's own object
        // (SampleMarkers). "worldPositionStays: false" means "position
        // this blob using LOCAL coordinates relative to its new parent,"
        // which is exactly what we want, since sample.position IS a
        // local/relative coordinate from your Excel data.
        marker.transform.SetParent(transform, worldPositionStays: false);

        // Move the blob to the exact spot recorded in the Excel file.
        marker.transform.localPosition = sample.position;

        // Resize the blob based on the "Marker Radius" setting. We
        // multiply by 2 because "radius" describes half the width, but
        // localScale describes the full width/height. Because our sprite
        // was built with pixelsPerUnit = its own texture width (see
        // PrepareBlobVisuals above), a scale of 1 = exactly 1 Unity unit
        // wide, so this scale value directly becomes the blob's diameter
        // in world units.
        float diameter = markerRadius * 2f;
        marker.transform.localScale = new Vector3(diameter, diameter, 1f);

        // If we're NOT billboarding (i.e. blobs should lie flat on the
        // ground like a painted decal instead of always facing the
        // camera), rotate it flat now. If we ARE billboarding, this
        // starting rotation doesn't matter much since Update() will
        // immediately start rotating it to face the camera every frame
        // anyway.
        if (!billboardMarkers)
            marker.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

        // Add a SpriteRenderer — this is the component that actually
        // draws our soft circle texture on screen, and (unlike a 3D
        // MeshRenderer + custom material) already knows how to handle
        // transparency and per-instance color correctly, with zero extra
        // setup required.
        var sr = marker.AddComponent<SpriteRenderer>();
        sr.sprite = blobSprite;

        // If you supplied your own custom material (for a fancier glow
        // effect, for example), use it. Otherwise, we leave this alone —
        // SpriteRenderer already comes with Unity's built-in default
        // sprite material, which reliably supports transparency out of
        // the box.
        if (blobMaterialOverride != null)
            sr.sharedMaterial = blobMaterialOverride;

        // Work out how "toxic" this sample is, as a fraction from 0 (not
        // toxic at all) to 1 (the worst reading in the whole dataset)...
        float t = maxValue > 0f ? sample.value / maxValue : 0f;

        // ...then tint this specific blob's sprite using our
        // green -> yellow -> red gradient. This is the ENTIRE color
        // setup needed — no MaterialPropertyBlock, no shader property
        // name guessing. Each SpriteRenderer simply remembers its own
        // color.
        sr.color = valueGradient.Evaluate(t);

        // Create our little "folder" that keeps this blob, its label (if
        // any), and its current visibility state bundled together.
        var entry = new MarkerEntry { marker = marker, currentlyVisible = true };

        // Only bother creating a floating number label if BOTH the
        // "Show Value Labels" checkbox is on AND you've actually dragged
        // a valid label prefab into the Inspector.
        if (showValueLabels && labelPrefab != null)
        {
            // Create a copy ("instance") of the label prefab, parented
            // under this same object, same as we did for the blob.
            GameObject label = Instantiate(labelPrefab, transform);

            // Position it directly above the blob, offset upward by
            // "Label Height."
            label.transform.localPosition = sample.position + Vector3.up * labelHeight;

            // Resize the label according to the "Label Scale" setting.
            label.transform.localScale = Vector3.one * labelScale;

            // Find the actual text component inside the label prefab
            // (it might be nested inside a child object) and set its
            // text to show the toxicity number, rounded to one decimal
            // place (the "0.0" format), e.g. "45.2".
            var tmp = label.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.SetText($"{sample.value:0.0}");

            // Remember this label as part of the same "folder" as its blob.
            entry.label = label;
        }

        // Add this completed folder (blob + label + visibility flag) to
        // our master list, so Update() can find and manage it every frame.
        entries.Add(entry);
    }

    // ------------------------------------------------------------------
    // Builds a simple built-in color gradient (green -> yellow -> red)
    // to use as a fallback, in case you didn't set up a custom one in
    // the Inspector. Green = safe/low toxicity, Yellow = medium, Red =
    // high/dangerous toxicity — just like a traffic light, and the
    // classic coloring scheme used by real-world heatmaps.
    // ------------------------------------------------------------------
    Gradient DefaultGradient()
    {
        // A Gradient in Unity is basically a strip of color that smoothly
        // blends from one color to the next at specific "stops" (0.0 =
        // the very start, 1.0 = the very end).
        var g = new Gradient();

        g.SetKeys(
            // Where each COLOR sits along the strip:
            new[]
            {
                new GradientColorKey(Color.green, 0f),    // At the very start (0%): green.
                new GradientColorKey(Color.yellow, 0.5f), // Halfway (50%): yellow.
                new GradientColorKey(Color.red, 1f)       // At the very end (100%): red.
            },
            // Where each TRANSPARENCY level sits along the strip. We keep
            // the blobs themselves fully "solid color" here — the actual
            // see-through softness comes from the soft-circle TEXTURE,
            // not from this gradient's own alpha.
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 1f)
            });

        return g; // Hand back the finished gradient to whoever asked for it.
    }
}