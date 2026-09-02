using UnityEngine;

public class ConstantScreenSize : MonoBehaviour
{
    private Camera cam;
    private float fixedDistance;
    private Vector3 fixedScale;

    [Tooltip("Canvas won't scale smaller than this distance")]
    public float minDistance = 0.5f;

    void Start()
    {
        cam = Camera.main;
        fixedDistance = Vector3.Distance(cam.transform.position, transform.position);
        fixedScale = transform.localScale;
    }

    void Update()
    {
        float currentDistance = Vector3.Distance(cam.transform.position, transform.position);
        float clampedDistance = Mathf.Max(currentDistance, minDistance);
        float scaleFactor = clampedDistance / fixedDistance;
        transform.localScale = fixedScale * scaleFactor;
    }
}