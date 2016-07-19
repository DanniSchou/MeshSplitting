using UnityEngine;

namespace MeshSplitting.MeshTools
{
    public interface IMeshSplitter
    {
#if UNITY_EDITOR
        void DebugDraw(bool debug);
#endif

        void SetCapUV(bool useCapUV, bool customUV, Vector2 uvMin, Vector2 uvMax);

        void MeshSplit();

        void MeshCreateCaps();
    }
}
