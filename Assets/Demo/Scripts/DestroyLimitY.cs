using UnityEngine;

namespace MeshSplitting.Demo
{
    public class DestroyLimitY : MonoBehaviour
    {
        public float YValue = -1f;
        private Transform _transform;

        private void Awake()
        {
            _transform = GetComponent<Transform>();
        }

        private void Update()
        {
            if (_transform.position.y <= YValue)
                Destroy(gameObject);
        }
    }
}