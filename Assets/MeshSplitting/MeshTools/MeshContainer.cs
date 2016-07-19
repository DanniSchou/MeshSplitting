using System.Collections.Generic;
using UnityEngine;

namespace MeshSplitting.MeshTools
{
    public class MeshContainer
    {
        // Mesh info
        public Mesh Mesh;
        public Transform transform;
        public Transform[] bones;
        public bool isAnimated;

        // mesh copy
        public int vertexCount;
        public Vector3[] wsVerts;
        public Vector3[] vertices;
        public Vector3[] normals;
        public Vector4[] tangents;
        public Vector2[] uv;
        public Vector2[] uv2;
        public Color[] colors;
        public BoneWeight[] boneWeights;

        public int[] triangles;
        public Matrix4x4[] bindPoses;

        // added parts
        public List<Vector3> wsVertsNew;
        public List<Vector3> verticesNew;
        public List<Vector3> normalsNew;
        public List<Vector4> tangentsNew;
        public List<Vector2> uvNew;
        public List<Vector2> uv2New;
        public List<Color> colorsNew;
        public List<BoneWeight> boneWeightsNew;

        // new meshes
        public List<int> trisUp;
        public List<int> trisDown;

        public MeshContainer(MeshFilter meshFilter)
        {
            Mesh = meshFilter.mesh;
            transform = meshFilter.GetComponent<Transform>();
            isAnimated = false;
        }

        public MeshContainer(SkinnedMeshRenderer skinnedRenderer)
        {
            Mesh = skinnedRenderer.sharedMesh;
            transform = skinnedRenderer.GetComponent<Transform>();
            bones = skinnedRenderer.bones;
            isAnimated = true;
        }

        public void MeshInitialize()
        {
            // store mesh data
            vertexCount = Mesh.vertexCount;
            vertices = Mesh.vertices;
            wsVerts = Mesh.vertices;
            triangles = Mesh.triangles;
            normals = Mesh.normals;
            tangents = Mesh.tangents;
            uv = Mesh.uv;
            uv2 = Mesh.uv2;
            colors = Mesh.colors;
            boneWeights = Mesh.boneWeights;
            bindPoses = Mesh.bindposes;

            // create arrays for added parts
            int countHalf = vertexCount / 2;
            if (wsVerts.Length != 0) wsVertsNew = new List<Vector3>(countHalf);
            if (vertices.Length != 0) verticesNew = new List<Vector3>(countHalf);
            if (normals.Length != 0) normalsNew = new List<Vector3>(countHalf);
            if (tangents.Length != 0) tangentsNew = new List<Vector4>(countHalf);
            if (uv.Length != 0) uvNew = new List<Vector2>(countHalf);
            if (uv2.Length != 0) uv2New = new List<Vector2>(countHalf);
            if (colors.Length != 0) colorsNew = new List<Color>(countHalf);
            if (boneWeights.Length != 0) boneWeightsNew = new List<BoneWeight>(countHalf);

            int count1_5x = (int)(triangles.Length * 1.5f);
            trisUp = new List<int>(count1_5x);
            trisDown = new List<int>(count1_5x);
        }

        public void CalculateWorldSpace()
        {
            if (!isAnimated)
                CalculateWorldSpaceStatic();
            else
                CalculateWorldSpaceAnimated();
        }

        private void CalculateWorldSpaceStatic()
        {
            Matrix4x4 localToWorld = transform.localToWorldMatrix;
            int count = wsVerts.Length;
            for (int i = 0; i < count; i++)
            {
                wsVerts[i] = localToWorld.MultiplyPoint3x4(wsVerts[i]);
            }
        }

        private void CalculateWorldSpaceAnimated()
        {
            int boneCount = bones.Length;
            Matrix4x4[] boneMatrices = new Matrix4x4[boneCount];
            for (int i = 0; i < boneCount; i++)
            {
                boneMatrices[i] = bones[i].localToWorldMatrix * bindPoses[i];
            }

            Matrix4x4 localBone0, localBone1, localBone2, localBone3;
            float bw0, bw1, bw2, bw3;
            BoneWeight boneWeight;
            Matrix4x4 worldMatrix = Matrix4x4.identity;

            int count = wsVerts.Length;
            for (int i = 0; i < count; i++)
            {
                boneWeight = boneWeights[i];
                // cache weights
                bw0 = boneWeight.weight0;
                bw1 = boneWeight.weight1;
                bw2 = boneWeight.weight2;
                bw3 = boneWeight.weight3;
                // cache matrices
                localBone0 = boneMatrices[boneWeight.boneIndex0];
                localBone1 = boneMatrices[boneWeight.boneIndex1];
                localBone2 = boneMatrices[boneWeight.boneIndex2];
                localBone3 = boneMatrices[boneWeight.boneIndex3];

                for (int k = 0; k < 3; k++)
                {
                    for (int j = 0; j < 4; j++)
                    {
                        int index = k + j * 4;
                        worldMatrix[index] =
                            localBone0[index] * bw0 +
                            localBone1[index] * bw1 +
                            localBone2[index] * bw2 +
                            localBone3[index] * bw3;
                    }
                }

                wsVerts[i] = worldMatrix.MultiplyPoint3x4(wsVerts[i]);
            }
        }

        public bool IsMeshSplit()
        {
            return HasMeshUpper() && HasMeshLower();
        }

        public bool HasMeshUpper()
        {
            return trisUp.Count > 0;
        }

        public bool HasMeshLower()
        {
            return trisDown.Count > 0;
        }

        public Mesh CreateMeshUpper()
        {
            return CreateMesh(trisUp);
        }

        public Mesh CreateMeshLower()
        {
            return CreateMesh(trisDown);
        }

        private Mesh CreateMesh(List<int> tris)
        {
            int triCount = tris.Count;
            int[] localTris = new int[triCount];

            int vCount = vertexCount + verticesNew.Count;
            int[] translateIndex = new int[vCount];
            for (int i = 0; i < vCount; i++)
                translateIndex[i] = -1;

            int uniTriCount = 0;
            for (int i = 0; i < triCount; i++)
            {
                int triIndex = tris[i];

                if (translateIndex[triIndex] == -1)
                {
                    translateIndex[triIndex] = uniTriCount++;
                }

                localTris[i] = translateIndex[triIndex];
            }

            Vector3[] localVerts = new Vector3[uniTriCount];
            Vector3[] localNormals = normals.Length != 0 ? new Vector3[uniTriCount] : normals;
            Vector4[] localTangents = tangents.Length != 0 ? new Vector4[uniTriCount] : tangents;
            Vector2[] localUv = uv.Length != 0 ? new Vector2[uniTriCount] : uv;
            Vector2[] localUv2 = uv2.Length != 0 ? new Vector2[uniTriCount] : uv2;
            Color[] localColors = colors.Length != 0 ? new Color[uniTriCount] : colors;
            BoneWeight[] localBoneWeights = boneWeights.Length != 0 ? new BoneWeight[uniTriCount] : boneWeights;

            uniTriCount = 0;
            for (int i = 0; i < triCount; i++)
            {
                int triIndex = tris[i];
                if (translateIndex[triIndex] >= uniTriCount)
                {
                    if (triIndex < vertexCount)
                    {
                        localVerts[uniTriCount] = vertices[triIndex];
                        if (normalsNew != null) localNormals[uniTriCount] = normals[triIndex];
                        if (tangentsNew != null) localTangents[uniTriCount] = tangents[triIndex];
                        if (uvNew != null) localUv[uniTriCount] = uv[triIndex];
                        if (uv2New != null) localUv2[uniTriCount] = uv2[triIndex];
                        if (colorsNew != null) localColors[uniTriCount] = colors[triIndex];
                        if (boneWeightsNew != null) localBoneWeights[uniTriCount] = boneWeights[triIndex];
                    }
                    else
                    {
                        triIndex -= vertexCount;
                        localVerts[uniTriCount] = verticesNew[triIndex];
                        if (normalsNew != null) localNormals[uniTriCount] = normalsNew[triIndex];
                        if (tangentsNew != null) localTangents[uniTriCount] = tangentsNew[triIndex];
                        if (uvNew != null) localUv[uniTriCount] = uvNew[triIndex];
                        if (uv2New != null) localUv2[uniTriCount] = uv2New[triIndex];
                        if (colorsNew != null) localColors[uniTriCount] = colorsNew[triIndex];
                        if (boneWeightsNew != null) localBoneWeights[uniTriCount] = boneWeightsNew[triIndex];
                    }

                    uniTriCount++;
                }
            }

            Mesh newMesh = new Mesh();
            newMesh.vertices = localVerts;
            newMesh.normals = localNormals;
            newMesh.tangents = localTangents;
            newMesh.uv = localUv;
            newMesh.uv2 = localUv2;
            newMesh.colors = localColors;
            newMesh.boneWeights = localBoneWeights;
            newMesh.triangles = localTris;
            newMesh.bindposes = bindPoses;

            newMesh.RecalculateBounds();
            //newMesh.Optimize(); // This might help optimize the mesh but could slow down performance while splitting, maybe it should be optional?

            return newMesh;
        }

        private static Vector2 Vector2Zero = Vector2.zero;
        private static Vector3 Vector3Up = Vector3.up;
        private static Vector3 Vector3Fwd = Vector3.forward;
        private static Vector4 Vector4Zero = Vector4.zero;

        public int AddLerpVertex(int from, int to, float t)
        {
            int index = vertexCount + verticesNew.Count;

            verticesNew.Add(Vector3.Lerp(vertices[from], vertices[to], t));
            wsVertsNew.Add(Vector3.Lerp(wsVerts[from], wsVerts[to], t));

            if (normalsNew != null) normalsNew.Add(Vector3.Lerp(normals[from], normals[to], t));
            if (tangentsNew != null) tangentsNew.Add(Vector4.Lerp(tangents[from], tangents[to], t));
            if (uvNew != null) uvNew.Add(Vector2.Lerp(uv[from], uv[to], t));
            if (uv2New != null) uv2New.Add(Vector2.Lerp(uv2[from], uv2[to], t));
            if (colorsNew != null) colorsNew.Add(Color.Lerp(colors[from], colors[to], t));
            if (boneWeightsNew != null) boneWeightsNew.Add(t >= .5f ? boneWeights[to] : boneWeights[from]);

            return index;
        }

        public int AddCapVertex(int refIndex, Vector3 normal)
        {
            if (uvNew != null)
            {
                if (refIndex >= vertexCount)
                {
                    return AddCapVertex(refIndex, normal, uvNew[refIndex - vertexCount]);
                }
                else
                {
                    return AddCapVertex(refIndex, normal, uv[refIndex]);
                }
            }
            else
            {
                return AddCapVertex(refIndex, normal, Vector2Zero);
            }
        }

        public int AddCapVertex(int refIndex, Vector3 normal, Vector2 capUV)
        {
            int index = vertexCount + verticesNew.Count;
            bool useArray = true;
            if (refIndex >= vertexCount)
            {
                refIndex -= vertexCount;
                useArray = false;
            }

            if (useArray)
            {
                verticesNew.Add(vertices[refIndex]);
                if (uv2New != null) uv2New.Add(uv2[refIndex]);
                if (colorsNew != null) colorsNew.Add(colors[refIndex]);
                if (boneWeightsNew != null) boneWeightsNew.Add(boneWeights[refIndex]);
            }
            else
            {
                verticesNew.Add(verticesNew[refIndex]);
                if (uv2New != null) uv2New.Add(uv2New[refIndex]);
                if (colorsNew != null) colorsNew.Add(colorsNew[refIndex]);
                if (boneWeightsNew != null) boneWeightsNew.Add(boneWeightsNew[refIndex]);
            }

            if (normalsNew != null) normalsNew.Add(normal);
            if (uvNew != null) uvNew.Add(capUV);
            if (tangentsNew != null)
            {
                Vector4 tangent = Vector4Zero;
                Vector3 c1 = Vector3.Cross(normal, Vector3Fwd);
                Vector3 c2 = Vector3.Cross(normal, Vector3Up);
                if (c1.sqrMagnitude > c2.sqrMagnitude)
                {
                    tangent.x = c1.x;
                    tangent.y = c1.y;
                    tangent.z = c1.z;
                }
                else
                {
                    tangent.x = c2.x;
                    tangent.y = c2.y;
                    tangent.z = c2.z;
                }
                tangentsNew.Add(tangent.normalized);
            }

            return index;
        }
    }
}
