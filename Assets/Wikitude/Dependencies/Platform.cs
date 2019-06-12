#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

using UnityEngine;

namespace Wikitude
{
    /// <summary>
    /// Class used by the Wikitude SDK for platform and version dependent compilation. For internal use only.
    /// </summary>
    public class Platform : PlatformBase {

    #if UNITY_EDITOR
        [InitializeOnLoadMethod]
    #endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Initialize() {
            if (_instance == null) {
                _instance = new Platform();
            }
        }

        public override void LoadImage(Texture2D texture, byte[] data) {
            texture.LoadImage(data);
        }

        public override byte[] EncodeToPNG(Texture2D texture) {
            return texture.EncodeToPNG();
        }

        public override string GetApplicationIdentifier() {
#if UNITY_EDITOR
#if UNITY_WSA || UNITY_WSA_10 || UNITY_UWP
            /* As of Unity 2018.3 there no way to get the package name for a UWP / WSA application, since 
             * PlayerSettings.GetApplicationIdentifier(BuildTargetGroup.WSA) returns com.Company.ProductName regardless
             * of what is specified in the PlayerSettings inspector.
             * The only alternative is to parse the ProjectSettings.asset file and get it from there.
             * In case this fails, please simply hardcode and return your bundle identifier / package name from this method.
             */
            
            try {
                var projectSettingsPath = Path.Combine(Path.Combine(Directory.GetParent(Application.dataPath).FullName, "ProjectSettings"), "ProjectSettings.asset");
                var lines = File.ReadAllLines(projectSettingsPath);
                foreach (var line in lines) {
                    var trimmed = line.Trim();
                    if (trimmed.StartsWith("metroPackageName: ")) {
                        return trimmed.Replace("metroPackageName: ", "");
                    }
                }
                throw new System.Exception("Could not find 'metroPackageName' in ProjectSettings!");
            } catch (System.Exception exception) {
                Debug.LogError("Getting the application identifier failed! Please modify the Platform.GetApplicationIdentifier method to hardcode and return the correct bundle identifier!\nError message: " + exception.Message);
                return "";
            }

#else
#if UNITY_2017_1_OR_NEWER
            return PlayerSettings.applicationIdentifier;
#else
            return PlayerSettings.bundleIdentifier;
#endif
#endif
#else
            return "";
#endif
        }

        public override UnityVersion GetUnityVersion() {
#if UNITY_2018_1_OR_NEWER
            return UnityVersion.Unity_2018_1;
#elif UNITY_2017_3_OR_NEWER
            return UnityVersion.Unity_2017_3;
#elif UNITY_2017_2_OR_NEWER
            return UnityVersion.Unity_2017_2;
#elif UNITY_2017_1_OR_NEWER
            return UnityVersion.Unity_2017_1;
#elif UNITY_5_6_OR_NEWER
            return UnityVersion.Unity_5_6;
#elif UNITY_5_5_OR_NEWER
            return UnityVersion.Unity_5_5;
#elif UNITY_5_4_OR_NEWER
            return UnityVersion.Unsupported;
#elif UNITY_5
            return UnityVersion.Unsupported;
#elif UNITY_4
            return UnityVersion.Unsupported;
#else
            return UnityVersion.Unknown;
#endif
        }
    }
}
