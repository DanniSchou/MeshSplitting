using MeshSplitting.MeshTools;
using MeshSplitting.SplitterMath;
using System;
using UnityEngine;

namespace MeshSplitting.Splitables
{
    [AddComponentMenu("Mesh Splitting/Splitable")]
    public class Splitable : MonoBehaviour, ISplitable
    {
#if UNITY_EDITOR
        [NonSerialized]
        public bool ShowDebug = false;
#endif

        public GameObject OptionalTargetObject;
        public bool Convex = false;
        public float SplitForce = 0f;

        public bool CreateCap = true;
        public bool UseCapUV = false;
        public bool CustomUV = false;
        public Vector2 CapUVMin = Vector2.zero;
        public Vector2 CapUVMax = Vector2.one;

        public bool ForceNoBatching = false;

        private Transform _transform;

        private PlaneMath _splitPlane;
        private MeshContainer[] _meshContainerStatic;
        private IMeshSplitter[] _meshSplitterStatic;
        private MeshContainer[] _meshContainerSkinned;
        private IMeshSplitter[] _meshSplitterSkinned;

        private bool _isSplitting = false;
        private bool _splitMesh = false;

        private void Awake()
        {
            _transform = GetComponent<Transform>();
        }

        private void Update()
        {
            if (_splitMesh)
            {
                _splitMesh = false;

                bool anySplit = false;

                for (int i = 0; i < _meshContainerStatic.Length; i++)
                {
                    _meshContainerStatic[i].MeshInitialize();
                    _meshContainerStatic[i].CalculateWorldSpace();

                    // split mesh
                    _meshSplitterStatic[i].MeshSplit();

                    if (_meshContainerStatic[i].IsMeshSplit())
                    {
                        anySplit = true;
                        if (CreateCap) _meshSplitterStatic[i].MeshCreateCaps();
                    }
                }

                for (int i = 0; i < _meshContainerSkinned.Length; i++)
                {
                    _meshContainerSkinned[i].MeshInitialize();
                    _meshContainerSkinned[i].CalculateWorldSpace();

                    // split mesh
                    _meshSplitterSkinned[i].MeshSplit();

                    if (_meshContainerSkinned[i].IsMeshSplit())
                    {
                        anySplit = true;
                        if (CreateCap) _meshSplitterSkinned[i].MeshCreateCaps();
                    }
                }

                if (anySplit) CreateNewObjects();
                _isSplitting = false;
            }
        }

        public void Split(Transform splitTransform)
        {
            if (!_isSplitting)
            {
                _isSplitting = _splitMesh = true;
                _splitPlane = new PlaneMath(splitTransform);

                MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
                SkinnedMeshRenderer[] skinnedRenderes = GetComponentsInChildren<SkinnedMeshRenderer>();

                _meshContainerStatic = new MeshContainer[meshFilters.Length];
                _meshSplitterStatic = new IMeshSplitter[meshFilters.Length];

                for (int i = 0; i < meshFilters.Length; i++)
                {
                    _meshContainerStatic[i] = new MeshContainer(meshFilters[i]);

                    _meshSplitterStatic[i] = Convex ? (IMeshSplitter)new MeshSplitterConvex(_meshContainerStatic[i], _splitPlane, splitTransform.rotation) :
                                                      (IMeshSplitter)new MeshSplitterConcave(_meshContainerStatic[i], _splitPlane, splitTransform.rotation);

                    if (UseCapUV) _meshSplitterStatic[i].SetCapUV(UseCapUV, CustomUV, CapUVMin, CapUVMax);
#if UNITY_EDITOR
                    _meshSplitterStatic[i].DebugDraw(ShowDebug);
#endif
                }

                _meshSplitterSkinned = new IMeshSplitter[skinnedRenderes.Length];
                _meshContainerSkinned = new MeshContainer[skinnedRenderes.Length];

                for (int i = 0; i < skinnedRenderes.Length; i++)
                {
                    _meshContainerSkinned[i] = new MeshContainer(skinnedRenderes[i]);

                    _meshSplitterSkinned[i] = Convex ? (IMeshSplitter)new MeshSplitterConvex(_meshContainerSkinned[i], _splitPlane, splitTransform.rotation) :
                                                      (IMeshSplitter)new MeshSplitterConcave(_meshContainerSkinned[i], _splitPlane, splitTransform.rotation);

                    if (UseCapUV) _meshSplitterSkinned[i].SetCapUV(UseCapUV, CustomUV, CapUVMin, CapUVMax);
#if UNITY_EDITOR
                    _meshSplitterSkinned[i].DebugDraw(ShowDebug);
#endif
                }
            }
        }

        private void CreateNewObjects()
        {
            Transform parent = _transform.parent;
            if (parent == null)
            {
                GameObject go = new GameObject("Parent: " + gameObject.name);
                parent = go.transform;
                parent.position = Vector3.zero;
                parent.rotation = Quaternion.identity;
                parent.localScale = Vector3.one;
            }

            Mesh origMesh = GetMeshOnGameObject(gameObject);
            Rigidbody ownBody = null;
            float ownMass = 100f;
            float ownVolume = 1f;

            if (origMesh != null)
            {
                ownBody = GetComponent<Rigidbody>();
                if (ownBody != null) ownMass = ownBody.mass;
                Vector3 ownMeshSize = origMesh.bounds.size;
                ownVolume = ownMeshSize.x * ownMeshSize.y * ownMeshSize.z;
            }

            GameObject[] newGOs = new GameObject[2];
            if (OptionalTargetObject == null)
            {
                newGOs[0] = Instantiate(gameObject) as GameObject;
                newGOs[0].name = gameObject.name;
                newGOs[1] = gameObject;
            }
            else
            {
                newGOs[0] = Instantiate(OptionalTargetObject) as GameObject;
                newGOs[1] = Instantiate(OptionalTargetObject) as GameObject;
            }

            Animation[] animSources = newGOs[1].GetComponentsInChildren<Animation>();
            Animation[] animDests = newGOs[0].GetComponentsInChildren<Animation>();
            for (int i = 0; i < animSources.Length; i++)
            {
                foreach (AnimationState stateSource in animSources[i])
                {
                    AnimationState stateDest = animDests[i][stateSource.name];
                    stateDest.enabled = stateSource.enabled;
                    stateDest.weight = stateSource.weight;
                    stateDest.time = stateSource.time;
                    stateDest.speed = stateSource.speed;
                    stateDest.layer = stateSource.layer;
                    stateDest.blendMode = stateSource.blendMode;
                }
            }

            for (int i = 0; i < 2; i++)
            {
                UpdateMeshesInChildren(i, newGOs[i]);

                Transform newTransform = newGOs[i].GetComponent<Transform>();
                newTransform.parent = parent;

                Mesh newMesh = GetMeshOnGameObject(newGOs[i]);
                if (newMesh != null)
                {
                    MeshCollider newCollider = newGOs[i].GetComponent<MeshCollider>();
                    if (newCollider != null)
                    {
                        newCollider.sharedMesh = newMesh;
                        newCollider.convex = Convex;

                        // if hull has less than 255 polygons set convex, Unity limit!
                        if (newCollider.convex && newMesh.triangles.Length > 765)
                            newCollider.convex = false;
                    }

                    Rigidbody newBody = newGOs[i].GetComponent<Rigidbody>();
                    if (ownBody != null && newBody != null)
                    {
                        Vector3 newMeshSize = newMesh.bounds.size;
                        float meshVolume = newMeshSize.x * newMeshSize.y * newMeshSize.z;
                        float newMass = ownMass * (meshVolume / ownVolume);

                        newBody.useGravity = ownBody.useGravity;
                        newBody.mass = newMass;
                        newBody.velocity = ownBody.velocity;
                        newBody.angularVelocity = ownBody.angularVelocity;
                        if (SplitForce > 0f) newBody.AddForce(_splitPlane.Normal * newMass * (i == 0 ? SplitForce : -SplitForce), ForceMode.Impulse);
                    }
                }

                PostProcessObject(newGOs[i]);
            }
        }

        private void UpdateMeshesInChildren(int i, GameObject go)
        {
            if (_meshContainerStatic.Length > 0)
            {
                MeshFilter[] meshFilters = go.GetComponentsInChildren<MeshFilter>();
                for (int j = 0; j < _meshContainerStatic.Length; j++)
                {
                    Renderer renderer = meshFilters[j].GetComponent<Renderer>();
                    if (ForceNoBatching)
                    {
                        renderer.materials = renderer.materials;
                    }
                    if (i == 0)
                    {
                        if (_meshContainerStatic[j].HasMeshUpper() & _meshContainerStatic[j].HasMeshLower())
                        {
                            meshFilters[j].mesh = _meshContainerStatic[j].CreateMeshUpper();
                        }
                        else if (!_meshContainerStatic[j].HasMeshUpper())
                        {
                            if (renderer != null) Destroy(renderer);
                            Destroy(meshFilters[j]);
                        }
                    }
                    else
                    {
                        if (_meshContainerStatic[j].HasMeshUpper() & _meshContainerStatic[j].HasMeshLower())
                        {
                            meshFilters[j].mesh = _meshContainerStatic[j].CreateMeshLower();
                        }
                        else if (!_meshContainerStatic[j].HasMeshLower())
                        {
                            if (renderer != null) Destroy(renderer);
                            Destroy(meshFilters[j]);
                        }
                    }
                }
            }

            if (_meshContainerSkinned.Length > 0)
            {
                SkinnedMeshRenderer[] skinnedRenderer = go.GetComponentsInChildren<SkinnedMeshRenderer>();
                for (int j = 0; j < _meshContainerSkinned.Length; j++)
                {
                    if (i == 0)
                    {
                        if (_meshContainerSkinned[j].HasMeshUpper() & _meshContainerSkinned[j].HasMeshLower())
                        {
                            skinnedRenderer[j].sharedMesh = _meshContainerSkinned[j].CreateMeshUpper();
                        }
                        else if (!_meshContainerSkinned[j].HasMeshUpper())
                        {
                            Destroy(skinnedRenderer[j]);
                        }
                    }
                    else
                    {
                        if (_meshContainerSkinned[j].HasMeshUpper() & _meshContainerSkinned[j].HasMeshLower())
                        {
                            skinnedRenderer[j].sharedMesh = _meshContainerSkinned[j].CreateMeshLower();
                        }
                        else if (!_meshContainerSkinned[j].HasMeshLower())
                        {
                            Destroy(skinnedRenderer[j]);
                        }
                    }
                }
            }
        }

        private Material[] GetSharedMaterials(GameObject go)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                return skinnedMeshRenderer.sharedMaterials;
            }
            else
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    return renderer.sharedMaterials;
                }
            }

            return null;
        }

        private void SetSharedMaterials(GameObject go, Material[] materials)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.sharedMaterials = materials;
            }
            else
            {
                Renderer renderer = go.GetComponent<Renderer>();
                if (renderer != null)
                {
                    renderer.sharedMaterials = materials;
                }
            }
        }

        private void SetMeshOnGameObject(GameObject go, Mesh mesh)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                skinnedMeshRenderer.sharedMesh = mesh;
            }
            else
            {
                MeshFilter meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = mesh;
                }
            }
        }

        private Mesh GetMeshOnGameObject(GameObject go)
        {
            SkinnedMeshRenderer skinnedMeshRenderer = go.GetComponent<SkinnedMeshRenderer>();
            if (skinnedMeshRenderer != null)
            {
                return skinnedMeshRenderer.sharedMesh;
            }
            else
            {
                MeshFilter meshFilter = go.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    return meshFilter.mesh;
                }
            }

            return null;
        }

        protected virtual void PostProcessObject(GameObject go) { }
    }
}
