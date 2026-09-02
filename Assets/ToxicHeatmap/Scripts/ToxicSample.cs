// using UnityEngine;

// [System.Serializable]
// public struct ToxicSample
// {
//     public Vector3 position;
//     public float value;
//     public ToxicSample(Vector3 pos, float val)
//     {
//         position = pos;
//         value = val;
//     }
// }

using UnityEngine;

// One measured point: where it was taken, and the toxicity value recorded there.
[System.Serializable]
public class ToxicSample
{
    public Vector3 position;
    public float value;

    // Parameterless — lets ExcelLoader.cs use object-initializer syntax:
    // new ToxicSample { position = ..., value = ... }
    public ToxicSample() { }

    // Matches CSVLoader.cs's usage: new ToxicSample(new Vector3(x, y, z), v)
    public ToxicSample(Vector3 position, float value)
    {
        this.position = position;
        this.value = value;
    }
}