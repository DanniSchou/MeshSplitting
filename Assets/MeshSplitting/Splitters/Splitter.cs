using MeshSplitting.Splitables;
using UnityEngine;

namespace MeshSplitting.Splitters
{
    [AddComponentMenu("Mesh Splitting/Splitter")]
    [RequireComponent(typeof(Collider))]
    public class Splitter : MonoBehaviour
    {
        protected Transform _transform;

        protected virtual void Awake()
        {
            _transform = GetComponent<Transform>();
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            MonoBehaviour[] components = other.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour component in components)
            {
                ISplitable splitable = component as ISplitable;
                if (splitable != null)
                {
                    SplitObject(splitable, other.gameObject);
                    break;
                }
            }
        }

        protected virtual void SplitObject(ISplitable splitable, GameObject go)
        {
            splitable.Split(_transform);
        }
    }
}