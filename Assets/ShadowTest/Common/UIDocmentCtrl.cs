using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Random = UnityEngine.Random;

namespace ShadowTest {
    public class UIDocumentCtrl : MonoBehaviour {
        public SceneType sceneType;
        
        public GameObject goGenerate;
        
        private UIDocument _document;
 
        private VisualElement _rootElement;
        private IntegerField _txtGenerate;
        private Button _btnGenerate;
        private Button _btnRandomLightDir;

        private readonly List<GameObject> _gameObjects = new List<GameObject>();

        /// <summary>
        /// 场景类型
        /// </summary>
        public enum SceneType {
            Default,
            Custom,
            ProjectorShadow,
            ShadowMap
        }
        
        private void Awake() {
            QualitySettings.SetQualityLevel((int)sceneType, true);
        }

        private void Start() {
            _document = GetComponent<UIDocument>();
 
            _rootElement = _document.rootVisualElement;//获取根
 
            //在根下，通过JQuery和名字获取控件
            _txtGenerate = _rootElement.Q<IntegerField>("txtGenerate");
            _btnGenerate = _rootElement.Q<Button>("btnGenerate");

            //为按钮绑定事件
            //使用普通方法绑定按钮事件
            _btnGenerate.RegisterCallback<ClickEvent>(OnBtnGenerateClick);
 
            //初始化内容显示
            UpdateLabelText();
        }
 
        private void OnBtnGenerateClick(ClickEvent evt) {
            if(goGenerate && _txtGenerate.value > 0) {
                foreach(var go in _gameObjects) {
                    if(go) {
                        Destroy(go);
                    }
                }
                _gameObjects.Clear();
                for(var i = 0; i < _txtGenerate.value; i++) {
                    _gameObjects.Add(Instantiate(goGenerate));
                }
            }
        }

        private void UpdateLabelText() {
            _txtGenerate.value = 150;
            _txtGenerate.label = SceneManager.GetActiveScene().name;
        }
    }
}