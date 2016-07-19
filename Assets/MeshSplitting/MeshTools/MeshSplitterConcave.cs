using System;
using UnityEngine;
using System.Collections.Generic;
using MeshSplitting.SplitterMath;

namespace MeshSplitting.MeshTools
{
    public class MeshSplitterConcave : IMeshSplitter
    {
        protected class Edge
        {
            private readonly static Vector2 _zero = Vector2.zero;
            private readonly static Quaternion _cw90 = Quaternion.AngleAxis(90f, Vector3.forward);

            private Vector2 _left = Vector2.zero;
            private Vector2 _right = Vector2.zero;
            private Vector2 _normal = Vector2.zero;

            public int IndexLeft;
            public int IndexRight;
            public Vector2 UVLeft;
            public Vector2 UVRight;

            public Vector2 Left
            {
                get { return _left; }
                set
                {
                    _left = value;
                    _normal = _zero;
                }
            }

            public Vector2 Right
            {
                get { return _right; }
                set
                {
                    _right = value;
                    _normal = _zero;
                }
            }

            public Vector2 Normal
            {
                get
                {
                    if (_normal == _zero)
                        CalculateNormal();
                    return _normal;
                }
            }

            public void CalculateNormal()
            {
                Vector3 right = new Vector3(Right.x, Right.y);
                Vector3 left = new Vector3(Left.x, Left.y);
                Vector3 normal = _cw90 * (right - left);   // rotate 90 degrees clockwise around z axis
                normal.Normalize();
                _normal.Set(normal.x, normal.y);
            }

            public bool EdgeIntersect(Edge other)
            {
                // x1 = Left.x          y1 = Left.y
                // x2 = Right.x         y2 = Right.y
                // x3 = edge.Left.x     y3 = edge.Left.y
                // x4 = edge.Right.x    y4 = edge.Right.y
                //
                //       (x4 - x3) * (y1 - y3) - (y4 - y3) * (x1 - x3)
                // u_a = ---------------------------------------------
                //       (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1)
                //
                //       (x2 - x1) * (y1 - y3) - (y2 - y1) * (x1 - x3)
                // u_b = ---------------------------------------------
                //       (y4 - y3) * (x2 - x1) - (x4 - x3) * (y2 - y1)

                float denominator = ((other.Right.y - other.Left.y) * (Right.x - Left.x) -
                                     (other.Right.x - other.Left.x) * (Right.y - Left.y));

                float u_a = ((other.Right.x - other.Left.x) * (Left.y - other.Left.y) -
                             (other.Right.y - other.Left.y) * (Left.x - other.Left.x))
                                                          /
                                                    denominator;

                float u_b = ((Right.x - Left.x) * (Left.y - other.Left.y) -
                             (Right.y - Left.y) * (Left.x - other.Left.x))
                                                /
                                            denominator;

                return u_a > .00001f && u_a < .99999f && u_b > .00001f && u_b < .99999f;
            }

            public void Flip()
            {
                int indexTmp = IndexLeft;
                IndexLeft = IndexRight;
                IndexRight = indexTmp;

                Vector2 tmp = Left;
                _left = _right;
                Right = tmp;

                tmp = UVLeft;
                UVLeft = UVRight;
                UVRight = tmp;
            }

            public bool SameVectors(Edge other)
            {
                if (!SplitterHelper.CompareVector2(ref _left, ref other._left))
                    return false;
                if (!SplitterHelper.CompareVector2(ref _right, ref other._right))
                    return false;

                return true;
            }

            public static Edge MeltEdges(Edge edgeLeft, Edge edgeRight)
            {
                Edge meltedEdge = new Edge();

                meltedEdge.IndexLeft = edgeLeft.IndexLeft;
                meltedEdge.Left = edgeLeft.Left;
                meltedEdge.UVLeft = edgeLeft.UVLeft;

                meltedEdge.IndexRight = edgeRight.IndexRight;
                meltedEdge.Right = edgeRight.Right;
                meltedEdge.UVRight = edgeRight.UVRight;

                return meltedEdge;
            }

            public static Edge CloseEdges(Edge edgeLeft, Edge edgeRight)
            {
                Edge meltedEdge = new Edge();

                meltedEdge.IndexLeft = edgeRight.IndexRight;
                meltedEdge.Left = edgeRight.Right;
                meltedEdge.UVLeft = edgeRight.UVRight;

                meltedEdge.IndexRight = edgeLeft.IndexLeft;
                meltedEdge.Right = edgeLeft.Left;
                meltedEdge.UVRight = edgeLeft.UVLeft;

                return meltedEdge;
            }
        }

        public bool UseCapUV = false;
        public bool CustomUV = false;
        public Vector2 CapUVMin = Vector2.zero;
        public Vector2 CapUVMax = Vector2.one;

        protected MeshContainer _mesh;
        protected PlaneMath _splitPlane;
        protected Quaternion _splitRotation;
        private Quaternion _ownRotation;

        private List<Edge> _edges;

        public MeshSplitterConcave(MeshContainer meshContainer, PlaneMath splitPlane, Quaternion splitRotation)
        {
            _mesh = meshContainer;
            _splitPlane = splitPlane;
            _splitRotation = splitRotation;
            _ownRotation = meshContainer.transform.rotation;

            _edges = new List<Edge>(meshContainer.vertexCount / 10);
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
                    if (!lineHit[2])
                    {   // tri cut at 0 & 1
                        SplitTriangle(triIndicies, lineLerp, 0);
                    }
                    else if (!lineHit[0])
                    {   // tri cut at 1 & 2
                        SplitTriangle(triIndicies, lineLerp, 1);
                    }
                    else
                    {   // tri cut at 2 & 0
                        SplitTriangle(triIndicies, lineLerp, 2);
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

        private void SplitTriangle(int[] triIndicies, float[] lineLerp, int offset)
        {
            int i0 = (0 + offset) % 3;
            int i1 = (1 + offset) % 3;
            int i2 = (2 + offset) % 3;

            int indexHit0 = _mesh.AddLerpVertex(triIndicies[i0], triIndicies[i1], lineLerp[i0]);
            int indexHit1 = _mesh.AddLerpVertex(triIndicies[i1], triIndicies[i2], lineLerp[i1]);

            AddEdge(indexHit0, indexHit1);

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

        private void AddEdge(int index0, int index1)
        {
            int vCount = _mesh.vertexCount;

            Edge edge = new Edge();
            edge.IndexLeft = index0;
            edge.IndexRight = index1;
            Vector3 vecL = _mesh.wsVertsNew[index0 - vCount];
            Vector3 vecR = _mesh.wsVertsNew[index1 - vCount];

            if (SplitterHelper.CompareVector3(ref vecL, ref vecR))
                return;

            _edges.Add(edge);

            //Debug drawing
            //#if UNITY_EDITOR
            //        float normalLength = .1f;
            //        Debug.DrawLine(edge.Vector0, edge.Vector1, Color.yellow, 5f);
            //        Vector3 center = Vector3.Lerp(edge.Vector0, edge.Vector1, .5f);
            //        Debug.DrawLine(center, center + edge.Normal * normalLength, Color.cyan, 5f);
            //        Debug.DrawLine(edge.Vector0, edge.Vector0 + _mesh.normalsNew[index0] * normalLength, Color.magenta, 5f);
            //        Debug.DrawLine(edge.Vector1, edge.Vector1 + _mesh.normalsNew[index1] * normalLength, Color.magenta, 5f);
            //#endif
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

        private LinkedList<LinkedList<int>> linkedBorders = new LinkedList<LinkedList<int>>();
        private List<int> triList;
        private List<Vector2> uvList;

        public void MeshCreateCaps()
        {
            if (_edges.Count == 0)
                return;

            triList = new List<int>(_mesh.vertexCount / 4);
            uvList = new List<Vector2>(_mesh.vertexCount / 4);

            // calculates 2D vectors (eliminates rotation)
            CalculateRotatedEdges();

            // link edges together as borders
            LinkEdges();

            // check and flip normals
            CheckNormals();

            // TODO: check for inner borders and link with outer borders

            // Setup UV data for each border
            if (UseCapUV) CreateUVs();

            CreateTriangles();

            AddTrianglesToMesh();
        }

        private void CalculateRotatedEdges()
        {
            Quaternion invRotation = Quaternion.Inverse(_splitRotation);
            int edgeCount = _edges.Count;
            int vCount = _mesh.vertexCount;

            for (int i = 0; i < edgeCount; i++)
            {
                Edge edge = _edges[i];
                Vector3 vecL = _mesh.wsVertsNew[edge.IndexLeft - vCount];
                Vector3 vecR = _mesh.wsVertsNew[edge.IndexRight - vCount];
                vecL = invRotation * vecL;
                vecR = invRotation * vecR;

                edge.Left = new Vector2(vecL.x, vecL.z);
                edge.Right = new Vector2(vecR.x, vecR.z);
            }
        }

        #region Link Edges
        private void LinkEdges()
        {
            int rotCount = _edges.Count;

            // Try to link borders together clockwise
            LinkedList<LinkedList<int>> edgeHolder = new LinkedList<LinkedList<int>>();
            for (int i = 0; i < rotCount; i++)
            {
                LinkedList<int> list = new LinkedList<int>();
                list.AddLast(i);
                edgeHolder.AddLast(list);
            }
            //Debug.Log("Edge count: " + edgeHolder.Count);

            LinkedListNode<LinkedList<int>> currentNode = edgeHolder.First;
            LinkedListNode<LinkedList<int>> testNode = currentNode.Next;

            while (edgeHolder.Count > 0)
            {
                Vector2 curLastLeft = _edges[currentNode.Value.Last.Value].Left;
                Edge testFirst = _edges[testNode.Value.First.Value];
                Vector2 testFirstRight = testFirst.Right;
                Vector2 testFirstLeft = testFirst.Left;
                bool otherTest = SplitterHelper.CompareVector2(ref curLastLeft, ref testFirstRight);
                bool sameTest = !otherTest ? SplitterHelper.CompareVector2(ref curLastLeft, ref testFirstLeft) : false;

                if (otherTest || sameTest)
                {
                    if (otherTest)
                        AttachLinkedList(testNode.Value, currentNode.Value);
                    else
                        AttachLinkedListFlip(testNode.Value, currentNode.Value);

                    edgeHolder.Remove(testNode);
                    testNode = LLCircularNext<LinkedList<int>>(currentNode);

                    Vector2 curFirstRight = _edges[currentNode.Value.First.Value].Right;
                    if (SplitterHelper.CompareVector2(ref curLastLeft, ref curFirstRight))
                    {
                        LinkedList<int> tmpList = new LinkedList<int>();
                        AttachLinkedList(currentNode.Value, tmpList);

                        linkedBorders.AddLast(tmpList);
                        edgeHolder.Remove(currentNode);

                        if (edgeHolder.Count == 0)
                            break;

                        currentNode = testNode;
                        testNode = LLCircularNext<LinkedList<int>>(currentNode);
                    }
                }
                else
                {
                    testNode = LLCircularNext<LinkedList<int>>(testNode);
                }

                if (currentNode == testNode)
                {
                    if (currentNode == edgeHolder.Last)
                    {
                        break;
                    }

                    currentNode = LLCircularNext<LinkedList<int>>(currentNode);
                    testNode = LLCircularNext<LinkedList<int>>(currentNode);
                }
            }

            //Debug.Log("End edge count: " + edgeHolder.Count);

            if (edgeHolder.Count > 0)
            {
                foreach (LinkedList<int> edgeList in edgeHolder) // if edgeList has 2 or more edges then we could close off the uncompleted border
                {
                    Edge edgeFirst = _edges[edgeList.First.Value];
                    Edge edgeLast = _edges[edgeList.Last.Value];

                    if (edgeList.Count > 2)
                    {
                        Edge newEdge = new Edge();
                        newEdge.IndexLeft = edgeFirst.IndexRight;
                        newEdge.Left = edgeFirst.Right;
                        newEdge.IndexRight = edgeLast.IndexLeft;
                        newEdge.Right = edgeLast.Left;

                        edgeList.AddLast(_edges.Count);
                        _edges.Add(newEdge);

                        linkedBorders.AddLast(edgeList);
                    }

                    //Debug.Log("First: " + edgeFirst.Right + " Last: " + edgeLast.Left);
                }
            }

            //Debug drawing
            //#if UNITY_EDITOR
            //        Debug.Log("Border Count: " + linkedBorders.Count);

            //        foreach (LinkedList<int> border in linkedBorders)
            //        //for (int i = 0; i < borders.Count; i++)
            //        {
            //            //int[] border = borders[i];
            //            Debug.Log("Border Edge Count: " + border.Count);
            //            foreach (int index in border)
            //            //for (int j = 0; j < border.Length; j++)
            //            {
            //                //Debug.DrawLine(rotEdges[border[j]].Left, rotEdges[border[j]].Right, Color.white * i / borders.Count, 5f);
            //                Debug.DrawLine(rotEdges[index].Left, rotEdges[index].Right, Color.white, 5f);
            //            }
            //        }
            //#endif
        }

        private void AttachLinkedList(LinkedList<int> source, LinkedList<int> destination)
        {
            foreach (int edgeIndex in source)
            {
                destination.AddLast(edgeIndex);
            }
        }

        private void AttachLinkedListFlip(LinkedList<int> source, LinkedList<int> destination)
        {
            foreach (int edgeIndex in source)
            {
                _edges[edgeIndex].Flip();
                destination.AddLast(edgeIndex);
            }
        }
        #endregion

        private void CheckNormals()
        {
            foreach (LinkedList<int> border in linkedBorders)
            {
                // find top index
                LinkedListNode<int> topRightNode = border.First;
                LinkedListNode<int> currentNode = topRightNode;
                while ((currentNode = currentNode.Next) != null)
                {
                    if (_edges[currentNode.Value].Right.y > _edges[topRightNode.Value].Right.y)
                    {
                        topRightNode = currentNode;
                    }
                }

                // check normals
                int next = LLCircularNext<int>(topRightNode).Value;
                Vector2 topRight = _edges[topRightNode.Value].Right;
                Vector2 testLeft = _edges[next].Left;
                if (!SplitterHelper.CompareVector2(ref topRight, ref testLeft))
                {
                    next = LLCircularPrevious<int>(topRightNode).Value;
                }

                if (TestInnerSide(_edges[topRightNode.Value], _edges[next], true) < 0)
                {
                    foreach (int edgeIndex in border)
                    {
                        _edges[edgeIndex].Flip();

#if UNITY_EDITOR
                        if (ShowDebug)
                        {
                            Edge rEdge = _edges[edgeIndex];
                            Vector3 center = Vector3.Lerp(rEdge.Left, rEdge.Right, .5f);
                            Debug.DrawLine(center, center + (Vector3)rEdge.Normal, Color.cyan, 5f);
                            Debug.DrawLine(rEdge.Left, center, Color.magenta, 5f);
                            Debug.DrawLine(center, rEdge.Right, Color.yellow, 5f);
                        }
#endif
                    }
                }
#if UNITY_EDITOR
                else
                {
                    if (ShowDebug)
                    {
                        foreach (int edgeIndex in border)
                        {
                            Edge rEdge = _edges[edgeIndex];
                            Vector3 center = Vector3.Lerp(rEdge.Left, rEdge.Right, .5f);
                            Debug.DrawLine(center, center + (Vector3)rEdge.Normal, Color.blue, 5f);
                            Debug.DrawLine(rEdge.Left, center, Color.red, 5f);
                            Debug.DrawLine(center, rEdge.Right, Color.green, 5f);
                        }
                    }
                }
#endif
            }
        }

        private void CreateUVs()
        {
            foreach (LinkedList<int> border in linkedBorders)
            {
                Vector2 minBounds = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 maxBounds = new Vector2(float.MinValue, float.MinValue);

                LinkedListNode<int> currentNode = border.First;
                do
                {
                    Vector2 vert = _edges[currentNode.Value].Left;

                    if (minBounds.x > vert.x)
                        minBounds.x = vert.x;
                    else if (maxBounds.x < vert.x)
                        maxBounds.x = vert.x;

                    if (minBounds.y > vert.y)
                        minBounds.y = vert.y;
                    else if (maxBounds.y < vert.y)
                        maxBounds.y = vert.y;
                }
                while ((currentNode = currentNode.Next) != null);

                Vector2 dBounds = maxBounds - minBounds;
                Vector2 dCustom = CapUVMax - CapUVMin;

                currentNode = border.First;
                do
                {
                    Edge edge = _edges[currentNode.Value];

                    edge.UVLeft.Set((edge.Left.x - minBounds.x) / dBounds.x, (edge.Left.y - minBounds.y) / dBounds.y);
                    edge.UVRight.Set((edge.Right.x - minBounds.x) / dBounds.x, (edge.Right.y - minBounds.y) / dBounds.y);

                    if (CustomUV)
                    {
                        edge.UVLeft.Set((edge.UVLeft.x * dCustom.x) + CapUVMin.x, (edge.UVLeft.y * dCustom.y) + CapUVMin.y);
                        edge.UVRight.Set((edge.UVRight.x * dCustom.x) + CapUVMin.x, (edge.UVRight.y * dCustom.y) + CapUVMin.y);
                    }

                    //Debug.DrawLine(edge.UVLeft, edge.UVRight, Color.cyan, 5f);
                    //Debug.DrawLine(edge.Left, edge.Right, Color.magenta, 5f);
                    //Debug.Log(edge.UVLeft.ToString("0.##") + " " + edge.UVRight.ToString("0.##"));
                }
                while ((currentNode = currentNode.Next) != null);

                //Debug.DrawLine(Vector2.zero, new Vector2(0, 1), Color.yellow, 5f);
                //Debug.DrawLine(Vector2.zero, new Vector2(1, 0), Color.yellow, 5f);
                //Debug.DrawLine(Vector2.one, new Vector2(1, 0), Color.yellow, 5f);
                //Debug.DrawLine(Vector2.one, new Vector2(0, 1), Color.yellow, 5f);
            }
        }

        private void CreateTriangles()
        {
            if (linkedBorders.Count <= 0) return;

            //Debug.Log("Num of Borders: " + linkedBorders.Count);

            foreach (LinkedList<int> border in linkedBorders)
            {
                //Debug.Log("Num of Nodes: " + border.Count);

                LinkedListNode<int> curNode = border.First;
                Vector2 curRight = _edges[curNode.Value].Right;
                Vector2 nextLeft = _edges[curNode.Next.Value].Left;
                bool cwIsNext = SplitterHelper.CompareVector2(ref curRight, ref nextLeft);
                bool cw = true;
                int insideCheckCount = 0;

                int escape = 0;
                while (border.Count > 3 && escape++ < 10000)
                {
                    bool xorCWs = cw ^ cwIsNext;
                    LinkedListNode<int> testNode = xorCWs ? LLCircularPrevious<int>(curNode) : LLCircularNext<int>(curNode);
                    Edge curEdge = _edges[curNode.Value];
                    Edge testEdge = _edges[testNode.Value];
                    int innerTest = TestInnerSide(curEdge, testEdge, cw);
                    //Debug.Log("cw: " + cw + " ^CWs: " + xorCWs + " InnerTest: " + innerTest);

                    if (innerTest == 0)
                    {
                        // melt current node and test node
                        Edge meltEdge;

                        if (cw)
                        {
                            //Debug.Log("Cur - Left: " + curEdge.Left.ToString("0.##") + " Right: " + curEdge.Right.ToString("0.##") +
                            //          " | Test - Left: " + testEdge.Left.ToString("0.##") + " Right: " + testEdge.Right.ToString("0.##"));
                            // current left - test right
                            meltEdge = Edge.MeltEdges(curEdge, testEdge);
                        }
                        else
                        {
                            //Debug.Log("Test - Left: " + testEdge.Left.ToString("0.##") + " Right: " + testEdge.Right.ToString("0.##") +
                            //          " | Cur - Left: " + curEdge.Left.ToString("0.##") + " Right: " + curEdge.Right.ToString("0.##"));
                            // current right - test left
                            meltEdge = Edge.MeltEdges(testEdge, curEdge);
                        }


                        //Debug.Log("Melt edge - L: " + meltEdge.Left.ToString("0.##") + " R: " + meltEdge.Right.ToString("0.##"));

                        // add melt node and remove old nodes
                        LinkedListNode<int> meltNode = border.AddAfter(curNode, _edges.Count);
                        _edges.Add(meltEdge);
                        border.Remove(curNode);
                        border.Remove(testNode);

                        // set our melted node as the current node
                        curNode = meltNode;
                        continue;
                    }
                    else if (innerTest == 1)
                    {
                        // test node is inner side
                        // make closing edge and find next and previous edges
                        Edge closingEdge;
                        LinkedListNode<int> prevNode, nextNode;

                        if (cw)
                        {
                            // current left - test right
                            closingEdge = Edge.CloseEdges(curEdge, testEdge);
                        }
                        else
                        {
                            // current right - test left
                            closingEdge = Edge.CloseEdges(testEdge, curEdge);
                        }

                        if (xorCWs)
                        {
                            prevNode = LLCircularNext<int>(curNode);
                            nextNode = LLCircularPrevious<int>(testNode);
                        }
                        else
                        {
                            prevNode = LLCircularPrevious<int>(curNode);
                            nextNode = LLCircularNext<int>(testNode);
                        }

                        Edge prevEdge = _edges[prevNode.Value];
                        Edge nextEdge = _edges[nextNode.Value];
                        LinkedListNode<int> matchingNode = null;

                        // test if closing edge is equal to next or previous edges
                        if (closingEdge.SameVectors(prevEdge))
                            matchingNode = prevNode;
                        else if (closingEdge.SameVectors(nextEdge))
                            matchingNode = nextNode;

                        // if theres a match add tris to trilist and remove current and test nodes and the next/prev edge is the new current
                        if (matchingNode != null)
                        {
                            //Debug.Log("Inner with match");
                            if (cw)
                                AddTriangle(curEdge, testEdge, closingEdge);
                            else
                                AddTriangle(curEdge, closingEdge, testEdge);

                            border.Remove(curNode);
                            border.Remove(testNode);
                            curNode = LLCircularNext<int>(matchingNode);
                            border.Remove(matchingNode);
                            insideCheckCount = 0;
                            continue;
                        }

                        // test if new edge would intersect with other edges
                        if (TestNewEdgeIntersect(closingEdge, border))
                        {
                            //Debug.Log("Inner but intersected");
                            // edge intersects with border so not valid
                            insideCheckCount++;
                            cw = !cw;
                        }
                        else
                        {
                            // no intersections so add triangle and add closing edge to the border
                            // then remove current node and test node, closing node is the new current
                            if (cw)
                                AddTriangle(curEdge, testEdge, closingEdge);
                            else
                                AddTriangle(curEdge, closingEdge, testEdge);

                            LinkedListNode<int> addedNode = border.AddAfter(curNode, _edges.Count);
                            closingEdge.Flip();
                            _edges.Add(closingEdge);

                            border.Remove(curNode);
                            border.Remove(testNode);
                            curNode = addedNode;
                            insideCheckCount = 0;
                            continue;
                        }
                    }
                    else
                    {
                        //Debug.Log("outer, check other side");
                        insideCheckCount++;
                        cw = !cw;
                    }

                    // if insideCheckCount >= 2, then current node = next node (insideCheckCount = 0)
                    if (insideCheckCount >= 2)
                    {
                        //Debug.Log("2 bad inside checks, try next edge");
                        curNode = cw ^ cwIsNext ? LLCircularPrevious<int>(curNode) : LLCircularNext<int>(curNode);
                        insideCheckCount = 0;
                    }
                }

                // The last triangle
                //Debug.Log("Close off, edges left: " + border.Count);
                if (border.Count == 3)
                {
                    Edge first = _edges[border.First.Value];
                    Edge mid = _edges[border.First.Next.Value];
                    Edge last = _edges[border.Last.Value];

                    if (cwIsNext)
                        AddTriangle(first, mid, last);
                    else
                        AddTriangle(first, last, mid);
                }
            }
        }

        private void AddTriangle(Edge edge1, Edge edge2, Edge edge3)
        {
            triList.Add(edge1.IndexRight);
            triList.Add(edge2.IndexRight);
            triList.Add(edge3.IndexRight);

            if (UseCapUV)
            {
                uvList.Add(edge1.UVRight);
                uvList.Add(edge2.UVRight);
                uvList.Add(edge3.UVRight);
            }
        }

        /// <summary>
        /// Test if the angle between next edge and current edge is less or equal to 180 degrees.
        /// </summary>
        /// <param name="currentEdge">Current Edge</param>
        /// <param name="nextEdge">Next Edge</param>
        /// <param name="cw">Direction is clockwise?</param>
        /// <returns></returns>
        private int TestInnerSide(Edge currentEdge, Edge nextEdge, bool cw)
        {
            PlaneMath plane = new PlaneMath(currentEdge.Left, currentEdge.Normal);
            float pointSide = plane.PointSide(cw ? nextEdge.Right : nextEdge.Left);

            if (pointSide < -.000001f)  // less than zero with a threshold, inner side
                return 1;

            if (pointSide < .000001f) // < 180 degree angle, needs to be melted
                return 0;

            return -1;
        }

        /// <summary>
        /// Test if the new edge willleft and right Represents the new edge (line segment) that will be tested against intersecting other edges.
        /// </summary>
        /// <param name="left">Left end of new edge</param>
        /// <param name="right">Right end of new edge</param>
        /// <param name="borderIndicies">Index of current border</param>
        /// <returns>True if new edge will intersect with current edges in border</returns>
        private bool TestNewEdgeIntersect(Edge edge, LinkedList<int> borderIndicies)
        {
            foreach (int edgeIndex in borderIndicies)
            {
                if (edge.EdgeIntersect(_edges[edgeIndex]))
                    return true;
            }

            return false;
        }

        private static LinkedListNode<T> LLCircularNext<T>(LinkedListNode<T> current)
        {
            return current.Next == null ? current.List.First : current.Next;
        }

        private static LinkedListNode<T> LLCircularPrevious<T>(LinkedListNode<T> current)
        {
            return current.Previous == null ? current.List.Last : current.Previous;
        }

        private void AddTrianglesToMesh()
        {
            //create cap verticies
            Vector3 normal = Quaternion.Inverse(_ownRotation) * _splitPlane.Normal;
            Vector3 invNormal = -normal;
            int capVertexCount = triList.Count;
            int[] capUpperOrder = new int[capVertexCount];
            int[] capLowerOrder = new int[capVertexCount];

            if (UseCapUV)
            {
                for (int i = 0; i < capVertexCount; i++)
                {
                    int triIndex = triList[i];
                    Vector2 capUV = uvList[i];
                    capUpperOrder[i] = _mesh.AddCapVertex(triIndex, invNormal, capUV);
                    capLowerOrder[i] = _mesh.AddCapVertex(triIndex, normal, capUV);
                }
            }
            else
            {
                for (int i = 0; i < capVertexCount; i++)
                {
                    int triIndex = triList[i];
                    capUpperOrder[i] = _mesh.AddCapVertex(triIndex, invNormal);
                    capLowerOrder[i] = _mesh.AddCapVertex(triIndex, normal);
                }
            }

            for (int i = 2; i < capVertexCount; i += 3)
            {
                _mesh.trisUp.Add(capUpperOrder[i - 2]);
                _mesh.trisUp.Add(capUpperOrder[i]);
                _mesh.trisUp.Add(capUpperOrder[i - 1]);

                _mesh.trisDown.Add(capLowerOrder[i - 2]);
                _mesh.trisDown.Add(capLowerOrder[i - 1]);
                _mesh.trisDown.Add(capLowerOrder[i]);
            }
        }
        #endregion
    }
}
