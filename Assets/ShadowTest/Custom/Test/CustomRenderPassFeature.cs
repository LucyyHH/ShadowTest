using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CustomRenderPassFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class postSettings
    {
        //定义该pass的绘制时间，是在哪个阶段之后
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        public Material featureMaterial;
        public float featureIntensity = 0.2f;
        public Color featureColor = Color.white;
    }
    [SerializeField]
    
    class CustomRenderPass : ScriptableRenderPass
    {
        public Material material;
        public float passIntensity;
        public Color color;
        
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //新建RT需要的一个类
            RenderTextureDescriptor rt = new RenderTextureDescriptor(Camera.main.pixelWidth , Camera.main.pixelHeight,RenderTextureFormat.Default,0);
            RenderTexture rtTex = new RenderTexture(rt);          // 新建贴图

            RenderTargetIdentifier cameraColorTexture = renderingData.cameraData.renderer.cameraColorTarget;        //获取相机的RT

            // var strack = VolumeManager.instance.stack;                              //实例化到堆栈
            // customVolume = strack.GetComponent<CustomRenderPassVolume>();           //从堆栈中获取到Volume上面的组件

            //获取一个新的命令缓冲区，并分配一个名称，命名命名缓冲区将为缓冲区执行饮食添加分析生成器
            CommandBuffer cmd = CommandBufferPool.Get(name:"customPostRenderPass");    //定义pass的名字，该名字会在FrameDebug窗口显示相应的pass
       
            material.SetFloat("_Intensity" , passIntensity);            
            material.SetColor("_Color",color);
            // material.SetColor("_Color" , customVolume.changeColor.value);       //使用Volume上面的参数替代了RenderFeature里面的参数

            cmd.Blit(cameraColorTexture , rtTex , material);      //对相机里面的画面进行一些操作，将相机获取到的RT图，通过material材质的计算，输出到tex
            cmd.Blit(rtTex,cameraColorTexture);                   //将上一步的tex，在重新写回相机
            
            context.ExecuteCommandBuffer(cmd);                   //自定义CommandBuffer执行
            cmd.Clear();                                         //清楚缓冲区的所有命令
            CommandBufferPool.Release(cmd);                      //释放CommandBuffer
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
        }
    }

    CustomRenderPass m_ScriptablePass;
    public postSettings settings = new postSettings();

    public override void Create()
    {
        m_ScriptablePass = new CustomRenderPass();

        m_ScriptablePass.material = settings.featureMaterial;
        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;
        m_ScriptablePass.passIntensity = settings.featureIntensity;
        m_ScriptablePass.color = settings.featureColor;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        renderer.EnqueuePass(m_ScriptablePass);
    }
}