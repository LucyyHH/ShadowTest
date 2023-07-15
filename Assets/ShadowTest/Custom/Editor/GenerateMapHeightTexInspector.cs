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
            base.OnInspectorGUI();

            // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
            serializedObject.Update();
        
            EditorGUILayout.Space();
            _generateMapHeightTex.mapGo = (GameObject) EditorGUILayout.ObjectField("GameObject:", _generateMapHeightTex.mapGo, typeof(GameObject), true);
            if(!ReferenceEquals(_generateMapHeightTex.mapGo, null)) {
                _generateMapHeightTex.normalHeightLayer = EditorGUILayout.LayerField("需要生成高度的普通层:", _generateMapHeightTex.normalHeightLayer);
                EditorGUILayout.Space();

                _generateMapHeightTex.resolutionX = EditorGUILayout.IntField("分辨率X:", _generateMapHeightTex.resolutionX);
                _generateMapHeightTex.resolutionY = EditorGUILayout.IntField("分辨率Y:", _generateMapHeightTex.resolutionY);
                EditorGUILayout.Space();

                _generateMapHeightTex.needLimitLeft = EditorGUILayout.Toggle("边界(-x)限制", _generateMapHeightTex.needLimitLeft);
                if(_generateMapHeightTex.needLimitLeft) {
                    _generateMapHeightTex.leftLimit = EditorGUILayout.FloatField(_generateMapHeightTex.leftLimit);
                }

                _generateMapHeightTex.needLimitRight = EditorGUILayout.Toggle("边界(+x)限制", _generateMapHeightTex.needLimitRight);
                if(_generateMapHeightTex.needLimitRight) {
                    _generateMapHeightTex.rightLimit = EditorGUILayout.FloatField(_generateMapHeightTex.rightLimit);
                }

                _generateMapHeightTex.needLimitBottom = EditorGUILayout.Toggle("边界(-y)限制", _generateMapHeightTex.needLimitBottom);
                if(_generateMapHeightTex.needLimitBottom) {
                    _generateMapHeightTex.bottomLimit = EditorGUILayout.FloatField(_generateMapHeightTex.bottomLimit);
                }

                _generateMapHeightTex.needLimitTop = EditorGUILayout.Toggle("边界(+y)限制", _generateMapHeightTex.needLimitTop);
                if(_generateMapHeightTex.needLimitTop) {
                    _generateMapHeightTex.topLimit = EditorGUILayout.FloatField(_generateMapHeightTex.topLimit);
                }

                _generateMapHeightTex.needLimitBack = EditorGUILayout.Toggle("边界(-z)限制", _generateMapHeightTex.needLimitBack);
                if(_generateMapHeightTex.needLimitBack) {
                    _generateMapHeightTex.backLimit = EditorGUILayout.FloatField(_generateMapHeightTex.backLimit);
                }

                _generateMapHeightTex.needLimitFront = EditorGUILayout.Toggle("边界(+z)限制", _generateMapHeightTex.needLimitFront);
                if(_generateMapHeightTex.needLimitFront) {
                    _generateMapHeightTex.frontLimit = EditorGUILayout.FloatField(_generateMapHeightTex.frontLimit);
                }

                EditorGUILayout.Space();

                _generateMapHeightTex.needHeightOffset = EditorGUILayout.Toggle("是否需要高度偏移", _generateMapHeightTex.needHeightOffset);
                if(_generateMapHeightTex.needHeightOffset) {
                    _generateMapHeightTex.checkHeightOffsetLayer = EditorGUILayout.Toggle("是否检查高度偏移层级", _generateMapHeightTex.checkHeightOffsetLayer);
                    if(_generateMapHeightTex.checkHeightOffsetLayer) {
                        _generateMapHeightTex.heightOffsetLayer = EditorGUILayout.LayerField("需要生成高度的特殊层(楼梯等有问题的):", _generateMapHeightTex.heightOffsetLayer);
                    }

                    _generateMapHeightTex.heightOffset = EditorGUILayout.FloatField("高度偏移", _generateMapHeightTex.heightOffset);
                    _generateMapHeightTex.heightOffsetSinMin = EditorGUILayout.FloatField("高度偏移最小角度值(sin)", _generateMapHeightTex.heightOffsetSinMin);
                    _generateMapHeightTex.heightOffsetSinMax = EditorGUILayout.FloatField("高度偏移最大角度值(sin)", _generateMapHeightTex.heightOffsetSinMax);
                    _generateMapHeightTex.checkLength = EditorGUILayout.IntField("高度偏移检查的距离", _generateMapHeightTex.checkLength);
                    EditorGUILayout.Space();
                }

                _generateMapHeightTex.mapMaterial = (Material) EditorGUILayout.ObjectField("Material:", _generateMapHeightTex.mapMaterial, typeof(Material), true);
                EditorGUILayout.Space();

                _generateMapHeightTex.path = EditorGUILayout.TextField("保存路径(从'Assets/'开始):", _generateMapHeightTex.path);
                EditorGUILayout.Space();
                if(GUILayout.Button("生成")) {
                    _generateMapHeightTex.GenerateMapHeightTexture();
                }
            }

            // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
            //需要在OnInspectorGUI之前修改属性，否则无法修改值
            serializedObject.ApplyModifiedProperties();
        }
    }
}