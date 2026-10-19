using System;
using System.Collections.Generic;
using DeepseaOil.Logic.Service;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeepseaOil.Presentation.RenderPass
{
    /// <summary>存档缩略图抓帧：降采样到 320×180 后异步回读，再在主线程编码 PNG</summary>
    /// <remarks>必须在 Renderer2D 资产上手工 Add Renderer Feature 才生效：未挂时不抓帧，存档照常但无图。取色与回读由渲染帧分两帧编排、结果每帧在主线程排空交给 SaveService，故都不占物理帧。编码**不能**下放后台线程：EncodeToPNG 实测在主线程之外直接抛异常，这里是"每档一次的主线程编码"。</remarks>
    public sealed class SaveCaptureFeature : ScriptableRendererFeature
    {
        public const int CaptureWidth = 320;

        public const int CaptureHeight = 180;

        private const int StateIdle = 0;
        private const int StateBlitQueued = 1;
        private const int StateReading = 2;

        private const int MaxWaitFrames = 60;

        private readonly struct ThumbnailResult
        {
            public readonly int SlotIndex;

            public readonly byte[] Png;

            public ThumbnailResult(int slotIndex, byte[] png)
            {
                SlotIndex = slotIndex;
                Png = png;
            }
        }

        private static readonly Queue<ThumbnailResult> Results = new Queue<ThumbnailResult>();

        private static SaveCaptureFeature _instance;

        private static bool _warnedUnavailable;

        private SaveCapturePass _pass;

        private RTHandle _thumbnail;

        private int _state = StateIdle;

        private int _pendingSlot = -1;

        private int _waitFrames;

        private bool _readbackInFlight;

        /// <summary>请求抓一次当前画面；未挂载或上一次回读还在途时返回 false，不排队</summary>
        public static bool TryRequestCapture(int slotIndex)
        {
            SaveCaptureFeature feature = _instance;

            if (feature == null || !feature.isActive)
            {
                WarnUnavailableOnce();

                return false;
            }

            if (slotIndex < 0 || feature._state != StateIdle || feature._readbackInFlight) return false;

            feature._pendingSlot = slotIndex;
            feature._state = StateBlitQueued;
            feature._waitFrames = 0;

            return true;
        }

        private static void WarnUnavailableOnce()
        {
            if (_warnedUnavailable) return;

            _warnedUnavailable = true;

            Debug.LogWarning(
                "[Save] Renderer2D.asset 未挂 Save Capture Feature（或它被禁用），本局存档不会生成缩略图。" +
                "挂法：选中 Assets/Settings/Renderer2D.asset → Add Renderer Feature → Save Capture Feature。");
        }

        public override void Create()
        {
            _instance = this;

            _pass = new SaveCapturePass
            {
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing
            };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            DrainResults();

            if (_state == StateIdle) return;

            if (renderingData.cameraData.cameraType != CameraType.Game) return;

            _waitFrames++;

            if (_waitFrames > MaxWaitFrames)
            {
                Debug.LogWarning("[Save] 存档缩略图抓帧超时，本次已跳过。");

                ResetRequest();

                return;
            }

            if (_state == StateBlitQueued)
            {
                if (!EnsureThumbnail())
                {
                    ResetRequest();

                    return;
                }

                _pass.Setup(_thumbnail);

                renderer.EnqueuePass(_pass);

                _state = StateReading;

                return;
            }

            StartReadback();

            ResetRequest();
        }

        protected override void Dispose(bool disposing)
        {
            if (ReferenceEquals(_instance, this)) _instance = null;

            ReleaseThumbnail();
        }

        private bool EnsureThumbnail()
        {
            if (_thumbnail != null && _thumbnail.rt != null) return true;

            ReleaseThumbnail();

            try
            {
                _thumbnail = RTHandles.Alloc(CaptureWidth, CaptureHeight, name: "SaveCaptureThumbnail");

                return _thumbnail != null;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 创建缩略图渲染纹理失败，本次抓帧已跳过：{ex.Message}");

                _thumbnail = null;

                return false;
            }
        }

        private void ReleaseThumbnail()
        {
            if (_thumbnail == null) return;

            RTHandles.Release(_thumbnail);

            _thumbnail = null;
        }

        private void StartReadback()
        {
            if (_thumbnail == null || _thumbnail.rt == null) return;

            int slotIndex = _pendingSlot;

            RenderTexture source = _thumbnail.rt;

            _readbackInFlight = true;

            if (SystemInfo.supportsAsyncGPUReadback)
            {
                AsyncGPUReadback.Request(source, 0, request => OnReadbackCompleted(slotIndex, request));

                return;
            }

            ReadSynchronously(slotIndex, source);
        }

        private void ReadSynchronously(int slotIndex, RenderTexture source)
        {
            RenderTexture previous = RenderTexture.active;

            try
            {
                RenderTexture.active = source;

                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);

                texture.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                texture.Apply();

                EncodeToPng(slotIndex, texture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 同步回读存档缩略图失败：{ex.Message}");
            }
            finally
            {
                RenderTexture.active = previous;

                _readbackInFlight = false;
            }
        }

        private void OnReadbackCompleted(int slotIndex, AsyncGPUReadbackRequest request)
        {
            try
            {
                if (request.hasError)
                {
                    Debug.LogError("[Save] 异步回读存档缩略图失败，本次已跳过。");

                    return;
                }

                var texture = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGBA32, false);

                texture.LoadRawTextureData(request.GetData<byte>());
                texture.Apply();

                EncodeToPng(slotIndex, texture);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 载入回读像素失败：{ex.Message}");
            }
            finally
            {
                _readbackInFlight = false;
            }
        }

        /// <remarks>EncodeToPNG 只准在主线程调，故编码不下放 Task；每个存档只跑一次，不在每帧链上</remarks>
        private static void EncodeToPng(int slotIndex, Texture2D texture)
        {
            try
            {
                byte[] png = ImageConversion.EncodeToPNG(texture);

                if (png != null && png.Length > 0) Results.Enqueue(new ThumbnailResult(slotIndex, png));
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Save] 编码存档缩略图失败，本档无缩略图：{ex.Message}");
            }
            finally
            {
                UnityEngine.Object.Destroy(texture);
            }
        }

        private static void DrainResults()
        {
            while (Results.Count > 0)
            {
                ThumbnailResult result = Results.Dequeue();

                GameRoot root = GameRoot.Instance;

                SaveService save = root != null ? root.Save : null;

                if (save != null) save.AttachThumbnail(result.SlotIndex, result.Png);
            }
        }

        private void ResetRequest()
        {
            _pendingSlot = -1;
            _state = StateIdle;
            _waitFrames = 0;
        }
    }

    /// <summary>只做一次全屏降采样拷贝，读写分离由 Feature 分两帧编排</summary>
    internal sealed class SaveCapturePass : ScriptableRenderPass
    {
        private RTHandle _target;

        public void Setup(RTHandle target)
        {
            _target = target;
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_target == null) return;

            CommandBuffer cmd = CommandBufferPool.Get("SaveCapture");

            Blitter.BlitCameraTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, _target);

            context.ExecuteCommandBuffer(cmd);

            CommandBufferPool.Release(cmd);
        }
    }
}
