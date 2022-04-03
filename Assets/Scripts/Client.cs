using System;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class Client : MonoBehaviour
{
    public Server Server;
    public GameObject[] PrefabTable;
    public float Latency;
    public float MaxClientTickRate; // TODO this should be tweakable in server settings, and enforced by the server
    private List<NetObject> _objects;
    private List<byte[]> _snapshots;
    private float _snapshotLerpT;
    private Queue<PendingPacket> _pendingPackets;
    private float _tickAccumulator;
    private int _tickId;
    private PredictedMove[] _predictionBuffer;
    private const int PREDICTION_BUFFER_SIZE = 1024;
    private const int PREDICTION_BUFFER_MASK = 1023;

    struct PredictedMove
    {
        public NetPlayer.Input input;
        public float dt;
        public Vector3 pos;
    }

    private void Start()
    {
        _objects = new List<NetObject>();
        _snapshots = new List<byte[]>();
        _pendingPackets = new Queue<PendingPacket>();
        _predictionBuffer = new PredictedMove[PREDICTION_BUFFER_SIZE];
    }

    public void ReceivePacket(byte[] packet)
    {
        _pendingPackets.Enqueue(new PendingPacket { timeToConsume = Time.time + Latency, packet = packet });
    }

    private void Update()
    {
        bool hasNewSnapshots = false;
        while(_pendingPackets.Count > 0 && _pendingPackets.Peek().timeToConsume <= Time.time)
        {
            _snapshots.Add(_pendingPackets.Dequeue().packet);
            hasNewSnapshots = true;
        }

        if (_snapshots.Count > 1)
        {
            float snapshotDeltaTime = BitConverter.ToSingle(_snapshots[0], 0);

            _snapshotLerpT += (Time.deltaTime / snapshotDeltaTime);
            while (_snapshotLerpT >= 1.0f && _snapshots.Count > 1)
            {
                _snapshotLerpT -= 1.0f;
                _snapshots.RemoveAt(0);
            }

            if (_snapshots.Count > 1)
            {
                Interpolate(_snapshots[0], _snapshots[1], _snapshotLerpT);
            }
            else
            {
                Interpolate(_snapshots[0], _snapshots[0], 0.0f);
                _snapshotLerpT = 0.0f;
            }
        }

        float minTickDuration = 1.0f / MaxClientTickRate;

        if (_objects.Count > 0)
        {
            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator >= minTickDuration)
            {
                NetPlayer.Input input = new NetPlayer.Input();
                input.forward = Input.GetKey(KeyCode.W);
                input.back = Input.GetKey(KeyCode.S);
                input.left = Input.GetKey(KeyCode.A);
                input.right = Input.GetKey(KeyCode.D);

                byte packedInput = (byte)(
                    (input.forward ? 1 : 0) |
                    (input.back ? 2 : 0) |
                    (input.left ? 4 : 0) |
                    (input.right ? 8 : 0));
                byte[] packet = new byte[1500];
                packet[0] = packedInput;
                BitConverter.GetBytes(_tickAccumulator).CopyTo(packet, 1);
                BitConverter.GetBytes(_tickId).CopyTo(packet, 5);

                Server.ReceivePacket(packet, Latency);

                NetPlayer netPlayer = _objects[0] as NetPlayer;
                netPlayer.Move(input, _tickAccumulator);

                PredictedMove predictedMove = new PredictedMove();
                predictedMove.input = input;
                predictedMove.dt = _tickAccumulator;
                predictedMove.pos = netPlayer.Pos;

                _predictionBuffer[_tickId & PREDICTION_BUFFER_MASK] = predictedMove;

                _tickAccumulator = 0.0f;
                ++_tickId;
            }

            for (int i = 0; i < PREDICTION_BUFFER_SIZE; ++i)
            {
                if (_predictionBuffer[i].dt > 0.0f)
                {
                    Vector3 globalPos = this.transform.TransformPoint(_predictionBuffer[i].pos);
                    Debug.DrawLine(globalPos - Vector3.up, globalPos + Vector3.up);
                }
            }

            if (hasNewSnapshots)
            {
                // TODO this is hardcoded to look for Pos property, need code gen to be used here

                byte[] snapshot = _snapshots[_snapshots.Count - 1];
                NetPlayer player = _objects[0] as NetPlayer;

                int readPos = 4; // skip snapshot delta
                int tickId = BitConverter.ToInt32(snapshot, readPos);
                readPos += 4;
                readPos += 4; // skip objects.count
                readPos += 4; // skip object[0].prefabId

                PropertyInfo[] properties = player.GetType().GetProperties(
                    BindingFlags.Public |
                    BindingFlags.NonPublic |
                    BindingFlags.Instance);
                for (int prop = 0; prop < properties.Length; ++prop)
                {
                    bool isNetSerialised = false;
                    foreach (CustomAttributeData customAttributeData in properties[prop].CustomAttributes)
                    {
                        if (customAttributeData.AttributeType == typeof(NetSerialiseAttribute))
                        {
                            isNetSerialised = true;
                            break;
                        }
                    }

                    if (isNetSerialised)
                    {
                        if (properties[prop].PropertyType == typeof(Vector3))
                        {
                            if (properties[prop].Name == "Pos")
                            {
                                Vector3 pos = new Vector3(
                                    BitConverter.ToSingle(snapshot, readPos),
                                    BitConverter.ToSingle(snapshot, readPos + 4),
                                    BitConverter.ToSingle(snapshot, readPos + 8));

                                if (Vector3.Distance(_predictionBuffer[tickId & PREDICTION_BUFFER_MASK].pos, pos) > 0.0001f)
                                {
                                    Debug.Log("CORRECTION");

                                    _predictionBuffer[tickId & PREDICTION_BUFFER_MASK].pos = pos;
                                    player.Pos = pos;
                                    for (int i = tickId + 1; i < _tickId; ++i)
                                    {
                                        player.Move(
                                            _predictionBuffer[i & PREDICTION_BUFFER_MASK].input,
                                            _predictionBuffer[i & PREDICTION_BUFFER_MASK].dt);

                                        _predictionBuffer[i & PREDICTION_BUFFER_MASK].pos = player.Pos;
                                    }
                                }

                                break;
                            }
                            readPos += 12;
                        }
                        else if (properties[prop].PropertyType == typeof(float))
                        {
                            readPos += 4;
                        }
                        if (properties[prop].PropertyType == typeof(Quaternion))
                        {
                            readPos += 16;
                        }
                    }
                }
            }
        }
    }

    private bool _hasInterpolatedPlayerOnce; // TODO this is just awful
    private void Interpolate(byte[] a, byte[] b, float t)
    {
        // TODO really badly need some kind of code gen or something for serialising packets
        int readPos = 8; // skip snapshot delta, and client tick id
        int objectCount = BitConverter.ToInt32(a, readPos);
        readPos += 4;

        for (int i = 0; i < objectCount; ++i)
        {
            int prefabId = BitConverter.ToInt32(a, readPos);
            readPos += 4;

            if (i == _objects.Count)
            {
                GameObject obj = Instantiate(PrefabTable[prefabId]);
                obj.transform.parent = this.transform;
                _objects.Add(obj.GetComponent<NetObject>()); // TODO check the NetObject component exists, warn accordingly if not
            }

            // TODO hack to stop client side prediction being overwritten by interpolation
            if (i == 0 && _hasInterpolatedPlayerOnce)
            {
                continue;
            }
            _hasInterpolatedPlayerOnce = true;

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
                            Vector3 from = new Vector3(
                                BitConverter.ToSingle(a, readPos),
                                BitConverter.ToSingle(a, readPos + 4),
                                BitConverter.ToSingle(a, readPos + 8));
                            Vector3 to = new Vector3(
                                BitConverter.ToSingle(b, readPos),
                                BitConverter.ToSingle(b, readPos + 4),
                                BitConverter.ToSingle(b, readPos + 8));
                            properties[prop].SetValue(_objects[i], Vector3.Lerp(from, to, t));
                            readPos += 12;
                        }
                        else if (properties[prop].PropertyType == typeof(float))
                        {
                            float from = BitConverter.ToSingle(a, readPos);
                            float to = BitConverter.ToSingle(b, readPos);
                            properties[prop].SetValue(_objects[i], Mathf.Lerp(from, to, t));
                            readPos += 4;
                        }
                        if (properties[prop].PropertyType == typeof(Quaternion))
                        {
                            Quaternion from = new Quaternion(
                                BitConverter.ToSingle(a, readPos),
                                BitConverter.ToSingle(a, readPos + 4),
                                BitConverter.ToSingle(a, readPos + 8),
                                BitConverter.ToSingle(a, readPos + 12));
                            Quaternion to = new Quaternion(
                                BitConverter.ToSingle(b, readPos),
                                BitConverter.ToSingle(b, readPos + 4),
                                BitConverter.ToSingle(b, readPos + 8),
                                BitConverter.ToSingle(b, readPos + 12));
                            properties[prop].SetValue(_objects[i], Quaternion.Slerp(from, to, t));
                            readPos += 16;
                        }

                        break;
                    }
                }
            }
        }
    }
}
