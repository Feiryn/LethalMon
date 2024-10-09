using UnityEngine;

namespace LethalMon.Behaviours.ClaySurgeon;

internal class Portal : MonoBehaviour
{
    public Camera playerCamera;

    public Transform portal;
    
    public Transform otherPortal;

    public Camera portalCamera;
    
    Quaternion QuaternionFromMatrix(Matrix4x4 m) { return Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)); }
    
    Vector4 PosToV4(Vector3 v) { return new Vector4(v.x, v.y, v.z, 1.0f); }
    
    Vector3 ToV3(Vector4 v) { return new Vector3(v.x, v.y, v.z); }
    
    void LateUpdate()
    {
 
    }
}