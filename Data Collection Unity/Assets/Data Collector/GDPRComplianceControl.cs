
using Llamahat.WorldMenu;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDK3.Persistence;
using VRC.SDKBase;
using VRC.Udon;

[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GDPRComplianceControl : UdonSharpBehaviour
{
    public const string Key = "AllowedToCollect";

    private bool m_cancollect = false;
    public bool canCollect
    {
        get { return m_cancollect; }
        set
        {
            m_cancollect = value;

            PlayerData.SetBool(Key, m_cancollect);
            
            optionSelector.SelectIndex(m_cancollect ? 1 : 0);
        }
    }
    public OptionSelector optionSelector;
    public static bool IsAllowedToCollect(VRCPlayerApi player)
    {
        if (player == null || !player.IsValid())
        {
            return false;
        }

        if (PlayerData.TryGetBool(player, Key, out bool isAllowed))
        {
            return isAllowed;
        }

        return false;
    }

    public void AcceptAgreement()
    {
        canCollect = true;
    }

    public void DeclineAgreement()
    {
        canCollect = false;
    }

    public override void OnPlayerRestored(VRCPlayerApi player)
    {
        // Ensure the toggle reflects the player's data when they are restored
        if (player == Networking.LocalPlayer)
        {
            canCollect = IsAllowedToCollect(player);
        }
    }
}
