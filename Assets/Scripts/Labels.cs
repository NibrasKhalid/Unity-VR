using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Labels : MonoBehaviour
{
    [SerializeField] GameObject[] items;
    [SerializeField] GameObject pfLabel; 
    [SerializeField] LayerMask occlusionMask = ~0;
    [SerializeField] float occlusionBuffer = 0.1f;

    [Header("Label World Space Settings")]
    [SerializeField] float labelHeight = 0.3f;        // How far above the item the label floats
    [SerializeField] float labelScale = 0.002f;       // World-space size (tweak per scene scale)
    [SerializeField] bool billboardLabels = true;     // Always face the camera

    private List<GameObject> labels = new List<GameObject>();
    private Camera cam;

    void Start()
    {
        cam = Camera.main;

        for (int i = 0; i < items.Length; i++)
    {
        GameObject newLabel = Instantiate(pfLabel);
        newLabel.transform.SetParent(transform);
        newLabel.GetComponentInChildren<TextMeshProUGUI>().SetText(items[i].name);
        newLabel.transform.localScale = Vector3.one * labelScale;
        labels.Add(newLabel);
    }
    }

    void Update()
    {
        for (int i = 0; i < items.Length; i++)
        {
            Vector3 itemPos = items[i].transform.position;
            bool visible = !IsBlocked(itemPos);

            if (visible)
            {
                // Position the label above the item in world space
                Vector3 labelPos = itemPos + Vector3.up * labelHeight;
                labels[i].transform.position = labelPos;

                // Billboard: rotate to face the camera
                if (billboardLabels)
                {
                    Vector3 dirToCamera = cam.transform.position - labelPos;
                    labels[i].transform.rotation = Quaternion.LookRotation(-dirToCamera, Vector3.up);
                }

                labels[i].SetActive(true);
            }
            else
            {
                labels[i].SetActive(false);
            }
        }
    }

    bool IsBlocked(Vector3 itemPos)
    {
        Vector3 camPos = cam.transform.position;
        Vector3 dir = itemPos - camPos;
        float dist = dir.magnitude - occlusionBuffer;

        if (dist <= 0f) return false;

        return Physics.Raycast(camPos, dir.normalized, dist, occlusionMask);
    }
}