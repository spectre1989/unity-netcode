using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomColourOnSpawn : MonoBehaviour
{
    void Start()
    {
        GetComponent<Renderer>().material.color = Random.ColorHSV(0.0f, 1.0f, 1.0f, 1.0f, 1.0f, 1.0f);
    }
}
