using System;
using UnityEngine;
using System.Collections.Generic;
using MeshSplitting.SplitterMath;

namespace MeshSplitting.MeshTools
{
    public class MeshSplitterConvex : IMeshSplitter
    {
        public bool UseCapUV = false;
        public bool CustomUV = false;
        public Vector2 CapUVMin = Vector2.zero;
        public Vector2 CapUVMax = Vector2.one;

        protected MeshContainer _mesh;
        protected PlaneMath _splitPlane;
        protected Quaternion _splitRotation;
        private Quaternion _ownRotation;

        public List<int> capInds;

        public MeshSplitterConvex(MeshContainer meshContainer, PlaneMath splitPlane, Quaternion splitRotation)
        {
            _mesh = meshContainer;
            _splitPlane = splitPlane;
            _splitRotation = splitRotation;
            _ownRotation = meshContainer.transform.rotation;

            capInds = new List<int>(meshContainer.vertexCount / 10);
        }

#if UNITY_EDITOR
        [NonSerialized]
        public bool ShowDebug = false;

        public void DebugDraw(bool debug)
        {
            ShowDebug = debug;
        }
#endif

        public void SetCapUV(bool useCapUV, bool customUV, Vector2 uvMin, Vector2 uvMax)
        {
            UseCapUV = useCapUV;
            CustomUV = customUV;
            CapUVMin = uvMin;
            CapUVMax = uvMax;
        }

        #region Mesh Split

        private int[] triIndicies = new int[3];
        private float[] lineLerp = new float[3];
        private bool[] lineHit = new bool[3];
        private Vector3[] triVertices = new Vector3[3];

        public void MeshSplit()
        {
            int triCount = _mesh.triangles.Length - 2;
            for (int triOffset = 0; triOffset < triCount; triOffset += 3)
            {
                triIndicies[0] = _mesh.triangles[triOffset];
                triIndicies[1] = _mesh.triangles[1 + triOffset];
                triIndicies[2] = _mesh.triangles[2 + triOffset];

                lineLerp[0] = _splitPlane.LineIntersect(_mesh.wsVerts[triIndicies[0]], _mesh.wsVerts[triIndicies[1]]);
                lineLerp[1] = _splitPlane.LineIntersect(_mesh.wsVerts[triIndicies[1]], _mesh.wsVerts[triIndicies[2]]);
                lineLerp[2] = _splitPlane.LineIntersect(_mesh.wsVerts[triIndicies[2]], _mesh.wsVerts[triIndicies[0]]);

                lineHit[0] = lineLerp[0] > 0f && lineLerp[0] < 1f;
                lineHit[1] = lineLerp[1] > 0f && lineLerp[1] < 1f;
                lineHit[2] = lineLerp[2] > 0f && lineLerp[2] < 1f;

                if (lineHit[0] || lineHit[1] || lineHit[2])
                {
                    if (lineHit[0] && lineHit[1])
                    {   // tri cut at 0 & 1
                        SplitTriangle(0);
                    }
                    else if (lineHit[1] && lineHit[2])
                    {   // tri cut at 1 & 2
                        SplitTriangle(1);
                    }
                    else if (lineHit[0] && lineHit[2])
                    {   // tri cut at 2 & 0
                        SplitTriangle(2);
                    }
                    else if (lineHit[1])
                    {   // try split between side 1 and vertex 0 
                        SplitTriangleAlternative(0);
                    }
                    else if (lineHit[2])
                    {   // try split between side 2 and vertex 1 
                        SplitTriangleAlternative(1);
                    }
                    else
                    {   // try split between side 0 and vertex 2 
                        SplitTriangleAlternative(2);
                    }
                }
                else
                {   // tri uncut
                    triVertices[0] = _mesh.wsVerts[triIndicies[0]];
                    triVertices[1] = _mesh.wsVerts[triIndicies[1]];
                    triVertices[2] = _mesh.wsVerts[triIndicies[2]];

                    if (SplitterHelper.GetPlaneSide(_splitPlane, triVertices) > 0f)
                    {
                        _mesh.trisUp.Add(triIndicies[0]);
                        _mesh.trisUp.Add(triIndicies[1]);
                        _mesh.trisUp.Add(triIndicies[2]);
#if UNITY_EDITOR
                        if (ShowDebug)
                            DrawDebugTri(triIndicies, Color.cyan);
#endif
                    }
                    else
                    {
                        _mesh.trisDown.Add(triIndicies[0]);
                        _mesh.trisDown.Add(triIndicies[1]);
                        _mesh.trisDown.Add(triIndicies[2]);
#if UNITY_EDITOR
                        if (ShowDebug)
                            DrawDebugTri(triIndicies, Color.magenta);
#endif
                    }
                }
            }
        }

        private int[] smallTri = new int[3];
        private int[] bigTri = new int[6];

        private void SplitTriangle(int offset)
        {
            int i0 = offset % 3;
            int i1 = (1 + offset) % 3;
            int i2 = (2 + offset) % 3;

            int indexHit0 = _mesh.AddLerpVertex(triIndicies[i0], triIndicies[i1], lineLerp[i0]);
            int indexHit1 = _mesh.AddLerpVertex(triIndicies[i1], triIndicies[i2], lineLerp[i1]);

            AddCapIndex(indexHit0);
            AddCapIndex(indexHit1);

            smallTri[0] = indexHit0;
            smallTri[1] = triIndicies[i1];
            smallTri[2] = indexHit1;

            bigTri[0] = triIndicies[i0];
            bigTri[1] = indexHit0;
            bigTri[2] = indexHit1;
            bigTri[3] = triIndicies[i0];
            bigTri[4] = indexHit1;
            bigTri[5] = triIndicies[i2];

            if (_splitPlane.PointSide(_mesh.wsVerts[triIndicies[i1]]) > 0f)
            {
                _mesh.trisUp.Add(smallTri[0]);
                _mesh.trisUp.Add(smallTri[1]);
                _mesh.trisUp.Add(smallTri[2]);

                _mesh.trisDown.Add(bigTri[0]);
                _mesh.trisDown.Add(bigTri[1]);
                _mesh.trisDown.Add(bigTri[2]);
                _mesh.trisDown.Add(bigTri[3]);
                _mesh.trisDown.Add(bigTri[4]);
                _mesh.trisDown.Add(bigTri[5]);
#if UNITY_EDITOR
                if (ShowDebug)
                {
                    DrawDebugTri(smallTri, Color.cyan);
                    DrawDebugTriDouble(bigTri, Color.magenta);
                }
#endif
            }
            else
            {
                _mesh.trisDown.Add(smallTri[0]);
                _mesh.trisDown.Add(smallTri[1]);
                _mesh.trisDown.Add(smallTri[2]);

                _mesh.trisUp.Add(bigTri[0]);
                _mesh.trisUp.Add(bigTri[1]);
                _mesh.trisUp.Add(bigTri[2]);
                _mesh.trisUp.Add(bigTri[3]);
                _mesh.trisUp.Add(bigTri[4]);
                _mesh.trisUp.Add(bigTri[5]);
#if UNITY_EDITOR
                if (ShowDebug)
                {
                    DrawDebugTri(smallTri, Color.magenta);
                    DrawDebugTriDouble(bigTri, Color.cyan);
                }
#endif
            }
        }

        private void SplitTriangleAlternative(int offset)
        {
            Debug.Log("alt tri split");
            int i0 = offset % 3;
            int i1 = (1 + offset) % 3;
            int i2 = (2 + offset) % 3;

            int indexHit = _mesh.AddLerpVertex(triIndicies[i0], triIndicies[i1], lineLerp[i0]);

            AddCapIndex(indexHit);

            smallTri[0] = triIndicies[i0];
            smallTri[1] = indexHit;
            smallTri[2] = triIndicies[i2];

            bigTri[0] = indexHit;
            bigTri[1] = triIndicies[i1];
            bigTri[2] = triIndicies[i2];

            if (_splitPlane.PointSide(_mesh.wsVerts[triIndicies[i0]]) > 0f)
            {
                _mesh.trisUp.Add(smallTri[0]);
                _mesh.trisUp.Add(smallTri[1]);
                _mesh.trisUp.Add(smallTri[2]);

                _mesh.trisDown.Add(bigTri[0]);
                _mesh.trisDown.Add(bigTri[1]);
                _mesh.trisDown.Add(bigTri[2]);
#if UNITY_EDITOR
                //if (ShowDebug)
                {
                    DrawDebugTri(smallTri, Color.cyan);
                    DrawDebugTri(bigTri, Color.magenta);
                }
#endif
            }
            else
            {
                _mesh.trisDown.Add(smallTri[0]);
                _mesh.trisDown.Add(smallTri[1]);
                _mesh.trisDown.Add(smallTri[2]);

                _mesh.trisUp.Add(bigTri[0]);
                _mesh.trisUp.Add(bigTri[1]);
                _mesh.trisUp.Add(bigTri[2]);
#if UNITY_EDITOR
                //if (ShowDebug)
                {
                    DrawDebugTri(smallTri, Color.magenta);
                    DrawDebugTri(bigTri, Color.cyan);
                }
#endif
            }
        }

        private void AddCapIndex(int index)
        {
            int newIndex = index - _mesh.vertexCount;
            Vector3 compVec = _mesh.verticesNew[newIndex];
            int capCount = capInds.Count;
            for (int k = 0; k < capCount; k++)
            {
                int i = capInds[k];
                int j = i;
                if (i >= _mesh.vertexCount) j -= _mesh.vertexCount;
                if (SplitterHelper.CompareVector3(_mesh.verticesNew[j], compVec))
                    return;
            }

            capInds.Add(index);
        }

#if UNITY_EDITOR
        private void DrawDebugTri(int[] tri, Color color)
        {
            Debug.DrawLine(GetWSPos(tri[0]), GetWSPos(tri[1]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[1]), GetWSPos(tri[2]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[2]), GetWSPos(tri[0]), color, 2f);
        }

        private void DrawDebugTriDouble(int[] tri, Color color)
        {
            Debug.DrawLine(GetWSPos(tri[0]), GetWSPos(tri[1]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[1]), GetWSPos(tri[2]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[2]), GetWSPos(tri[0]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[3]), GetWSPos(tri[4]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[4]), GetWSPos(tri[5]), color, 2f);
            Debug.DrawLine(GetWSPos(tri[5]), GetWSPos(tri[3]), color, 2f);
        }

        public Vector3 GetWSPos(int index)
        {
            if (index >= _mesh.vertexCount)
            {
                index -= _mesh.vertexCount;
                return _mesh.wsVertsNew[index];
            }

            return _mesh.wsVerts[index];
        }
#endif
        #endregion

        #region Mesh Caps

        private static Vector2 Vector2Up = Vector2.up;
        protected int[] capsSorted;
        protected Vector2[] capsUV;

        public void MeshCreateCaps()
        {
            if (capInds.Count == 0)
                return;

            CreateCap();

            int newCapCount = capsSorted.Length;
            if (CustomUV)
            {
                float offX = CapUVMin.x, offY = CapUVMin.y;
                float dX = CapUVMax.x - CapUVMin.x, dY = CapUVMax.y - CapUVMin.y;
                for (int i = 0; i < newCapCount; i++)
                {
                    capsUV[i].x = (capsUV[i].x * dX) + offX;
                    capsUV[i].y = (capsUV[i].y * dY) + offY;
                }
            }

            //create cap verticies
            Vector3 normal = Quaternion.Inverse(_ownRotation) * _splitPlane.Normal;
            Vector3 invNormal = -normal;
            int[] capUpperOrder = new int[capsSorted.Length];
            int[] capLowerOrder = new int[capsSorted.Length];
            if (UseCapUV)
            {
                for (int i = 0; i < newCapCount; i++)
                {
                    capUpperOrder[i] = _mesh.AddCapVertex(capsSorted[i], invNormal, capsUV[i]);
                    capLowerOrder[i] = _mesh.AddCapVertex(capsSorted[i], normal, capsUV[i]);
                }
            }
            else
            {
                for (int i = 0; i < newCapCount; i++)
                {
                    capUpperOrder[i] = _mesh.AddCapVertex(capsSorted[i], invNormal);
                    capLowerOrder[i] = _mesh.AddCapVertex(capsSorted[i], normal);
                }
            }

            int capOderCount = capUpperOrder.Length;
            for (int i = 2; i < capOderCount; i++)
            {
                _mesh.trisUp.Add(capUpperOrder[0]);
                _mesh.trisUp.Add(capUpperOrder[i - 1]);
                _mesh.trisUp.Add(capUpperOrder[i]);

                _mesh.trisDown.Add(capLowerOrder[0]);
                _mesh.trisDown.Add(capLowerOrder[i]);
                _mesh.trisDown.Add(capLowerOrder[i - 1]);
            }

#if UNITY_EDITOR
            // debug draw
            if (ShowDebug)
            {
                for (int i = 2; i < capUpperOrder.Length; i++)
                {
                    Debug.DrawLine(GetVertPos(capUpperOrder[0]), GetVertPos(capUpperOrder[i - 1]), Color.yellow, 2f);
                    Debug.DrawLine(GetVertPos(capUpperOrder[i - 1]), GetVertPos(capUpperOrder[i]), Color.yellow, 2f);
                    Debug.DrawLine(GetVertPos(capUpperOrder[i]), GetVertPos(capUpperOrder[0]), Color.yellow, 2f);
                }
            }
#endif
        }

#if UNITY_EDITOR
        public Vector3 GetVertPos(int index)
        {
            if (index >= _mesh.vertexCount)
            {
                index -= _mesh.vertexCount;
                return _mesh.verticesNew[index];
            }

            return _mesh.vertices[index];
        }
#endif

        private void CreateCap()
        {
            Quaternion invRotation = Quaternion.Inverse(_splitRotation);
            int capCount = capInds.Count;
            Vector3 pos = _mesh.transform.position;
            Vector2[] rotVerts = new Vector2[capCount];
            for (int i = 0; i < capCount; i++)
            {
                Vector3 vec = capInds[i] < _mesh.vertexCount ? _mesh.wsVerts[capInds[i]] : _mesh.wsVertsNew[capInds[i] - _mesh.vertexCount];
                vec = invRotation * (vec - pos);
                rotVerts[i] = new Vector2(vec.x, vec.z);
            }

            // init sorting
            int[] sorted = new int[capCount];
            for (int i = 0; i < capCount; i++)
            {
                sorted[i] = i;
            }

            // find start point
            int lowestIndex = 0;
            Vector2 lowestVec = rotVerts[sorted[lowestIndex]];
            for (int i = 1; i < capCount; i++)
            {
                if (SortLowY(rotVerts[sorted[i]], lowestVec))
                {
                    lowestIndex = i;
                    lowestVec = rotVerts[sorted[lowestIndex]];
                }
            }
            if (lowestIndex != 0)
                Swap(sorted, 0, lowestIndex);

            // calc angles
            float angleSensitivity = 90f * 10f;
            int[] angles = new int[capCount];
            Vector2 sVec = rotVerts[sorted[0]];
            for (int i = 1; i < capCount; i++)
            {
                Vector2 vec = rotVerts[sorted[i]];
                float dot = Vector2.Dot(Vector2Up, (vec - sVec).normalized);
                if (sVec.x <= vec.x)
                {
                    angles[sorted[i]] = (int)(dot * angleSensitivity);
                }
                else
                {
                    angles[sorted[i]] = (int)((2f - dot) * angleSensitivity);
                }

                if (angles[sorted[i]] < 0) angles[sorted[i]] = 0;
            }
            angles[sorted[0]] = -1;

            // sorting
            GnomeSort(sorted, angles);
            SortEvenStart(sorted, angles, rotVerts);
            SortEvenEnd(sorted, angles, rotVerts);

            // Remove redundant verts
            float invThreshold = 1f - SplitterHelper.Threshold;
            int sortCount = sorted.Length;
            int sortSize = sortCount;
            int sort0 = 0, sort1 = 1 % sortCount, sort2 = 2 % sortCount;
            while (true)
            {
                Vector2 vec01 = rotVerts[sorted[sort1]] - rotVerts[sorted[sort0]];
                Vector2 vec12 = rotVerts[sorted[sort2]] - rotVerts[sorted[sort1]];

                if (Vector2.Dot(vec01.normalized, vec12.normalized) > invThreshold)
                {
                    sorted[sort1] = -1;
                    sortSize--;
                }
                else
                {
                    sort0 = sort1;
                }

                if (sort2 == 0)
                    break;

                sort1 = sort2;
                sort2 = (sort2 + 1) % sortCount;
            }

            capsSorted = new int[sortSize];
            int capsIndex = 0;
            for (int i = 0; i < sortCount && capsIndex < sortSize; i++)
            {
                int sortIndex = sorted[i];
                if (sortIndex >= 0)
                {
                    capsSorted[capsIndex++] = capInds[sortIndex];
                }
            }

            if (UseCapUV)
            {
                capsUV = new Vector2[sortSize];
                Vector2 minBounds = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 maxBounds = new Vector2(float.MinValue, float.MinValue);

                for (int i = 0; i < sortCount; i++)
                {
                    int sortIndex = sorted[i];
                    if (sortIndex >= 0)
                    {
                        Vector2 vert = rotVerts[sortIndex];

                        if (minBounds.x > vert.x)
                            minBounds.x = vert.x;
                        else if (maxBounds.x < vert.x)
                            maxBounds.x = vert.x;

                        if (minBounds.y > vert.y)
                            minBounds.y = vert.y;
                        else if (maxBounds.y < vert.y)
                            maxBounds.y = vert.y;
                    }
                }

                float dX = maxBounds.x - minBounds.x, dY = maxBounds.y - minBounds.y;
                capsIndex = 0;
                for (int i = 0; i < sortCount && capsIndex < sortSize; i++)
                {
                    int sortIndex = sorted[i];
                    if (sortIndex >= 0)
                    {
                        Vector2 vert = rotVerts[sortIndex];
                        capsUV[capsIndex++] = new Vector2((vert.x - minBounds.x) / dX, (vert.y - minBounds.y) / dY);
                    }
                }
            }
        }

        private void Swap(int[] array, int a, int b)
        {
            int tmp = array[a];
            array[a] = array[b];
            array[b] = tmp;
        }

        private bool SortLowY(Vector2 a, Vector2 b)
        {
            if (a.y > b.y)
                return false;
            else if (a.y < b.y)
                return true;
            else if (a.x < b.x)
                return true;

            return false;
        }

        private void GnomeSort(int[] index, int[] value)
        {
            int pos = 1;
            int count = index.Length;
            while (pos < count)
            {
                if (value[index[pos]] >= value[index[pos - 1]])
                    pos++;
                else
                {
                    Swap(index, pos, pos - 1);
                    if (pos > 1)
                        pos--;
                    else
                        pos++;
                }
            }
        }

        private void SortEvenStart(int[] index, int[] value, Vector2[] localVerts)
        {
            int pos = 2;
            int count = index.Length;
            while (pos < count)
            {
                if (value[index[pos]] == value[index[pos - 1]])
                {
                    Vector2 vecPos1 = localVerts[index[pos - 1]];
                    Vector2 vecPos2 = localVerts[index[pos]];

                    if (vecPos1.y > vecPos2.y || (vecPos1.x > vecPos2.x && vecPos1.y == vecPos2.y))
                    {
                        Swap(index, pos, pos - 1);
                        if (pos > 2)
                            pos--;
                        else
                            pos++;
                    }
                    else
                        pos++;
                }
                else
                    break;
            }
        }

        private void SortEvenEnd(int[] index, int[] value, Vector2[] localVerts)
        {
            int count = index.Length;
            int pos = count - 2;
            while (pos > 0)
            {
                if (value[index[pos]] == value[index[pos + 1]])
                {
                    Vector2 vecPos1 = localVerts[index[pos]];
                    Vector2 vecPos2 = localVerts[index[pos + 1]];

                    if (vecPos1.y < vecPos2.y)
                    {
                        Swap(index, pos, pos + 1);
                        if (pos < count - 2)
                            pos++;
                        else
                            pos--;
                    }
                    else
                        pos--;
                }
                else
                    break;
            }
        }
        #endregion
    }
}
