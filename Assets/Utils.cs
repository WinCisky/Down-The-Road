using System.Collections.Generic;
using UnityEngine;

public class Utils
{

    public static void Normalize(ref float x, ref float y)
    {
        float invLen = 1f / Mathf.Sqrt(x * x + y * y);
        x *= invLen;
        y *= invLen;
    }

    public static float DistanceSquared(Vector2 a, Vector2 b)
    {
        float dx = b.x - a.x;
        float dy = b.y - a.y;
        return dx * dx + dy * dy;
    }

    public static float Dot(Vector2 a, Vector2 b)
    {
        return a.x * b.x + a.y * b.y;
    }

    public static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    public static T GetItem<T>(List<T> list, int index)
    {
        if (index >= list.Count)
        {
            return list[index % list.Count];
        }
        else if (index < 0)
        {
            return list[list.Count + index];
        }
        else
        {
            return list[index];
        }
    }
}
