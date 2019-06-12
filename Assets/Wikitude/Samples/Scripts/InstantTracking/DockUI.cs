using UnityEngine;

/* Script that scales the dock used in the Instant Tracking samples based on the aspect ratio. */
public class DockUI : MonoBehaviour
{
    public RectTransform ButtonsBackground;
    public RectTransform ButtonsParent;

    private void Awake() {
        Update();
    }

    private void Update() {
        float currentRatio = (float)Screen.width / (float)Screen.height;
        if (currentRatio > 1.0f) {
            ButtonsBackground.sizeDelta = new Vector2(0f, -200f);
            ButtonsParent.sizeDelta = new Vector2(-600f, 0f);
        } else {
            ButtonsBackground.sizeDelta = new Vector2(0f, -206f);
            ButtonsParent.sizeDelta = new Vector2(0f, 0f);
        }
    }
}
