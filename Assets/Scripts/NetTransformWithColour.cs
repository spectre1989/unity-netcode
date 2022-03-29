using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NetTransformWithColour : NetTransform
{
    private bool _hasOwnMaterial;

    [NetSerialise]
    public float Hue
    {
        get 
        {
            float h, s, v;
            Color.RGBToHSV(GetComponent<Renderer>().sharedMaterial.color, out h, out s, out v);
            return h;
        }
        set
        {
            Color c = Color.HSVToRGB(value, 1.0f, 1.0f);

            if (!_hasOwnMaterial)
            {
                _hasOwnMaterial = true;
                GetComponent<Renderer>().material.color = c;
            }
            else
            {
                GetComponent<Renderer>().sharedMaterial.color = c;
            }
        }
    }
}
