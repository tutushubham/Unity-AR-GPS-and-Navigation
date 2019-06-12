using UnityEngine;
using System;
using System.Collections;
using Wikitude;
using System.Collections.Generic;
using System.Runtime.InteropServices;

/* Handles forwarding the camera frame to the CustomCameraRenderer. */
public class CustomCameraController : SampleController
{
    /* Simple struct to keep track of camera frames and their index when they are cached in the RingBuffer. */
    private struct InputFrameData {
        public long Index;
        public Texture2D Texture;

        public InputFrameData(long index, Texture2D texture) {
            Index = index;
            Texture = texture;
        }
    }

    public Plugin InputPlugin;

    protected WebCamTexture _feed;

    /* The width and height of the camera frame we expect to get from the WebCamTexture. */
    public const int FrameWidth = 640;
    public const int FrameHeight = 480;

    /* The size in bytes of the camera frame data. */
    private int _frameDataSize = 0;
    /* The index of the current camera frame. Is incremented whenever a new camera frame is passed to the InputPlugin. */
    private int _frameIndex = 0;

    /* The index in the ring buffer where the next frame should be written to. */
    private int _bufferWriteIndex = 0;
    /* The index in the ring buffer where the next frame should be read from. */
    private int _bufferReadIndex = 0;
    /* The maximum number of frames that can be stored in the ring buffer. */
    private int _bufferCount = 5;

    /* The ring buffer stored as a list of frames.
     * Because it can take longer to process a frame, they are stored in this ring buffer until they can be rendered.
     * The order of events is as follows:
     *      - a new camera frame is received from the WebCamTexture
     *      - the frame is stored in the ring buffer with index 1 and sent to the InputPlugin
     *      - another frame is received from the WebCamTexture. It is stored with index 2 and also sent to the InputPlugin
     *      - the frame with index 1 was processed as reported through the InputPlugin.GetProcessedFrameId
     *      - the frame with index 1 is retrieved from the ring buffer and rendered
     * If we were to just render the most recent frame that the WebCamTexture gives us, there could be significant delay between
     * the augmentations and the camera frame, making it look as if the augmentations are not properly attached to their targets.
     */
    private List<InputFrameData> _ringBuffer;

    /* Buffer where the color data is extracted from the WebCamTexture */
    private Color32[] _colorData;

    private bool _cameraReleasedByTheSDK = false;

    public CustomCameraRenderer Renderer;

    /* This event is registered to the Plugin. It is important that we don't attempt to start the camera before the SDK releases it. */
    public void OnCameraReleased() {
        _cameraReleasedByTheSDK = true;
        StartCoroutine(Initialize(true));
    }

    /* This event is called when there was an error inside the SDK and the camera could not be released. */
    public void OnCameraReleaseFailed(Error error) {
        PrintError("Input plugin failed!", error);
    }

    public void OnImageRecognized(ImageTarget target) {
        Renderer.IsEffectVisible = false;
    }

    public void OnImageLost(ImageTarget target) {
        Renderer.IsEffectVisible = true;
    }

    private IEnumerator Initialize(bool firstStart) {
        if (!firstStart) {
            /* If we are resuming from background, we wait a frame to make sure that everything is initialized
             * before starting the camera again.
             */
            yield return null;
        }

        if (_feed == null) {
            WebCamDevice? selectedDevice = null;
            /* First search for a back-facing device */
            foreach (var device in WebCamTexture.devices) {
                if (!device.isFrontFacing) {
                    selectedDevice = device;
                    break;
                }
            }

            /* If no back-facing device was found, search again for a front facing device */
            if (selectedDevice == null) {
                if (WebCamTexture.devices.Length > 0) {
                    selectedDevice = WebCamTexture.devices[0];
                }
            }

            if (selectedDevice != null) {
                _feed = new WebCamTexture(selectedDevice.Value.name, FrameWidth, FrameHeight);
                _feed.Play();
            }
        } else {
            _feed.Play();
        }

        if (_feed == null) {
            Debug.LogError("Could not find any cameras on the device.");
        }

        ResetBuffers(FrameWidth, FrameHeight, 4);

        // Wait a frame before getting the camera rotation, otherwise it might not be initialized yet
        yield return null;
        if (Application.platform == RuntimePlatform.Android) {

            bool rotatedSensor = false;
            switch (Screen.orientation) {
                case ScreenOrientation.Portrait: {
                    rotatedSensor = _feed.videoRotationAngle == 270;
                    break;
                }
                case ScreenOrientation.LandscapeLeft: {
                    rotatedSensor = _feed.videoRotationAngle == 180;
                    break;
                }
                case ScreenOrientation.LandscapeRight: {
                    rotatedSensor = _feed.videoRotationAngle == 0;
                    break;
                }
                case ScreenOrientation.PortraitUpsideDown: {
                    rotatedSensor = _feed.videoRotationAngle == 90;
                    break;
                }
            }

            if (rotatedSensor) {
                // Because the sensor is inverted, we need to flip the image when rendering it to the screen.
                Renderer.FlipImage = true;
            }
        }
    }

    /* Initialize all the buffers to have the correct size.
     * The buffers are used so that the memory is not reallocated every frame, thus avoiding GC spikes.
     */
    private void ResetBuffers(int width, int height, int bytesPerPixel) {
        _frameDataSize = width * height * bytesPerPixel;
        _ringBuffer = new List<InputFrameData>(_bufferCount);
        for (int i = 0; i < _bufferCount; ++i) {
            _ringBuffer.Add(new InputFrameData(-1 , new Texture2D(width, height)));
        }

        _colorData = new Color32[width * height];

        Renderer.CurrentFrame = _ringBuffer[0].Texture;
    }

    protected override void Update() {
        base.Update();
        if (_feed == null || !_feed.didUpdateThisFrame) {
            return;
        }

        if (_feed.width < 100 || _feed.height < 100) {
            Debug.LogError("Camera feed has unexpected size.");
            return;
        }

        int newFrameDataSize = _feed.width * _feed.height * 4;
        if (newFrameDataSize != _frameDataSize) {
            /* Resize all the buffers if the frame size changed. */
            ResetBuffers(_feed.width, _feed.height, 4);
        }

        /* Start processing the frame */

        /* Get the actual raw colors from the frame and store them in the buffer */
        _feed.GetPixels32(_colorData);
        /* Add the frame to the ring buffer and update the index */
        _ringBuffer[_bufferWriteIndex].Texture.SetPixels32(_colorData);
        _ringBuffer[_bufferWriteIndex].Texture.Apply();
        InputPlugin.CameraToSurfaceAngle = (float)_feed.videoRotationAngle;
        /* Send the data from the _colorData buffer to the SDK. */
        SendNewCameraFrame();
        var inputFrameData = _ringBuffer[_bufferWriteIndex];
        inputFrameData.Index = _frameIndex;
        _ringBuffer[_bufferWriteIndex] = inputFrameData;

        /* Update the texture that should be renderer based on the last processed frame id. */
        long presentableIndex = InputPlugin.GetProcessedFrameId();
        /* Default to the last written buffer */
        _bufferReadIndex = _bufferWriteIndex;
        if (presentableIndex != -1) {
            for (int i = 0; i < _bufferCount; ++i) {
                if (_ringBuffer[i].Index == presentableIndex) {
                    _bufferReadIndex = i;
                }
            }
        }

        Renderer.CurrentFrame = _ringBuffer[_bufferReadIndex].Texture;
        /* Increase the write index, in preparation for the next frame. */
        _bufferWriteIndex = (_bufferWriteIndex + 1) % _bufferCount;
    }

    private void SendNewCameraFrame() {
        /* The GCHandle is used to pin the _colorData in place, so that native code can access it directly, without additional copies. */
        GCHandle handle = default(GCHandle);
        try {
            handle = GCHandle.Alloc(_colorData, GCHandleType.Pinned);
            IntPtr frameData = handle.AddrOfPinnedObject();

            var metadata = new ColorCameraFrameMetadata();
            metadata.HorizontalFieldOfView = 58.0f;
            metadata.Width = _feed.width;
            metadata.Height = _feed.height;
            metadata.CameraPosition = CaptureDevicePosition.Back;
            metadata.ColorSpace = FrameColorSpace.RGBA;
            metadata.TimestampScale = 1;

            var plane = new CameraFramePlane();
            plane.Data = frameData;
            plane.DataSize = (uint)_frameDataSize;
            plane.PixelStride = 4;
            plane.RowStride = _feed.width;
            var planes = new List<CameraFramePlane>();
            planes.Add(plane);

            var cameraFrame = new CameraFrame(++_frameIndex, 0, metadata, planes);
            InputPlugin.NotifyNewCameraFrame(cameraFrame);
        } finally {
            if (handle != default(GCHandle)) {
                handle.Free();
            }
        }
    }

    protected virtual void Cleanup() {
        _frameDataSize = 0;
        if (_feed != null) {
            _feed.Stop();
        }

        if (Renderer) {
            Renderer.CurrentFrame = null;
        }
    }

    private void OnApplicationPause(bool paused) {
        if (paused) {
            /* If the application is paused, make sure that the camera is properly released. */
            Cleanup();
        } else {
            if (_cameraReleasedByTheSDK) {
                /* Only attempt to start the camera if the Wikitude SDK already released it.
                 * Otherwise simply wait until the Wikitude SDK releases it and the OnCameraRelease callback is called.
                 */
                StartCoroutine(Initialize(false));
            }
        }
    }

    private void OnDestroy() {
        /* When the GameObject is destroyed and the user quits the sample, make sure that the camera is properly released. */
        Cleanup();
    }
}
