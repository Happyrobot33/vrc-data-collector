
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Llamahat.WorldMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OptionSelectorButton : UdonSharpBehaviour
    {
        public OptionSelector selector;
        public int optionIndex;

        public override void Interact() => Press();
        public void OnButtonClick() => Press();

        public void Press()
        {
            if (selector == null) return;
            selector.SelectIndex(optionIndex);
        }
    }
}