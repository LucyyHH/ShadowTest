using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace RenderPipeline.RenderFeature{
    public class ProjectorShadowPass : ScriptableRenderPass{
        private readonly LinkedList<Renderer> m_renderers;
        private readonly Material m_shadowMat;

        private readonly List<ShaderTagId> _mShaderTagIdList = new List<ShaderTagId>()
        {
            //new ShaderTagId("ShadowCaster"),
            new ShaderTagId("UniversalForward")
            //new ShaderTagId("UniversalPipeline")
        };
        private FilteringSettings _mFilteringSettings;

        public ProjectorShadowPass (RenderPassEvent passEvent, LinkedList<Renderer> renderers, Material shadowMat){
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
            
            /*if (m_renderers != null && m_renderers.Count > 0 && m_shadowMat != null) {
                var cmd = CommandBufferPool.Get ("ProjectorShadowPass");
                var current = m_renderers.First;
                //拿第一个不是空的,且显示的Renderer
                while (current != null && current.Value == null){
                    if (current.Value == null){
                        m_renderers.Remove (current);
                        current = m_renderers.First;
                    }
                    else if (!current.Value.gameObject.activeInHierarchy){
                        current = current.Next;
                    }
                    else{
                        break;
                    }
                }
                
                while (current != null){
                    cmd.DrawRenderer (current.Value, m_shadowMat);
                    while (current.Next != null){
                        //下一个renderer如果是空的移除,不显示的，跳过
                        if (current.Next.Value == null){
                            m_renderers.Remove (current.Next);
                        }else if (!current.Next.Value.gameObject.activeInHierarchy){
                            current = current.Next;
                        }
                        else{
                            break;
                        }
                    }
                    current = current.Next;
                }
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }*/
        }
    }
}