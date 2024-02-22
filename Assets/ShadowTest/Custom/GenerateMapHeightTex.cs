using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ShadowTest.Custom {
    [CreateAssetMenu(fileName = "GenerateMapHeightTex", menuName = "ScriptableObject/GenerateMapHeightTex", order = 0)]
    public class GenerateMapHeightTex : ScriptableObject {
        [Header("GameObject")]
        public GameObject mapGo;

        [Header("Material")]
        public Material mapMaterial;

        [Header("需要生成高度的普通层")]
        public LayerMask normalHeightLayer;
        [Header("分辨率X")]
        public int resolutionX;
        [Header("分辨率Y")]
        public int resolutionY;
        [Header("保存路径(从'Assets/'开始)")]
        public string path;

        [Header("高度的分割线(百分比),高度以下存到r通道，以上存到g通道"), Range(0, 1)]
        public float highCuttingLine;
        
        [Header("固定影子(灯光)方向")]
        public bool fixedShadowDir;
        public float3 shadowDir = Vector3.down;

        [Header("边界(-x)限制")]
        public bool needLimitLeft;
        public float leftLimit = float.MinValue;
        [Header("边界(+x)限制")]
        public bool needLimitRight;
        public float rightLimit = float.MaxValue;
        [Header("边界(-y)限制")]
        public bool needLimitBottom;
        public float bottomLimit = float.MinValue;
        [Header("边界(+y)限制")]
        public bool needLimitTop;
        public float topLimit = float.MaxValue;
        [Header("边界(-z)限制")]
        public bool needLimitBack;
        public float backLimit = float.MinValue;
        [Header("边界(+z)限制")]
        public bool needLimitFront;
        public float frontLimit = float.MaxValue;

        [Header("是否需要高度偏移")]
        public bool needHeightOffset;
        [Header("高度偏移")]
        public float heightOffset = 0.1f;
        [Header("高度偏移最小角度值(sin)")]
        public float heightOffsetSinMin = 0.05f;
        [Header("高度偏移最大角度值(sin)")]
        public float heightOffsetSinMax = 0.6f;
        [Header("高度偏移检查的距离")]
        public int checkLength = 2;
        [Header("是否检查高度偏移层级")]
        public bool checkHeightOffsetLayer;
        [Header("需要生成高度的特殊层(楼梯等有问题的)")]
        public LayerMask heightOffsetLayer;

        private static readonly int ShaderHeightTex = Shader.PropertyToID("_HeightTex");
        private static readonly int ShaderHeightTexLeft = Shader.PropertyToID("_HeightTexLeft");
        private static readonly int ShaderHeightTexLength = Shader.PropertyToID("_HeightTexLength");
        private static readonly int ShaderHeightTexBack = Shader.PropertyToID("_HeightTexBack");
        private static readonly int ShaderHeightTexWidth = Shader.PropertyToID("_HeightTexWidth");
        private static readonly int ShaderHeightTexBottom = Shader.PropertyToID("_HeightTexBottom");
        private static readonly int ShaderHeightTexHigh = Shader.PropertyToID("_HeightTexHigh");
        private static readonly int ShaderMaxHeight1 = Shader.PropertyToID("_MaxHeight1");
        private static readonly int ShaderMaxHeight2 = Shader.PropertyToID("_MaxHeight2");
        private static readonly int ShaderMaxOffset = Shader.PropertyToID("_MaxOffset");
        private static readonly int ShadowDir = Shader.PropertyToID("_ShadowDir");

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

        public void GenerateMapHeightTexture() {
            if(ReferenceEquals(mapGo, null)) {
                Debug.LogError("mapGO为空！！！");
                return;
            }

            CalculateWithJobSystem();
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

        private static MapBoundary GetDefaultMapBoundary() {
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
        private void CalculateWithJobSystem() {
            var curTime = DateTime.Now;

            var mapBoundary = GetDefaultMapBoundary();

            var meshFilters = mapGo.GetComponentsInChildren<MeshFilter>();

            var meshInfoVoList = new NativeList<MeshInfoVo>(Allocator);
            foreach(var meshFilter in meshFilters) {
                var gameObject = meshFilter.gameObject;
                var normalLayer = ((1 << gameObject.layer) & normalHeightLayer.value) != 0;
                var offsetLayer = checkHeightOffsetLayer && ((1 << gameObject.layer) & heightOffsetLayer.value) != 0;
                if(normalLayer || offsetLayer) {
                    var sharedMesh = meshFilter.sharedMesh;

                    var curType = normalLayer ? TriangleType.Normal : TriangleType.Offset;

                    for(var i = 0; i < sharedMesh.triangles.Length; i += 3) {
                        meshInfoVoList.Add(new MeshInfoVo
                        {
                            Type = curType,
                            MeshWordPos1 = LeftTwoDecimal(meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i]])),
                            MeshWordPos2 = LeftTwoDecimal(meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 1]])),
                            MeshWordPos3 = LeftTwoDecimal(meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 2]])),
                        });
                    }
                }
            }

            var shadowDirNormalize = math.normalize(new float3(-shadowDir.x / shadowDir.y, -1 / shadowDir.y, -shadowDir.z / shadowDir.y));
            /*var shadowMatrix = fixedShadowDir ? math.orthonormalize(new float3x3(1, 0, 0,
                                                                    shadowDirNormalize.x, shadowDirNormalize.y, shadowDirNormalize.z,
                                                                    0, 0, 1))
                                                        : float3x3.identity;*/
            var shadowMatrix = fixedShadowDir ? new float3x3(1, shadowDirNormalize.x, 0,
                    0, shadowDirNormalize.y, 0,
                    0, shadowDirNormalize.z, 1)
                : float3x3.identity;
            
            var triangleInfoArray = new NativeArray<TriangleInfo>(meshInfoVoList.Length, Allocator);
            var handleMeshVerticesJob = new HandleMeshVerticesJob
            {
                MeshInfoVoArray = meshInfoVoList.AsArray(),
                NeedLimitRight = needLimitRight,
                NeedLimitLeft = needLimitLeft,
                NeedLimitFront = needLimitFront,
                NeedLimitBack = needLimitBack,
                NeedLimitTop = needLimitTop,
                NeedLimitBottom = needLimitBottom,
                TriangleInfoArray = triangleInfoArray,
                RightLimit = rightLimit,
                LeftLimit = leftLimit,
                FrontLimit = frontLimit,
                BackLimit = backLimit,
                TopLimit = topLimit,
                BottomLimit = bottomLimit,
                ChangeShadowDir = fixedShadowDir,
                ShadowMatrix = shadowMatrix,
            };
            var meshVerticesHandle = handleMeshVerticesJob.Schedule(meshInfoVoList.Length, 64);
            meshVerticesHandle.Complete();
            meshInfoVoList.Dispose();

            // 处理
            var usedTriangleInfoList = new NativeList<TriangleInfo>(Allocator);
            foreach(var triangleInfo in triangleInfoArray) {
                if(triangleInfo.Type == TriangleType.Unavailable) continue;

                CheckBounds(ref mapBoundary.Left, ref mapBoundary.Right, ref mapBoundary.Bottom, ref mapBoundary.Top,
                    ref mapBoundary.Back, ref mapBoundary.Front, triangleInfo.Left,
                    triangleInfo.Right, triangleInfo.Bottom, triangleInfo.Top, triangleInfo.Back,
                    triangleInfo.Front);

                usedTriangleInfoList.Add(triangleInfo);
            }

            triangleInfoArray.Dispose();

            // 设置边界限制
            if(needLimitLeft && mapBoundary.Left < leftLimit) {
                mapBoundary.Left = leftLimit;
            }
            if(needLimitRight && mapBoundary.Right > rightLimit) {
                mapBoundary.Right = rightLimit;
            }
            if(needLimitBottom && mapBoundary.Bottom < bottomLimit) {
                mapBoundary.Bottom = bottomLimit;
            }
            if(needLimitTop && mapBoundary.Top > topLimit) {
                mapBoundary.Top = topLimit;
            }
            if(needLimitBack && mapBoundary.Back < backLimit) {
                mapBoundary.Back = backLimit;
            }
            if(needLimitFront && mapBoundary.Front > frontLimit) {
                mapBoundary.Front = frontLimit;
            }

            var length = mapBoundary.Right - mapBoundary.Left;
            var width = mapBoundary.Front - mapBoundary.Back;
            var high = mapBoundary.Top - mapBoundary.Bottom;
            var stepX = 1.0f / resolutionX * length;
            var stepY = 1.0f / resolutionY * width;
            var texture2D = new Texture2D(resolutionX, resolutionY);

            var maxHeight1 = LeftTwoDecimal(high * highCuttingLine);
            var maxHeight2 = high - maxHeight1;

            var pixelCount = resolutionX * resolutionY;
            var curHeightArray1 = new NativeArray<TriangleHeightInfo>(pixelCount, Allocator);
            var curHeightArray2 = new NativeArray<TriangleHeightInfo>(pixelCount, Allocator);
            var calculateHeightJob = new CalculateHeightJob
            {
                TriangleInfoArray = usedTriangleInfoList.AsArray(),
                ResolutionX = resolutionX,
                StepX = stepX,
                StepY = stepY,
                Left = mapBoundary.Left,
                Bottom = mapBoundary.Bottom,
                Back = mapBoundary.Back,
                MaxHeight1 = maxHeight1,
                CurHeightArray1 = curHeightArray1,
                CurHeightArray2 = curHeightArray2
            };
            var calculateHeightHandle = calculateHeightJob.Schedule(pixelCount, 64);
            calculateHeightHandle.Complete();
            usedTriangleInfoList.Dispose();

            var curOffsetHeightArray1 = new NativeArray<float>(pixelCount, Allocator);
            var curOffsetHeightArray2 = new NativeArray<float>(pixelCount, Allocator);
            var maxOffset = 0f;
            if(needHeightOffset) {
                var calculateOffsetHeightJob = new CalculateOffsetHeightJob
                {
                    CurHeightArray1 = curHeightArray1,
                    CurHeightArray2 = curHeightArray2,
                    ResolutionX = resolutionX,
                    ResolutionY = resolutionY,
                    CheckHeightOffsetLayer = checkHeightOffsetLayer,
                    CheckLength = checkLength,
                    HeightOffset = heightOffset,
                    HeightOffsetSinMin = heightOffsetSinMin,
                    HeightOffsetSinMax = heightOffsetSinMax,
                    StepX = stepX,
                    StepY = stepY,
                    Bottom = mapBoundary.Bottom,
                    CurOffsetHeightArray1 = curOffsetHeightArray1,
                    CurOffsetHeightArray2 = curOffsetHeightArray2,
                };
                var calculateOffsetHeightHandle = calculateOffsetHeightJob.Schedule(pixelCount, 64);
                calculateOffsetHeightHandle.Complete();

                foreach(var offset in curOffsetHeightArray1) {
                    if(maxOffset < offset) {
                        maxOffset = offset;
                    }
                }
                foreach(var offset in curOffsetHeightArray2) {
                    if(maxOffset < offset) {
                        maxOffset = offset;
                    }
                }
            }

            var pixelIndex = 0;
            for(var y = 0; y < resolutionY; y++) {
                for(var x = 0; x < resolutionX; x++) {
                    texture2D.SetPixel(x, y,
                        new Color(curHeightArray1[pixelIndex].Height / maxHeight1, curHeightArray2[pixelIndex].Height / maxHeight2, 
                            (curOffsetHeightArray2[pixelIndex] > curOffsetHeightArray1[pixelIndex] ? curOffsetHeightArray2[pixelIndex] : curOffsetHeightArray1[pixelIndex]) / maxOffset));
                    pixelIndex++;
                }
            }

            curHeightArray1.Dispose();
            curHeightArray2.Dispose();
            curOffsetHeightArray1.Dispose();
            curOffsetHeightArray2.Dispose();

            var costTime = DateTime.Now.Subtract(curTime).TotalMilliseconds / 1000d;

            var texturePath = $"/{path}/{mapGo.name}.png";

            texture2D.Apply();
            //保存图片
            var dataBytes = texture2D.EncodeToPNG();
            var fileStream = File.Open($"{Application.dataPath}{texturePath}", FileMode.OpenOrCreate);
            fileStream.Write(dataBytes, 0, dataBytes.Length);
            fileStream.Close();
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            //是否有材质球，没有则创建并保存
            if(mapMaterial == null) {
                var matPath = $"Assets/{path}/{mapGo.name}.mat";
                mapMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mapMaterial == null) {
                    mapMaterial = new Material(Shader.Find("Custom/LC/Shadow/ProjShadowURP"));
                    AssetDatabase.CreateAsset(mapMaterial, $"Assets/{path}/{mapGo.name}.mat");
                }
            }

            var heightTextureImporter = AssetImporter.GetAtPath($"Assets{texturePath}") as TextureImporter;
            heightTextureImporter.textureType = TextureImporterType.Default;
            heightTextureImporter.sRGBTexture = false;
            heightTextureImporter.mipmapEnabled = false;
            heightTextureImporter.isReadable = false;
            heightTextureImporter.textureCompression = TextureImporterCompression.Compressed;
            
            mapMaterial.SetTexture(ShaderHeightTex, AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets{texturePath}"));
            mapMaterial.SetFloat(ShaderHeightTexLeft, mapBoundary.Left);
            mapMaterial.SetFloat(ShaderHeightTexLength, length);
            mapMaterial.SetFloat(ShaderHeightTexBack, mapBoundary.Back);
            mapMaterial.SetFloat(ShaderHeightTexWidth, width);
            mapMaterial.SetFloat(ShaderHeightTexBottom, mapBoundary.Bottom);
            mapMaterial.SetFloat(ShaderHeightTexHigh, high);
            mapMaterial.SetFloat(ShaderMaxHeight1, maxHeight1);
            mapMaterial.SetFloat(ShaderMaxHeight2, maxHeight2);
            mapMaterial.SetFloat(ShaderMaxOffset, maxOffset);
            if(fixedShadowDir) {
                mapMaterial.SetVector(ShadowDir, (Vector3)shadowDir);
                mapMaterial.EnableKeyword("_FIXED_LIGHT_DIR");
            } else {
                mapMaterial.SetVector(ShadowDir, Vector3.down);
                mapMaterial.DisableKeyword("_FIXED_LIGHT_DIR");
            }
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"left:{mapBoundary.Left}_length:{length}_back:{mapBoundary.Back}_width:{width}_bottom:{mapBoundary.Bottom}_high:{high}_baseHeight1:{maxHeight1}_baseHeight2:{maxHeight2}_maxOffset:{maxOffset}_costTime:{costTime}");
        }

        /// <summary>
        /// 处理mesh顶点信息
        /// </summary>
        [BurstCompile]
        private struct HandleMeshVerticesJob : IJobParallelFor {
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
            [ReadOnly] public bool ChangeShadowDir;
            [ReadOnly] public float3x3 ShadowMatrix;

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
                    Normal = math.normalize(math.cross(meshInfoVo.MeshWordPos2 - meshInfoVo.MeshWordPos1,
                        meshInfoVo.MeshWordPos3 - meshInfoVo.MeshWordPos2))
                };

                if(ChangeShadowDir) {
                    /*triangleInfo.Vertices1 = math.mul(meshInfoVo.MeshWordPos1, ShadowMatrix);
                    triangleInfo.Vertices2 = math.mul(meshInfoVo.MeshWordPos2, ShadowMatrix);
                    triangleInfo.Vertices3 = math.mul(meshInfoVo.MeshWordPos3, ShadowMatrix);*/
                    triangleInfo.Vertices1 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos1);
                    triangleInfo.Vertices2 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos2);
                    triangleInfo.Vertices3 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos3);
                }

                CheckBounds(ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos1);
                CheckBounds(ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos2);
                CheckBounds(ref triangleInfo.Left, ref triangleInfo.Right, ref triangleInfo.Bottom,
                    ref triangleInfo.Top, ref triangleInfo.Back, ref triangleInfo.Front, meshInfoVo.MeshWordPos3);

                if(NeedLimitRight && triangleInfo.Left > RightLimit ||
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
        private struct CalculateHeightJob : IJobParallelFor {
            [ReadOnly] public NativeArray<TriangleInfo> TriangleInfoArray;

            [ReadOnly] public int ResolutionX;

            [ReadOnly] public float StepX;
            [ReadOnly] public float StepY;

            [ReadOnly] public float Left;
            [ReadOnly] public float Back;
            [ReadOnly] public float Bottom;
            
            [ReadOnly] public float MaxHeight1;

            public NativeArray<TriangleHeightInfo> CurHeightArray1;
            public NativeArray<TriangleHeightInfo> CurHeightArray2;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;
                var curPosX = Left + (0.5f + xIndex) * StepX;
                var curPosY = Back + (0.5f + yIndex) * StepY;

                var curHeightArray1 = new TriangleHeightInfo
                {
                    Height = 0f,
                    Offset = false
                };
                var curHeightArray2 = new TriangleHeightInfo
                {
                    Height = 0f,
                    Offset = false
                };
                
                foreach(var triangleInfo in TriangleInfoArray) {
                    if(triangleInfo.Type == TriangleType.Unavailable) {
                        continue;
                    }

                    // 是否在包围盒里
                    if(curPosX < triangleInfo.Left || curPosX > triangleInfo.Right ||
                       curPosY < triangleInfo.Back || curPosY > triangleInfo.Front) {
                        continue;
                    }

                    // 检测是否在三角形内
                    var tempPoint = new float3(curPosX, 0, curPosY);
                    if(!IsInsideTriangle(tempPoint, triangleInfo.Vertices1, triangleInfo.Vertices2,
                           triangleInfo.Vertices3)) {
                        continue;
                    }

                    // 如果在三角形内,则计算高度
                    if(curPosX - triangleInfo.Vertices1.x > 0.001f || curPosY - triangleInfo.Vertices1.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices1.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices1.z;
                        tempPoint.y = triangleInfo.Vertices1.y;
                    } else if(curPosX - triangleInfo.Vertices2.x > 0.001f || curPosY - triangleInfo.Vertices2.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices2.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices2.z;
                        tempPoint.y = triangleInfo.Vertices2.y;
                    }

                    if(curPosX - triangleInfo.Vertices3.x > 0.001f || curPosY - triangleInfo.Vertices3.z > 0.001f) {
                        tempPoint.x = curPosX - triangleInfo.Vertices3.x;
                        tempPoint.z = curPosY - triangleInfo.Vertices3.z;
                        tempPoint.y = triangleInfo.Vertices3.y;
                    }

                    var tempH =
                        -(tempPoint.x * triangleInfo.Normal.x + tempPoint.z * triangleInfo.Normal.z) /
                        triangleInfo.Normal.y + tempPoint.y - Bottom;

                    if(tempH < MaxHeight1) {
                        if(curHeightArray1.Height < tempH) {
                            curHeightArray1.Height = tempH;
                        }
                        if(!curHeightArray1.Offset && triangleInfo.Type == TriangleType.Offset) {
                            curHeightArray1.Offset = true;
                        }
                    } else {
                        tempH -= MaxHeight1;
                        if(curHeightArray2.Height < tempH) {
                            curHeightArray2.Height = tempH;
                        }
                        if(!curHeightArray2.Offset && triangleInfo.Type == TriangleType.Offset) {
                            curHeightArray2.Offset = true;
                        }
                    }
                }

                CurHeightArray1[index] = curHeightArray1;
                CurHeightArray2[index] = curHeightArray2;
            }
        }

        /// <summary>
        /// 计算偏移高度
        /// </summary>
        [BurstCompile]
        private struct CalculateOffsetHeightJob : IJobParallelFor {
            [ReadOnly] public NativeArray<TriangleHeightInfo> CurHeightArray1;
            [ReadOnly] public NativeArray<TriangleHeightInfo> CurHeightArray2;

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

            public NativeArray<float> CurOffsetHeightArray1;
            public NativeArray<float> CurOffsetHeightArray2;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;

                CurOffsetHeightArray1[index] = CalculateCurOffset(index, xIndex, yIndex, CurHeightArray1, StepX, StepY, Bottom, ResolutionX, ResolutionY, CheckLength, CheckHeightOffsetLayer, HeightOffsetSinMin, HeightOffsetSinMax, HeightOffset);
                CurOffsetHeightArray2[index] = CalculateCurOffset(index, xIndex, yIndex, CurHeightArray2, StepX, StepY, Bottom, ResolutionX, ResolutionY, CheckLength, CheckHeightOffsetLayer, HeightOffsetSinMin, HeightOffsetSinMax, HeightOffset);
            }
        }

        /// <summary>
        /// 保留两位小数
        /// </summary>
        private static float3 LeftTwoDecimal(float3 worldPos) {
            worldPos.x = LeftTwoDecimal(worldPos.x);
            worldPos.y = LeftTwoDecimal(worldPos.y);
            worldPos.z = LeftTwoDecimal(worldPos.z);
            return worldPos;
        }
        /// <summary>
        /// 保留两位小数
        /// </summary>
        private static float LeftTwoDecimal(float value) {
            return math.ceil(value * 100) / 100f;
        }

        /// <summary>
        /// 计算高度偏移
        /// </summary>
        private static float CalculateCurOffset(int index, int xIndex, int yIndex, NativeArray<TriangleHeightInfo> curHeightArray, float stepX, float stepY, float bottom, int resolutionX, int resolutionY, int checkLength, bool checkHeightOffsetLayer, float heightOffsetSinMin, float heightOffsetSinMax, float heightOffset) {
            var curSin = 0f;
            var triangleHeightInfo = curHeightArray[index];
            var curH = triangleHeightInfo.Height;
            if(curH > bottom) {
                for(var i = 1; i <= checkLength; i++) {
                    if(xIndex - i >= 0) {
                        CalculateCurSin(ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index - i],
                            stepX, heightOffsetSinMin, heightOffsetSinMax);
                    }

                    if(xIndex + i < resolutionX) {
                        CalculateCurSin(ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index + i],
                            stepX, heightOffsetSinMin, heightOffsetSinMax);
                    }

                    if(yIndex - i >= 0) {
                        CalculateCurSin(ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index - i * resolutionX],
                            stepY, heightOffsetSinMin, heightOffsetSinMax);
                    }

                    if(yIndex + i < resolutionY) {
                        CalculateCurSin(ref curSin, bottom, curH, checkHeightOffsetLayer, curHeightArray[index + i * resolutionX], 
                            stepY, heightOffsetSinMin, heightOffsetSinMax);
                    }
                }

                return math.round(curSin * heightOffset * 100) / 100f;
            } else {
                return 0;
            }
        }

        /// <summary>
        /// 计算sin值
        /// </summary>
        private static void CalculateCurSin(ref float curSin, float bottom, float curH, bool checkHeightOffsetLayer,
            TriangleHeightInfo curTest, float step, float heightOffsetSinMin, float heightOffsetSinMax) {
            if(curTest.Height <= bottom || (checkHeightOffsetLayer && !curTest.Offset)) return;

            var dif = math.abs(curH - curTest.Height);

            if(dif <= 0) return;

            var tempSin = dif / math.sqrt(dif * dif + step * step);
            if(tempSin > curSin && tempSin > heightOffsetSinMin && tempSin < heightOffsetSinMax) {
                curSin = tempSin;
            }
        }

        #endregion

        private static void CheckBounds(ref float left, ref float right, ref float bottom, ref float top, ref float back,
            ref float front, float3 pos) {
            CheckBounds(ref left, ref right, ref bottom, ref top, ref back, ref front, pos.x, pos.x, pos.y, pos.y, pos.z,
                pos.z);
        }

        private static void CheckBounds(ref float left, ref float right, ref float bottom, ref float top, ref float back,
            ref float front, float checkLeft, float checkRight, float checkBottom, float checkTop, float checkBack,
            float checkFront) {
            if(left > checkLeft) {
                left = checkLeft;
            }

            if(right < checkRight) {
                right = checkRight;
            }

            if(bottom > checkBottom) {
                bottom = checkBottom;
            }

            if(top < checkTop) {
                top = checkTop;
            }

            if(back > checkBack) {
                back = checkBack;
            }

            if(front < checkFront) {
                front = checkFront;
            }
        }

        /// <summary>
        /// 是否在三角形内
        /// </summary>
        private static bool IsInsideTriangle(float3 point, float3 vertices1, float3 vertices2, float3 vertices3) {
            var pa = new float3(vertices1.x, 0, vertices1.z) - point;
            var pb = new float3(vertices2.x, 0, vertices2.z) - point;
            var pc = new float3(vertices3.x, 0, vertices3.z) - point;

            var pab = math.cross(pa, pb);
            var pbc = math.cross(pb, pc);
            var pca = math.cross(pc, pa);

            var d1 = math.dot(pab, pbc);
            var d2 = math.dot(pab, pca);
            var d3 = math.dot(pbc, pca);

            return d1 >= 0 && d2 >= 0 && d3 >= 0;
        }
    }
}
