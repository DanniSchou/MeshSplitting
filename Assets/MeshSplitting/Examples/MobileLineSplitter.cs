using MeshSplitting.Splitables;
using MeshSplitting.Splitters;
using UnityEngine;

namespace MeshSplitting.Examples
{
    [AddComponentMenu("Mesh Splitting/Examples/Mobile Line Splitter")]
    [RequireComponent(typeof(Camera))]
    [RequireComponent(typeof(LineRenderer))]
    public class MobileLineSplitter : MonoBehaviour
    {
        public float CutPlaneDistance = 5f;
        public float CutPlaneSize = 10f;
        public float MinSplitDistance = 20f;

        private LineRenderer _lineRenderer;
        private Camera _camera;
        private Transform _transform;

        private bool _hasStartPos = false;
        private Vector3 _startPos;
        private Vector3 _endPos;

        public Vector2 View = new Vector2(0f, 10f);
        public float Distance = 5f;
        public Vector3 Target = Vector3.up;
        public float ForcePush = 1f;

        public GUISkin GuiSkin;
        public Texture2D[] SplitableIcons;
        public GameObject[] SplitablePrefabs;

        private Rect[] _rects;
        private bool _mouseDown = false;

        private void Awake()
        {
            _transform = GetComponent<Transform>();
            _lineRenderer = GetComponent<LineRenderer>();
            _camera = GetComponent<Camera>();

            _lineRenderer.enabled = false;

            if (SplitablePrefabs.Length > 0)
            {
                Instantiate(SplitablePrefabs[0], Vector3.up * 2f, Quaternion.identity);
            }

            _rects = new Rect[SplitableIcons.Length + 2];
            int width = Screen.width, height = Screen.height;
            int width20th = width / 20, height20th = height / 20;
            int i;

            for (i = 0; i < SplitableIcons.Length; i++)
            {
                int offset = width20th * (i * 2 + 1);
                _rects[i] = new Rect(offset, height20th, width20th * 2, width20th * 2);
            }

            _rects[i++] = new Rect(width - height20th - 50, height20th, 50, height - height20th * 5);
            _rects[i] = new Rect(height20th, height - height20th - 50, width - height20th * 5, 50);
        }

        private void Update()
        {
            if (Input.GetKey(KeyCode.Escape)) Application.Quit();

            Vector3 pos;
            CalcPosition(out pos);
            _transform.position = pos;
            _transform.LookAt(Target);

            if (Input.GetMouseButtonDown(0))
            {
                _mouseDown = true;
                Vector3 mousePos = Input.mousePosition;
                mousePos.y = Screen.height - mousePos.y;
                for (int i = 0; i < _rects.Length; i++)
                {
                    Rect rect = _rects[i];
                    int dX = (int)(mousePos.x - rect.x),
                        dY = (int)(mousePos.y - rect.y);
                    if (0 < dX && dX < rect.width && 0 < dY && dY < rect.height)
                    {
                        _mouseDown = false;
                        break;
                    }
                }
            }

            if (Input.GetMouseButtonDown(0) && _mouseDown)
            {
                _startPos = Input.mousePosition;
                _hasStartPos = true;
            }
            else if (_hasStartPos && Input.GetMouseButtonUp(0) && _mouseDown)
            {
                _endPos = Input.mousePosition;
                if (Vector3.Distance(_startPos, _endPos) > MinSplitDistance)
                    CreateCutPlane();
                else
                {
                    RaycastHit hitInfo;
                    Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out hitInfo))
                    {
                        Rigidbody body = hitInfo.collider.GetComponent<Rigidbody>();
                        if (body != null)
                        {
                            body.AddForce(ray.direction * body.mass * ForcePush, ForceMode.Impulse);
                        }
                    }
                }

                _hasStartPos = false;
                _lineRenderer.enabled = false;
            }

            if (_hasStartPos)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.SetPosition(0, GetPosInWorld(_startPos));
                _lineRenderer.SetPosition(1, GetPosInWorld(Input.mousePosition));
            }
        }

        private void OnGUI()
        {
            if (GuiSkin != null) GUI.skin = GuiSkin;

            View.x = GUI.VerticalScrollbar(_rects[SplitableIcons.Length], View.x, 7, 70, 0);
            View.y = GUI.HorizontalScrollbar(_rects[SplitableIcons.Length + 1], View.y, 36, -180, 180);

            for (int i = 0; i < SplitableIcons.Length; i++)
            {
                if (GUI.Button(_rects[i], SplitableIcons[i]))
                {
                    CreateNewObject(i);
                }
            }

        }

        private void CreateNewObject(int i)
        {
            if (SplitablePrefabs[i] != null)
            {
                Splitable splitable = FindObjectOfType(typeof(Splitable)) as Splitable;
                if (splitable != null)
                {
                    if (splitable.transform.parent == null)
                        Destroy(splitable.gameObject);
                    else
                        Destroy(splitable.transform.parent.gameObject);
                }

                Instantiate(SplitablePrefabs[i], Vector3.up * 2f, SplitablePrefabs[i].transform.rotation);
            }
        }

        private void CalcPosition(out Vector3 position)
        {
            Vector3 direction = Vector3.forward * -Distance;
            Quaternion rotation = Quaternion.Euler(View.x, View.y, 0);
            position = Target + rotation * direction;
        }

        private Vector3 GetPosInWorld(Vector3 pos)
        {
            Ray ray = _camera.ScreenPointToRay(pos);
            return ray.origin + ray.direction * CutPlaneDistance;
        }

        private void CreateCutPlane()
        {
            Vector3 startPos = GetPosInWorld(_startPos);
            Vector3 endPos = GetPosInWorld(_endPos);

            Vector3 center = Vector3.Lerp(startPos, endPos, .5f);
            Vector3 cut = (endPos - startPos).normalized;
            Vector3 fwd = (center - _transform.position).normalized;
            Vector3 normal = Vector3.Cross(fwd, cut).normalized;

            GameObject goCutPlane = new GameObject("CutPlane", typeof(BoxCollider), typeof(Rigidbody), typeof(SplitterSingleCut));

            goCutPlane.GetComponent<Collider>().isTrigger = true;
            Rigidbody bodyCutPlane = goCutPlane.GetComponent<Rigidbody>();
            bodyCutPlane.useGravity = false;
            bodyCutPlane.isKinematic = true;

            Transform transformCutPlane = goCutPlane.transform;
            transformCutPlane.position = center;
            transformCutPlane.localScale = new Vector3(CutPlaneSize, .01f, CutPlaneSize);
            transformCutPlane.rotation = _transform.rotation;
            transformCutPlane.up = normal;
        }
    }
}
