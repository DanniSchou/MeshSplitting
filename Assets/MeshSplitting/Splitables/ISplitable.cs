using UnityEngine;

public static class SplitableJointHelper
{
    public delegate void JointHandler(Rigidbody bodyOrig, Rigidbody bodyUpper, Rigidbody bodyLower);
}

public interface ISplitable
{
    void Split(Transform splitTransform);
}
