using MeshSplitting.Splitables;
using UnityEngine;

namespace MeshSplitting.Demo
{
    public class DemoController : MonoBehaviour
    {
        public GameObject[] SplitablePrefabs;
        public Material[] Materials;
        public int[] NoBatchMaterials;

        private int _materialIndex;

        private void Start()
        {
            if (SplitablePrefabs.Length > 0)
            {
                Instantiate(SplitablePrefabs[0], Vector3.up * 2f, Quaternion.identity);
            }
        }

        private void Update()
        {
            for (int i = 0; i < SplitablePrefabs.Length; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    Splitable splitable = FindObjectOfType(typeof(Splitable)) as Splitable;
                    if (splitable != null)
                    {
                        if (splitable.transform.parent == null)
                            Destroy(splitable.gameObject);
                        else
                            Destroy(splitable.transform.parent.gameObject);
                    }

                    Instantiate(SplitablePrefabs[i]);
                }
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                _materialIndex--;
                if (_materialIndex < 0)
                    _materialIndex = Materials.Length - 1;

                ChangeMaterial();
            }
            else if (Input.GetKeyDown(KeyCode.E))
            {
                _materialIndex++;
                if (_materialIndex >= Materials.Length)
                    _materialIndex = 0;

                ChangeMaterial();
            }
        }

        private void ChangeMaterial()
        {
            Material[] mats = { Materials[_materialIndex] };
            Splitable[] Splitables = Object.FindObjectsOfType<Splitable>();
            bool noBatch = Contains(NoBatchMaterials, _materialIndex);
            foreach (Splitable splitable in Splitables)
            {
                if (noBatch)
                    splitable.ForceNoBatching = true;
                else
                    splitable.ForceNoBatching = false;

                Renderer[] renderers = splitable.GetComponentsInChildren<Renderer>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].sharedMaterials = mats;
                }
            }
        }

        private bool Contains(int[] array, int value)
        {
            foreach (int index in array)
            {
                if (index == value)
                    return true;
            }

            return false;
        }
    }
}
