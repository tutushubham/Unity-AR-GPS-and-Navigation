using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using Wikitude;
using System;

using Plane = UnityEngine.Plane;

public class InstantTrackingController : SampleController
{
    /* The GameObject that contains the all the furniture buttons. */
    public GameObject ButtonDock;
    /* The GameObject that contains the UI elements used to initialize instant tracking. */
    public GameObject InitializationControls;
    /* The label indicating the current DeviceHeightAboveGround. */
    public Text HeightLabel;

    public InstantTracker Tracker;

    public Button ResetButton;

    /* The order in theses arrays indicate which button corresponds to which model. */
    public List<Button> Buttons;
    public List<GameObject> Models;

    public Text MessageBox;

    /* Status bar at the bottom of the screen, indicating if the scene is being tracked or not. */
    public Image ActivityIndicator;

    /* The colors of the bottom status status bar */
    public Color EnabledColor = new Color(0.2f, 0.75f, 0.2f, 0.8f);
    public Color DisabledColor = new Color(1.0f, 0.2f, 0.2f, 0.8f);

    /* Controller that moves the furniture based on user input. */
    private MoveController _moveController;

    /* Renders the grid used when initializing the tracker, indicating the ground plane. */
    private GridRenderer _gridRenderer;

    /* The currently rendered augmentations. */
    private HashSet<GameObject> _activeModels = new HashSet<GameObject>();
    /* The state in which the tracker currently is. */
    private InstantTrackingState _currentState = InstantTrackingState.Initializing;
    public InstantTrackingState CurrentState {
        get { return _currentState; }
    }
    private bool _isTracking = false;

    public HashSet<GameObject> ActiveModels {
        get {
            return _activeModels;
        }
    }

    private void Awake() {
        Application.targetFrameRate = 60;

        _moveController = GetComponent<MoveController>();
        _gridRenderer = GetComponent<GridRenderer>();
    }

    protected override void Start() {
        base.Start();
        QualitySettings.shadowDistance = 4.0f;

        MessageBox.text = "Starting the SDK";
        /* The Wikitude SDK needs to be fully started before we can query for platform assisted tracking support
         * SDK initialization happens during start, so we wait one frame in a coroutine
         */
        StartCoroutine(CheckPlatformAssistedTrackingSupport());
    }

    private IEnumerator CheckPlatformAssistedTrackingSupport() {
        yield return null;
        if (Tracker.SMARTEnabled) {
            Tracker.IsPlatformAssistedTrackingSupported((SmartAvailability smartAvailability) => {
                UpdateTrackingMessage(smartAvailability);
            });
        }
    }

    private void UpdateTrackingMessage(SmartAvailability smartAvailability) {
        if (Tracker.SMARTEnabled) {
            string sdk;
            if (Application.platform == RuntimePlatform.Android) {
                sdk = "ARCore";
            } else if (Application.platform == RuntimePlatform.IPhonePlayer) {
                sdk = "ARKit";
            } else {
                MessageBox.text = "Running without platform assisted tracking support.";
                return;
            }

            switch (smartAvailability) {
                case SmartAvailability.IndeterminateQueryFailed: {
                    MessageBox.text = "Platform support query failed. Running without platform assisted tracking support.";
                    break;
                }
                case SmartAvailability.CheckingQueryOngoing: {
                    MessageBox.text = "Platform support query ongoing.";
                    break;
                }
                case SmartAvailability.Unsupported: {
                    MessageBox.text = "Running without platform assisted tracking support.";
                    break;
                }
                case SmartAvailability.SupportedUpdateRequired:
                case SmartAvailability.Supported: {
                    string runningWithMessage = "Running with platform assisted tracking support (" + sdk + ").";

                    if (_currentState == InstantTrackingState.Tracking) {
                        MessageBox.text = runningWithMessage;
                    } else {
                        MessageBox.text = runningWithMessage + "\n Move your phone around until the target turns green, which is when you can start tracking.";
                    }
                    break;
                }
            }
        } else {
            MessageBox.text = "Running without platform assisted tracking support.";
        }
    }

    protected override void Update() {
        base.Update();
        if (_currentState == InstantTrackingState.Initializing) {
            /* Change the color of the grid to indicate if tracking can be started or not. */
            if (Tracker.CanStartTracking()) {
                _gridRenderer.TargetColor = Color.green;
            } else {
                _gridRenderer.TargetColor = GridRenderer.DefaultTargetColor;
            }
        } else {
            _gridRenderer.TargetColor = GridRenderer.DefaultTargetColor;
        }
    }

    #region UI Events
    public void OnInitializeButtonClicked() {
        Tracker.SetState(InstantTrackingState.Tracking);
    }

    public void OnHeightValueChanged(float newHeightValue) {
        HeightLabel.text = string.Format("{0:0.##} m", newHeightValue);
        Tracker.DeviceHeightAboveGround = newHeightValue;
    }

    public void OnBeginDrag (int modelIndex) {
        if (_isTracking) {
            /* If we're tracking, instantiate a new model prefab based on the button index and */
            GameObject modelPrefab = Models[modelIndex];
            Transform model = Instantiate(modelPrefab).transform;
            _activeModels.Add(model.gameObject);
            /* Set model position at touch position */
            var cameraRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            Plane p = new Plane(Vector3.up, Vector3.zero);
            float enter;
            if (p.Raycast(cameraRay, out enter)) {
                model.position = cameraRay.GetPoint(enter);
            }

            /* Set model orientation to face toward the camera */
            Quaternion modelRotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-Camera.main.transform.forward, Vector3.up), Vector3.up);
            model.rotation = modelRotation;

            /* Assign the new model to the move controller, so that it can be further dragged after it leaves the button area. */
            _moveController.SetMoveObject(model);
        }
    }

    public void OnResetButtonClicked() {
        Tracker.SetState(InstantTrackingState.Initializing);
        ResetButton.gameObject.SetActive(false);
    }
    #endregion

    #region Tracker Events
    public void OnSceneRecognized(InstantTarget target) {
        SetSceneActive(true);
    }

    public void OnSceneLost(InstantTarget target) {
        SetSceneActive(false);
    }

    private void SetSceneActive(bool active) {
        /* Because SetSceneActive(false) can be called when the scene is destroyed,
         * first check if the GameObjects and Components are still valid.
         */
        foreach (var button in Buttons) {
            if (button) {
                button.interactable = active;
            }
        }

        foreach (var model in _activeModels) {
            if (model) {
                model.SetActive(active);
            }
        }

        if (ActivityIndicator) {
            ActivityIndicator.color = active ? EnabledColor : DisabledColor;
        }

        if (_gridRenderer) {
            _gridRenderer.enabled = active;
        }
        _isTracking = active;
    }

    public void OnStateChanged(InstantTrackingState newState) {
        _currentState = newState;
        if (newState == InstantTrackingState.Tracking) {
            if (InitializationControls != null) {
                InitializationControls.SetActive(false);
            }
            ButtonDock.SetActive(true);
            ResetButton.gameObject.SetActive(true);
        } else {
            /* When the state is changed back to initialization, make sure that all the previous augmentations are cleared */
            foreach (var model in _activeModels) {
                Destroy(model);
            }
            _activeModels.Clear();

            if (InitializationControls != null) {
                InitializationControls.SetActive(true);
            }
            ButtonDock.SetActive(false);
        }
    }

    /* Used when augmentations are loaded from disk. Please see SaveInstantTarget and LoadInstantTarget for more information. */
    internal void LoadAugmentation(AugmentationDescription augmentation) {
        GameObject modelPrefab = Models[augmentation.ID];
        Transform model = Instantiate(modelPrefab).transform;
        _activeModels.Add(model.gameObject);

        model.localPosition = augmentation.LocalPosition;
        model.localRotation = augmentation.LocalRotation;
        model.localScale = augmentation.LocalScale;

        model.gameObject.SetActive(false);
    }

    public void OnError(Error error) {
        PrintError("Instant Tracker error!", error);
    }

    public void OnFailedStateChange(InstantTrackingState failedState, Error error) {
        PrintError("Failed to change state to " + failedState, error);
    }
    #endregion
}
