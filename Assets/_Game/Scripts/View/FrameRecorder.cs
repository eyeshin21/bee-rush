using System.Collections;
using System.IO;
using HoneyBeeRush.Core;
using HoneyBeeRush.Gameplay;
using HoneyBeeRush.Gameplay.Runtime;
using UnityEngine;

namespace HoneyBeeRush.View
{
    public sealed class FrameRecorder : MonoBehaviour
    {
        public int width = 720;
        public int height = 1280;
        public int captureFramerate = 30;
        public int jpgQuality = 92;
        public string outputDirectory = "";
        public float maxSeconds = 180f;
        public float tailSecondsAfterWin = 4.5f;
        public bool stopPlayModeWhenDone = true;

        private Camera _camera;
        private LevelController _loop;
        private RenderTexture _rt;
        private Texture2D _readback;
        private int _frameIndex;
        private bool _recording;
        private float _elapsed;
        private float _tail = -1f;

        public int FramesWritten
        {
            get { return _frameIndex; }
        }

        public bool Recording
        {
            get { return _recording; }
        }

        private IEnumerator Start()
        {
            _loop = GetComponent<LevelController>();
            if (_loop == null) _loop = FindAnyObjectByType<LevelController>();

            yield return null;

            _camera = Camera.main;
            if (_camera == null)
            {
                Debug.LogError("FrameRecorder: no main camera.");
                yield break;
            }

            if (string.IsNullOrEmpty(outputDirectory))
            {
                outputDirectory = Path.Combine(Application.dataPath, "../Recordings/frames");
            }
            outputDirectory = Path.GetFullPath(outputDirectory);
            if (Directory.Exists(outputDirectory))
            {
                var stale = Directory.GetFiles(outputDirectory, "*.jpg");
                for (int i = 0; i < stale.Length; i++) File.Delete(stale[i]);
            }
            else
            {
                Directory.CreateDirectory(outputDirectory);
            }

            _rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "HBR_CaptureRT" };
            _rt.Create();
            _readback = new Texture2D(width, height, TextureFormat.RGB24, false);

            Time.captureFramerate = captureFramerate;
            _recording = true;
            Debug.Log("FrameRecorder: writing to " + outputDirectory);

            yield return StartCoroutine(CaptureLoop());
        }

        private IEnumerator CaptureLoop()
        {
            var wait = new WaitForEndOfFrame();
            while (_recording)
            {
                yield return wait;
                CaptureOne();

                _elapsed += 1f / captureFramerate;

                if (_loop != null && _loop.Phase == GamePhase.Won)
                {
                    if (_tail < 0f) _tail = tailSecondsAfterWin;
                    _tail -= 1f / captureFramerate;
                    if (_tail <= 0f) Finish();
                }
                else if (_loop != null && _loop.Phase == GamePhase.Lost)
                {
                    if (_tail < 0f) _tail = 2.5f;
                    _tail -= 1f / captureFramerate;
                    if (_tail <= 0f) Finish();
                }

                if (_elapsed >= maxSeconds) Finish();
            }
        }

        private void CaptureOne()
        {
            if (_camera == null || _rt == null) return;

            RenderTexture prevTarget = _camera.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            _camera.targetTexture = _rt;
            _camera.aspect = width / (float)height;
            _camera.Render();

            RenderTexture.active = _rt;
            _readback.ReadPixels(new Rect(0f, 0f, width, height), 0, 0, false);
            _readback.Apply(false);

            RenderTexture.active = prevActive;
            _camera.targetTexture = prevTarget;

            byte[] bytes = _readback.EncodeToJPG(jpgQuality);
            string path = Path.Combine(outputDirectory, "f" + _frameIndex.ToString("D5") + ".jpg");
            File.WriteAllBytes(path, bytes);
            _frameIndex++;
        }

        private void Finish()
        {
            if (!_recording) return;
            _recording = false;
            Time.captureFramerate = 0;
            Debug.Log("FrameRecorder: done, " + _frameIndex + " frames in " + outputDirectory);
            File.WriteAllText(Path.Combine(outputDirectory, "..", "capture_done.txt"),
                _frameIndex + "\n" + captureFramerate + "\n" + (_loop != null ? _loop.Phase.ToString() : "Unknown"));

            if (stopPlayModeWhenDone)
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            }
        }

        private void OnDestroy()
        {
            Time.captureFramerate = 0;
            _recording = false;
            if (_camera != null && _camera.targetTexture == _rt) _camera.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                Destroy(_rt);
                _rt = null;
            }
            if (_readback != null)
            {
                Destroy(_readback);
                _readback = null;
            }
        }
    }
}
