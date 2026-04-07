using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace MornLib
{
    /// <summary>複数スプライトの統合アウトラインを描画するRenderer Feature</summary>
    public sealed class Morn2DOutlineRendererFeature : ScriptableRendererFeature
    {
        [Serializable]
        public sealed class Settings
        {
            [Tooltip("アウトライン対象のUnityレイヤー")]
            public LayerMask TargetLayerMask;

            [Tooltip("シルエット描画用シェーダー (Hidden/Morn2D/OutlineSilhouette)")]
            public Shader SilhouetteShader;

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
            if (_settings.SilhouetteShader != null)
            {
                _silhouetteMaterial = CoreUtils.CreateEngineMaterial(_settings.SilhouetteShader);
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
            CoreUtils.Destroy(_silhouetteMaterial);
        }

        /// <summary>対象レイヤーのスプライトをシルエットとしてRTに描画するパス</summary>
        private sealed class Morn2DOutlineSilhouettePass : ScriptableRenderPass
        {
            private static readonly int s_silhouetteTexId = Shader.PropertyToID("_Morn2DOutlineSilhouetteTex");
            private static readonly int s_silhouetteTexTexelSizeId = Shader.PropertyToID("_Morn2DOutlineSilhouetteTex_TexelSize");

            private readonly Material _silhouetteMaterial;
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

            private sealed class SilhouettePassData
            {
                public RendererListHandle RendererListHandle;
                public TextureHandle SilhouetteTexture;
                public int Width;
                public int Height;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var cameraData = frameData.Get<UniversalCameraData>();
                var renderingData = frameData.Get<UniversalRenderingData>();
                var lightData = frameData.Get<UniversalLightData>();

                var desc = cameraData.cameraTargetDescriptor;
                desc.msaaSamples = 1;
                desc.depthBufferBits = 0;
                desc.colorFormat = RenderTextureFormat.R8;
                var silhouetteTexture = UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph, desc, "_Morn2DOutlineSilhouetteTex", false,
                    FilterMode.Bilinear, TextureWrapMode.Clamp);

                using (var builder = renderGraph.AddRasterRenderPass<SilhouettePassData>(
                           "Morn2DOutlineSilhouette", out var passData, _profilingSampler))
                {
                    builder.SetRenderAttachment(silhouetteTexture, 0);

                    var drawingSettings = RenderingUtils.CreateDrawingSettings(
                        _shaderTagIds, renderingData, cameraData, lightData,
                        SortingCriteria.CommonTransparent);
                    drawingSettings.overrideMaterial = _silhouetteMaterial;
                    drawingSettings.overrideMaterialPassIndex = 0;

                    var filteringSettings = new FilteringSettings(
                        RenderQueueRange.all, TargetLayerMask);

                    var rendererListParams = new RendererListParams(
                        renderingData.cullResults, drawingSettings, filteringSettings);
                    passData.RendererListHandle = renderGraph.CreateRendererList(rendererListParams);
                    builder.UseRendererList(passData.RendererListHandle);
                    passData.SilhouetteTexture = silhouetteTexture;

                    builder.SetRenderFunc(static (SilhouettePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.ClearRenderTarget(false, true, Color.clear);
                        context.cmd.DrawRendererList(data.RendererListHandle);
                    });
                }

                // グローバルテクスチャとTexelSizeの設定
                using (var builder = renderGraph.AddUnsafePass<SilhouettePassData>(
                           "Morn2DOutlineSilhouetteSetGlobals", out var globalPassData))
                {
                    globalPassData.SilhouetteTexture = silhouetteTexture;
                    globalPassData.Width = desc.width;
                    globalPassData.Height = desc.height;
                    builder.UseTexture(silhouetteTexture);

                    builder.SetRenderFunc(static (SilhouettePassData data, UnsafeGraphContext context) =>
                    {
                        var cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);
                        cmd.SetGlobalTexture(s_silhouetteTexId, data.SilhouetteTexture);
                        cmd.SetGlobalVector(s_silhouetteTexTexelSizeId, new Vector4(
                            1f / data.Width, 1f / data.Height, data.Width, data.Height));
                    });
                }
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

            private sealed class CompositePassData
            {
                public Material CompositeMaterial;
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                using (var builder = renderGraph.AddRasterRenderPass<CompositePassData>(
                           "Morn2DOutlineComposite", out var passData, _profilingSampler))
                {
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0);
                    passData.CompositeMaterial = _settings.CompositeMaterial;

                    builder.SetRenderFunc(static (CompositePassData data, RasterGraphContext context) =>
                    {
                        context.cmd.DrawProcedural(
                            Matrix4x4.identity, data.CompositeMaterial, 0,
                            MeshTopology.Triangles, 3, 1);
                    });
                }
            }
        }
    }
}
