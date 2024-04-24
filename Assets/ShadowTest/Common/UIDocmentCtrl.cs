using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace ShadowTest {
    public class UIDocumentCtrl : MonoBehaviour {
        public SceneType sceneType;
        
        public GameObject[] goGenerates;
        
        private UIDocument _document;
 
        private VisualElement _rootElement;
        private Label _txtDebugInfo;
        private IntegerField _txtGenerate;
        private Button _btnGenerate;
        private Button _btnChangeScene;

        private readonly List<GameObject> _gameObjects = new List<GameObject>();

        /// <summary>
        /// 场景类型
        /// </summary>
        public enum SceneType {
            Default,
            Custom,
            ProjectorShadow
        }

        public int initGenerateCount = 500;
        public int frameCount = 100; // 要计算的帧数

        private float deltaTime = 0.0f;
        private float totalTime = 0f;
        private float averageFrameRate = 0.0f;

        private void Awake() {
            QualitySettings.SetQualityLevel((int)sceneType, true);
        }

        private void Start() {
            deltaTime = 0.0f;
            
            _document = GetComponent<UIDocument>();
 
            _rootElement = _document.rootVisualElement;//获取根
 
            //在根下，通过JQuery和名字获取控件
            _txtDebugInfo = _rootElement.Q<Label>("txtDebugInfo");
            _txtGenerate = _rootElement.Q<IntegerField>("txtGenerate");
            _btnGenerate = _rootElement.Q<Button>("btnGenerate");

            //为按钮绑定事件
            //使用普通方法绑定按钮事件
            _btnGenerate.RegisterCallback<ClickEvent>(OnBtnGenerateClick);
 
            //初始化内容显示
            UpdateLabelText();

            _txtGenerate.value = initGenerateCount;
            GenerateGos();
            
            InvokeRepeating(nameof(UpdateDebugInfo), 0f, 0.5f);
        }

        private void Update()
        {
            deltaTime += (Time.deltaTime - deltaTime) * 0.1f;
            
            // 累加每一帧的时间
            if (Time.frameCount > frameCount)
            {
                totalTime += Time.deltaTime;
            }
            averageFrameRate = (Time.frameCount - frameCount) / totalTime;

            /*// 累加每一帧的时间
            totalTime += Time.deltaTime;
            // 当累加到指定帧数时，计算平均帧数并输出
            if (Time.frameCount % frameCount == 0)
            {
                averageFrameRate = frameCount / totalTime;
                totalTime = 0f; // 重置累加时间
            }*/
        }
 
        private void OnBtnGenerateClick(ClickEvent evt) {
            GenerateGos();
        }

        private void GenerateGos() {
            if(goGenerates == null || goGenerates.Length == 0 || _txtGenerate.value <= 0) {
                return;
            }
            
            foreach (var go in _gameObjects.Where(go => go))
            {
                Destroy(go);
            }
            _gameObjects.Clear();
            
            // 生成近点
            var nearValue = 2.5f;
            // 生成远点
            var farValue = 6.5f;
            // 计算距离
            var length = Mathf.CeilToInt(Mathf.Sqrt(_txtGenerate.value));
            // 计算每次生成的z轴值
            var perStepZ = farValue / length;
            // 循环生成
            for(var i = 0; i < length; i++) {
                // 计算每次生成的x轴值
                var rightX = nearValue + i * perStepZ;
                // 计算每个循环的x轴值
                var perStepX = rightX * 2 / length;
                // 循环每个循环
                for(var j = 0; j < length; j++) {
                    // 判断是否达到数量要求
                    if(i * length + j >= _txtGenerate.value) {
                        GetComponent<IGenerateComplete>()?.Callback(_gameObjects);
                        
                        return;
                    }
                    
                    // 实例化
                    var go = Instantiate(goGenerates[Random.Range(0, goGenerates.Length)]);
                    // 设置位置
                    go.transform.position = new Vector3(-rightX + j * perStepX, 0, i * perStepZ);
                    // 添加到列表
                    _gameObjects.Add(go);
                }
            }
            
            GetComponent<IGenerateComplete>()?.Callback(_gameObjects);
        }

        private void UpdateLabelText() {
            _txtGenerate.value = 150;
            _txtGenerate.label = SceneManager.GetActiveScene().name;
        }

        private void UpdateDebugInfo() {
#if UNITY_EDITOR
            _txtDebugInfo.text = $"{UnityStats.drawCalls} drawCall, {UnityStats.batches} batches, {UnityStats.setPassCalls} setPassCalls, {1.0f / deltaTime:0.0} fps, avg {averageFrameRate:0.0} fps";
#endif
        }
    }
}