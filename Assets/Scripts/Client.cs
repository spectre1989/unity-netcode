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
            SnapshotHeaderMsg header = NetSerialisationUtils.ReadStruct<SnapshotHeaderMsg>(_snapshots[0]);
            
            _snapshotLerpT += (Time.deltaTime / header.snapshotDeltaTime);
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

            if (_tickId > 0)
            {
                (_objects[0] as NetPlayer).Pos = _predictionBuffer[(_tickId - 1) & PREDICTION_BUFFER_MASK].pos;
            }
        }

        float minTickDuration = 1.0f / MaxClientTickRate;

        if (_objects.Count > 0)
        {
            _tickAccumulator += Time.deltaTime;
            if (_tickAccumulator >= minTickDuration)
            {
                NetPlayer netPlayer = _objects[0] as NetPlayer;

                float yaw = netPlayer.transform.localEulerAngles.y + (Input.GetAxis("Mouse X") * netPlayer.MouseSensitivity);
                float pitch = netPlayer.Head.localEulerAngles.x + (-Input.GetAxis("Mouse Y") * netPlayer.MouseSensitivity);

                // normalise within range -180 < pitch <= +180 so we can clamp
                while (pitch > 180.0f) 
                {
                    pitch -= 360.0f;
                }
                while (pitch <= -180.0f)
                {
                    pitch += 360.0f;
                }

                ClientInputMsg msg = new ClientInputMsg();
                msg.tickId = _tickId;
                msg.dt = _tickAccumulator;
                msg.input.forward = Input.GetKey(KeyCode.W);
                msg.input.back = Input.GetKey(KeyCode.S);
                msg.input.left = Input.GetKey(KeyCode.A);
                msg.input.right = Input.GetKey(KeyCode.D);
                msg.input.pitch = pitch;
                msg.input.yaw = yaw;

                byte[] packet = new byte[1500];
                NetSerialisationUtils.WriteStruct(packet, msg);
                Server.ReceivePacket(packet, Latency);
                
                netPlayer.Move(msg.input, _tickAccumulator);

                PredictedMove predictedMove = new PredictedMove();
                predictedMove.input = msg.input;
                predictedMove.dt = _tickAccumulator;
                predictedMove.pos = netPlayer.Pos;

                _predictionBuffer[_tickId & PREDICTION_BUFFER_MASK] = predictedMove;

                _tickAccumulator = 0.0f;
                ++_tickId;
            }

            /*
            for (int i = 0; i < PREDICTION_BUFFER_SIZE; ++i)
            {
                if (_predictionBuffer[i].dt > 0.0f)
                {
                    Vector3 globalPos = this.transform.TransformPoint(_predictionBuffer[i].pos);
                    Debug.DrawLine(globalPos - Vector3.up, globalPos + Vector3.up);
                }
            }
            */

            if (hasNewSnapshots)
            {
                // TODO this is hardcoded to look for Pos property, need code gen to be used here

                byte[] snapshot = _snapshots[_snapshots.Count - 1];
                NetPlayer player = _objects[0] as NetPlayer;

                int readPos = 0;
                
                SnapshotHeaderMsg header;
                readPos += NetSerialisationUtils.ReadStruct(out header, snapshot, readPos);
                
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

                                if (Vector3.Distance(_predictionBuffer[header.clientTickId & PREDICTION_BUFFER_MASK].pos, pos) > 0.0001f)
                                {
                                    Debug.Log("CORRECTION");

                                    _predictionBuffer[header.clientTickId & PREDICTION_BUFFER_MASK].pos = pos;
                                    player.Pos = pos;
                                    for (int i = header.clientTickId + 1; i < _tickId; ++i)
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

    private void Interpolate(byte[] a, byte[] b, float t)
    {
        int readPos = 0;

        SnapshotHeaderMsg header;
        readPos += NetSerialisationUtils.ReadStruct(out header, a, 0);

        for (int i = 0; i < header.objectCount; ++i)
        {
            int prefabId = BitConverter.ToInt32(a, readPos);
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
