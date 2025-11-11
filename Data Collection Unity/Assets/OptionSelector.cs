using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace Llamahat.WorldMenu
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OptionSelector : UdonSharpBehaviour
    {
        // assign the visible selector object (the thing that moves)
        public Transform selector;

        // positions (anchors) for each selectable option
        public Transform[] optionAnchors;

        // current selected index
        public int currentIndex;

        [Tooltip("Seconds it takes for the selector to move.")]
        public float transitionDuration = 0.2f;

        [Tooltip("If true, match selector scale to the anchors once at Start.")]
        public bool matchSizeAtStart = true;

        bool isTransitioning;
        Vector3 fromPosition;
        Vector3 toPosition;
        float transitionElapsed;

        void Start()
        {
            if (!matchSizeAtStart || selector == null || optionAnchors == null || optionAnchors.Length == 0) return;
            RectTransform anchorRect = optionAnchors[0].GetComponent<RectTransform>();
            RectTransform selectorRect = selector.GetComponent<RectTransform>();
            selectorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, anchorRect.rect.width);
            selectorRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, anchorRect.rect.height);
        }

        void Update()
        {
            if (selector == null || optionAnchors == null || optionAnchors.Length == 0) return;

            currentIndex = Mathf.Clamp(currentIndex, 0, optionAnchors.Length - 1);
            var targetPos = optionAnchors[currentIndex].position;

            if (!isTransitioning && selector.position != targetPos)
                StartTransition(targetPos);

            if (!isTransitioning) return;

            transitionElapsed += Time.deltaTime;
            var t = Mathf.Clamp01(transitionElapsed / Mathf.Max(0.0001f, transitionDuration));
            selector.position = Vector3.Lerp(fromPosition, toPosition, Mathf.SmoothStep(0f, 1f, t));

            if (t >= 1f)
            {
                isTransitioning = false;
                selector.position = toPosition;
            }
        }

        void StartTransition(Vector3 destination)
        {
            fromPosition = selector.position;
            toPosition = destination;
            transitionElapsed = 0f;
            isTransitioning = true;
        }

        // Public API

        // Select a specific option by index (animated)
        public void SelectIndex(int index)
        {
            if (optionAnchors == null || optionAnchors.Length == 0) return;
            currentIndex = Mathf.Clamp(index, 0, optionAnchors.Length - 1);
            StartTransition(optionAnchors[currentIndex].position);
        }

        // Select the next option (animated)
        public void SelectNext() => SelectIndex(currentIndex + 1);

        // Select the previous option (animated)
        public void SelectPrevious() => SelectIndex(currentIndex - 1);

        // Immediately jump to an index without animation
        public void JumpToIndexImmediate(int index)
        {
            if (optionAnchors == null || optionAnchors.Length == 0) return;
            currentIndex = Mathf.Clamp(index, 0, optionAnchors.Length - 1);
            isTransitioning = false;
            selector.position = optionAnchors[currentIndex].position;
        }
    }
}

