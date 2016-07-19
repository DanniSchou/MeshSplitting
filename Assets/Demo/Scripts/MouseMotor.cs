using UnityEngine;

namespace MeshSplitting.Demo
{
    [AddComponentMenu("Player/MouseMotor")]
    public class MouseMotor : MonoBehaviour
    {
        public enum RotationAxes { MouseXAndY = 0, MouseX = 1, MouseY = 2 }
        public RotationAxes Axes = RotationAxes.MouseXAndY;
        public float SensitivityX = 15f;
        public float SensitivityY = 15f;

        public float MinimumX = -360f;
        public float MaximumX = 360f;

        public float MinimumY = -60f;
        public float MaximumY = 60f;

        private float _rotationY = 0f;

        private Transform _transform = null;
        public new Transform transform
        {
            get
            {
                if (_transform == null) _transform = GetComponent<Transform>();
                return _transform;
            }
        }

        private void Awake()
        {
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.freezeRotation = true;
            }
        }

        public void FixedUpdate()
        {
            Vector2 mouseDelta = Vector2.zero;
            if (!Input.GetKey(KeyCode.Space))
            {
                mouseDelta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
            }

            if (Axes == RotationAxes.MouseXAndY)
            {
                float rotationX = transform.localEulerAngles.y + mouseDelta.x * SensitivityX;
                _rotationY += mouseDelta.y * SensitivityY;
                _rotationY = Mathf.Clamp(_rotationY, MinimumY, MaximumY);

                transform.localEulerAngles = new Vector3(-_rotationY, rotationX, 0f);
            }
            else if (Axes == RotationAxes.MouseX)
            {
                transform.Rotate(0f, mouseDelta.x * SensitivityX, 0f);
            }
            else
            {
                _rotationY += mouseDelta.y * SensitivityY;
                _rotationY = Mathf.Clamp(_rotationY, MinimumY, MaximumY);

                transform.localEulerAngles = new Vector3(-_rotationY, transform.localEulerAngles.y, 0f);
            }
        }
    }
}
