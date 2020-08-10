using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class UnityCommonExtensions
{
    public static byte[] ToByteArray(this Vector4 v)
    {
        byte[] buff = new byte[16]; // 4 bytes per float

        Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buff, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buff, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, buff, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.w), 0, buff, 12, 4);
        return buff;
    }
    public static byte[] ToByteArray(this Quaternion v)
    {
        byte[] buff = new byte[16]; // 4 bytes per float

        Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buff, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buff, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, buff, 8, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.w), 0, buff, 12, 4);
        return buff;
    }
    public static byte[] ToByteArray(this Vector3 v)
    {
        byte[] buff = new byte[12]; // 4 bytes per float

        Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buff, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buff, 4, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.z), 0, buff, 8, 4);
        return buff;
    }
    public static byte[] ToByteArray(this Vector2 v)
    {
        byte[] buff = new byte[8]; // 4 bytes per float

        Buffer.BlockCopy(BitConverter.GetBytes(v.x), 0, buff, 0, 4);
        Buffer.BlockCopy(BitConverter.GetBytes(v.y), 0, buff, 4, 4);
        return buff;
    }

    public static Quaternion ToQuaternion(this byte[] buff)
    {
        Quaternion v = new Quaternion();
        v.x = BitConverter.ToSingle(buff, 0 * sizeof(float));
        v.y = BitConverter.ToSingle(buff, 1 * sizeof(float));
        v.z = BitConverter.ToSingle(buff, 2 * sizeof(float));
        v.w = BitConverter.ToSingle(buff, 3 * sizeof(float));
        return v;
    }
    public static Vector3 ToVector4(this byte[] buff)
    {
        Vector4 v = new Vector4();
        v.x = BitConverter.ToSingle(buff, 0 * sizeof(float));
        v.y = BitConverter.ToSingle(buff, 1 * sizeof(float));
        v.z = BitConverter.ToSingle(buff, 2 * sizeof(float));
        v.w = BitConverter.ToSingle(buff, 3 * sizeof(float));
        return v;
    }
    public static Vector3 ToVector3(this byte[] buff)
    {
        Vector3 v = new Vector3();
        v.x = BitConverter.ToSingle(buff, 0 * sizeof(float));
        v.y = BitConverter.ToSingle(buff, 1 * sizeof(float));
        v.z = BitConverter.ToSingle(buff, 2 * sizeof(float));
        return v;
    }
    public static Vector2 ToVector2(this byte[] buff)
    {
        Vector2 v = new Vector2();
        v.x = BitConverter.ToSingle(buff, 0 * sizeof(float));
        v.y = BitConverter.ToSingle(buff, 1 * sizeof(float));
        return v;
    }
}
