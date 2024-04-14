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
            iterator.NextVisible(true);
            EditorGUILayout.PropertyField(iterator);
            var haveMapGo = _generateMapHeightTex.mapGo != null;
            while(iterator.NextVisible(false)) {
                var curShow = haveMapGo;
                
                if(iterator.name == "mapGo") {
                    curShow = curNeedShow = true;
                } else if(iterator.name == "fixedShadowDir") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.fixedShadowDir;
                } else if(iterator.name == "fixedShadowDir") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.fixedShadowDir;
                } else if(iterator.name == "needLimitLeft") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitLeft;
                } else if(iterator.name == "needLimitRight") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitRight;
                } else if(iterator.name == "needLimitBottom") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitBottom;
                } else if(iterator.name == "needLimitTop") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitTop;
                } else if(iterator.name == "needLimitBack") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitBack;
                } else if(iterator.name == "needLimitFront") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needLimitFront;
                } else if(iterator.name == "needHeightOffset") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.needHeightOffset;
                } else if(iterator.name == "checkHeightOffsetLayer") {
                    curNeedShow = haveMapGo && _generateMapHeightTex.checkHeightOffsetLayer;
                } else {
                    curShow = haveMapGo && curNeedShow;
                }
                
                if(curShow) {
                    EditorGUILayout.PropertyField(iterator, includeChildren: false);
                }
            }
            
            if(haveMapGo && GUILayout.Button("生成")) {
                _generateMapHeightTex.GenerateMapHeightTexture();
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            //需要在OnInspectorGUI之前修改属性，否则无法修改值
            serializedObject.ApplyModifiedProperties();
        }
    }
}