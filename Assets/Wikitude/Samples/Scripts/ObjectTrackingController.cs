using UnityEngine;
using Wikitude;

public class ObjectTrackingController : SampleController
{
    /* Name of the object that should trigger the instructions animation when pressed. */
    private const string InstructionMarkerObjectName = "marker";
    /* Name of the object that should trigger the siren when pressed. */
    private const string SirenMarkerObjectName = "marker_siren";
    /* Animation trigger names. */
    private const string PlayTriggerName = "Play Instructions";
    private const string SirenTriggerName = "Play Siren";
    private const string IdleTriggerName = "Play Idle";

    /* Flags to keep track of which animations are currently playing. */
    private bool _isInstructionsAnimationPlaying = false;
    private bool _isSirenAnimationPlaying = false;

    protected override void Start() {
        base.Start();
        QualitySettings.shadowDistance = 8.0f;
    }

    public void OnObjectRecognized(ObjectTarget recognizedTarget) {
        /* Because the augmentation is set as a drawable on the ObjectTrackable, every time a target is recognized,
         * the prefab is reinstantiated and the animations are not playing.
         */
        _isInstructionsAnimationPlaying = false;
        _isSirenAnimationPlaying = false;
    }

    protected override void Update() {
        base.Update();

        /* If a touch was detected, do a raycast to see if any of the trigger objects was hit. */
        if (Input.GetMouseButtonUp(0)) {
            var touchRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hitInfo;
            if (Physics.Raycast(touchRay, out hitInfo)) {
                if (hitInfo.collider.gameObject.name == InstructionMarkerObjectName) {
                    /* Toggle the building instruction animation on or off. */
                    var instructions = hitInfo.collider.transform.parent.gameObject;
                    var animator = instructions.GetComponent<Animator>();
                    if (!_isInstructionsAnimationPlaying) {
                        animator.SetTrigger(PlayTriggerName);
                        animator.ResetTrigger(IdleTriggerName);
                    } else {
                        animator.SetTrigger(IdleTriggerName);
                        animator.ResetTrigger(PlayTriggerName);
                    }
                    _isInstructionsAnimationPlaying = !_isInstructionsAnimationPlaying;
                } else if (hitInfo.collider.gameObject.name == SirenMarkerObjectName) {
                    /* Toggle the siren animation on or off. */
                    var sirenAnimator = hitInfo.collider.transform.parent.GetComponent<Animator>();
                    if (!_isSirenAnimationPlaying) {
                        sirenAnimator.SetTrigger(SirenTriggerName);
                        sirenAnimator.ResetTrigger(IdleTriggerName);
                    } else {
                        sirenAnimator.SetTrigger(IdleTriggerName);
                        sirenAnimator.ResetTrigger(SirenTriggerName);
                    }
                    _isSirenAnimationPlaying = !_isSirenAnimationPlaying;
                }
            }
        }
    }
}
