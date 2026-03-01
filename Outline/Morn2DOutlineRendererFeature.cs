using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace MornLib
{
    /// <summary>複数スプライトの統合アウトラインを描画するRenderer Feature</summary>
    public sealed class Morn2DOutlineRendererFeature : ScriptableRendererFeature
    {
        private const string SilhouetteShaderName = "Hidden/Morn2D/OutlineSilhouette";

        [Serializable]
        public sealed class Settings
        {
            [Tooltip("アウトライン対象のUnityレイヤー")]
            public LayerMask TargetLayerMask;

            [Tooltip("アウトライン合成用マテリアル (Morn2D/OutlineComposite)")]
            public Material CompositeMaterial;

            public RenderPassEvent RenderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        }

        [SerializeField] private Settings _settings = new();
        private Material _silhouetteMaterial;
        private Morn2DOutlineSilhouettePass _silhouettePass;
        private Morn2DOutlineCompositePass _compositePass;

        public override void Create()
        {
            var silhouetteShader = Shader.Find(SilhouetteShaderName);
            if (silhouetteShader != null)
            {
                _silhouetteMaterial = CoreUtils.CreateEngineMaterial(silhouetteShader);
            }

            _silhouettePass = new Morn2DOutlineSilhouettePass(_silhouetteMaterial);
            _compositePass = new Morn2DOutlineCompositePass(_settings);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_silhouetteMaterial == null || _settings.CompositeMaterial == null)
            {
                return;
            }

            _silhouettePass.renderPassEvent = _settings.RenderPassEvent;
            _silhouettePass.TargetLayerMask = _settings.TargetLayerMask;
            _compositePass.renderPassEvent = _settings.RenderPassEvent + 1;

            renderer.EnqueuePass(_silhouettePass);
            renderer.EnqueuePass(_compositePass);
        }

        protected override void Dispose(bool disposing)
        {
            _silhouettePass?.Dispose();
            CoreUtils.Destroy(_silhouetteMaterial);
        }

        /// <summary>対象レイヤーのスプライトをシルエットとしてRTに描画するパス</summary>
        private sealed class Morn2DOutlineSilhouettePass : ScriptableRenderPass, IDisposable
        {
            private static readonly int s_silhouetteTexId = Shader.PropertyToID("_Morn2DOutlineSilhouetteTex");
            private static readonly int s_silhouetteTexTexelSizeId = Shader.PropertyToID("_Morn2DOutlineSilhouetteTex_TexelSize");

            private readonly Material _silhouetteMaterial;
            private RTHandle _silhouetteRT;
            private readonly List<ShaderTagId> _shaderTagIds;
            private readonly ProfilingSampler _profilingSampler;

            public LayerMask TargetLayerMask { get; set; }

            public Morn2DOutlineSilhouettePass(Material silhouetteMaterial)
            {
                _silhouetteMaterial = silhouetteMaterial;
                _profilingSampler = new ProfilingSampler("Morn2DOutlineSilhouette");
                _shaderTagIds = new List<ShaderTagId>
                {
                    new ShaderTagId("Universal2D"),
                    new ShaderTagId("UniversalForward"),
                    new ShaderTagId("UniversalForwardOnly"),
                    new ShaderTagId("SRPDefaultUnlit"),
                };
            }

#pragma warning disable CS0618
            public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
            {
                var desc = renderingData.cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.R8;
                RenderingUtils.ReAllocateHandleIfNeeded(
                    ref _silhouetteRT, desc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                    name: "_Morn2DOutlineSilhouetteTex");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    CoreUtils.SetRenderTarget(cmd, _silhouetteRT, ClearFlag.Color, Color.clear);

                    context.ExecuteCommandBuffer(cmd);
                    cmd.Clear();

                    var drawingSettings = CreateDrawingSettings(
                        _shaderTagIds, ref renderingData, SortingCriteria.CommonTransparent);
                    drawingSettings.overrideMaterial = _silhouetteMaterial;
                    drawingSettings.overrideMaterialPassIndex = 0;

                    var filteringSettings = new FilteringSettings(
                        RenderQueueRange.all, TargetLayerMask);

                    context.DrawRenderers(
                        renderingData.cullResults, ref drawingSettings, ref filteringSettings);

                    cmd.SetGlobalTexture(s_silhouetteTexId, _silhouetteRT.nameID);
                    var rt = _silhouetteRT.rt;
                    cmd.SetGlobalVector(s_silhouetteTexTexelSizeId, new Vector4(
                        1f / rt.width, 1f / rt.height, rt.width, rt.height));
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618

            public void Dispose()
            {
                _silhouetteRT?.Release();
            }
        }

        /// <summary>シルエットRTからアウトラインを生成してカメラカラーバッファに合成するパス</summary>
        private sealed class Morn2DOutlineCompositePass : ScriptableRenderPass
        {
            private readonly Settings _settings;
            private readonly ProfilingSampler _profilingSampler;

            public Morn2DOutlineCompositePass(Settings settings)
            {
                _settings = settings;
                _profilingSampler = new ProfilingSampler("Morn2DOutlineComposite");
            }

#pragma warning disable CS0618
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, _profilingSampler))
                {
                    CoreUtils.SetRenderTarget(
                        cmd, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    cmd.DrawProcedural(
                        Matrix4x4.identity, _settings.CompositeMaterial, 0,
                        MeshTopology.Triangles, 3, 1);
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
#pragma warning restore CS0618
        }
    }
}
