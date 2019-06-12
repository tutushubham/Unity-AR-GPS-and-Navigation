using UnityEngine;
using UnityEngine.UI;
using System;
using System.Threading;
using System.Runtime.InteropServices;
using Wikitude;
using ZXing;

public class PluginController : SampleController
{
    /* UI element to display the scan result. */
    public Text ResultText;

    /* Background thread that performs the scanning computation. */
    private Thread _backgroundThread;

    /* Flag that triggers the closure of the background thread. */
    private bool _closeBackgroundThread = false;
    /* Flag that keeps track of whether a scanning is currently taking place, in which case we should wait for it to finish before starting a new one. */
    private bool _scanningInProgress = false;
    /* The result of the most recent scan that was not yet displayed. */
    private string _scanningResult = null;
    /* The width and height of the camera frame we want to scan. */
    private int _width = 0;
    private int _height = 0;
    /* The cached color data. */
    private Color32[] _data = null;
    /* How long to display the result before reverting to the default message. This helps with flickering on some devices. */
    private float _resultTimeout = 0.3f;
    /* Timestamp of the last result. */
    private float _lastResultTimestamp = 0.0f;

    /* Monitor used to synchronize the main thread with the background thread. */
    private readonly object _monitor = new object();
    private const string DefaultInfoText = "Scanning for barcodes...";

    protected override void Start() {
        base.Start();
        /* Start the background thread immediately. */
        _backgroundThread = new Thread(() => {
            Scan();
        });
        _backgroundThread.Start();

        ResultText.text = DefaultInfoText;
    }

    protected override void Update() {
        base.Update();

        /* Every frame, check scanning is done. */
        lock(_monitor) {
            if (_scanningInProgress) {
                return;
            }
        }

        /* If there is a result, display it to the screen. */
        if (_scanningResult != null) {
            ResultText.text = _scanningResult;
            _scanningResult = null;
            _lastResultTimestamp = Time.time;
        } else if (Time.time - _lastResultTimestamp > _resultTimeout) {
            /* Only show the default info text after the timeout expired. */
            ResultText.text = DefaultInfoText;
        }
    }

    /* This event is called by the Wikitude SDK whenever a new camera frame is received from the device camera. */
    public void OnCameraFrameAvailable(CameraFrame frame) {
        lock(_monitor) {
            if (_scanningInProgress) {
                /* Don't process this frame if the previous frame was not scanned yet. */
                return;
            }
        }

        /* Extract the data from the native frame into a Color32 array that can be scanned. */
        var metadata = frame.ColorMetadata;
        var data = new Color32[metadata.Width * metadata.Height];
        if (metadata.ColorSpace == FrameColorSpace.RGBA) {
            if (frame.ColorData.Count != 1) {
                Debug.LogError("Unexpected number of data planes. Expected 1, but got " + frame.ColorData.Count);
                return;
            }
            var rawBytes = new byte[frame.ColorData[0].DataSize];
            Marshal.Copy(frame.ColorData[0].Data, rawBytes, 0, (int)frame.ColorData[0].DataSize);

            for (int i = 0; i < metadata.Width * metadata.Height; ++i) {
                data[i] = new Color32(rawBytes[i * 4], rawBytes[i * 4 + 1], rawBytes[i * 4 + 2], rawBytes[i * 4 + 3]);
            }
        } else if (metadata.ColorSpace == FrameColorSpace.RGB) {
            if (frame.ColorData.Count != 1) {
                Debug.LogError("Unexpected number of data planes. Expected 1, but got " + frame.ColorData.Count);
                return;
            }

            var rawBytes = new byte[frame.ColorData[0].DataSize];
            Marshal.Copy(frame.ColorData[0].Data, rawBytes, 0, (int)frame.ColorData[0].DataSize);

            for (int i = 0; i < metadata.Width * metadata.Height; ++i) {
                data[i] = new Color32(rawBytes[i * 3], rawBytes[i * 3 + 1], rawBytes[i * 3 + 2], 0);
            }

        } else if (metadata.ColorSpace == FrameColorSpace.YUV_420_NV12 ||
                   metadata.ColorSpace == FrameColorSpace.YUV_420_NV21 ||
                   metadata.ColorSpace == FrameColorSpace.YUV_420_YV12 ||
                   metadata.ColorSpace == FrameColorSpace.YUV_420_888) {

            if (frame.ColorData.Count < 1) {
                Debug.LogError("Unexpected number of data planes. Expected at least 1, but got " + frame.ColorData.Count);
                return;
            }

            var rawBytes = new byte[frame.ColorData[0].DataSize];
            Marshal.Copy(frame.ColorData[0].Data, rawBytes, 0, (int)frame.ColorData[0].DataSize);

            for (int i = 0; i < metadata.Width * metadata.Height; ++i) {
                data[i] = new Color32(rawBytes[i], rawBytes[i], rawBytes[i], 0);
            }
        }

        /* Lock the monitor to update the data. */
        lock(_monitor) {
            _width = metadata.Width;
            _height = metadata.Height;
            _data = data;
            _scanningInProgress = true;
            /* Trigger a new scan. */
            Monitor.Pulse(_monitor);
        }
    }

    private void Scan() {
        /* Continuously loop the background thread until we get a signal to stop. */
        while (!_closeBackgroundThread) {
            /* If we have new data, trigger a scan and update the shared data. */
            if (_data != null) {
                var result = Decode(_data, _width, _height);
                lock(_monitor) {
                    _scanningResult = result;
                    _data = null;
                    _scanningInProgress = false;
                }
            }

            /* Wait for new data. */
            lock(_monitor) {
                if (_closeBackgroundThread) {
                    return;
                }
                Monitor.Wait(_monitor);
            }
        }
    }

    private string Decode(Color32[] colors, int width, int height) {
        /* Use BarcodeReader from the ZXing library to scan for barcodes and QR codes. */
        var reader = new BarcodeReader();
        /* Scan all orientations. */
        reader.AutoRotate = true;
        var result = reader.Decode(colors, width, height);
        if (result != null) {
            return result.Text;
        } else {
            return null;
        }
    }

    void OnDestroy() {
        /* When the sample is closed, trigger the background thread to close, wake it up in case it was waiting for new data and wait for it to be joined. */
        lock(_monitor) {
            _closeBackgroundThread = true;
            Monitor.Pulse(_monitor);
        }
        _backgroundThread.Join();
    }

    public void OnPluginFailure(Error error) {
        PrintError("Plugin failed!", error);
    }
}
