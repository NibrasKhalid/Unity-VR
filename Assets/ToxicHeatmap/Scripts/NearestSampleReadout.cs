using System.Collections.Generic;
using UnityEngine;

// Attach to the VR camera rig / player. Shows the nearest sample's exact
// value in a world-space TextMesh when the player is close to a measured point.
public class NearestSampleReadout : MonoBehaviour
{
    public string csvPath;
    public float showWithinDistance = 1.0f;
    public TextMesh readoutText; // assign a floating TextMesh (e.g. attached to a small panel)

    private List<ToxicSample> samples;

    void Start()
    {
        samples = CSVLoader.Load(csvPath);
    }

    void Update()
    {
        if (samples == null || samples.Count == 0 || readoutText == null) return;

        float bestDist = float.MaxValue;
        float bestValue = 0f;

        foreach (var s in samples)
        {
            float d = Vector3.Distance(transform.position, s.position);
            if (d < bestDist) { bestDist = d; bestValue = s.value; }
        }

        if (bestDist <= showWithinDistance)
        {
            readoutText.gameObject.SetActive(true);
            readoutText.text = $"{bestValue:0.0} (measured {bestDist:0.0}m away)";
        }
        else
        {
            readoutText.gameObject.SetActive(false);
        }
    }
}