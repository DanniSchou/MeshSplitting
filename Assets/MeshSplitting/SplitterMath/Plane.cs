using UnityEngine;

public class Plane
{
    public Vector3 Point;
    public Vector3 Normal;

    public Plane()
    {
        Point = Vector3.zero;
        Normal = Vector3.up;
    }

    public Plane(Plane plane)
    {
        Point = plane.Point;
        Normal = plane.Normal;
    }

    public Plane(Transform transform)
    {
        Point = transform.position;
        Normal = transform.up;
    }

    public Plane(Vector3 point, Vector3 normal)
    {
        Point = point;
        Normal = normal;
    }

    public float LineIntersect(Vector3 lineStart, Vector3 lineEnd)
    {
        return Vector3.Dot(Normal, Point - lineStart) / Vector3.Dot(Normal, lineEnd - lineStart);
    }

    public float PointSide(Vector3 point)
    {
        return Vector3.Dot(Normal, point - Point);
    }

    public float PointSideNormalized(Vector3 point)
    {
        return Vector3.Dot(Normal, (point - Point).normalized);
    }
}
