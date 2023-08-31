using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HLLR {
    public class SceneProjShadowRenderFeature : ScriptableRendererFeature {
        public Material projShadowMaterial;
        public LayerMask layerMask;
        public ComputeShader changeShadowDirComputeShader;

        private ShadowRenderPass _mPass;
        //private static readonly int MainLightDir = Shader.PropertyToID ("_MainLightDir");

        public override void Create() {
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData) {
            //只有主相机需要添加
            if (renderingData.cameraData.renderType == CameraRenderType.Base) {
                if (projShadowMaterial) {
                    _mPass = new ShadowRenderPass(projShadowMaterial, layerMask, changeShadowDirComputeShader);
                }

                if (_mPass != null) {
                    renderer.EnqueuePass(_mPass);
                }
            }
        }

        private class ShadowRenderPass : ScriptableRenderPass {
            private readonly List<ShaderTagId> _mShaderTagIdList = new List<ShaderTagId>()
            {
                //new ShaderTagId("ShadowCaster"),
                new ShaderTagId("UniversalForward")
                //new ShaderTagId("UniversalPipeline")
            };

            private readonly Material _overrideMaterial;
            private readonly ComputeShader _changeShadowDirComputeShader;
            private FilteringSettings _mFilteringSettings;
            
            public ShadowRenderPass(Material material, LayerMask layerMask, ComputeShader changeShadowDirComputeShader) {
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;

                _overrideMaterial = material;
                _changeShadowDirComputeShader = changeShadowDirComputeShader;
            
                _mFilteringSettings = FilteringSettings.defaultValue;
                _mFilteringSettings.layerMask = layerMask;
                //_mFilteringSettings.renderingLayerMask = 2U;
                _mFilteringSettings.renderQueueRange = RenderQueueRange.opaque;
                //_mFilteringSettings = new FilteringSettings(RenderQueueRange.all);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData) {
                if (_overrideMaterial) {
                    var drawingSettings = CreateDrawingSettings(_mShaderTagIdList, ref renderingData, SortingCriteria.CommonOpaque);

                    // 如果影子方向发生变化，则重新计算
                    if(true) {
                        var mainKernel = _changeShadowDirComputeShader.FindKernel("CSMain");
                        
                    }
                    
                    drawingSettings.overrideMaterial = _overrideMaterial;
                    //这里不需要所以没有直接写CommandBuffer，在下面Feature的AddRenderPasses加入了渲染队列，底层还是CB
                    context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _mFilteringSettings);
                }
            }
        }
    }
}