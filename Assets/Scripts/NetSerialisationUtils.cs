using System;
using System.Reflection;
using UnityEngine;

public class NetSerialisationUtils
{
    public static void WriteStruct(byte[] packet, object o)
    {
        WriteStruct(packet, o, 0);
    }

    public static int WriteStruct(byte[] packet, object o, int writeIndex)
    {
        int writePos = writeIndex;

        FieldInfo[] fields = o.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; ++i)
        {
            if (fields[i].FieldType == typeof(int))
            {
                int value = (int)fields[i].GetValue(o);
                BitConverter.GetBytes(value).CopyTo(packet, writePos);
                writePos += 4;
            }
            else if (fields[i].FieldType == typeof(float))
            {
                float value = (float)fields[i].GetValue(o);
                BitConverter.GetBytes(value).CopyTo(packet, writePos);
                writePos += 4;
            }
            else if (fields[i].FieldType == typeof(bool))
            {
                bool value = (bool)fields[i].GetValue(o);
                packet[writePos] = (byte)(value ? 1 : 0);
                ++writePos;
            }
            else if (!fields[i].FieldType.IsPrimitive)
            {
                int bytesWritten = WriteStruct(packet, fields[i].GetValue(o), writePos);
                writePos += bytesWritten;
            }
            else
            {
                Debug.LogError("Unhandled type " + fields[i].FieldType.FullName);
            }
        }

        return writePos - writeIndex;
    }

    public static T ReadStruct<T>(byte[] packet) where T : struct
    {
        return (T)ReadStruct(packet, typeof(T));
    }

    public static object ReadStruct(byte[] packet, Type type)
    {
        object o;
        ReadStruct(out o, packet, type, 0);
        return o;
    }

    public static int ReadStruct<T>(out T t, byte[] packet, int readIndex) where T : struct
    {
        object o;
        int bytesRead = ReadStruct(out o, packet, typeof(T), readIndex);
        t = (T)o;
        return bytesRead;
    }

    public static int ReadStruct(out object o, byte[] packet, Type type, int readIndex)
    {
        int readPos = readIndex;

        o = Activator.CreateInstance(type);

        FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        for (int i = 0; i < fields.Length; ++i)
        {
            if (fields[i].FieldType == typeof(int))
            {
                int value = BitConverter.ToInt32(packet, readPos);
                fields[i].SetValue(o, value);
                readPos += 4;
            }
            else if (fields[i].FieldType == typeof(float))
            {
                float value = BitConverter.ToSingle(packet, readPos);
                fields[i].SetValue(o, value);
                readPos += 4;
            }
            else if (fields[i].FieldType == typeof(bool))
            {
                fields[i].SetValue(o, packet[readPos] == 1 ? true : false);
                ++readPos;
            }
            else if (!fields[i].FieldType.IsPrimitive)
            {
                object value;
                int bytesRead = ReadStruct(out value, packet, fields[i].FieldType, readPos);
                fields[i].SetValue(o, value);
                readPos += bytesRead;
            }
            else
            {
                Debug.LogError("Unhandled type " + fields[i].FieldType.FullName);
            }
        }

        return readPos - readIndex;
    }
}
