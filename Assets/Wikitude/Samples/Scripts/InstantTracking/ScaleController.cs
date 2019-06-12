using UnityEngine;

/* Script that controls scaling the furniture augmentations. */
public class ScaleController : MonoBehaviour
{
    public float MinScale = 0.1f;
    public float MaxScale = 0.5f;

    private InstantTrackingController _controller;
    private Transform _activeObject = null;

    private Vector3 _touch1StartGroundPosition;
    private Vector3 _touch2StartGroundPosition;
    private Vector3 _startObjectScale;

    private void Start () {
        _controller = GetComponent<InstantTrackingController>();
    }

    private void Update () {
        if (Input.touchCount >= 2) {
            /* We need at least two touches to perform a scaling gesture */
            Touch touch1 = Input.GetTouch(0);
            Touch touch2 = Input.GetTouch(1);
            Transform hitTransform;

            /* If we're currently not scaling any augmentation, do a raycast for each touch position to find one. */
            if (_activeObject == null) {
                /* If either touch hits an augmentation, or if their average position hits an augmentation,
                 * use that augmentation for scaling.
                 */
                if (GetTouchObject(touch1.position, out hitTransform)) {
                    SetTouchObject(hitTransform);
                } else if (GetTouchObject(touch2.position, out hitTransform)) {
                    SetTouchObject(hitTransform);
                } else if (GetTouchObject((touch1.position + touch2.position) / 2, out hitTransform)) {
                    SetTouchObject(hitTransform);
                }

                if (_activeObject != null) {
                    _touch1StartGroundPosition = GetGroundPosition(touch1.position);
                    _touch2StartGroundPosition = GetGroundPosition(touch2.position);
                    _startObjectScale = _activeObject.localScale;
                }
            }

            /* If we are scaling an augmentation, do a raycast for each touch position
             * against the ground plane and use the change in the magnitude of the vector between the hits
             * to scale the augmentation.
             */
            if (_activeObject != null) {
                var touch1GroundPosition = GetGroundPosition(touch1.position);
                var touch2GroundPosition = GetGroundPosition(touch2.position);

                float startMagnitude = (_touch1StartGroundPosition - _touch2StartGroundPosition).magnitude;
                float currentMagnitude = (touch1GroundPosition - touch2GroundPosition).magnitude;

                _activeObject.localScale = _startObjectScale * (currentMagnitude / startMagnitude);

                /* Make sure the scale is within reasonable bounds. */
                if (_activeObject.localScale.x < MinScale) {
                    _activeObject.localScale = new Vector3(MinScale, MinScale, MinScale);
                }

                if (_activeObject.localScale.x > MaxScale) {
                    _activeObject.localScale = new Vector3(MaxScale, MaxScale, MaxScale);
                }
            }
        } else {
            /* If there are less than two touches on the screen, stop scaling the currently scaled augmentation,
             * if there is one.
             */
            _activeObject = null;
        }
    }

    private bool GetTouchObject(Vector2 touchPosition, out Transform hitTransform) {
        var touchRay = Camera.main.ScreenPointToRay(touchPosition);
        touchRay.origin -= touchRay.direction * 100.0f;

        RaycastHit hit;
        if (Physics.Raycast(touchRay, out hit)) {
            hitTransform = hit.transform;
            return true;
        }

        hitTransform = null;
        return false;
    }

    private Vector3 GetGroundPosition(Vector2 touchPosition) {
        var groundPlane = new Plane(Vector3.up, Vector3.zero);
        var touchRay = Camera.main.ScreenPointToRay(touchPosition);
        float enter;
        if (groundPlane.Raycast(touchRay, out enter)) {
            return touchRay.GetPoint(enter);
        }
        return Vector3.zero;
    }

    private void SetTouchObject(Transform newObject) {
        if (_controller.ActiveModels.Contains(newObject.gameObject)) {
            _activeObject = newObject;
        }
    }
}
