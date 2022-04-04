using System.Collections.Generic;
using System;
using UnityEngine;
using System.Reflection;

struct PendingPacket
{
    public float timeToConsume;
    public byte[] packet;
}

public class Server : MonoBehaviour
{
    public float Tickrate;
    public int SnapshotInterval;
    public Client Client;
    public GameObject[] PrefabTable;
    private List<NetObject> _objects;
    private List<int> _objectPrefabIds;
    private float _tickAccumulator;
    private int _snapshotAccumulator;
    private Queue<PendingPacket> _pendingPackets;
    private int _clientTickId;

    private void Start()
    {
        _objects = new List<NetObject>();
        _objectPrefabIds = new List<int>();
        _pendingPackets = new Queue<PendingPacket>();

        CreateNetObject(0);
    }

    private void CreateNetObject(int prefabId)
    {
        GameObject obj = Instantiate(PrefabTable[prefabId]);
        obj.transform.parent = this.transform;
        obj.transform.localPosition = new Vector3(0.0f, 3.0f, 0.0f);

        _objects.Add(obj.GetComponent<NetObject>()); // TODO detect absence of NetObject script and log accordingly
        _objectPrefabIds.Add(prefabId);
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Reset Player"))
        {
            (_objects[0] as NetPlayer).Pos = new Vector3(0.0f, 1.0f, 0.0f);
        }

        for (int i = 0; i < PrefabTable.Length; ++i)
        {
            if (GUILayout.Button(PrefabTable[i].name))
            {
                CreateNetObject(i);
            }
        }
    }

    private void Update()
    {
        while (_pendingPackets.Count > 0 && _pendingPackets.Peek().timeToConsume <= Time.time)
        {
            ProcessPacket(_pendingPackets.Dequeue().packet);
        }

        float tickDeltaTime = 1.0f / Tickrate;

        _tickAccumulator += Time.deltaTime;
        while (_tickAccumulator >= tickDeltaTime)
        {
            _tickAccumulator -= tickDeltaTime;

            Physics.Simulate(tickDeltaTime);

            ++_snapshotAccumulator;
            if (_snapshotAccumulator == SnapshotInterval)
            {
                _snapshotAccumulator = 0;

                byte[] packet = new byte[1500]; // TODO how to properly handle MTU

                int writePos = 0;

                SnapshotHeaderMsg header = new SnapshotHeaderMsg();
                header.snapshotDeltaTime = tickDeltaTime * SnapshotInterval;
                header.clientTickId = _clientTickId;
                header.objectCount = _objects.Count;
                writePos += NetSerialisationUtils.WriteStruct(packet, header, writePos);

                for (int i = 0; i < _objects.Count; ++i)
                {
                    BitConverter.GetBytes(_objectPrefabIds[i]).CopyTo(packet, writePos);
                    writePos += 4;

                    PropertyInfo[] properties = _objects[i].GetType().GetProperties(
                        BindingFlags.Public |
                        BindingFlags.NonPublic |
                        BindingFlags.Instance);

                    for (int prop = 0; prop < properties.Length; ++prop)
                    {
                        foreach (CustomAttributeData customAttributeData in properties[prop].CustomAttributes)
                        {
                            if (customAttributeData.AttributeType == typeof(NetSerialiseAttribute))
                            {
                                if (properties[prop].PropertyType == typeof(Vector3))
                                {
                                    Vector3 v = (Vector3)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(v.x).CopyTo(packet, writePos);
                                    BitConverter.GetBytes(v.y).CopyTo(packet, writePos + 4);
                                    BitConverter.GetBytes(v.z).CopyTo(packet, writePos + 8);
                                    writePos += 12;
                                }
                                else if (properties[prop].PropertyType == typeof(float))
                                {
                                    float f = (float)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(f).CopyTo(packet, writePos);
                                    writePos += 4;
                                }
                                else if (properties[prop].PropertyType == typeof(Quaternion))
                                {
                                    Quaternion q = (Quaternion)properties[prop].GetValue(_objects[i]);
                                    BitConverter.GetBytes(q.x).CopyTo(packet, writePos);
                                    BitConverter.GetBytes(q.y).CopyTo(packet, writePos + 4);
                                    BitConverter.GetBytes(q.z).CopyTo(packet, writePos + 8);
                                    BitConverter.GetBytes(q.w).CopyTo(packet, writePos + 12);
                                    writePos += 16;
                                }

                                break;
                            }
                        }
                    }
                }

                Client.ReceivePacket(packet);
            }
        }
    }

    public void ReceivePacket(byte[] packet, float fakeLatency)
    {
        _pendingPackets.Enqueue(new PendingPacket { timeToConsume = Time.time + fakeLatency, packet = packet });
    }

    private void ProcessPacket(byte[] packet)
    {
        ClientInputMsg msg = NetSerialisationUtils.ReadStruct<ClientInputMsg>(packet);
        
        _clientTickId = msg.tickId;

        // TODO do something about speed hacks
        NetPlayer player = _objects[0] as NetPlayer;
        player.Move(msg.input, msg.dt);
    }
}
