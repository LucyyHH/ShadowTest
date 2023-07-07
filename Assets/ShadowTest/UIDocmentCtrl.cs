using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace ShadowTest {
    public class UIDocumentCtrl : MonoBehaviour {
        public Light curLight;
        
        public GameObject goGenerateShadowMap;
        public GameObject goGenerateCustomShadow;
        
        private UIDocument _document;
 
        private VisualElement _rootElement;
        private IntegerField _txtGenerate;
        private Button _btnShadowMap;
        private Button _btnCustom;
        private Button _btnRandomLightDir;

        private readonly List<GameObject> _gameObjects = new List<GameObject>();

        private void Start() {
            _document = GetComponent<UIDocument>();
 
            _rootElement = _document.rootVisualElement;//获取根
 
            //在根下，通过JQuery和名字获取控件
            _txtGenerate = _rootElement.Q<IntegerField>("txtGenerate");
            _btnShadowMap = _rootElement.Q<Button>("btnShadowMap");
            _btnCustom = _rootElement.Q<Button>("btnCustom");
            _btnRandomLightDir = _rootElement.Q<Button>("btnRandomLightDir");

            //为按钮绑定事件
            //使用普通方法绑定按钮事件
            _btnShadowMap.RegisterCallback<ClickEvent>(OnBtnShadowMapClick);
            _btnCustom.RegisterCallback<ClickEvent>(OnBtnCustomClick);
            _btnRandomLightDir.RegisterCallback<ClickEvent>(OnBtnRandomLightDir);
 
            //初始化内容显示
            UpdateLabelText();
        }
 
        private void OnBtnShadowMapClick(ClickEvent evt) {
            if(goGenerateShadowMap && _txtGenerate.value > 0) {
                foreach(var go in _gameObjects) {
                    if(go) {
                        Destroy(go);
                    }
                }
                _gameObjects.Clear();
                for(var i = 0; i < _txtGenerate.value; i++) {
                    _gameObjects.Add(Instantiate(goGenerateShadowMap));
                }

                if(curLight) {
                    curLight.shadows = LightShadows.Hard;
                }
                
                _txtGenerate.label = "ShadowMap";
            }
        }
        private void OnBtnCustomClick(ClickEvent evt) {
            if(goGenerateCustomShadow && _txtGenerate.value > 0) {
                foreach(var go in _gameObjects) {
                    if(go) {
                        Destroy(go);
                    }
                }
                _gameObjects.Clear();
                for(var i = 0; i < _txtGenerate.value; i++) {
                    _gameObjects.Add(Instantiate(goGenerateCustomShadow));
                }
                
                curLight.shadows = LightShadows.None;
 
                _txtGenerate.label = "Custom";
            }
        }

        private void OnBtnRandomLightDir(ClickEvent evt) {
            
        }

        private void UpdateLabelText() {
            _txtGenerate.value = 150;
            _txtGenerate.label = "未生成";
        }
 
    }
}