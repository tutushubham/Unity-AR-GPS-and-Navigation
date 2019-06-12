using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Wikitude;
using System.Runtime.InteropServices;

/* Handles getting the camera frame from the device and forwarding it to the Wikitude SDK. */
public class SimpleInputPluginController : SampleController
{
    public Plugin InputPlugin;
    protected WebCamTexture _feed;

    /* The width and height of the camera frame we expect to get from the WebCamTexture. */
    public const int FrameWidth = 640;
    public const int FrameHeight = 480;

    /* The size in bytes of the camera frame data. */
    private int _frameDataSize = 0;
    /* The index of the current camera frame. Is incremented whenever a new camera frame is passed to the InputPlugin. */
    private int _frameIndex = 0;
    /* Buffer where the color data is extracted from the WebCamTexture */
    private Color32[] _pixels;
    private bool _cameraReleasedByTheSDK = false;

    /* This event is registered to the Plugin. It is important that we don't attempt to start the camera before the SDK releases it. */
    public void OnCameraReleased() {
        _cameraReleasedByTheSDK = true;
        StartCoroutine(Initialize());
    }

    /* This event is called when there was an error inside the SDK and the camera could not be released. */
    public void OnCameraReleaseFailed(Error error) {
        PrintError("Input plugin failed!", error);
    }

    protected IEnumerator Initialize() {
        /* Waiting for a frame can help on some devices, especially when initializing the camera when returning from  background */
        yield return null;
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

        if (_feed == null) {
            Debug.LogError("Could not find any cameras on the device.");
        }

        ResetBuffers(FrameWidth, FrameHeight, 4);
    }

    /* Initialize all the buffers to have the correct size.
     * The buffers are used so that the memory is not reallocated every frame, thus avoiding GC spikes.
     */
    protected virtual void ResetBuffers(int width, int height, int bytesPerPixel) {
        _frameDataSize = width * height * bytesPerPixel;
        _pixels = new Color32[width * height];
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

        /* Get the actual raw colors from the frame and store them in the buffer */
        _feed.GetPixels32(_pixels);
        InputPlugin.CameraToSurfaceAngle = (float)_feed.videoRotationAngle;
        /* Send the data from the _colorData buffer to the SDK. */
        SendNewCameraFrame();
    }

    private void SendNewCameraFrame() {
        /* The GCHandle is used to pin the _colorData in place, so that native code can access it directly, without additional copies. */
        GCHandle handle = default(GCHandle);
        try {
            handle = GCHandle.Alloc(_pixels, GCHandleType.Pinned);
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
            _feed = null;
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
                StartCoroutine(Initialize());
            }
        }
    }

    private void OnDestroy() {
        /* When the GameObject is destroyed and the user quits the sample, make sure that the camera is properly released. */
        Cleanup();
    }
}
