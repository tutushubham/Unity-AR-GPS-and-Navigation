using UnityEngine;
using UnityEngine.UI;
using Wikitude;
using System.Collections;

public class CameraSettingsController : SampleController
{
    public WikitudeCamera Camera;
    public ImageTracker CurrentImageTracker;

    /* Reference to all the UI controls. */
    public GameObject ConfirmationTab;
    public GameObject ControlsLayout;
    public Dropdown PositionDropdown;
    public GameObject FocusModeLayout;
    public Dropdown FocusModeDropdown;
    public GameObject AutoFocusRestrictionLayout;
    public Dropdown AutoFocusRestrictionDropdown;
    public GameObject FlashModeLayout;
    public Dropdown FlashModeDropdown;
    public GameObject ZoomLayout;
    public Scrollbar ZoomScrollbar;
    public GameObject ManualFocusLayout;
    public GameObject FocusPoint;

    /* Prefabs for the WikitudeCamera, ImageTracker and WikitudeEye augmentations, in case we need to recreate them. */
    public GameObject WikitudeCameraPrefab;
    public GameObject ImageTrackerPrefab;
    public GameObject WikitudeEyePrefab;

    private bool _suppressErrors = false;

    public void OnCameraOpened() {
        /* After the camera was opened, check what capabilities it has, to make sure that only the appropriate controls are displayed. */

        /* When checking if a certain camera feature is supported, the SDK will report an error if it is not, indicating the reason.
         * Since we are not interested in the reason, we temporarily suppress error handling until the end of the method.
         */
        _suppressErrors = true;
        PositionDropdown.value = (int)Camera.DevicePosition;

        if (Camera.IsFocusModeSupported(CaptureFocusMode.AutoFocus)) {
            FocusModeLayout.SetActive(true);
            FocusModeDropdown.value = (int)Camera.FocusMode;
        } else {
            FocusModeLayout.SetActive(false);
        }
        /* AutoFocusRestriction is only available on iOS, so disable the controls for other platforms. */
        if (Application.platform == RuntimePlatform.IPhonePlayer && Camera.IsAutoFocusRestrictionSupported) {
            AutoFocusRestrictionLayout.SetActive(true);
            AutoFocusRestrictionDropdown.value = (int)Camera.AutoFocusRestriction;
        } else {
            AutoFocusRestrictionLayout.SetActive(false);
        }
        if (Camera.IsFlashModeSupported(CaptureFlashMode.On)) {
            FlashModeLayout.SetActive(true);
            FlashModeDropdown.value = (int)Camera.FlashMode;
        } else {
            FlashModeLayout.SetActive(false);
        }
        if (Mathf.Approximately(Camera.MaxZoomLevel, 1.0f)) {
            /* If the maximum zoom level is 1 or close to 1, the camera doesn't support zooming. */
            ZoomLayout.SetActive(false);
        } else {
            ZoomLayout.SetActive(true);
            ZoomScrollbar.value = (Camera.ZoomLevel - 1.0f) / (Camera.MaxZoomLevel - 1.0f);
        }

        /* Manual focus in only available when the FocusMode is set to Locked. Otherwise the control is disabled. */
        if (Camera.FocusMode == CaptureFocusMode.Locked && Camera.IsFocusModeSupported(CaptureFocusMode.Locked) && Camera.IsManualFocusAvailable) {
            ManualFocusLayout.SetActive(true);
        } else {
            ManualFocusLayout.SetActive(false);
        }

        StartCoroutine(EnableErrors());
    }

    private IEnumerator EnableErrors() {
        /* Because errors are reported in the next frame, wait a frame before enabling the errors back. */
        yield return null;
        _suppressErrors = false;
    }

    public void OnCameraControlsButtonClicked() {
        ControlsLayout.SetActive(!ControlsLayout.activeSelf);
    }

    public void OnPositionChanged(int newPosition) {
        Camera.DevicePosition = (CaptureDevicePosition)newPosition;
    }

    public void OnFocusModeChanged(int newFocusMode) {
        Camera.FocusMode = (CaptureFocusMode)newFocusMode;

        if (Camera.FocusMode == CaptureFocusMode.Locked) {
            if (Camera.IsManualFocusAvailable) {
                ManualFocusLayout.SetActive(true);
            }
        } else {
            ManualFocusLayout.SetActive(false);
        }
    }

    public void OnAutoFocusChanged(int newAutoFocus) {
        Camera.AutoFocusRestriction = (CaptureAutoFocusRestriction)newAutoFocus;
    }

    public void OnFlashModeChanged(int newFlashMode) {
        Camera.FlashMode = (CaptureFlashMode)newFlashMode;
    }

    public void OnZoomLevelChanged(float newZoomLevel) {
        Camera.ZoomLevel = newZoomLevel * (Camera.MaxZoomLevel - 1.0f) + 1.0f;
    }

    public void OnManualFocusChanged(float manualFocus) {
        Camera.ManualFocusDistance = manualFocus;
    }

    public void OnBackgroundClicked() {
        /* The background is a UI component that covers the entire screen and is triggered when no other control was pressed.
         * It is used to trigger focus and expose at point of interest commands.
         */

        /* When checking if a certain camera feature is supported, the SDK will report an error if it is not, indicating the reason.
         * Since we are not interested in the reason, we temporarily suppress error handling until the end of the method.
         */
        _suppressErrors = true;
        bool isFocusAtPointOfInterestSupported = Camera.IsFocusAtPointOfInterestSupported;
        bool isExposeAtPointOfInterestSupported = Camera.IsExposeAtPointOfInterstSupported;

        if (isFocusAtPointOfInterestSupported || isExposeAtPointOfInterestSupported) {
            var position = Input.mousePosition;
            FocusPoint.SetActive(true);
            FocusPoint.GetComponent<RectTransform>().position = position;

            if (Camera.IsFocusAtPointOfInterestSupported) {
                Camera.FocusAtPointOfInterest(position);
            }

            if (isExposeAtPointOfInterestSupported) {
                Camera.ExposeAtPointOfInterest(position, CaptureExposureMode.ContinuousAutoExpose);
            }
        } else {
            FocusPoint.SetActive(false);
        }

        StartCoroutine(EnableErrors());
    }

    public override void OnCameraError(Error error) {
        if (!_suppressErrors) {
            /* Log the error to the on-screen console */
            base.OnCameraError(error);

            /* If there was an error try to restart using Camera 1 API if running on Android
            * WikitudeCamera properties cannot be changed while it is running,
            * so we need to destroy it and create it back from prefabs.
            */
            if (Application.platform == RuntimePlatform.Android && Camera.EnableCamera2) {
                ShowRestartConfirmationTab();
            }
        }
    }

    private void ShowRestartConfirmationTab() {
        ConfirmationTab.SetActive(true);
    }

    private void HideRestatConfirmationTab() {
        ConfirmationTab.SetActive(false);
    }

    public void OnRestartButtonPressed() {
        HideRestatConfirmationTab();
        StartCoroutine(Restart());
    }

    public void OnCancelRestartButtonPressed() {
        HideRestatConfirmationTab();
    }

    private IEnumerator Restart() {
        /* Destroy the existing ImageTracker and WikitudeCamera before creating them again. */
        Destroy(CurrentImageTracker.gameObject);
        Destroy(Camera.gameObject);

        /* Wait a frame before recreating everything again. */
        yield return null;

        Camera = GameObject.Instantiate(WikitudeCameraPrefab).GetComponent<WikitudeCamera>();
        CurrentImageTracker = GameObject.Instantiate(ImageTrackerPrefab).GetComponent<ImageTracker>();
        GameObject.Instantiate(WikitudeEyePrefab).transform.SetParent(CurrentImageTracker.transform.GetChild(0));
    }
}
