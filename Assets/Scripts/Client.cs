using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class Client : MonoBehaviour
{
    public GameObject[] PrefabTable;
    private List<NetObject> _objects;

    private void Start()
    {
        _objects = new List<NetObject>();
    }

    public void ReceivePacket(byte[] packet)
    {
        int readPos = 0;
        int objectCount = BitConverter.ToInt32(packet, readPos);
        readPos += 4;

        for (int i = 0; i < objectCount; ++i)
        {
            int prefabId = BitConverter.ToInt32(packet, readPos);
            readPos += 4;

            if (i == _objects.Count)
            {
                GameObject obj = Instantiate(PrefabTable[prefabId]);
                obj.transform.parent = this.transform;
                _objects.Add(obj.GetComponent<NetObject>()); // TODO check the NetObject component exists, warn accordingly if not
            }

            PropertyInfo[] properties = _objects[i].GetType().GetProperties(
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.Instance);

            for (int prop = 0; prop < properties.Length; ++prop)
            {
                if (properties[prop].PropertyType == typeof(Vector3))
                {
                    Vector3 v = new Vector3(
                        BitConverter.ToSingle(packet, readPos),
                        BitConverter.ToSingle(packet, readPos + 4),
                        BitConverter.ToSingle(packet, readPos + 8));
                    properties[prop].SetValue(_objects[i], v);
                    readPos += 12;
                }
                else if (properties[prop].PropertyType == typeof(float))
                {
                    float f = BitConverter.ToSingle(packet, readPos);
                    properties[prop].SetValue(_objects[i], f);
                    readPos += 4;
                }
            }
        }
    }
}
