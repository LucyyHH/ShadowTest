using System;
using System.IO;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace ShadowTest.Custom.Editor {
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
        
        [Header("检查点是否在三角形中的容差(出现裂缝是可以适当增大)")] 
        public float checkInInsideTriangleTolerance = 0.001f;
        
        [Header("两个相邻像素高度差值超过当前值，则当前像素不显示影子")] 
        public float heightCheckHideDiff = 3f;
        
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
        private static readonly int ShaderMaxOffset = Shader.PropertyToID("_MaxOffset");
        private static readonly int ShadowDir = Shader.PropertyToID("_ShadowDir");

        private const Allocator Allocator = Unity.Collections.Allocator.TempJob;
        private const int InnerLoopBatchCount = 64;

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
            public Boundary Boundary;
            public Boundary ConvertBoundary;
            public float3 WorldPos1;
            public float3 WorldPos2;
            public float3 WorldPos3;
            public float3 ConvertWorldPos1;
            public float3 ConvertWorldPos2;
            public float3 ConvertWorldPos3;
            /// <summary>
            /// 世界空间下的法线
            /// </summary>
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

        private struct Boundary {
            public float Left;
            public float Right;
            public float Bottom;
            public float Top;
            public float Back;
            public float Front;
        }

        private static Boundary GetDefaultMapBoundary() {
            return new Boundary
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
            var convertMapBoundary = GetDefaultMapBoundary();
            
            // 1. 遍历整个场景需要接收影子的mesh，将mesh的顶点转化到世界坐标系，并存储到meshInfoVoList中
            var meshFilters = mapGo.GetComponentsInChildren<MeshFilter>();
            var meshInfoVoList = new NativeList<MeshInfoVo>(Allocator);
            foreach(var meshFilter in meshFilters) {
                var gameObject = meshFilter.gameObject;
                var normalLayer = ((1 << gameObject.layer) & normalHeightLayer.value) != 0;
                var offsetLayer = checkHeightOffsetLayer && ((1 << gameObject.layer) & heightOffsetLayer.value) != 0;
                
                if(!normalLayer && !offsetLayer) continue;
                
                var sharedMesh = meshFilter.sharedMesh;
                var curType = normalLayer ? TriangleType.Normal : TriangleType.Offset;
                for(var i = 0; i < sharedMesh.triangles.Length; i += 3) {
                    meshInfoVoList.Add(new MeshInfoVo
                    {
                        Type = curType,
                        MeshWordPos1 = meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i]]),
                        MeshWordPos2 = meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 1]]),
                        MeshWordPos3 = meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 2]]),
                    });
                }
            }

            var yAxis = math.normalizesafe(-shadowDir);
            /*var shadowMatrix = fixedShadowDir ? math.orthonormalize(new float3x3(1, 0, 0,
                                                                    shadowDirNormalize.x, shadowDirNormalize.y, shadowDirNormalize.z,
                                                                    0, 0, 1))
                                                        : float3x3.identity;*/
            var inverseShadowMatrix = fixedShadowDir ? 
                new float3x3(
                    1, yAxis.x, 0,
                    0, yAxis.y, 0,
                    0, yAxis.z, 1)
                : float3x3.identity;
            var shadowMatrix = fixedShadowDir ? 
                new float3x3(
                    1, -yAxis.x / yAxis.y, 0,
                    0, 1 / yAxis.y, 0,
                    0, -yAxis.z / yAxis.y, 1)
                //math.inverse(invShadowMatrix)
                : float3x3.identity;
            
            /*Debug.Log(shadowMatrix);
            Debug.Log(invShadowMatrix);
            Debug.Log(math.mul(invShadowMatrix, shadowMatrix));*/
            
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
                ShadowMatrix = shadowMatrix,
            };
            var meshVerticesHandle = handleMeshVerticesJob.Schedule(meshInfoVoList.Length, InnerLoopBatchCount);
            meshVerticesHandle.Complete();
            meshInfoVoList.Dispose();

            // 处理生成的三角形信息
            var usedTriangleInfoList = new NativeList<TriangleInfo>(Allocator);
            foreach(var triangleInfo in triangleInfoArray) {
                if(triangleInfo.Type == TriangleType.Unavailable) continue;
                
                CheckBounds(ref mapBoundary, triangleInfo.Boundary);
                CheckBounds(ref convertMapBoundary, triangleInfo.ConvertBoundary);

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
            var high = convertMapBoundary.Top - convertMapBoundary.Bottom;
            var stepX = 1.0f / resolutionX * length;
            var stepY = 1.0f / resolutionY * width;
            var texture2D = new Texture2D(resolutionX, resolutionY);

            var pixelCount = resolutionX * resolutionY;
            var curHeightArray = new NativeArray<TriangleHeightInfo>(pixelCount, Allocator);
            var calculateHeightJob = new CalculateHeightJob
            {
                TriangleInfoArray = usedTriangleInfoList.AsArray(),
                ResolutionX = resolutionX,
                StepX = stepX,
                StepY = stepY,
                Left = mapBoundary.Left,
                Bottom = mapBoundary.Bottom,
                Back = mapBoundary.Back,
                ConvertBottom = convertMapBoundary.Bottom,
                CurHeightArray = curHeightArray,
                ShadowMatrix = shadowMatrix,
                CheckInInsideTriangleTolerance = checkInInsideTriangleTolerance
            };
            var calculateHeightHandle = calculateHeightJob.Schedule(pixelCount, InnerLoopBatchCount);
            calculateHeightHandle.Complete();
            usedTriangleInfoList.Dispose();

            var curOffsetHeightArray = new NativeArray<float>(pixelCount, Allocator);
            var curEdgeHeightArray = new NativeArray<float>(pixelCount, Allocator);
            var maxOffset = 0f;
            var calculateOffsetHeightJob = new CalculateHeightOtherJob
            {
                CurHeightArray = curHeightArray,
                ResolutionX = resolutionX,
                ResolutionY = resolutionY,
                CheckHeightOffsetLayer = checkHeightOffsetLayer,
                CheckLength = checkLength,
                HeightOffset = heightOffset,
                HeightOffsetSinMin = heightOffsetSinMin,
                HeightOffsetSinMax = heightOffsetSinMax,
                StepX = stepX,
                StepY = stepY,
                ConvertBottom = convertMapBoundary.Bottom,
                HeightCheckHideDiff = heightCheckHideDiff,
                NeedHeightOffset = needHeightOffset,
                CurOffsetHeightArray = curOffsetHeightArray,
                CurEdgeHeightArray = curEdgeHeightArray,
            };
            var calculateOffsetHeightHandle = calculateOffsetHeightJob.Schedule(pixelCount, InnerLoopBatchCount);
            calculateOffsetHeightHandle.Complete();
            foreach(var offset in curOffsetHeightArray) {
                if(maxOffset < offset) {
                    maxOffset = offset;
                }
            }

            var pixelIndex = 0;
            for(var y = 0; y < resolutionY; y++) {
                for(var x = 0; x < resolutionX; x++) {
                    texture2D.SetPixel(x, y,
                        new Color( curHeightArray[pixelIndex].Height / high, curOffsetHeightArray[pixelIndex] / maxOffset, curEdgeHeightArray[pixelIndex]));
                    pixelIndex++;
                }
            }

            curHeightArray.Dispose();
            curOffsetHeightArray.Dispose();
            curEdgeHeightArray.Dispose();

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
            mapMaterial.SetFloat(ShaderHeightTexBottom, convertMapBoundary.Bottom);
            mapMaterial.SetFloat(ShaderHeightTexHigh, high);
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

            Debug.Log($"left:{mapBoundary.Left}_length:{length}_back:{mapBoundary.Back}_width:{width}_bottom:{mapBoundary.Bottom}_high:{high}_maxOffset:{maxOffset}_costTime:{costTime}");
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
            [ReadOnly] public float3x3 ShadowMatrix;

            public NativeArray<TriangleInfo> TriangleInfoArray;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var meshInfoVo = MeshInfoVoArray[index];

                var triangleInfo = new TriangleInfo
                {
                    Type = meshInfoVo.Type,
                    Boundary = GetDefaultMapBoundary(),
                    ConvertBoundary = GetDefaultMapBoundary(),
                    WorldPos1 = meshInfoVo.MeshWordPos1,
                    WorldPos2 = meshInfoVo.MeshWordPos2,
                    WorldPos3 = meshInfoVo.MeshWordPos3,
                    ConvertWorldPos1 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos1),
                    ConvertWorldPos2 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos2),
                    ConvertWorldPos3 = math.mul(ShadowMatrix, meshInfoVo.MeshWordPos3)
                };

                // 计算法线
                triangleInfo.Normal = math.normalizesafe(math.cross(triangleInfo.ConvertWorldPos2 - triangleInfo.ConvertWorldPos1,
                    triangleInfo.ConvertWorldPos3 - triangleInfo.ConvertWorldPos2));

                // 生成边界信息
                CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos1);
                CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos2);
                CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos3);
                
                CheckBounds(ref triangleInfo.ConvertBoundary, triangleInfo.ConvertWorldPos1);
                CheckBounds(ref triangleInfo.ConvertBoundary, triangleInfo.ConvertWorldPos2);
                CheckBounds(ref triangleInfo.ConvertBoundary, triangleInfo.ConvertWorldPos3);

                if(NeedLimitRight && triangleInfo.Boundary.Left > RightLimit ||
                   NeedLimitLeft && triangleInfo.Boundary.Right < LeftLimit ||
                   NeedLimitFront && triangleInfo.Boundary.Back > FrontLimit ||
                   NeedLimitBack && triangleInfo.Boundary.Front < BackLimit ||
                   NeedLimitTop && triangleInfo.Boundary.Bottom > TopLimit ||
                   NeedLimitBottom && triangleInfo.Boundary.Top < BottomLimit) {
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
            [ReadOnly] public float ConvertBottom;
            
            [ReadOnly] public float3x3 ShadowMatrix;

            [ReadOnly] public float CheckInInsideTriangleTolerance;

            public NativeArray<TriangleHeightInfo> CurHeightArray;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var curHeightArray = new TriangleHeightInfo
                {
                    Height = 0f,
                    Offset = false
                };
                
                // 计算当前像素世界坐标
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;
                var curPositionX = Left + (0.5f + xIndex) * StepX;
                var curPositionY = Back + (0.5f + yIndex) * StepY;
                var curPoint = new float3(curPositionX, Bottom, curPositionY);
                // 当前像素点转化到新坐标系后的坐标
                var curConvertPoint = math.mul(ShadowMatrix, curPoint);
                //Debug.Log($"{curPoint}_{curConvertPoint}");
                
                foreach(var triangleInfo in TriangleInfoArray) {
                    if(triangleInfo.Type == TriangleType.Unavailable) {
                        continue;
                    }

                    // 是否在包围盒里
                    if(curPositionX < triangleInfo.ConvertBoundary.Left || curPositionX > triangleInfo.ConvertBoundary.Right ||
                       curPositionY < triangleInfo.ConvertBoundary.Back || curPositionY > triangleInfo.ConvertBoundary.Front) {
                        continue;
                    }

                    // 检测是否在三角形内
                    if(!IsInsideTriangleMy(curConvertPoint,
                           new float3(triangleInfo.ConvertWorldPos1.x, curConvertPoint.y, triangleInfo.ConvertWorldPos1.z),
                           new float3(triangleInfo.ConvertWorldPos2.x, curConvertPoint.y, triangleInfo.ConvertWorldPos2.z),
                           new float3(triangleInfo.ConvertWorldPos3.x, curConvertPoint.y, triangleInfo.ConvertWorldPos3.z),
                           CheckInInsideTriangleTolerance)) {
                        continue;
                    }
                    
                    /*// 检测是否在三角形内
                    if(!IsInsideTriangle(curConvertPoint, 
                           new float3(triangleInfo.ConvertWorldPos1.x, curConvertPoint.y, triangleInfo.ConvertWorldPos1.z), 
                           new float3(triangleInfo.ConvertWorldPos2.x, curConvertPoint.y, triangleInfo.ConvertWorldPos2.z), 
                           new float3(triangleInfo.ConvertWorldPos3.x, curConvertPoint.y, triangleInfo.ConvertWorldPos3.z))) {
                        continue;
                    }*/
                    
                    /*// 检测是否在三角形内
                    if(!IsInsideTriangle(new float2(curConvertPoint.x, curConvertPoint.z),
                           new float2(triangleInfo.ConvertWorldPos1.x, triangleInfo.ConvertWorldPos1.z),
                           new float2(triangleInfo.ConvertWorldPos2.x, triangleInfo.ConvertWorldPos2.z),
                           new float2(triangleInfo.ConvertWorldPos3.x, triangleInfo.ConvertWorldPos3.z))) {
                        continue;
                    }*/
                    
                    /*// 检测是否在三角形内
                    if(!IsInTriangle(math.normalizesafe(new float2(curConvertPoint.x, curConvertPoint.z)),
                           math.normalizesafe(new float2(triangleInfo.ConvertWorldPos1.x, triangleInfo.ConvertWorldPos1.z)),
                           math.normalizesafe(new float2(triangleInfo.ConvertWorldPos2.x, triangleInfo.ConvertWorldPos2.z)),
                               math.normalizesafe(new float2(triangleInfo.ConvertWorldPos3.x, triangleInfo.ConvertWorldPos3.z)))) {
                        continue;
                    }*/

                    // 如果在三角形内,则计算高度
                    float3 tempPoint;
                    if(curConvertPoint.x - triangleInfo.ConvertWorldPos1.x > 0.001f || curConvertPoint.z - triangleInfo.ConvertWorldPos1.z > 0.001f) {
                        tempPoint = triangleInfo.ConvertWorldPos1;
                    } else if(curConvertPoint.x - triangleInfo.ConvertWorldPos2.x > 0.001f || curConvertPoint.z - triangleInfo.ConvertWorldPos2.z > 0.001f) {
                        tempPoint = triangleInfo.ConvertWorldPos2;
                    } else if(curConvertPoint.x - triangleInfo.ConvertWorldPos3.x > 0.001f || curConvertPoint.z - triangleInfo.ConvertWorldPos3.z > 0.001f) {
                        tempPoint = triangleInfo.ConvertWorldPos3;
                    } else {
                        tempPoint = curConvertPoint;
                    }
                    //tempPoint = math.mul(InvShadowMatrix, tempPoint);
                    var tempH = -((curConvertPoint.x - tempPoint.x) * triangleInfo.Normal.x + (curConvertPoint.z - tempPoint.z) * triangleInfo.Normal.z) /
                        triangleInfo.Normal.y + tempPoint.y - ConvertBottom;

                    if(curHeightArray.Height < tempH) {
                        curHeightArray.Height = tempH;
                    }
                    if(!curHeightArray.Offset && triangleInfo.Type == TriangleType.Offset) {
                        curHeightArray.Offset = true;
                    }
                }
                
                CurHeightArray[index] = curHeightArray;
            }
        }

        /// <summary>
        /// 计算高度其他信息，offset和边界
        /// </summary>
        [BurstCompile]
        private struct CalculateHeightOtherJob : IJobParallelFor {
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

            [ReadOnly] public float ConvertBottom;
            
            [ReadOnly] public float HeightCheckHideDiff;

            [ReadOnly] public bool NeedHeightOffset;

            public NativeArray<float> CurOffsetHeightArray;
            public NativeArray<float> CurEdgeHeightArray;

            // Each Execute call processes only an individual index.
            public void Execute(int index) {
                var xIndex = index % ResolutionX;
                var yIndex = index / ResolutionX;

                if(NeedHeightOffset) {
                    CurOffsetHeightArray[index] = CalculateCurHeightOffset(index, xIndex, yIndex, CurHeightArray, StepX, StepY, ConvertBottom, ResolutionX, ResolutionY, CheckLength, CheckHeightOffsetLayer, HeightOffsetSinMin, HeightOffsetSinMax, HeightOffset);
                }
                CurEdgeHeightArray[index] = CalculateCurHeightEdge(index, xIndex, yIndex, HeightCheckHideDiff, CurHeightArray, ConvertBottom, ResolutionX, ResolutionY);
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
        private static float CalculateCurHeightOffset(int index, int xIndex, int yIndex, NativeArray<TriangleHeightInfo> curHeightArray, float stepX, float stepY, float bottom, int resolutionX, int resolutionY, int checkLength, bool checkHeightOffsetLayer, float heightOffsetSinMin, float heightOffsetSinMax, float heightOffset) {
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

                return curSin * heightOffset;
            }
            
            return 0;
        }
        
        /// <summary>
        /// 计算高度偏移
        /// </summary>
        private static float CalculateCurHeightEdge(int index, int xIndex, int yIndex, float checkHideDiff, NativeArray<TriangleHeightInfo> curHeightArray, float bottom, int resolutionX, int resolutionY) {
            var triangleHeightInfo = curHeightArray[index];
            var curH = triangleHeightInfo.Height;
            if(curH > bottom) {
                // 检查左右上下两像素高度差是否大于设置的值checkHideDiff,大于则标记为边界
                for(var i = 1; i <= 2; i++) {
                    if(xIndex - i >= 0 && math.abs(curH - curHeightArray[index - i].Height) > checkHideDiff || 
                       yIndex - i >= 0 && math.abs(curH - curHeightArray[index - i * resolutionX].Height) > checkHideDiff) {
                        return 1f;
                    }
                    if(xIndex + i < resolutionX && math.abs(curH - curHeightArray[index + i].Height) > checkHideDiff || 
                       yIndex + i < resolutionY && math.abs(curH - curHeightArray[index + i * resolutionX].Height) > checkHideDiff) {
                        return 1f;
                    }
                }
            } 
            return 0;
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
        
        private static void CheckBounds(ref Boundary boundary, Boundary checkBoundary) {
            CheckBounds(ref boundary.Left, ref boundary.Right, ref boundary.Bottom, ref boundary.Top, ref boundary.Back, ref boundary.Front, 
                checkBoundary.Left, checkBoundary.Right, checkBoundary.Bottom, checkBoundary.Top, checkBoundary.Back, checkBoundary.Front);
        }

        private static void CheckBounds(ref Boundary boundary, float3 pos) {
            CheckBounds(ref boundary.Left, ref boundary.Right, ref boundary.Bottom, ref boundary.Top, ref boundary.Back, ref boundary.Front, 
                pos);
        }
        
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
        private static bool IsInsideTriangleMy(float3 point, float3 vertices1, float3 vertices2, float3 vertices3, float tolerance = 0.001f) {
            var pa = vertices1 - point;
            var pb = vertices2 - point;
            var pc = vertices3 - point;

            var pab = math.cross(pa, pb);
            var pbc = math.cross(pb, pc);
            var pca = math.cross(pc, pa);

            var d1 = math.dot(pab, pbc);
            var d2 = math.dot(pab, pca);
            var d3 = math.dot(pbc, pca);

            return d1 >= -tolerance && d2 >= -tolerance && d3 >= -tolerance;
        }

        /// <summary>
        /// 是否在三角形内
        /// </summary>
        private static bool IsInsideTriangle(float3 point, float3 vertices1, float3 vertices2, float3 vertices3) {
            /*var v12 = vertices2 - vertices1;
            var v23 = vertices3 - vertices2;
            var v31 = vertices1 - vertices3;
            var v13 = vertices3 - vertices1;
            
            var normal = math.normalizesafe(math.cross(v12, v13));

            double dot12 = math.dot(v12, normal);
            double dot23 = math.dot(v23, normal);
            double dot31 = math.dot(v31, normal);

            return (dot12 >= 0 && dot23 >= 0 && dot31 >= 0) || (dot12 <= 0 && dot23 <= 0 && dot31 <= 0);*/
            return SameSide(vertices1, vertices2, vertices3, point) &&
                   SameSide(vertices2, vertices3, vertices1, point) &&
                   SameSide(vertices3, vertices1, vertices2, point);
        }
        
        // Determine whether two vectors v1 and v2 point to the same direction
        // v1 = Cross(AB, AC)
        // v2 = Cross(AB, AP)
        private static bool SameSide(float3 A, float3 B, float3 C, float3 P)
        {
            float3 AB = B - A ;
            float3 AC = C - A ;
            float3 AP = P - A ;

            float3 v1 = math.normalizesafe(math.cross(AB, AC));
            float3 v2 = math.normalizesafe(math.cross(AB, AP));

            // v1 and v2 should point to the same direction
            return math.dot(v1, v2) >= 0;
        }
        
        /// <summary>
        /// 是否在三角形内
        /// </summary>
        private static bool IsInsideTriangle(float2 p, float2 a, float2 b, float2 c) {
            
            double areaAbc = Math.Abs(CrossProduct(a, b) + CrossProduct(b, c) + CrossProduct(c, a)) / 2;
            double areaPbc = Math.Abs(CrossProduct(p, b) + CrossProduct(b, c) + CrossProduct(c, p)) / 2;
            double areaPca = Math.Abs(CrossProduct(a, p) + CrossProduct(p, c) + CrossProduct(c, a)) / 2;
            double areaPab = Math.Abs(CrossProduct(a, p) + CrossProduct(p, b) + CrossProduct(b, a)) / 2;

            return Math.Abs(areaAbc - (areaPbc + areaPca + areaPab)) < 0.5; // Tolerance for floating point comparisons
        }
        
        private static float CrossProduct(float2 p1, float2 p2)
        {
            return p1.x * p2.y - p2.x * p1.y;
        }
        
        private static float Product(float2 p1, float2 p2, float2 p3) {
            //首先根据坐标计算p1p2和p1p3的向量，然后再计算叉乘
            //p1p2 向量表示为 (p2.x-p1.x,p2.y-p1.y)
            //p1p3 向量表示为 (p3.x-p1.x,p3.y-p1.y)
            return (p2.x - p1.x) * (p3.y - p1.y) - (p2.y - p1.y) * (p3.x - p1.x);
        }

        private static bool IsInTriangle(float2 o, float2 p1, float2 p2, float2 p3) {
            while(true) {
                //保证p1，p2，p3是逆时针顺序
                if(!(Product(p1, p2, p3) < 0)) {
                    var product1 = Product(p1, p2, o);
                    var product2 = Product(p2, p3, o);
                    var product3 = Product(p3, p1, o);
                    /*if(product1 < 0 && product1 > -0.01 || product2 < 0 && product2 > -0.01 || product3 < 0 && product3 > -0.01) {
                        Debug.Log($"{o}_{p1}_{p2}_{p3}:每次的结果：{product1}_{product2}_{product3}");
                    }*/
                    return product1 >= -0.001 && product2 >= -0.001 && product3 >= -0.001;
                }
                (p2, p3) = (p3, p2);
            }
        }
    }
}
