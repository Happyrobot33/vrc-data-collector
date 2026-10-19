
using System;
using TMPro;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Components;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Persistence;
using UnityEngine.UI;
using VRC.SDK3.Rendering;

namespace DataCollector
{
    public enum PositionIndex
    {
        LeftHand = 0,
        RightHand = 1,
        Head = 2,
        LeftEye = 3,
        RightEye = 4
    }

    public enum RotationIndex
    {
        LeftHand = 0,
        RightHand = 1,
        Head = 2,
        LeftEye = 3,
        RightEye = 4
    }

    //[RequireComponent(typeof(VRCPlayerObject))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class PlayerSpecificCollector : UdonSharpBehaviour
    {
        [UdonSynced]
        public float FPS;

        //These are arrays to work around some weird synching behaviour with many composite fields

        //position values
        //Left Hand Position
        //Right Hand Position
        //Head Position
        //Left Eye Position
        //Right Eye Position
        [UdonSynced]
        public Vector3[] Positions = new Vector3[5];
        
        //rotation values
        //Left Hand Rotation
        //Right Hand Rotation
        //Head Rotation
        //Left Eye Rotation
        //Right Eye Rotation
        [UdonSynced]
        public Quaternion[] Rotations = new Quaternion[5];

        const float UpdateInterval = 0.1f;

        public void Start()
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
                    VRCPlayerApi localPlayer = Networking.LocalPlayer;
                    Positions[(int)PositionIndex.Head] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
                    Rotations[(int)RotationIndex.Head] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).rotation;
                    Positions[(int)PositionIndex.LeftHand] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).position;
                    Rotations[(int)RotationIndex.LeftHand] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.LeftHand).rotation;
                    Positions[(int)PositionIndex.RightHand] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).position;
                    Rotations[(int)RotationIndex.RightHand] = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.RightHand).rotation;
                    Positions[(int)PositionIndex.LeftEye] = localPlayer.GetBonePosition(HumanBodyBones.LeftEye);
                    Rotations[(int)RotationIndex.LeftEye] = localPlayer.GetBoneRotation(HumanBodyBones.LeftEye);
                    Positions[(int)PositionIndex.RightEye] = localPlayer.GetBonePosition(HumanBodyBones.RightEye);
                    Rotations[(int)RotationIndex.RightEye] = localPlayer.GetBoneRotation(HumanBodyBones.RightEye);
                    RequestSerialization();

                    Debug.Log("LocalLogger" 
                    + " utcTime:" + DateTime.UtcNow.Ticks
                    + " Head Pos:" + Positions[(int)PositionIndex.Head].ToString()
                    + " Head Rot:" + Rotations[(int)RotationIndex.Head].ToString()
                    + " HandL Pos:" + Positions[(int)PositionIndex.LeftHand].ToString()
                    + " HandL Rot:" + Rotations[(int)RotationIndex.LeftHand].ToString()
                    + " HandR Pos:" + Positions[(int)PositionIndex.RightHand].ToString()
                    + " HandR Rot:" + Rotations[(int)RotationIndex.RightHand].ToString()
                    + " EyeL Pos:" + Positions[(int)PositionIndex.LeftEye].ToString()
                    + " EyeL Rot:" + Rotations[(int)RotationIndex.LeftEye].ToString()
                    + " EyeR Pos:" + Positions[(int)PositionIndex.RightEye].ToString()
                    + " EyeR Rot:" + Rotations[(int)RotationIndex.RightEye].ToString()
                    );
                }
            }

            SendCustomEventDelayedSeconds(nameof(_Coroutine), UpdateInterval);
        }
    }
}
