using System;
using System.Collections.Generic;
using RenderPipeline.RenderFeature;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

    /// <summary>
    ///     用于实现基于Projector的影子
    /// </summary>
    public class ProjectorShadow: IDisposable{
        /// <summary>
        ///     初始化投影器所需的信息
        /// </summary>
        public struct ProjectorConfig{
            public int ShadowTexHeight;
            public int ShadowTexWidth;
            public float CameraNearClipPlane;
            public float CameraFarClipPlane;
            public bool Orthographic;
            public float CameraSize;
            public LayerMask ProjectorIgnoreLayers;
            public Vector3 ProjectorPos;
            public Quaternion ProjectorRotate;
            public Vector3 ShadowCameraPos;
            public Quaternion ShadowCameraRotate;
            public float ShadowIntensity;
        }

        public LinkedList<Renderer> ShadowRenderer{ get; private set; }
        private Material m_shadowCasterMat;

        /// <summary>
        ///     相机渲染出来的RT
        /// </summary>
        private RenderTexture m_cameraRT;

        /// <summary>
        ///     自身用的投影器
        /// </summary>
        private Projector m_urpProjector;

        public Projector urpProjector
        {
            get => m_urpProjector;
        }
        
        private ProjectorForLWRP.ProjectorForLWRP m_projectorForLwrp;

        /// <summary>
        ///     自身用的Camera
        /// </summary>
        private Camera m_camera;

        /// <summary>
        ///     自身用的相机数据
        /// </summary>
        private UniversalAdditionalCameraData m_cameraData;
        

        private ProjectorShadowPass m_casterPass;
        
        private GameObject m_parentObj;
        private static readonly int ShadowTex = Shader.PropertyToID ("_ShadowTex");
        private static readonly int Intensity = Shader.PropertyToID ("_Intensity");

        ~ProjectorShadow (){
            Dispose ();
        }

        public void Dispose (){
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            m_projectorForLwrp = null;
            m_cameraData = null;
            m_casterPass = null;
            if (m_shadowCasterMat){
                Object.Destroy (m_shadowCasterMat);
            }

            if (m_urpProjector){
                if (m_urpProjector.material){
                    Object.Destroy (m_urpProjector.material);
                }

                m_urpProjector = null;
            }

            if (m_camera){
                m_camera.targetTexture = null;
                m_camera = null;
            }

            if (m_cameraRT){
                RenderTexture.ReleaseTemporary (m_cameraRT);
                m_cameraRT = null;
            }

            if (ShadowRenderer == null){
                return;
            }

            ShadowRenderer.Clear ();
            ShadowRenderer = null;

            if (m_parentObj){
                Object.Destroy (m_parentObj);
            }
        }

        /// <summary>
        ///     初始化
        /// </summary>
        public void Init (ProjectorConfig config, Material material){
            //--------------------------------------------创建对象--------------------------------------------
            if (!m_parentObj){
                m_parentObj = new GameObject ("Projector shadow");

                var projectorObj = new GameObject ("Projector"){
                    transform ={
                        parent = m_parentObj.transform,
                        position = config.ProjectorPos,
                        rotation = config.ProjectorRotate
                    },
                    gameObject ={
                        layer = LayerMask.NameToLayer ("GenerateHeightMap")
                    }
                };
            
                var cameraObj = new GameObject ("ShadowCamera"){
                    transform ={
                        parent = m_parentObj.transform,
                        position = config.ShadowCameraPos,
                        rotation = config.ShadowCameraRotate
                    },
                    gameObject ={
                        layer = LayerMask.NameToLayer ("GenerateHeightMap")
                    }
                };
                //--------------------------------------------准备各种组件--------------------------------------------
                if (ShadowRenderer == null)
                    ShadowRenderer = new LinkedList<Renderer> ();
                if (m_urpProjector == null)
                    m_urpProjector = projectorObj.AddComponent<Projector> ();
                if (m_projectorForLwrp == null)
                    m_projectorForLwrp = projectorObj.AddComponent<ProjectorForLWRP.ProjectorForLWRP> ();
                if (m_camera == null)
                    m_camera = cameraObj.AddComponent<Camera> ();
                if (m_cameraData == null)
                    m_cameraData = cameraObj.AddComponent<UniversalAdditionalCameraData> ();
                if (m_cameraRT == null){
                    //从rt池里取出一个R8格式的rt对象，尺寸需要暴露给使用者
                    m_cameraRT = RenderTexture.GetTemporary (config.ShadowTexWidth, config.ShadowTexHeight, 0,
                        RenderTextureFormat.R8);
                    //--------------------------------------------RT属性设置--------------------------------------------
                    //设置rt部分属性，名字，保真值，筛选模式，重复模式
                    m_cameraRT.name = "ShadowRT";
                    m_cameraRT.filterMode = FilterMode.Bilinear;
                    m_cameraRT.wrapMode = TextureWrapMode.Clamp;
                    m_cameraRT.antiAliasing = 1;
                    m_cameraRT.autoGenerateMips = false;
                }
            }
            else{
                Transform transform = m_urpProjector.transform;
                transform.position = config.ProjectorPos;
                transform.rotation = config.ProjectorRotate;
                Transform transform1 = m_camera.transform;
                transform1.position = config.ShadowCameraPos;
                transform1.rotation = config.ShadowCameraRotate;
                if (m_cameraRT){
                    RenderTexture.ReleaseTemporary (m_cameraRT);
                    m_cameraRT = null;
                }
                //从rt池里取出一个R8格式的rt对象，尺寸需要暴露给使用者
                m_cameraRT = RenderTexture.GetTemporary (config.ShadowTexWidth, config.ShadowTexHeight, 0,
                    RenderTextureFormat.R8);
                //--------------------------------------------RT属性设置--------------------------------------------
                //设置rt部分属性，名字，保真值，筛选模式，重复模式
                m_cameraRT.name = "ShadowRT";
                m_cameraRT.filterMode = FilterMode.Bilinear;
                m_cameraRT.wrapMode = TextureWrapMode.Clamp;
                m_cameraRT.antiAliasing = 2;
                m_cameraRT.autoGenerateMips = false;
            }
            //--------------------------------------------公共属性设置--------------------------------------------
            m_urpProjector.nearClipPlane = m_camera.nearClipPlane = config.CameraNearClipPlane;
            m_urpProjector.farClipPlane = m_camera.farClipPlane = config.CameraFarClipPlane;
            m_urpProjector.orthographic = m_camera.orthographic = config.Orthographic;
            if (config.Orthographic){
                m_urpProjector.orthographicSize = m_camera.orthographicSize = config.CameraSize;
            }
            else{
                m_urpProjector.fieldOfView = m_camera.fieldOfView = config.CameraSize;
            }

            //--------------------------------------------Projector属性设置--------------------------------------------
            m_urpProjector.material = material;
            // todo 设置材质
            m_urpProjector.material.SetTexture (ShadowTex, m_cameraRT);
            m_urpProjector.material.SetFloat (Intensity, config.ShadowIntensity);
            m_urpProjector.ignoreLayers = config.ProjectorIgnoreLayers;
            m_urpProjector.aspectRatio = config.ShadowTexWidth / (float) config.ShadowTexHeight;
            m_projectorForLwrp.renderQueueUpperBound = 2000;
            m_projectorForLwrp.renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            //--------------------------------------------Camera属性设置--------------------------------------------
            m_camera.cullingMask = 0;
            m_camera.depth = -100;
            m_camera.clearFlags = CameraClearFlags.SolidColor;
            m_camera.targetTexture = m_cameraRT;
            m_camera.backgroundColor = Color.black;
            //--------------------------------------------其他设置--------------------------------------------
            m_shadowCasterMat = new Material (Shader.Find ("BokeGame/ProjectShadowCaster_2D"));
            //m_shadowCasterMat = new Material (BY_AssetUtilU5.GetAsset(0,"Common/shader@/ProjectShadowCaster_2D.shader") as Shader);
            m_casterPass = new ProjectorShadowPass (RenderPassEvent.AfterRenderingTransparents, ShadowRenderer, m_shadowCasterMat);
        }

        public void EnableShadow (){
            m_parentObj.SetActive (true);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        public void DisableShadow (){
            m_parentObj.SetActive (false);
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        /// <summary>
        ///     当开始渲染时触发
        /// </summary>
        /// <param name="context">渲染上下文</param>
        /// <param name="rendererCamera">当前渲染的相机</param>
        private void OnBeginCameraRendering (ScriptableRenderContext context, Camera rendererCamera){
            if (rendererCamera == m_camera && m_cameraData && m_casterPass != null){
                m_cameraData.scriptableRenderer.EnqueuePass (m_casterPass);
            }
        }
    }
