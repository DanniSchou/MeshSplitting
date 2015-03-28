using UnityEngine;
using System;

[AddComponentMenu("Mesh Splitting/Examples/Camera Line Splitter")]
[RequireComponent(typeof(Camera))]
[RequireComponent(typeof(LineRenderer))]
public class CameraLineSplitter : MonoBehaviour
{
#if UNITY_EDITOR
    [NonSerialized]
    public bool ShowDebug = false;
#endif

    public float CutPlaneDistance = 1f;
    public float CutPlaneSize = 2f;

    private LineRenderer _lineRenderer;
    private Camera _camera;
    private Transform _transform;

    private bool _inCutMode = false;
    private bool _hasStartPos = false;
    private Vector3 _startPos;
    private Vector3 _endPos;

    private void Awake()
    {
        _transform = transform;
        _lineRenderer = GetComponent<LineRenderer>();
        _camera = GetComponent<Camera>();

        _lineRenderer.enabled = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _inCutMode = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetKeyUp(KeyCode.Space))
        {
            _inCutMode = false;
            _lineRenderer.enabled = false;
            _hasStartPos = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (_inCutMode)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _startPos = GetMousePosInWorld();
                _hasStartPos = true;
            }
            else if (_hasStartPos && Input.GetMouseButtonUp(0))
            {
                _endPos = GetMousePosInWorld();
                if (_startPos != _endPos)
                    CreateCutPlane();

                _hasStartPos = false;
                _lineRenderer.enabled = false;
            }

            if (_hasStartPos)
            {
                _lineRenderer.enabled = true;
                _lineRenderer.SetPosition(0, _startPos);
                _lineRenderer.SetPosition(1, GetMousePosInWorld());
            }
        }
    }

    private Vector3 GetMousePosInWorld()
    {
        Ray ray = _camera.ScreenPointToRay(Input.mousePosition);
        return ray.origin + ray.direction * CutPlaneDistance;
    }

    private void CreateCutPlane()
    {
        Vector3 center = Vector3.Lerp(_startPos, _endPos, .5f);
        Vector3 cut = (_endPos - _startPos).normalized;
        Vector3 fwd = (center - _transform.position).normalized;
        Vector3 normal = Vector3.Cross(fwd, cut).normalized;

#if UNITY_EDITOR
        if (ShowDebug)
        {
            Debug.DrawLine(center, center + normal, Color.red, 2f);
            Debug.DrawLine(center + fwd * CutPlaneSize / 2f + cut * CutPlaneSize / 2f, center + fwd * CutPlaneSize / 2f + cut * CutPlaneSize / -2f, Color.green, 2f);
            Debug.DrawLine(center + fwd * CutPlaneSize / 2f + cut * CutPlaneSize / -2f, center + fwd * CutPlaneSize / -2f + cut * CutPlaneSize / -2f, Color.green, 2f);
            Debug.DrawLine(center + fwd * CutPlaneSize / -2f + cut * CutPlaneSize / -2f, center + fwd * CutPlaneSize / -2f + cut * CutPlaneSize / 2f, Color.green, 2f);
            Debug.DrawLine(center + fwd * CutPlaneSize / -2f + cut * CutPlaneSize / 2f, center + fwd * CutPlaneSize / 2f + cut * CutPlaneSize / 2f, Color.green, 2f);
        }
#endif

        GameObject goCutPlane = new GameObject("CutPlane", typeof(BoxCollider), typeof(Rigidbody), typeof(SplitterSingleCut));

        goCutPlane.GetComponent<Collider>().isTrigger = true;
        Rigidbody bodyCutPlane = goCutPlane.GetComponent<Rigidbody>();
        bodyCutPlane.useGravity = false;
        bodyCutPlane.isKinematic = true;

        Transform transformCutPlane = goCutPlane.transform;
        transformCutPlane.position = center;
        transformCutPlane.localScale = new Vector3(CutPlaneSize, .01f, CutPlaneSize);
        transformCutPlane.up = normal;
        float angleFwd = Vector3.Angle(transformCutPlane.forward, fwd);
        transformCutPlane.RotateAround(center, normal, normal.y < 0f ? -angleFwd : angleFwd);
    }
}
