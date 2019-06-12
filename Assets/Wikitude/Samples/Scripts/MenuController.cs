using UnityEngine;
#if UNITY_2018_3 && UNITY_ANDROID
using UnityEngine.Android;
#endif
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Wikitude;

public class MenuController : MonoBehaviour
{
    public GameObject InfoPanel;

    public Text VersionNumberText;
    public Text BuildDateText;
    public Text BuildNumberText;
    public Text BuildConfigurationText;
    public Text UnityVersionText;

    private void Awake() {
#if UNITY_2018_3 && UNITY_ANDROID
        /* Since 2018.3, Unity doesn't automatically handle permissions on Android, so as soon as
         * the menu is displayed, ask for camera permissions. */
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera)) {
            Permission.RequestUserPermission(Permission.Camera);
        }
#endif
    }

    public void OnSampleButtonClicked(Button sender) {
        /* Start the appropriate scene based on the button name that was pressed. */
        SceneManager.LoadScene(sender.name);
    }

    public void OnInfoButtonPressed() {
        /* Display the info panel, which contains additional information about the Wikitude SDK. */
        InfoPanel.SetActive(true);

        var buildInfo = WikitudeSDK.BuildInformation;
        VersionNumberText.text = buildInfo.SDKVersion;
        BuildDateText.text = buildInfo.BuildDate;
        BuildNumberText.text = buildInfo.BuildNumber;
        BuildConfigurationText.text = buildInfo.BuildConfiguration;
        UnityVersionText.text = Application.unityVersion;
    }

    public void OnInfoDoneButtonPressed() {
        InfoPanel.SetActive(false);
    }

    void Update() {
        /* Also handles the back button on Android */
        if (Input.GetKeyDown(KeyCode.Escape)) {
            /* There is nowhere else to go back, so quit the app. */
            Application.Quit();
        }
    }
}