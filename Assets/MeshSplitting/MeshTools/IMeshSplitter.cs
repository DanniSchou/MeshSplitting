using UnityEngine;
using System;

public interface IMeshSplitter
{
#if UNITY_EDITOR
    void DebugDraw(bool debug);
#endif

    void SetCapUV(bool useCapUV, bool customUV, Vector2 uvMin, Vector2 uvMax);

    void MeshSplit();

    void MeshCreateCaps();
}
