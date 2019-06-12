using UnityEngine;
using UnityEngine.Rendering;

/* Handles custom camera rendering of a frame. */
public class CustomCameraRenderer : MonoBehaviour
{
    /* The material that contains the scan effect shader. */
    public Material EffectMaterial;

    private Texture _currentFrame;
    /* The texture that should be displayed. */
    public Texture CurrentFrame {
        set {
            if (value != null) {
                _currentFrame = value;
                enabled = true;
                SetCommandBuffer();

                _currentScreenWidth = Screen.width;
                _currentScreenHeight = Screen.height;
                UpdateOrientation(Screen.orientation);
            }
        }
    }

    /* Simple struct that keeps track of how the frame should be rotated in order to be properly displayed. */
    private struct ImageRotation {
        public bool FlipHorizontally;
        public bool FlipVertically;
        public bool Rotate;

        public ImageRotation(bool flipHorizontally, bool flipVertically, bool rotate) {
            FlipHorizontally = flipHorizontally;
            FlipVertically = flipVertically;
            Rotate = rotate;
        }
    }

    [HideInInspector]
    private bool _flipImage = false;
    public bool FlipImage {
        get {
            return _flipImage;
        }
        set {
            if (_flipImage != value) {
                _flipImage = value;
                UpdateOrientation(Screen.orientation);
            }
        }
    }

    private CommandBuffer _drawFrameBuffer;
    private int _currentScreenWidth = 0;
    private int _currentScreenHeight = 0;

    /* Toggles the visibility of the effect by modifying the intensity. */
    public bool IsEffectVisible {
        set {
            EffectMaterial.SetFloat("_ScanIntensity", value ? 1 : 0);
        }
    }

    /* Creates the appropriate command buffer that displays the frame on the screen */
    private void SetCommandBuffer() {
        var camera = GetComponent<Camera>();
        CameraEvent eventForBlit;

        if (camera.actualRenderingPath == RenderingPath.Forward) {
            eventForBlit = CameraEvent.BeforeForwardOpaque;
        } else {
            eventForBlit = CameraEvent.BeforeGBuffer;
        }

        /* Remove any existing command buffer, if it was already created. */
        if (_drawFrameBuffer != null) {
            camera.RemoveCommandBuffer(eventForBlit, _drawFrameBuffer);
        }

        /* Only create a new command buffer if we actually have a frame to render */
        if (_currentFrame != null) {
            EffectMaterial.SetInt("_ResolutionX", _currentFrame.width);
            EffectMaterial.SetInt("_ResolutionY", _currentFrame.height);

            _drawFrameBuffer = new CommandBuffer();
            _drawFrameBuffer.Blit(_currentFrame, BuiltinRenderTextureType.CameraTarget, EffectMaterial);
            camera.AddCommandBuffer(eventForBlit, _drawFrameBuffer);
        }
    }

    private void Update() {
        /* Every frame, check if the orientation changed and update the rendering accordingly. */
        if ((Screen.width != _currentScreenWidth || Screen.height != _currentScreenHeight)) {
            _currentScreenWidth = Screen.width;
            _currentScreenHeight = Screen.height;
            UpdateOrientation(Screen.orientation);
        }
    }

    /* Defines how the frame should be rotated and scaled in order for it to be properly rendered. */
    private void UpdateOrientation(ScreenOrientation screenOrientation) {
        /* Compute the required rotation of the frame. */
        var rotation = new ImageRotation(false, false, false);

#if !UNITY_EDITOR
        switch (screenOrientation) {
            case ScreenOrientation.LandscapeLeft:
                rotation = new ImageRotation(false, false, false);
                break;
            case ScreenOrientation.LandscapeRight:
                rotation = new ImageRotation(true, true, false);
                break;
            case ScreenOrientation.Portrait:
                rotation = new ImageRotation(false, true, true);
                break;
            case ScreenOrientation.PortraitUpsideDown:
                rotation = new ImageRotation(true, false, true);
                break;
        }

        if (FlipImage) {
            rotation.FlipVertically = !rotation.FlipVertically;
            rotation.FlipHorizontally = !rotation.FlipHorizontally;
        }
#endif

        SetImageRotation(rotation);

        /* Compute the required scaling and panning factors of the frame. */
        float frameAspectRatio = (float)_currentFrame.width / (float)_currentFrame.height;
        float screenAspectRatio = (float)Screen.width / (float)Screen.height;

        float ratio = 1.0f;

#if !UNITY_EDITOR
        switch (screenOrientation) {
        case ScreenOrientation.LandscapeLeft:
        case ScreenOrientation.LandscapeRight:
            ratio = frameAspectRatio / screenAspectRatio;
            break;
        case ScreenOrientation.Portrait:
        case ScreenOrientation.PortraitUpsideDown:
            ratio = frameAspectRatio * screenAspectRatio;
            break;
        }
#else
        ratio = frameAspectRatio / screenAspectRatio;
#endif


        /* Sets the appropriate material properties based on how the image should be scaled and panned. */
        EffectMaterial.SetFloat("_Scale", ratio);
        EffectMaterial.SetFloat("_Pan", (1.0f - ratio) / 2.0f);
    }

    /* Sets the appropriate material properties based on how the image should be rotated. */
    private void SetImageRotation(ImageRotation rotation) {
        EffectMaterial.SetFloat("_FlipU", rotation.FlipHorizontally ? 1 : 0);
        EffectMaterial.SetFloat("_FlipV", rotation.FlipVertically ? 1 : 0);
        EffectMaterial.SetFloat("_Rotate", rotation.Rotate ? 1 : 0);
    }
}
