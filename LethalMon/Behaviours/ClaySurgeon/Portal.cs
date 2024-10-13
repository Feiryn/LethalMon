using UnityEngine;

namespace LethalMon.Behaviours.ClaySurgeon;

internal class Portal : MonoBehaviour
{
    public Camera playerCamera;

    public Transform portal;
    
    public Transform otherPortal;

    public Camera portalCamera;
    
    void LateUpdate()
    {
        Vector3 playerOffsetFromPortal = playerCamera.transform.position - otherPortal.position;
        portalCamera.transform.position = portal.position + playerOffsetFromPortal;
        
        float angularDifferenceBetweenPortalRotations = Quaternion.Angle(portal.rotation, otherPortal.rotation);
        
        Quaternion portalRotationalDifference = Quaternion.AngleAxis(angularDifferenceBetweenPortalRotations, Vector3.up);
        Vector3 newCameraDirection = portalRotationalDifference * playerCamera.transform.forward;
        portalCamera.transform.rotation = Quaternion.LookRotation(newCameraDirection, Vector3.up);
        
        Plane p = new Plane(otherPortal.forward, otherPortal.position);
        Vector4 clipPlaneWorldSpace = new Vector4(p.normal.x, p.normal.y, p.normal.z, p.distance);
        Vector4 clipPlaneCameraSpace = Matrix4x4.Transpose(Matrix4x4.Inverse(portalCamera.worldToCameraMatrix)) * clipPlaneWorldSpace;

        Matrix4x4 newMatrix = playerCamera.CalculateObliqueMatrix(clipPlaneCameraSpace);
        portalCamera.projectionMatrix = newMatrix;
    }
}