using UnityEngine;

namespace MeshSplitting.Demo
{
    [AddComponentMenu("Player/RigidMotor")]
    [RequireComponent(typeof(Rigidbody))]
    public class RigidMotor : MonoBehaviour
    {
        public float Speed = 10f;

        private Transform _transform = null;
        public new Transform transform
        {
            get
            {
                if (_transform == null) _transform = GetComponent<Transform>();
                return _transform;
            }
        }

        private Rigidbody _rigidbody = null;
        public new Rigidbody rigidbody
        {
            get
            {
                if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
                return _rigidbody;
            }
        }

        private void Awake()
        {
            rigidbody.freezeRotation = true;
            rigidbody.useGravity = false;
        }

        private void FixedUpdate()
        {
            Vector3 moveDirection = Vector3.zero;
            if (!Input.GetKey(KeyCode.Space))
            {
                moveDirection = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            }

            Vector3 targetVelocity = transform.TransformDirection(moveDirection) * Speed;
            Vector3 changeVelocity = targetVelocity - rigidbody.velocity;
            changeVelocity.y = 0f;

            float deltaVelocity = changeVelocity.magnitude;
            if (deltaVelocity > Speed)
            {
                changeVelocity = changeVelocity / deltaVelocity * Speed;
            }

            rigidbody.AddForce(changeVelocity, ForceMode.VelocityChange);
            rigidbody.AddForce(Physics.gravity * rigidbody.mass, ForceMode.Force);
        }
    }
}
