//指定类型

using ShadowTest.Custom;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GenerateMapHeightTex))]
public class GenerateMapHeightTexInspector : Editor
{
    private GenerateMapHeightTex generateMapHeightTex;
 
    void OnEnable()
    {
        generateMapHeightTex = target as GenerateMapHeightTex;
    }
 
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        // Update the serializedProperty - always do this in the beginning of OnInspectorGUI.
        serializedObject.Update();
        
        /*EditorGUILayout.Space();
        generateMapHeightTex.mapGo = (GameObject) EditorGUILayout.ObjectField("GameObject:", generateMapHeightTex.mapGo, typeof(GameObject), true);
        if(!ReferenceEquals(generateMapHeightTex.mapGo, null)) {
            /*string[] layerNameArray = new string[32];
            for (int i = 0; i < 32; i++) {
                layerNameArray[i] = LayerMask.LayerToName (i);
            }
            List<string> layerNameList = new List<string> ();
            bool checkFirst = true;
            for (int i = 0; i < layerNameArray.Length; i++) {
                bool curIsNullOrEmpty = string.IsNullOrEmpty (layerNameArray[i]);
                if (checkFirst) {
                    if (curIsNullOrEmpty) {
                        continue;
                    }
                    layerNameList.Add (layerNameArray[i]);
                    checkFirst = false;
                } else {
                    if(!curIsNullOrEmpty || (i + 1 < layerNameArray.Length && !string.IsNullOrEmpty (layerNameArray[i + 1]))) {
                        layerNameList.Add (layerNameArray[i]);
                    }
                }
            }#1#

            generateMapHeightTex.normalHeightLayer = EditorGUILayout.LayerField("需要生成高度的普通层:", generateMapHeightTex.normalHeightLayer);
            //selectLayer = EditorGUILayout.MaskField ("LayerMask:", selectLayer, layerNameList.ToArray ());
            EditorGUILayout.Space();

            generateMapHeightTex.resolutionX = EditorGUILayout.IntField("分辨率X:", generateMapHeightTex.resolutionX);
            generateMapHeightTex.resolutionY = EditorGUILayout.IntField("分辨率Y:", generateMapHeightTex.resolutionY);
            EditorGUILayout.Space();

            generateMapHeightTex.needLimitLeft = EditorGUILayout.Toggle("边界(-x)限制", generateMapHeightTex.needLimitLeft);
            if(generateMapHeightTex.needLimitLeft) {
                generateMapHeightTex.leftLimit = EditorGUILayout.FloatField(generateMapHeightTex.leftLimit);
            }

            generateMapHeightTex.needLimitRight = EditorGUILayout.Toggle("边界(+x)限制", generateMapHeightTex.needLimitRight);
            if(generateMapHeightTex.needLimitRight) {
                generateMapHeightTex.rightLimit = EditorGUILayout.FloatField(generateMapHeightTex.rightLimit);
            }

            generateMapHeightTex.needLimitBottom = EditorGUILayout.Toggle("边界(-y)限制", generateMapHeightTex.needLimitBottom);
            if(generateMapHeightTex.needLimitBottom) {
                generateMapHeightTex.bottomLimit = EditorGUILayout.FloatField(generateMapHeightTex.bottomLimit);
            }

            generateMapHeightTex.needLimitTop = EditorGUILayout.Toggle("边界(+y)限制", generateMapHeightTex.needLimitTop);
            if(generateMapHeightTex.needLimitTop) {
                generateMapHeightTex.topLimit = EditorGUILayout.FloatField(generateMapHeightTex.topLimit);
            }

            generateMapHeightTex.needLimitBack = EditorGUILayout.Toggle("边界(-z)限制", generateMapHeightTex.needLimitBack);
            if(generateMapHeightTex.needLimitBack) {
                generateMapHeightTex.backLimit = EditorGUILayout.FloatField(generateMapHeightTex.backLimit);
            }

            generateMapHeightTex.needLimitFront = EditorGUILayout.Toggle("边界(+z)限制", generateMapHeightTex.needLimitFront);
            if(generateMapHeightTex.needLimitFront) {
                generateMapHeightTex.frontLimit = EditorGUILayout.FloatField(generateMapHeightTex.frontLimit);
            }

            EditorGUILayout.Space();

            generateMapHeightTex.needHeightOffset = EditorGUILayout.Toggle("是否需要高度偏移", generateMapHeightTex.needHeightOffset);
            if(generateMapHeightTex.needHeightOffset) {
                generateMapHeightTex.checkHeightOffsetLayer = EditorGUILayout.Toggle("是否检查高度偏移层级", generateMapHeightTex.checkHeightOffsetLayer);
                if(generateMapHeightTex.checkHeightOffsetLayer) {
                    generateMapHeightTex.heightOffsetLayer = EditorGUILayout.LayerField("需要生成高度的特殊层(楼梯等有问题的):", generateMapHeightTex.heightOffsetLayer);
                }

                generateMapHeightTex.heightOffset = EditorGUILayout.FloatField("高度偏移", generateMapHeightTex.heightOffset);
                generateMapHeightTex.heightOffsetSinMin = EditorGUILayout.FloatField("高度偏移最小角度值(sin)", generateMapHeightTex.heightOffsetSinMin);
                generateMapHeightTex.heightOffsetSinMax = EditorGUILayout.FloatField("高度偏移最大角度值(sin)", generateMapHeightTex.heightOffsetSinMax);
                generateMapHeightTex.checkLength = EditorGUILayout.IntField("高度偏移检查的距离", generateMapHeightTex.checkLength);
                EditorGUILayout.Space();
            }

            generateMapHeightTex.mapMaterial = (Material) EditorGUILayout.ObjectField("Material:", generateMapHeightTex.mapMaterial, typeof(Material), true);
            EditorGUILayout.Space();

            generateMapHeightTex.path = EditorGUILayout.TextField("保存路径(从'Assets/'开始):", generateMapHeightTex.path);
            EditorGUILayout.Space();
            if(GUILayout.Button("生成")) {
                generateMapHeightTex.GenerateMapHeightTexture();
            }
        }*/
        
        if(GUILayout.Button("生成")) {
            generateMapHeightTex.GenerateMapHeightTexture();
        }

        // Apply changes to the serializedProperty - always do this in the end of OnInspectorGUI.
        //需要在OnInspectorGUI之前修改属性，否则无法修改值
        serializedObject.ApplyModifiedProperties();
    }
}