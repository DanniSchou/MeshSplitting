using UnityEngine;

namespace MeshSplitting.SplitterMath
{
    public static class SplitterHelper
    {
        public static float Threshold = .00001f;

        /// <summary>
        /// Checks if two Vector2 are equal within a threshold
        /// </summary>
        /// <param name="vecA">Vector A</param>
        /// <param name="vecB">Vector B</param>
        /// <returns>true if they are equal</returns>
        public static bool CompareVector2(Vector2 vecA, Vector2 vecB)
        {
            return CompareVector2(ref vecA, ref vecB);
        }

        /// <summary>
        /// Checks if two Vector2 are equal within a threshold
        /// </summary>
        /// <param name="vecA">Vector A</param>
        /// <param name="vecB">Vector B</param>
        /// <returns>true if they are equal</returns>
        public static bool CompareVector2(ref Vector2 vecA, ref Vector2 vecB)
        {
            float dX = vecA.x - vecB.x;
            if (dX < Threshold && dX > -Threshold)
            {
                float dY = vecA.y - vecB.y;
                if (dY < Threshold && dY > -Threshold)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if two Vector3 are equal within a threshold
        /// </summary>
        /// <param name="vecA">Vector A</param>
        /// <param name="vecB">Vector B</param>
        /// <returns>true if they are equal</returns>
        public static bool CompareVector3(Vector3 vecA, Vector3 vecB)
        {
            return CompareVector3(ref vecA, ref vecB);
        }

        /// <summary>
        /// Checks if two Vector3 are equal within a threshold
        /// </summary>
        /// <param name="vecA">Vector A</param>
        /// <param name="vecB">Vector B</param>
        /// <returns>true if they are equal</returns>
        public static bool CompareVector3(ref Vector3 vecA, ref Vector3 vecB)
        {
            float dX = vecA.x - vecB.x;
            if (dX < Threshold && dX > -Threshold)
            {
                float dY = vecA.y - vecB.y;
                if (dY < Threshold && dY > -Threshold)
                {
                    float dZ = vecA.z - vecB.z;
                    if (dZ < Threshold && dZ > -Threshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public static float GetPlaneSide(PlaneMath plane, Vector3[] vertices)
        {
            float side = plane.PointSide(vertices[0]);
            if (side > Threshold && side < -Threshold)
            {
                side = plane.PointSide(vertices[1]);
                if (side > Threshold && side < -Threshold)
                {
                    side = plane.PointSide(vertices[2]);
                }
            }
            return side;
        }
    }
}