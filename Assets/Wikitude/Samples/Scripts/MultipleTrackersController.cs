using UnityEngine;
using Wikitude;

public class MultipleTrackersController : SampleController
{
    public ImageTracker CarTracker;
    public ImageTracker MagazineTracker;

    /* Prefabs that display which targets should be scanned, based on which tracker is active. */
    public GameObject CarInstructions;
    public GameObject MagazineInstructions;

    /* Flag to keep track if the Wikitude SDK is currently transitioning from one tracker to another.
     * We shouldn't start another transition until the previous one completed.
     */
    private bool _waitingForTrackerToLoad = false;

    public void OnTrackCar() {
        if (!CarTracker.enabled && !_waitingForTrackerToLoad) {
            _waitingForTrackerToLoad = true;

            MagazineInstructions.SetActive(false);
            CarInstructions.SetActive(true);

            CarTracker.enabled = true;
        }
    }

    public void OnTrackMagazine() {
        if (!MagazineTracker.enabled && !_waitingForTrackerToLoad) {
            _waitingForTrackerToLoad = true;
            MagazineInstructions.SetActive(true);

            CarInstructions.SetActive(false);

            MagazineTracker.enabled = true;
        }
    }

    public override void OnTargetsLoaded() {
        _waitingForTrackerToLoad = false;
    }
}
