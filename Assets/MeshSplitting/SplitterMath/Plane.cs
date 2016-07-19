using UnityEngine;

namespace MeshSplitting.SplitterMath
{
    public class PlaneMath
    {
        public Vector3 Point;
        public Vector3 Normal;

        public PlaneMath()
        {
            Point = Vector3.zero;
            Normal = Vector3.up;
        }

        public PlaneMath(PlaneMath plane)
        {
            Point = plane.Point;
            Normal = plane.Normal;
        }

        public PlaneMath(Transform transform)
        {
            Point = transform.position;
            Normal = transform.up;
        }

        public PlaneMath(Vector3 point, Vector3 normal)
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
}
