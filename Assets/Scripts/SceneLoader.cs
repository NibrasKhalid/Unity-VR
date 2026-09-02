// ============================================================================
// WHAT THIS FILE DOES (read this first!)
// ============================================================================
// Unity projects are often split into multiple "scenes" — separate self-
// contained levels/screens (like your main scene and your Heatmap scene).
// Only ONE scene is ever active/running at a time on a real device. Which
// scene starts first is decided by "File > Build Settings > Scenes In
// Build" — whichever scene is listed at the TOP (index 0) is the one that
// automatically loads when the app launches.
//
// Getting from one scene to another during gameplay does NOT happen
// automatically — something has to explicitly tell Unity "stop what
// you're doing, and load this other scene now." That's what this script
// does: it listens for a button press, and when it happens, calls Unity's
// built-in SceneManager.LoadScene() function to switch to your Heatmap
// scene.
// ============================================================================

using UnityEngine;
using UnityEngine.InputSystem;   // Lets us listen for keyboard/controller button presses.
using UnityEngine.SceneManagement; // Contains SceneManager — Unity's tool for switching scenes.

public class SceneLoader : MonoBehaviour
{
    [Header("Which Scene To Load")]
    // IMPORTANT: this must be spelled EXACTLY the same (including
    // capital/lowercase letters) as the scene's name shown in
    // File > Build Settings > Scenes In Build. If it doesn't match
    // exactly, Unity will fail to find the scene and nothing will happen
    // (check the Console for an error if that occurs).
    [Tooltip("Must exactly match the scene's name in File > Build Settings > Scenes In Build.")]
    public string sceneToLoad = "Toxic";

    // This is the same "InputAction" pattern already used elsewhere in
    // your project (see PlayerDetection.cs) — it lets one action respond
    // to multiple different buttons, so the same trigger works whether
    // you're testing in the Editor with a keyboard, or wearing the
    // headset and using a controller.
    private InputAction loadSceneAction;

    void Awake()
    {
        loadSceneAction = new InputAction("LoadHeatmapScene");

        // Press "H" on the keyboard — handy for quick testing in the
        // Unity Editor without needing the headset on.
        loadSceneAction.AddBinding("<Keyboard>/h");

        // Press the SECONDARY button (usually B on the right Touch
        // controller, Y on the left) on either hand while in VR. We use
        // "secondary" rather than "primary" here so this doesn't
        // accidentally clash with the existing interact button used by
        // PlayerDetection.cs.
        loadSceneAction.AddBinding("<XRController>{RightHand}/secondaryButton");
        loadSceneAction.AddBinding("<XRController>{LeftHand}/secondaryButton");
    }

    // InputActions need to be explicitly turned on/off — forgetting this
    // is a common reason a button "does nothing."
    void OnEnable() { loadSceneAction.Enable(); }
    void OnDisable() { loadSceneAction.Disable(); }

    void Update()
    {
        // "WasPressedThisFrame" fires exactly once per press (not
        // continuously while held down), which is what we want — we
        // don't want to spam LoadScene() every single frame the button
        // happens to still be held.
        if (loadSceneAction.WasPressedThisFrame())
        {
            LoadHeatmapScene();
        }
    }

    // Marked "public" (and kept separate from the button-press logic
    // above) so you can ALSO call this directly from other places if you
    // want — for example, from a UI button's OnClick(), or from another
    // script once some in-game condition is met (finishing the MCQ
    // dialogue, reaching a certain spot, etc.) instead of only a raw
    // button press.
    public void LoadHeatmapScene()
    {
        Debug.Log($"SceneLoader: loading scene '{sceneToLoad}'...");

        // This is the actual scene switch. Unity unloads everything in
        // the current scene and loads the new one in its place.
        SceneManager.LoadScene(sceneToLoad);
    }
}