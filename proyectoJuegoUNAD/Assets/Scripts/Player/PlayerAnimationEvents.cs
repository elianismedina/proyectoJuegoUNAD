using UnityEngine;

/// <summary>
/// Lives on the child object that holds the Animator and forwards animation events
/// (<c>OnFootstep</c>, <c>OnLand</c>) to the <see cref="SideScrollerController"/> on the player root.
/// Animation events are delivered to the Animator's own GameObject, not to its parent.
/// </summary>
[RequireComponent(typeof(Animator))]
public class PlayerAnimationEvents : MonoBehaviour
{
    private SideScrollerController _controller;

    private void Awake()
    {
        _controller = GetComponentInParent<SideScrollerController>();
    }

    private void OnFootstep(AnimationEvent animationEvent)
    {
        if (_controller != null) _controller.OnFootstep(animationEvent);
    }

    private void OnLand(AnimationEvent animationEvent)
    {
        if (_controller != null) _controller.OnLand(animationEvent);
    }
}
