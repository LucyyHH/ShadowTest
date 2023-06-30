using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ShadowTest.Editor {
    public class GenerateMapHeightTexEditor : EditorWindow {
        private GameObject _mapGo;
        private Material _mapMaterial;
        private int _normalHeightLayer;
        private int _resolutionX;
        private int _resolutionY;
        private string _path;

        private bool _needLimitLeft;
        private float _leftLimit = float.MinValue;
        private bool _needLimitRight;
        private float _rightLimit = float.MaxValue;
        private bool _needLimitBottom;
        private float _bottomLimit = float.MinValue;
        private bool _needLimitTop;
        private float _topLimit = float.MaxValue;
        private bool _needLimitBack;
        private float _backLimit = float.MinValue;
        private bool _needLimitFront;
        private float _frontLimit = float.MaxValue;
    
        private bool _needHeightOffset;
        private bool _checkHeightOffsetLayer;
        private int _heightOffsetLayer;
        private float _heightOffset = 5f;
        private float _heightOffsetSinMin = 0.1f;
        private float _heightOffsetSinMax = 0.5f;
        private int _checkLength = 5;

        private static readonly int HeightTex = Shader.PropertyToID("_HeightTex");
        private static readonly int HeightTexLeft = Shader.PropertyToID("_HeightTexLeft");
        private static readonly int HeightTexLength = Shader.PropertyToID("_HeightTexLength");
        private static readonly int HeightTexBack = Shader.PropertyToID("_HeightTexBack");
        private static readonly int HeightTexWidth = Shader.PropertyToID("_HeightTexWidth");
        private static readonly int HeightTexBottom = Shader.PropertyToID("_HeightTexBottom");
        private static readonly int HeightTexHigh = Shader.PropertyToID("_HeightTexHigh");
        private static readonly int MaxOffset = Shader.PropertyToID("_MaxOffset");

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
            public TriangleType Type;
            public float Left;
            public float Right;
            public float Bottom;
            public float Top;
            public float Back;
            public float Front;
            public float3 Vertices1;
            public float3 Vertices2;
            public float3 Vertices3;
            public float3 Normal;
        }
    
        /// <summary>
        /// 三角形高度信息
        /// </summary>
        private struct TriangleHeightInfo {
            /// <summary>
            /// 高度
            /// </summary>
            public float Height;
            /// <summary>
            /// 是否需要计算offset
            /// </summary>
            public bool Offset;
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
            public TriangleType Type;
            /// <summary>
            /// 世界坐标位置1
            /// </summary>
            public float3 MeshWordPos1;
            /// <summary>
            /// 世界坐标位置2
            /// </summary>
            public float3 MeshWordPos2;
            /// <summary>
            /// 世界坐标位置3
            /// </summary>
            public float3 MeshWordPos3;
        }
    
        private struct MapBoundary {
            public float Left;
            public float Right;
            public float Bottom;
            public float Top;
            public float Back;
            public float Front;
        }

        private static MapBoundary GetDefaultMapBoundary () {
            return new MapBoundary
            {
                Left = float.MaxValue,
                Right = float.MinValue,
                Bottom = float.MaxValue,
                Top = float.MinValue,
                Back = float.MaxValue,
                Front = float.MinValue,
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
                            Type = curType,
                            MeshWordPos1 =
                                LeftTwoDecimal (
                                    meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i]])),
                            MeshWordPos2 =
                                LeftTwoDecimal (
                                    meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i + 1]])),
                            MeshWordPos3 =
                                LeftTwoDecimal (
                                    meshFilter.transform.TransformPoint (sharedMesh.vertices[sharedMesh.triangles[i + 2]])),
                        });
                    }
                }
            }

            var triangleInfoArray = new NativeArray<TriangleInfo> (meshInfoVoList.Length, Allocator);
            var handleMeshVerticesJob = new HandleMeshVerticesJob
            {
                MeshInfoVoArray = meshInfoVoList.AsArray (),
                NeedLimitRight = _needLimitRight,
                NeedLimitLeft = _needLimitLeft,
                NeedLimitFront = _needLimitFront,
                NeedLimitBack = _needLimitBack,
                NeedLimitTop = _needLimitTop,
                NeedLimitBottom = _needLimitBottom,
                TriangleInfoArray = triangleInfoArray,
                RightLimit = _rightLimit,
                LeftLimit = _leftLimit,
                FrontLimit = _frontLimit,
                BackLimit = _backLimit,
                TopLimit = _topLimit,
                BottomLimit = _bottomLimit,
            };
            var meshVerticesHandle = handleMeshVerticesJob.Schedule(meshInfoVoList.Length, 64);
            meshVerticesHandle.Complete();
            meshInfoVoList.Dispose ();
        
            // 处理
            var usedTriangleInfoList = new NativeList<TriangleInfo> (Allocator);
            foreach (var triangleInfo in triangleInfoArray) {
                if (triangleInfo.Type == TriangleType.Unavailable) continue;
            
                CheckBounds (ref mapBoundary.Left, ref mapBoundary.Right, ref mapBoundary.Bottom, ref mapBoundary.Top, ref mapBoundary.Back, ref mapBoundary.Front, triangleInfo.Left,
                    triangleInfo.Right, triangleInfo.Bottom, triangleInfo.Top, triangleInfo.Back,
                    triangleInfo.Front);
                
                usedTriangleInfoList.Add (triangleInfo);
            }
            triangleInfoArray.Dispose ();
 
            // 设置边界限制
            if (_needLimitLeft && mapBoundary.Left < _leftLimit) {
                mapBoundary.Left = _leftLimit;
            }
            if (_needLimitRight && mapBoundary.Right > _rightLimit) {
                mapBoundary.Right = _rightLimit;
            }
            if (_needLimitBottom && mapBoundary.Bottom < _bottomLimit) {
                mapBoundary.Bottom = _bottomLimit;
            }
            if (_needLimitTop && mapBoundary.Top > _topLimit) {
                mapBoundary.Top = _topLimit;
            }
            if (_needLimitBack && mapBoundary.Back < _backLimit) {
                mapBoundary.Back = _backLimit;
            }
            if (_needLimitFront && mapBoundary.Front > _frontLimit) {
                mapBoundary.Front = _frontLimit;
            }

            var length = mapBoundary.Right - mapBoundary.Left;
            var width = mapBoundary.Front - mapBoundary.Back;
            var high = mapBoundary.Top - mapBoundary.Bottom;
            var stepX = 1.0f / _resolutionX * length;
            var stepY = 1.0f / _resolutionY * width;
            var texture2D = new Texture2D (_resolutionX, _resolutionY, TextureFormat.RG16, 0, true);

            var pixelCount = _resolutionX * _resolutionY;
            var curHeightArray = new NativeArray<TriangleHeightInfo> (pixelCount, Allocator);
            var calculateHeightJob = new CalculateHeightJob
            {
                TriangleInfoArray = usedTriangleInfoList.AsArray (),
                ResolutionX = _resolutionX,
                StepX = stepX,
                StepY = stepY,
                Left = mapBoundary.Left,
                Bottom = mapBoundary.Bottom,
                Back = mapBoundary.Back,
                CurHeightArray = curHeightArray,
            };
            var calculateHeightHandle = calculateHeightJob.Schedule(pixelCount, 64);
            calculateHeightHandle.Complete();
            usedTriangleInfoList.Dispose ();
            
            var curOffsetHeightArray = new NativeArray<float> (pixelCount, Allocator);
            var maxOffset = 0f;
            if (_needHeightOffset) {
                var calculateOffsetHeightJob = new CalculateOffsetHeightJob
                {
                    CurHeightArray = curHeightArray,
                    ResolutionX = _resolutionX,
                    ResolutionY = _resolutionY,
                    CheckHeightOffsetLayer = _checkHeightOffsetLayer,
                    CheckLength = _checkLength,
                    HeightOffset = _heightOffset,
                    HeightOffsetSinMin = _heightOffsetSinMin,
                    HeightOffsetSinMax = _heightOffsetSinMax,
                    StepX = stepX,
                    StepY = stepY,
                    Bottom = mapBoundary.Bottom,
                    CurOffsetHeightArray = curOffsetHeightArray,
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
                    texture2D.SetPixel (x, y, new Color (curHeightArray[pixelIndex].Height / high, curOffsetHeightArray[pixelIndex] / maxOffset, 0));
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
                _mapMaterial.SetTexture (HeightTex, AssetDatabase.LoadAssetAtPath<Texture2D> ($"Assets{texturePath}"));
                _mapMaterial.SetFloat (HeightTexLeft, mapBoundary.Left);
                _mapMaterial.SetFloat (HeightTexLength, length);
                _mapMaterial.SetFloat (HeightTexBack, mapBoundary.Back);
                _mapMaterial.SetFloat (HeightTexWidth, width);
                _mapMaterial.SetFloat (HeightTexBottom, mapBoundary.Bottom);
                _mapMaterial.SetFloat (HeightTexHigh, high);
                _mapMaterial.SetFloat (MaxOffset, maxOffset);
            }
        
            Debug.Log ($"left:{mapBoundary.Left}_length:{length}_back:{mapBoundary.Back}_width:{width}_bottom:{mapBoundary.Bottom}_high:{high}_maxOffset:{maxOffset}_costTime:{costTime}");
        }
    
        /// <summary>
        /// 处理mesh顶点信息
        /// </summary>
        [BurstCompile]
        private struct HandleMeshVerticesJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<MeshInfoVo> MeshInfoVoArray;
            [ReadOnly] public bool NeedLimitRight;
            [ReadOnly] public bool NeedLimitLeft;
            [ReadOnly] public bool NeedLimitFront;
            [ReadOnly] public bool NeedLimitBack;
            [ReadOnly] public bool NeedLimitTop;
            [ReadOnly] public bool NeedLimitBottom;
            [ReadOnly] public float RightLimit;
            [ReadOnly] public float LeftLimit;
            [ReadOnly] public float FrontLimit;
            [ReadOnly] public float BackLimit;
            [ReadOnly] public float TopLimit;
            [ReadOnly] public float BottomLimit;

            public NativeArray<TriangleInfo> TriangleInfoArray;
        
            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var meshInfoVo = MeshInfoVoArray[index];
            
                var triangleInfo = new TriangleInfo
                {
                    Type = meshInfoVo.Type,
                    Left = float.MaxValue,
                    Right = float.MinValue,
                    Back = float.MaxValue,
                    Front = float.MinValue,
                    Bottom = float.MaxValue,
                    Top = float.MinValue,
                    Vertices1 = meshInfoVo.MeshWordPos1,
                    Vertices2 = meshInfoVo.MeshWordPos2,
                    Vertices3 = meshInfoVo.MeshWordPos3,
                    Normal = math.normalize (math.cross (meshInfoVo.MeshWordPos2 - meshInfoVo.MeshWordPos1,
                        meshInfoVo.MeshWordPos3 - meshInfoVo.MeshWordPos2))
                };

                CheckBounds (ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos1);
                CheckBounds (ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos2);
                CheckBounds (ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos3);

                if (NeedLimitRight && triangleInfo.Left > RightLimit ||
                    NeedLimitLeft && triangleInfo.Right < LeftLimit ||
                    NeedLimitFront && triangleInfo.Back > FrontLimit ||
                    NeedLimitBack && triangleInfo.Front < BackLimit ||
                    NeedLimitTop && triangleInfo.Bottom > TopLimit ||
                    NeedLimitBottom && triangleInfo.Top < BottomLimit) {
                    triangleInfo.Type = TriangleType.Unavailable;
                }

                TriangleInfoArray[index] = triangleInfo;
            }
        }
    
        /// <summary>
        /// 计算高度
        /// </summary>
        [BurstCompile]
        private struct CalculateHeightJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TriangleInfo> TriangleInfoArray;
        
            [ReadOnly] public int ResolutionX;
        
            [ReadOnly] public float StepX;
            [ReadOnly] public float StepY;
        
            [ReadOnly] public float Left;
            [ReadOnly] public float Back;
            [ReadOnly] public float Bottom;

            public NativeArray<TriangleHeightInfo> CurHeightArray;
        
            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;
                var curPosX = Left + (0.5f + xIndex) * StepX;
                var curPosY = Back + (0.5f + yIndex) * StepY;

                var curH = 0f;
                var offset = false;
                foreach(var triangleInfo in TriangleInfoArray) {
                    if (triangleInfo.Type == TriangleType.Unavailable) {
                        continue;
                    }
                
                    // 是否在包围盒里
                    if (curPosX < triangleInfo.Left || curPosX > triangleInfo.Right ||
                        curPosY < triangleInfo.Back || curPosY > triangleInfo.Front) {
                        continue;
                    }

                    // 检测是否在三角形内
                    var tempPoint = new float3 (curPosX, 0, curPosY);
                    if (!IsInsideTriangle (tempPoint, triangleInfo.Vertices1, triangleInfo.Vertices2, triangleInfo.Vertices3)) {
                        continue;
                    }

                    // 如果在三角形内,则计算高度
                    if (curPosX - triangleInfo.Vertices1.x > 0.001f || curPosY - triangleInfo.Vertices1.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices1.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices1.z;
                        tempPoint.y = triangleInfo.Vertices1.y;
                    } else if (curPosX - triangleInfo.Vertices2.x > 0.001f || curPosY - triangleInfo.Vertices2.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices2.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices2.z;
                        tempPoint.y = triangleInfo.Vertices2.y;
                    } if (curPosX - triangleInfo.Vertices3.x > 0.001f || curPosY - triangleInfo.Vertices3.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices3.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices3.z;
                        tempPoint.y = triangleInfo.Vertices3.y;
                    } 

                    var tempH =
                        -(tempPoint.x * triangleInfo.Normal.x + tempPoint.z * triangleInfo.Normal.z) /
                        triangleInfo.Normal.y + tempPoint.y - Bottom;

                    if (curH < tempH) {
                        curH = tempH;
                    }

                    if (!offset && triangleInfo.Type == TriangleType.Offset) {
                        offset = true;
                    }
                }

                CurHeightArray[index] = new TriangleHeightInfo
                {
                    Height = curH,
                    Offset = offset
                };
            }
        }
    
        /// <summary>
        /// 计算偏移高度
        /// </summary>
        [BurstCompile]
        private struct CalculateOffsetHeightJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<TriangleHeightInfo> CurHeightArray;
        
            [ReadOnly] public int ResolutionX;
            [ReadOnly] public int ResolutionY;

            [ReadOnly] public bool CheckHeightOffsetLayer;
            [ReadOnly] public int CheckLength;
            [ReadOnly] public float HeightOffset;
            [ReadOnly] public float HeightOffsetSinMin;
            [ReadOnly] public float HeightOffsetSinMax;

            [ReadOnly] public float StepX;
            [ReadOnly] public float StepY;
        
            [ReadOnly] public float Bottom;

            public NativeArray<float> CurOffsetHeightArray;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;

                var curSin = 0f;
                var triangleHeightInfo = CurHeightArray[index];
                var curH = triangleHeightInfo.Height;
                if (curH > Bottom) {
                    for (var i = 1; i <= CheckLength; i++) {
                        if (xIndex - i >= 0) {
                            CalculateCurSin (ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index - i], StepX, HeightOffsetSinMin, HeightOffsetSinMax);
                        }
                        if (xIndex + i < ResolutionX) {
                            CalculateCurSin (ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index + i], StepX, HeightOffsetSinMin, HeightOffsetSinMax);
                        }

                        if (yIndex - i >= 0) {
                            CalculateCurSin (ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index - i * ResolutionX], StepY, HeightOffsetSinMin, HeightOffsetSinMax);
                        }

                        if (yIndex + i < ResolutionY) {
                            CalculateCurSin (ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index + i * ResolutionX], StepY, HeightOffsetSinMin, HeightOffsetSinMax);
                        }
                    }

                    var curOffset = math.round (curSin * HeightOffset * 100) / 100f;
                    CurOffsetHeightArray[index] = curOffset;
                } else {
                    CurOffsetHeightArray[index] = 0;
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
            if (curTest.Height <= bottom || (checkHeightOffsetLayer && !curTest.Offset)) return;
        
            var dif = math.abs (curH - curTest.Height);
        
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
}