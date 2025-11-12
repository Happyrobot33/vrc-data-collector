
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Persistence;
using UnityEngine.UI;
using VRC.SDK3.Rendering;

//[RequireComponent(typeof(VRCPlayerObject))]
[UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
public class PlayerSpecificCollector : UdonSharpBehaviour
{
    [UdonSynced]
    public float FPS;

    [UdonSynced]
    public Vector3 LeftHandPosition = Vector3.zero;
    [UdonSynced]
    public Vector3 RightHandPosition = Vector3.zero;

    [UdonSynced]
    public Quaternion LeftHandRotation = Quaternion.identity;
    [UdonSynced]
    public Quaternion RightHandRotation = Quaternion.identity;

    [UdonSynced]
    public Vector3 HeadPosition = Vector3.zero;
    [UdonSynced]
    public Quaternion HeadRotation = Quaternion.identity;

    const float UpdateInterval = 0.5f;

    void Start()
    {
        //check if the local player owns this object
        if (Networking.GetOwner(gameObject) == Networking.LocalPlayer)
        {
            //start the coroutine
            _Coroutine();
        }
    }

    public void _Coroutine()
    {
        if (PlayerData.TryGetBool(Networking.LocalPlayer, GDPRComplianceControl.Key, out bool isAllowed))
        {
            if (isAllowed)
            {
                FPS = 1f / Time.smoothDeltaTime;
                /* LeftEyePosition = VRCCameraSettings.GetEyePosition(Camera.StereoscopicEye.Left);
                RightEyePosition = VRCCameraSettings.GetEyePosition(Camera.StereoscopicEye.Right);
                LeftEyeRotation = VRCCameraSettings.GetEyeRotation(Camera.StereoscopicEye.Left);
                RightEyeRotation = VRCCameraSettings.GetEyeRotation(Camera.StereoscopicEye.Right); */
                VRCPlayerApi localPlayer = Networking.LocalPlayer;
                HeadPosition = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                HeadRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                LeftHandPosition = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                LeftHandRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
                RightHandPosition = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                RightHandRotation = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
                RequestSerialization();
            }
        }

        SendCustomEventDelayedSeconds(nameof(_Coroutine), UpdateInterval);
    }
}
