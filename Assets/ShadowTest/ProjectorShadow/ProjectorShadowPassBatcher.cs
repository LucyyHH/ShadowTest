using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderPipeline.RenderFeature{
    public class ProjectorShadowPassBatcher : ScriptableRenderPass{
        private readonly LinkedList<Renderer> m_renderers;
        private readonly Material m_shadowMat;

        private readonly List<ShaderTagId> _mShaderTagIdList = new List<ShaderTagId>()
        {
            //new ShaderTagId("ShadowCaster"),
            new ShaderTagId("UniversalForward")
            //new ShaderTagId("UniversalPipeline")
        };
        private FilteringSettings _mFilteringSettings;

        public ProjectorShadowPassBatcher (RenderPassEvent passEvent, LinkedList<Renderer> renderers, Material shadowMat){
            renderPassEvent = passEvent;
            m_renderers = renderers;
            m_shadowMat = shadowMat;
            
            _mFilteringSettings = FilteringSettings.defaultValue;
            _mFilteringSettings.layerMask = LayerMask.NameToLayer("GenerateShadow");
            //_mFilteringSettings.renderingLayerMask = 2U;
            _mFilteringSettings.renderQueueRange = RenderQueueRange.opaque;
            //_mFilteringSettings = new FilteringSettings(RenderQueueRange.all);
        }
        public override void Execute (ScriptableRenderContext context, ref RenderingData renderingData){
            if (m_shadowMat) {
                var drawingSettings = CreateDrawingSettings(_mShaderTagIdList, ref renderingData, SortingCriteria.CommonOpaque);
                    
                drawingSettings.overrideMaterial = m_shadowMat;
                //这里不需要所以没有直接写CommandBuffer，在下面Feature的AddRenderPasses加入了渲染队列，底层还是CB
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref _mFilteringSettings);
            }
        }
    }
}