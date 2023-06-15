using System;
using System.Collections.Generic;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class GenerateMapHeightTexEditor : EditorWindow {
    private GameObject _mapGo = null;
    private Material _mapMaterial = null;
    private int _normalHeightLayer = 0;
    private int _resolutionX = 0;
    private int _resolutionY = 0;
    private string _path = "";

    private bool _needLimitLeft = false;
    private float _leftLimit = float.MinValue;
    private bool _needLimitRight = false;
    private float _rightLimit = float.MaxValue;
    private bool _needLimitBottom = false;
    private float _bottomLimit = float.MinValue;
    private bool _needLimitTop = false;
    private float _topLimit = float.MaxValue;
    private bool _needLimitBack = false;
    private float _backLimit = float.MinValue;
    private bool _needLimitFront = false;
    private float _frontLimit = float.MaxValue;
    
    private bool _needHeightOffset = false;
    private bool _checkHeightOffsetLayer = false;
    private int _heightOffsetLayer = 0;
    private float _heightOffset = 5f;
    private float _heightOffsetSinMin = 0.1f;
    private float _heightOffsetSinMax = 0.5f;
    private int _checkLength = 5;
    
    private const Allocator Allocator = Unity.Collections.Allocator.TempJob;

    /// <summary>
    /// 三角形类型
    /// </summary>
    private enum TriangleType {
        /// <summary>
        /// 不可用
        /// </summary>
        Unavailable,
        /// <summary>
        /// 普通计算高度
        /// </summary>
        Normal,
        /// <summary>
        /// 需要计算offset的
        /// </summary>
        Offset
    }
    /// <summary>
    /// 三角形信息
    /// </summary>
    private struct TriangleInfo {
        public TriangleType type;
        public float left;
        public float right;
        public float bottom;
        public float top;
        public float back;
        public float front;
        public float3 vertices1;
        public float3 vertices2;
        public float3 vertices3;
        public float3 normal;
    }
    
    /// <summary>
    /// 三角形高度信息
    /// </summary>
    private struct TriangleHeightInfo {
        /// <summary>
        /// 高度
        /// </summary>
        public float height;
        /// <summary>
        /// 是否需要计算offset
        /// </summary>
        public bool offset;
    }

    [MenuItem ("Tools/GenerateMapHeightTex")]
    private static void ShowGenerateMapHeightTexWindow () {
        GetWindow (typeof (GenerateMapHeightTexEditor));
    }

    private void OnGUI () {
        EditorGUILayout.Space ();
        _mapGo = (GameObject) EditorGUILayout.ObjectField ("GameObject:", _mapGo, typeof (GameObject), true);
        if (!ReferenceEquals (_mapGo, null)) {
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
            }*/
            
            _normalHeightLayer = EditorGUILayout.LayerField ("需要生成高度的普通层:", _normalHeightLayer);
            //selectLayer = EditorGUILayout.MaskField ("LayerMask:", selectLayer, layerNameList.ToArray ());
            EditorGUILayout.Space ();

            _resolutionX = EditorGUILayout.IntField ("分辨率X:", _resolutionX);
            _resolutionY = EditorGUILayout.IntField ("分辨率Y:", _resolutionY);
            EditorGUILayout.Space ();

            _needLimitLeft = EditorGUILayout.Toggle ("边界(-x)限制", _needLimitLeft);
            if (_needLimitLeft) {
                _leftLimit = EditorGUILayout.FloatField (_leftLimit);
            }

            _needLimitRight = EditorGUILayout.Toggle ("边界(+x)限制", _needLimitRight);
            if (_needLimitRight) {
                _rightLimit = EditorGUILayout.FloatField (_rightLimit);
            }

            _needLimitBottom = EditorGUILayout.Toggle ("边界(-y)限制", _needLimitBottom);
            if (_needLimitBottom) {
                _bottomLimit = EditorGUILayout.FloatField (_bottomLimit);
            }

            _needLimitTop = EditorGUILayout.Toggle ("边界(+y)限制", _needLimitTop);
            if (_needLimitTop) {
                _topLimit = EditorGUILayout.FloatField (_topLimit);
            }

            _needLimitBack = EditorGUILayout.Toggle ("边界(-z)限制", _needLimitBack);
            if (_needLimitBack) {
                _backLimit = EditorGUILayout.FloatField (_backLimit);
            }

            _needLimitFront = EditorGUILayout.Toggle ("边界(+z)限制", _needLimitFront);
            if (_needLimitFront) {
                _frontLimit = EditorGUILayout.FloatField (_frontLimit);
            }

            EditorGUILayout.Space ();

            _needHeightOffset = EditorGUILayout.Toggle ("是否需要高度偏移", _needHeightOffset);
            if (_needHeightOffset) {
                _checkHeightOffsetLayer = EditorGUILayout.Toggle ("是否检查高度偏移层级", _checkHeightOffsetLayer);
                if (_checkHeightOffsetLayer) {
                    _heightOffsetLayer = EditorGUILayout.LayerField ("需要生成高度的特殊层(楼梯等有问题的):", _heightOffsetLayer);
                }
                _heightOffset = EditorGUILayout.FloatField ("高度偏移", _heightOffset);
                _heightOffsetSinMin = EditorGUILayout.FloatField ("高度偏移最小角度值(sin)", _heightOffsetSinMin);
                _heightOffsetSinMax = EditorGUILayout.FloatField ("高度偏移最大角度值(sin)", _heightOffsetSinMax);
                _checkLength = EditorGUILayout.IntField ("高度偏移检查的距离", _checkLength);
                EditorGUILayout.Space ();
            }

            _mapMaterial = (Material) EditorGUILayout.ObjectField ("Material:", _mapMaterial, typeof (Material), true);
            EditorGUILayout.Space ();

            _path = EditorGUILayout.TextField ("保存路径(从'Assets/'开始):", _path);
            EditorGUILayout.Space ();
            if (GUILayout.Button ("生成")) {
                GenerateMapHeightTex ();
            }
        }
    }

    private void GenerateMapHeightTex () {
        if (ReferenceEquals (_mapGo, null)) {
            Debug.LogError ("mapGO为空！！！");
            return;
        }

        if (ReferenceEquals (_mapMaterial, null)) {
            /*_mapMaterial = new Material (Shader.Find ("Custom/LC/ProjShadowURP"));
            string path = Application.dataPath + $"/{_path}/{_mapGo.name}.mat";
            // 为资源创建一个新的唯一路径。
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            // 通过在导入资源（例如，FBX 文件）中提取外部资源，在对象（例如，材质）中创建此资源。
            string value = AssetDatabase.ExtractAsset(item, path);
            // 成功提取( 如果 Unity 已成功提取资源，则返回一个空字符串)
            if (string.IsNullOrEmpty(value))
            {
                AssetDatabase.WriteImportSettingsIfDirty(assetPath);
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                isCreate = true;
            }*/
        }

        CalculateWithJobSystem ();
    }

    #region JobSystem
    
    private struct MeshInfoVo {
        public TriangleType type;
        /// <summary>
        /// 世界坐标位置1
        /// </summary>
        public float3 meshWordPos1;
        /// <summary>
        /// 世界坐标位置2
        /// </summary>
        public float3 meshWordPos2;
        /// <summary>
        /// 世界坐标位置3
        /// </summary>
        public float3 meshWordPos3;
    }
    
    private struct MapBoundary {
        public float left;
        public float right;
        public float bottom;
        public float top;
        public float back;
        public float front;
    }

    private static MapBoundary GetDefaultMapBoundary () {
        return new MapBoundary
        {
            left = float.MaxValue,
            right = float.MinValue,
            bottom = float.MaxValue,
            top = float.MinValue,
            back = float.MaxValue,
            front = float.MinValue,
        };
    }
    
     /// <summary>
    /// 使用JobSystem进行计算
    /// </summary>
    private void CalculateWithJobSystem () { 
        var curTime = DateTime.Now;

        var mapBoundary = GetDefaultMapBoundary();

        var meshFilters = _mapGo.GetComponentsInChildren<MeshFilter> ();
        
        var meshInfoVoList = new NativeList<MeshInfoVo> (Allocator);
        foreach (var meshFilter in meshFilters) {
            var gameObject = meshFilter.gameObject;
            var normalLayer = gameObject.layer == _normalHeightLayer;
            var offsetLayer = _checkHeightOffsetLayer && gameObject.layer == _heightOffsetLayer;
            if (normalLayer || offsetLayer) {
                var sharedMesh = meshFilter.sharedMesh;

                var curType = normalLayer ? TriangleType.Normal : TriangleType.Offset;
                
                for (var i = 0; i < sharedMesh.triangles.Length; i += 3) {
                    meshInfoVoList.Add (new MeshInfoVo
                    {
                        type = curType,
                        meshWordPos1 =
                            LeftTwoDecimal (
                                meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i]])),
                        meshWordPos2 =
                            LeftTwoDecimal (
                                meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i + 1]])),
                        meshWordPos3 =
                            LeftTwoDecimal (
                                meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i + 2]])),
                    });
                }
            }
        }

        var triangleInfoArray = new NativeArray<TriangleInfo> (meshInfoVoList.Length, Allocator);
        var handleMeshVerticesJob = new HandleMeshVerticesJob
        {
            meshInfoVoArray = meshInfoVoList.AsArray (),
            needLimitRight = _needLimitRight,
            needLimitLeft = _needLimitLeft,
            needLimitFront = _needLimitFront,
            needLimitBack = _needLimitBack,
            needLimitTop = _needLimitTop,
            needLimitBottom = _needLimitBottom,
            triangleInfoArray = triangleInfoArray,
            rightLimit = _rightLimit,
            leftLimit = _leftLimit,
            frontLimit = _frontLimit,
            backLimit = _backLimit,
            topLimit = _topLimit,
            bottomLimit = _bottomLimit,
        };
        var meshVerticesHandle = handleMeshVerticesJob.Schedule(meshInfoVoList.Length, 64);
        meshVerticesHandle.Complete();
        meshInfoVoList.Dispose ();
        
        // 处理
        var usedTriangleInfoList = new NativeList<TriangleInfo> (Allocator);
        foreach (var triangleInfo in triangleInfoArray) {
            if (triangleInfo.type == TriangleType.Unavailable) continue;
            
            CheckBounds (ref mapBoundary.left, ref mapBoundary.right, ref mapBoundary.bottom, ref mapBoundary.top, ref mapBoundary.back, ref mapBoundary.front, triangleInfo.left,
                triangleInfo.right, triangleInfo.bottom, triangleInfo.top, triangleInfo.back,
                triangleInfo.front);
                
            usedTriangleInfoList.Add (triangleInfo);
        }
        triangleInfoArray.Dispose ();
 
        // 设置边界限制
        if (_needLimitLeft && mapBoundary.left < _leftLimit) {
            mapBoundary.left = _leftLimit;
        }
        if (_needLimitRight && mapBoundary.right > _rightLimit) {
            mapBoundary.right = _rightLimit;
        }
        if (_needLimitBottom && mapBoundary.bottom < _bottomLimit) {
            mapBoundary.bottom = _bottomLimit;
        }
        if (_needLimitTop && mapBoundary.top > _topLimit) {
            mapBoundary.top = _topLimit;
        }
        if (_needLimitBack && mapBoundary.back < _backLimit) {
            mapBoundary.back = _backLimit;
        }
        if (_needLimitFront && mapBoundary.front > _frontLimit) {
            mapBoundary.front = _frontLimit;
        }

        var length = mapBoundary.right - mapBoundary.left;
        var width = mapBoundary.front - mapBoundary.back;
        var high = mapBoundary.top - mapBoundary.bottom;
        var stepX = 1.0f / _resolutionX * length;
        var stepY = 1.0f / _resolutionY * width;
        var texture2D = new Texture2D (_resolutionX, _resolutionY, TextureFormat.RG16, 0, true);

        var pixelCount = _resolutionX * _resolutionY;
        var curHeightArray = new NativeArray<TriangleHeightInfo> (pixelCount, Allocator);
        var calculateHeightJob = new CalculateHeightJob
        {
            triangleInfoArray = usedTriangleInfoList.AsArray (),
            resolutionX = _resolutionX,
            stepX = stepX,
            stepY = stepY,
            left = mapBoundary.left,
            bottom = mapBoundary.bottom,
            back = mapBoundary.back,
            curHeightArray = curHeightArray,
        };
        var calculateHeightHandle = calculateHeightJob.Schedule(pixelCount, 64);
        calculateHeightHandle.Complete();
        usedTriangleInfoList.Dispose ();

        var curOffsetHeightArray = new NativeArray<float> (pixelCount, Allocator);
        var maxOffset = 0f;
        if (_needHeightOffset) {
            var calculateOffsetHeightJob = new CalculateOffsetHeightJob
            {
                curHeightArray = curHeightArray,
                resolutionX = _resolutionX,
                resolutionY = _resolutionY,
                checkHeightOffsetLayer = _checkHeightOffsetLayer,
                checkLength = _checkLength,
                heightOffset = _heightOffset,
                heightOffsetSinMin = _heightOffsetSinMin,
                heightOffsetSinMax = _heightOffsetSinMax,
                stepX = stepX,
                stepY = stepY,
                bottom = mapBoundary.bottom,
                curOffsetHeightArray = curOffsetHeightArray,
            };
            var calculateOffsetHeightHandle = calculateOffsetHeightJob.Schedule(pixelCount, 64);
            calculateOffsetHeightHandle.Complete();
        
            foreach (var offset in curOffsetHeightArray) {
                if (maxOffset < offset) {
                    maxOffset = offset;
                }
            }
        }
        
        var pixelIndex = 0;
        for (var y = 0; y < _resolutionY; y++) {
            for (var x = 0; x < _resolutionX; x++) {
                texture2D.SetPixel (x, y, new Color (curHeightArray[pixelIndex].height / high, curOffsetHeightArray[pixelIndex] / maxOffset, 0));
                pixelIndex++;
            }
        }

        curHeightArray.Dispose ();
        curOffsetHeightArray.Dispose ();
        
        var costTime = DateTime.Now.Subtract (curTime).TotalMilliseconds / 1000d;

        var texturePath = $"/{_path}/{_mapGo.name}.png";
        
        texture2D.Apply ();
        //保存图片
        var dataBytes = texture2D.EncodeToPNG ();
        var savePath = $"{Application.dataPath}{texturePath}";
        var fileStream = File.Open (savePath, FileMode.OpenOrCreate);
        fileStream.Write (dataBytes, 0, dataBytes.Length);
        fileStream.Close ();
        AssetDatabase.SaveAssets ();
        AssetDatabase.Refresh ();
        
        if (!ReferenceEquals (_mapMaterial, null)) {
            _mapMaterial.SetTexture ("_HeightTex", AssetDatabase.LoadAssetAtPath<Texture2D> ($"Assets{texturePath}"));
            _mapMaterial.SetFloat ("_HeightTexLeft", mapBoundary.left);
            _mapMaterial.SetFloat ("_HeightTexLength", length);
            _mapMaterial.SetFloat ("_HeightTexBack", mapBoundary.back);
            _mapMaterial.SetFloat ("_HeightTexWidth", width);
            _mapMaterial.SetFloat ("_HeightTexBottom", mapBoundary.bottom);
            _mapMaterial.SetFloat ("_HeightTexHigh", high);
            _mapMaterial.SetFloat ("_MaxOffset", maxOffset);
        }
        
        Debug.Log ($"left:{mapBoundary.left}_length:{length}_back:{mapBoundary.back}_width:{width}_bottom:{mapBoundary.bottom}_high:{high}_maxOffset:{maxOffset}_costTime:{costTime}");
    }
    
    /// <summary>
    /// 处理mesh顶点信息
    /// </summary>
    [BurstCompile]
    private struct HandleMeshVerticesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<MeshInfoVo> meshInfoVoArray;
        [ReadOnly] public bool needLimitRight;
        [ReadOnly] public bool needLimitLeft;
        [ReadOnly] public bool needLimitFront;
        [ReadOnly] public bool needLimitBack;
        [ReadOnly] public bool needLimitTop;
        [ReadOnly] public bool needLimitBottom;
        [ReadOnly] public float rightLimit;
        [ReadOnly] public float leftLimit;
        [ReadOnly] public float frontLimit;
        [ReadOnly] public float backLimit;
        [ReadOnly] public float topLimit;
        [ReadOnly] public float bottomLimit;

        public NativeArray<TriangleInfo> triangleInfoArray;
        
        // Each Execute call processes only an individual index.
        public void Execute(int index) {
            var meshInfoVo = meshInfoVoArray[index];
            
            var triangleInfo = new TriangleInfo
            {
                type = meshInfoVo.type,
                left = float.MaxValue,
                right = float.MinValue,
                back = float.MaxValue,
                front = float.MinValue,
                bottom = float.MaxValue,
                top = float.MinValue,
                vertices1 = meshInfoVo.meshWordPos1,
                vertices2 = meshInfoVo.meshWordPos2,
                vertices3 = meshInfoVo.meshWordPos3,
                normal = math.normalize (math.cross (meshInfoVo.meshWordPos2 - meshInfoVo.meshWordPos1,
                    meshInfoVo.meshWordPos3 - meshInfoVo.meshWordPos2))
            };

            CheckBounds (ref triangleInfo.left, ref triangleInfo.right, ref triangleInfo.bottom,
                ref triangleInfo.top, ref triangleInfo.back, ref triangleInfo.front, meshInfoVo.meshWordPos1);
            CheckBounds (ref triangleInfo.left, ref triangleInfo.right, ref triangleInfo.bottom,
                ref triangleInfo.top, ref triangleInfo.back, ref triangleInfo.front, meshInfoVo.meshWordPos2);
            CheckBounds (ref triangleInfo.left, ref triangleInfo.right, ref triangleInfo.bottom,
                ref triangleInfo.top, ref triangleInfo.back, ref triangleInfo.front, meshInfoVo.meshWordPos3);

            if (needLimitRight && triangleInfo.left > rightLimit ||
                  needLimitLeft && triangleInfo.right < leftLimit ||
                  needLimitFront && triangleInfo.back > frontLimit ||
                  needLimitBack && triangleInfo.front < backLimit ||
                  needLimitTop && triangleInfo.bottom > topLimit ||
                  needLimitBottom && triangleInfo.top < bottomLimit) {
                triangleInfo.type = TriangleType.Unavailable;
            }

            triangleInfoArray[index] = triangleInfo;
        }
    }
    
    /// <summary>
    /// 计算高度
    /// </summary>
    [BurstCompile]
    private struct CalculateHeightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TriangleInfo> triangleInfoArray;
        
        [ReadOnly] public int resolutionX;
        
        [ReadOnly] public float stepX;
        [ReadOnly] public float stepY;
        
        [ReadOnly] public float left;
        [ReadOnly] public float back;
        [ReadOnly] public float bottom;

        public NativeArray<TriangleHeightInfo> curHeightArray;
        
        // Each Execute call processes only an individual index.
        public void Execute(int index) {
            var xIndex = index % resolutionX;
            var yIndex = index / resolutionX;
            var curPosX = left + (0.5f + xIndex) * stepX;
            var curPosY = back + (0.5f + yIndex) * stepY;

            var curH = 0f;
            var offset = false;
            for (var i = 0; i < triangleInfoArray.Length; i++) {
                var triangleInfo = triangleInfoArray[i];
                
                if (triangleInfo.type == TriangleType.Unavailable) {
                    continue;
                }
                
                // 是否在包围盒里
                if (curPosX < triangleInfo.left || curPosX > triangleInfo.right ||
                    curPosY < triangleInfo.back || curPosY > triangleInfo.front) {
                    continue;
                }

                // 检测是否在三角形内
                var tempPoint = new float3 (curPosX, 0, curPosY);
                if (!IsInsideTriangle (tempPoint, triangleInfo.vertices1, triangleInfo.vertices2, triangleInfo.vertices3)) {
                    continue;
                }

                // 如果在三角形内,则计算高度
                if (curPosX - triangleInfo.vertices1.x > 0.001f || curPosY - triangleInfo.vertices1.z > 0.001f) {
                    tempPoint.x = curPosX - triangleInfo.vertices1.x;
                    tempPoint.z = curPosY - triangleInfo.vertices1.z;
                    tempPoint.y = triangleInfo.vertices1.y;
                } else if (curPosX - triangleInfo.vertices2.x > 0.001f || curPosY - triangleInfo.vertices2.z > 0.001f) {
                    tempPoint.x = curPosX - triangleInfo.vertices2.x;
                    tempPoint.z = curPosY - triangleInfo.vertices2.z;
                    tempPoint.y = triangleInfo.vertices2.y;
                } if (curPosX - triangleInfo.vertices3.x > 0.001f || curPosY - triangleInfo.vertices3.z > 0.001f) {
                    tempPoint.x = curPosX - triangleInfo.vertices3.x;
                    tempPoint.z = curPosY - triangleInfo.vertices3.z;
                    tempPoint.y = triangleInfo.vertices3.y;
                } 

                var tempH =
                    -(tempPoint.x * triangleInfo.normal.x + tempPoint.z * triangleInfo.normal.z) /
                    triangleInfo.normal.y + tempPoint.y - bottom;

                if (curH < tempH) {
                    curH = tempH;
                }

                if (!offset && triangleInfo.type == TriangleType.Offset) {
                    offset = true;
                }
            }

            curHeightArray[index] = new TriangleHeightInfo
            {
                height = curH,
                offset = offset
            };
        }
    }
    
    /// <summary>
    /// 计算偏移高度
    /// </summary>
    [BurstCompile]
    private struct CalculateOffsetHeightJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<TriangleHeightInfo> curHeightArray;
        
        [ReadOnly] public int resolutionX;
        [ReadOnly] public int resolutionY;

        [ReadOnly] public bool checkHeightOffsetLayer;
        [ReadOnly] public int checkLength;
        [ReadOnly] public float heightOffset;
        [ReadOnly] public float heightOffsetSinMin;
        [ReadOnly] public float heightOffsetSinMax;

        [ReadOnly] public float stepX;
        [ReadOnly] public float stepY;
        
        [ReadOnly] public float bottom;

        public NativeArray<float> curOffsetHeightArray;

        // Each Execute call processes only an individual index.
        public void Execute(int index) {
            var xIndex = index % resolutionX;
            var yIndex = index / resolutionX;

            var curSin = 0f;
            var triangleHeightInfo = curHeightArray[index];
            var curH = triangleHeightInfo.height;
            if (curH > bottom) {
                for (var i = 1; i <= checkLength; i++) {
                    if (xIndex - i >= 0) {
                        CalculateCurSin (ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index - i], stepX, heightOffsetSinMin, heightOffsetSinMax);
                    }
                    if (xIndex + i < resolutionX) {
                        CalculateCurSin (ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index + i], stepX, heightOffsetSinMin, heightOffsetSinMax);
                    }

                    if (yIndex - i >= 0) {
                        CalculateCurSin (ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index - i * resolutionX], stepY, heightOffsetSinMin, heightOffsetSinMax);
                    }

                    if (yIndex + i < resolutionY) {
                        CalculateCurSin (ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index + i * resolutionX], stepY, heightOffsetSinMin, heightOffsetSinMax);
                    }
                }

                var curOffset = math.round (curSin * heightOffset * 100) / 100f;
                curOffsetHeightArray[index] = curOffset;
            } else {
                curOffsetHeightArray[index] = 0;
            }
        }
    }

    /// <summary>
    /// 保留两位小数
    /// </summary>
    /// <param name="worldPos"></param>
    /// <returns></returns>
    private static float3 LeftTwoDecimal (float3 worldPos) {
        worldPos.x = math.ceil (worldPos.x * 100) / 100f;
        worldPos.y = math.ceil (worldPos.y * 100) / 100f;
        worldPos.z = math.ceil (worldPos.z * 100) / 100f;
        return worldPos;
    }
    
    private static void CalculateCurSin (ref float curSin, float bottom, float curH, bool checkHeightOffsetLayer, TriangleHeightInfo curTest, float step, float heightOffsetSinMin, float heightOffsetSinMax) {
        if (curTest.height <= bottom || (checkHeightOffsetLayer && !curTest.offset)) return;
        
        var dif = math.abs (curH - curTest.height);
        
        if (dif <= 0) return;
        
        var tempSin = dif / math.sqrt (dif * dif + step * step);
        if (tempSin > curSin && tempSin > heightOffsetSinMin && tempSin < heightOffsetSinMax) {
            curSin = tempSin;
        }
    }

    #endregion
    
    private static void CheckBounds (ref float left, ref float right, ref float bottom, ref float top, ref float back,
        ref float front, float3 pos) {
        CheckBounds (ref left, ref right, ref bottom, ref top, ref back, ref front, pos.x, pos.x, pos.y, pos.y, pos.z,
            pos.z);
    }

    private static void CheckBounds (ref float left, ref float right, ref float bottom, ref float top, ref float back,
        ref float front, float checkLeft, float checkRight, float checkBottom, float checkTop, float checkBack,
        float checkFront) {
        if (left > checkLeft) {
            left = checkLeft;
        }

        if (right < checkRight) {
            right = checkRight;
        }

        if (bottom > checkBottom) {
            bottom = checkBottom;
        }

        if (top < checkTop) {
            top = checkTop;
        }

        if (back > checkBack) {
            back = checkBack;
        }

        if (front < checkFront) {
            front = checkFront;
        }
    }

    /// <summary>
    /// 是否在三角形内
    /// </summary>
    private static bool IsInsideTriangle (float3 point, float3 vertices1, float3 vertices2, float3 vertices3) {
        var pa = new float3(vertices1.x, 0, vertices1.z) - point;
        var pb = new float3(vertices2.x, 0, vertices2.z) - point;
        var pc = new float3(vertices3.x, 0, vertices3.z) - point;

        var pab = math.cross (pa, pb);
        var pbc = math.cross (pb, pc);
        var pca = math.cross (pc, pa);

        var d1 = math.dot (pab, pbc);
        var d2 = math.dot (pab, pca);
        var d3 = math.dot (pbc, pca);

        return d1 >= 0 && d2 >= 0 && d3 >= 0;
    }

    private void OnEnable () {
        _mapGo =
            EditorUtility.InstanceIDToObject (EditorPrefs.GetInt ("GenerateMapHeightTexWindow_mapGO", 0)) as GameObject;
        _mapMaterial =
            EditorUtility.InstanceIDToObject (EditorPrefs.GetInt ("GenerateMapHeightTexWindow_mapMaterial", 0)) as
                Material;
        _normalHeightLayer = EditorPrefs.GetInt ("GenerateMapHeightTexWindow_normalHeightLayer", 0);
        _resolutionX = EditorPrefs.GetInt ("GenerateMapHeightTexWindow_resolutionX", 0);
        _resolutionY = EditorPrefs.GetInt ("GenerateMapHeightTexWindow_resolutionY", 0);
        _path = EditorPrefs.GetString ("GenerateMapHeightTexWindow_path", _path);

        _needLimitLeft = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitLeft", false);
        _needLimitRight = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitRight", false);
        _needLimitBottom = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitBottom", false);
        _needLimitTop = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitTop", false);
        _needLimitBack = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitBack", false);
        _needLimitFront = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needLimitFront", false);

        _leftLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_leftLimit", float.MinValue);
        _rightLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_rightLimit", float.MaxValue);
        _bottomLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_bottomLimit", float.MinValue);
        _topLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_topLimit", float.MaxValue);
        _backLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_backLimit", float.MinValue);
        _frontLimit = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_frontLimit", float.MaxValue);

        _needHeightOffset = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_needHeightOffset", false);
        _checkHeightOffsetLayer = EditorPrefs.GetBool ("GenerateMapHeightTexWindow_checkHeightOffsetLayer", false);
        _heightOffsetLayer = EditorPrefs.GetInt ("GenerateMapHeightTexWindow_heightOffsetLayer", 0);
        _heightOffset = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_heightOffset", 5.0f);
        _heightOffsetSinMin = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_heightOffsetSinMin", 0.1f);
        _heightOffsetSinMax = EditorPrefs.GetFloat ("GenerateMapHeightTexWindow_heightOffsetSinMax", 0.5f);
        _checkLength = EditorPrefs.GetInt ("GenerateMapHeightTexWindow_checkLength", 1);
    }

    private void OnDisable () {
        if (!ReferenceEquals (_mapGo, null)) {
            EditorPrefs.SetInt ("GenerateMapHeightTexWindow_mapGO", _mapGo.GetInstanceID ());
        }

        if (!ReferenceEquals (_mapMaterial, null)) {
            EditorPrefs.SetInt ("GenerateMapHeightTexWindow_mapMaterial", _mapMaterial.GetInstanceID ());
        }

        EditorPrefs.SetInt ("GenerateMapHeightTexWindow_normalHeightLayer", _normalHeightLayer);
        EditorPrefs.SetInt ("GenerateMapHeightTexWindow_resolutionX", _resolutionX);
        EditorPrefs.SetInt ("GenerateMapHeightTexWindow_resolutionY", _resolutionY);
        EditorPrefs.SetString ("GenerateMapHeightTexWindow_path", _path);

        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitLeft", _needLimitLeft);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitRight", _needLimitRight);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitBottom", _needLimitBottom);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitTop", _needLimitTop);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitBack", _needLimitBack);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needLimitFront", _needLimitFront);

        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_leftLimit", _leftLimit);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_rightLimit", _rightLimit);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_bottomLimit", _bottomLimit);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_topLimit", _topLimit);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_backLimit", _backLimit);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_frontLimit", _frontLimit);

        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_needHeightOffset", _needHeightOffset);
        EditorPrefs.SetBool ("GenerateMapHeightTexWindow_checkHeightOffsetLayer", _checkHeightOffsetLayer);
        EditorPrefs.SetInt ("GenerateMapHeightTexWindow_heightOffsetLayer", _heightOffsetLayer);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_heightOffset", _heightOffset);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_heightOffsetSinMin", _heightOffsetSinMin);
        EditorPrefs.SetFloat ("GenerateMapHeightTexWindow_heightOffsetSinMax", _heightOffsetSinMax);
        EditorPrefs.SetInt ("GenerateMapHeightTexWindow_checkLength", _checkLength);
    }
}