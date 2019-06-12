using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using Wikitude;

public class LoadInstantTarget : MonoBehaviour {
    public InstantTracker Tracker;
    public InstantTrackingController Controller;
    public Text InfoMessage;
    public Button LoadButton;

    public void OnLoadButtonPressed() {
        LoadTarget();
        LoadScene();
    }

    public void OnChangedState(InstantTrackingState state) {
        if (state == InstantTrackingState.Tracking) {
            LoadButton.gameObject.SetActive(false);
        } else {
            LoadButton.gameObject.SetActive(true);
        }
    }

    /* Loads the instant target from the disk, without any augmentations. */
    private void LoadTarget() {
        /* A TargetCollectionResource is needed to manage file loading. */
        var targetCollectionResource = new TargetCollectionResource();
        /* UseCustomURL is used to specify that the file is not inside the "StreamingAssets" folder */
        targetCollectionResource.UseCustomURL = true;
        /* The "file://" is used to indicate that the file is located on disk, and not on a server. */
        targetCollectionResource.TargetPath = "file://" + Application.persistentDataPath + "/InstantTarget.wto";
        var configuration = new InstantTargetRestorationConfiguration();
        configuration.ExpansionPolicy = InstantTargetExpansionPolicy.Allow;
        Tracker.LoadInstantTarget(targetCollectionResource, configuration, LoadSuccessHandler, LoadErrorHandler);
    }

    /* Loads all augmentations from disk. */
    private void LoadScene() {
        try {
            string json = File.ReadAllText(Application.persistentDataPath + "/InstantScene.json");
            var sceneDescription = JsonUtility.FromJson<SceneDescription>(json);

            foreach (var augmentation in sceneDescription.Augmentations) {
                Controller.LoadAugmentation(augmentation);
            }
        } catch (Exception ex) {
            InfoMessage.text = "Error loading scene augmentations.";
            Debug.LogError("Error loading augmentations: " + ex.Message);
        }
    }

    private void LoadSuccessHandler(string path) {
        InfoMessage.text = "The instant target was successfully loaded from path: " + path;
    }

    private void LoadErrorHandler(Error error) {
        InfoMessage.text = "The following error occurred when loading the instant target. " +
            "Error code: " + error.Code + " domain: " + error.Domain + " message: " + error.Message;
    }
}
