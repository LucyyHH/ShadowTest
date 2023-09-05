using UnityEditor;
using UnityEngine;

namespace ShadowTest.Custom.Editor {
    [CustomEditor(typeof(GenerateMapHeightTex))]
    public class GenerateMapHeightTexInspector : UnityEditor.Editor {
        
        private GenerateMapHeightTex _generateMapHeightTex;
 
        private void OnEnable() {
            _generateMapHeightTex = target as GenerateMapHeightTex;
        }
 
        public override void OnInspectorGUI() {
            //base.OnInspectorGUI();

            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();
            
            var iterator = serializedObject.GetIterator();
            var curNeedShow = true;
            while(iterator.NextVisible(true)) {
                var curShow = true;
                
                if(iterator.name == "mapGo") {
                    curNeedShow = !ReferenceEquals(_generateMapHeightTex.mapGo, null);
                } else if(iterator.name == "changeShadowDir") {
                    curNeedShow = _generateMapHeightTex.changeLightDir;
                } else if(iterator.name == "needLimitLeft") {
                    curNeedShow = _generateMapHeightTex.needLimitLeft;
                } else if(iterator.name == "needLimitRight") {
                    curNeedShow = _generateMapHeightTex.needLimitRight;
                } else if(iterator.name == "needLimitBottom") {
                    curNeedShow = _generateMapHeightTex.needLimitBottom;
                } else if(iterator.name == "needLimitTop") {
                    curNeedShow = _generateMapHeightTex.needLimitTop;
                } else if(iterator.name == "needLimitBack") {
                    curNeedShow = _generateMapHeightTex.needLimitBack;
                } else if(iterator.name == "needLimitFront") {
                    curNeedShow = _generateMapHeightTex.needLimitFront;
                } else if(iterator.name == "needHeightOffset") {
                    curNeedShow = _generateMapHeightTex.needHeightOffset;
                } else if(iterator.name == "checkHeightOffsetLayer") {
                    curNeedShow = _generateMapHeightTex.checkHeightOffsetLayer;
                } else {
                    curShow = curNeedShow;
                }
                
                if(curShow) {
                    EditorGUILayout.PropertyField(iterator);
                }
            }
            
            if(GUILayout.Button("生成")) {
                _generateMapHeightTex.GenerateMapHeightTexture();
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            //需要在OnInspectorGUI之前修改属性，否则无法修改值
            serializedObject.ApplyModifiedProperties();
        }
    }
}