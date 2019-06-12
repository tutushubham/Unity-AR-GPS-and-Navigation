using UnityEngine;
using UnityEngine.UI;
using Wikitude;

public class RuntimeTrackerController : SampleController
{
    /* UI control to specify the URL from which the TargetCollectionResource should be loaded. */
    public InputField Url;
    public GameObject TrackablePrefab;
    public GameObject CarInstructions;

    private ImageTracker _currentTracker;
    /* Flag to keep track if a tracker is currently loading. */
    private bool _isLoadingTracker = false;

    public void OnLoadTracker() {
        if (_isLoadingTracker) {
            /* Wait until previous request was completed. */
            return;
        }
        /* Destroy any previously loaded tracker. */
        if (_currentTracker != null) {
            Destroy(_currentTracker.gameObject);
        }

        _isLoadingTracker = true;

        /* Create and configure the tracker. */
        GameObject trackerObject = new GameObject("ImageTracker");
        _currentTracker = trackerObject.AddComponent<ImageTracker>();
        _currentTracker.TargetSourceType = TargetSourceType.TargetCollectionResource;
        _currentTracker.TargetCollectionResource = new TargetCollectionResource();
        _currentTracker.TargetCollectionResource.UseCustomURL = true;
        _currentTracker.TargetCollectionResource.TargetPath = Url.text;

        _currentTracker.TargetCollectionResource.OnFinishLoading.AddListener(OnFinishLoading);
        _currentTracker.TargetCollectionResource.OnErrorLoading.AddListener(OnErrorLoading);

        _currentTracker.OnTargetsLoaded.AddListener(OnTargetsLoaded);
        _currentTracker.OnErrorLoadingTargets.AddListener(OnErrorLoadingTargets);
        _currentTracker.OnInitializationError.AddListener(OnInitializationError);

        /* Create and configure the trackable. */
        GameObject trackableObject = GameObject.Instantiate(TrackablePrefab);
        trackableObject.transform.SetParent(_currentTracker.transform, false);
    }

    public override void OnTargetsLoaded() {
        base.OnTargetsLoaded();
        CarInstructions.SetActive(true);
        _isLoadingTracker = false;
    }

    public override void OnErrorLoadingTargets(Error error) {
        base.OnErrorLoadingTargets(error);
        _isLoadingTracker = false;
    }
}
