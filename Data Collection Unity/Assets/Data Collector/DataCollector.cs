
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;
using VRC.Udon;
using UnityEngine.SceneManagement;

//datacollector scene preprocess
#if !COMPILER_UDONSHARP && UNITY_EDITOR
using VRC.SDKBase.Editor.BuildPipeline;
using UnityEditor;
using UnityEditorInternal;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using VRC.Core;

public class DataCollectorScenePreprocessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        PipelineManager pipelineManager = GameObject.FindObjectOfType<PipelineManager>();
        string worldId = pipelineManager.blueprintId;

        //find all data collectors
        DataCollector[] collectors = GameObject.FindObjectsOfType<DataCollector>();
        foreach (DataCollector collector in collectors)
        {
            collector.worldid = worldId;
        }
    }
}
#endif

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class DataCollector : UdonSharpBehaviour
{
    [HideInInspector]
    public bool ReceiveDataFromPlayers = false;
    [Tooltip("Generate synthetic data to fill up to 80 players")]
    public bool GenerateSyntheticData = false;
    [HideInInspector]
    public string worldid = "";
    public string BotAccountName = "";

    void Start()
    {
        if (Networking.LocalPlayer.displayName == BotAccountName)
        {
            ReceiveDataFromPlayers = true;
        }
    }

    void Update()
    {
        if (!ReceiveDataFromPlayers)
        {
            return;
        }

        DataDictionary dataBlob = new DataDictionary();
        //add the current collection time
        dataBlob.Add("utc time", DateTime.UtcNow.Ticks);

        dataBlob.Add("total players", VRCPlayerApi.GetPlayerCount());

        dataBlob.Add("world id", worldid);

        //get all players
        VRCPlayerApi[] players = new VRCPlayerApi[VRCPlayerApi.GetPlayerCount()];
        VRCPlayerApi.GetPlayers(players);

        DataList playerList = new DataList();
        foreach (VRCPlayerApi player in players)
        {
            //check if we are allowed
            if (!GDPRComplianceControl.IsAllowedToCollect(player))
            {
                continue;
            }

            DataDictionary playerData = CollectPlayerData(player);
            playerList.Add(playerData);
        }

        if (GenerateSyntheticData)
        {
            //synthetic generation, fill up to 80 players
            for (int i = playerList.Count + 1; i <= 80; i++)
            {
                DataDictionary syntheticData = CollectPlayerData(Networking.LocalPlayer, true, i);
                playerList.Add(syntheticData);
                //Debug.Log(i);
            }
        }

        dataBlob.Add("player data collected", playerList);

        //serialize the data
        if (VRCJson.TrySerializeToJson(dataBlob, JsonExportType.Minify, out DataToken json))
        {
            Debug.Log("DATACOLLECTOR_JSON: " + json.ToString());
        }
        else
        {
            Debug.LogError("Failed to serialize data");
        }
    }

    public void ToggleCollection()
    {
        ReceiveDataFromPlayers = !ReceiveDataFromPlayers;
    }

    private DataDictionary CollectPlayerData(VRCPlayerApi player, bool synthetic = false, int syntheticId = -1)
    {
        DataDictionary collectedPlayerData = new DataDictionary();
        string name = player.displayName;
        //if its synthetic, then change the playername to synthetic
        if (synthetic)
        {
            name = $"Synthetic Player {name}_synthetic_{syntheticId}";
        }
        collectedPlayerData.Add("display name", name);
        collectedPlayerData.Add("player id", player.playerId);

        collectedPlayerData.Add("position", EncodePosition(player.GetPosition()));

        //get euler angles
        Quaternion rotation = player.GetRotation();
        Vector3 eulerAngles = rotation.eulerAngles;
        collectedPlayerData.Add("rotation", eulerAngles.y);

        collectedPlayerData.Add("size", player.GetAvatarEyeHeightAsMeters());

        collectedPlayerData.Add("vr", player.IsUserInVR());

        collectedPlayerData.Add("is grounded", player.IsPlayerGrounded());

        //get the FPS data
        GameObject[] playerobjects = player.GetPlayerObjects();

        //find the player specific collector
        foreach (GameObject playerobject in playerobjects)
        {
            PlayerSpecificCollector collector = playerobject.GetComponent<PlayerSpecificCollector>();
            if (collector != null)
            {
                collectedPlayerData.Add("fps", collector.FPS);

                collectedPlayerData.Add("left eye position", EncodePosition(collector.LeftEyePosition));
                collectedPlayerData.Add("left eye rotation", EncodeQuaternion(collector.LeftEyeRotation));

                collectedPlayerData.Add("right eye position", EncodePosition(collector.RightEyePosition));
                collectedPlayerData.Add("right eye rotation", EncodeQuaternion(collector.RightEyeRotation));

                collectedPlayerData.Add("head position", EncodePosition(collector.HeadPosition));
                collectedPlayerData.Add("head rotation", EncodeQuaternion(collector.HeadRotation));

                collectedPlayerData.Add("right hand position", EncodePosition(collector.RightHandPosition));
                collectedPlayerData.Add("right hand rotation", EncodeQuaternion(collector.RightHandRotation));

                collectedPlayerData.Add("left hand position", EncodePosition(collector.LeftHandPosition));
                collectedPlayerData.Add("left hand rotation", EncodeQuaternion(collector.LeftHandRotation));
                break;
            }
        }

        return collectedPlayerData;
    }

    private DataDictionary EncodePosition(Vector3 position)
    {
        DataDictionary posDict = new DataDictionary();
        posDict.Add("x", position.x);
        posDict.Add("y", position.y);
        posDict.Add("z", position.z);
        return posDict;
    }

    private DataDictionary EncodeQuaternion(Quaternion rotation)
    {
        DataDictionary rotDict = new DataDictionary();
        rotDict.Add("x", rotation.x);
        rotDict.Add("y", rotation.y);
        rotDict.Add("z", rotation.z);
        rotDict.Add("w", rotation.w);
        return rotDict;
    }
}
