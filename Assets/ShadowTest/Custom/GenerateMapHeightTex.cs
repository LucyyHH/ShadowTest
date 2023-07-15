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
        //[NonSerialized]
        public GameObject mapGo;

        //[NonSerialized]
        public Material mapMaterial;

        //[NonSerialized]
        public LayerMask normalHeightLayer;
        //[NonSerialized]
        public int resolutionX;
        //[NonSerialized]
        public int resolutionY;
        //[NonSerialized]
        public string path;

        //[NonSerialized]
        public bool needLimitLeft;
        //[NonSerialized]
        public float leftLimit = float.MinValue;
        //[NonSerialized]
        public bool needLimitRight;
        //[NonSerialized]
        public float rightLimit = float.MaxValue;
        //[NonSerialized]
        public bool needLimitBottom;
        //[NonSerialized]
        public float bottomLimit = float.MinValue;
        //[NonSerialized]
        public bool needLimitTop;
        //[NonSerialized]
        public float topLimit = float.MaxValue;
        //[NonSerialized]
        public bool needLimitBack;
        //[NonSerialized]
        public float backLimit = float.MinValue;
        //[NonSerialized]
        public bool needLimitFront;
        //[NonSerialized]
        public float frontLimit = float.MaxValue;

        //[NonSerialized]
        public bool needHeightOffset;
        //[NonSerialized]
        public bool checkHeightOffsetLayer;
        //[NonSerialized]
        public LayerMask heightOffsetLayer;
        //[NonSerialized]
        public float heightOffset = 5f;
        //[NonSerialized]
        public float heightOffsetSinMin = 0.1f;
        //[NonSerialized]
        public float heightOffsetSinMax = 0.5f;
        //[NonSerialized]
        public int checkLength = 5;

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

        public void GenerateMapHeightTexture() {
            if(ReferenceEquals(mapGo, null)) {
                Debug.LogError("mapGO为空！！！");
                return;
            }

            if(ReferenceEquals(mapMaterial, null)) {
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
                var normalLayer = (normalHeightLayer.value >> gameObject.layer & 1) == 1;
                var offsetLayer = checkHeightOffsetLayer && (heightOffsetLayer.value >> gameObject.layer & 1) == 1;
                if(normalLayer || offsetLayer) {
                    var sharedMesh = meshFilter.sharedMesh;

                    var curType = normalLayer ? TriangleType.Normal : TriangleType.Offset;

                    for(var i = 0; i < sharedMesh.triangles.Length; i += 3) {
                        meshInfoVoList.Add(new MeshInfoVo
                        {
                            Type = curType,
                            MeshWordPos1 =
                                LeftTwoDecimal(
                                    meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i]])),
                            MeshWordPos2 =
                                LeftTwoDecimal(
                                    meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 1]])),
                            MeshWordPos3 =
                                LeftTwoDecimal(
                                    meshFilter.transform.TransformPoint(sharedMesh.vertices[sharedMesh.triangles[i + 2]])),
                        });
                    }
                }
            }

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
            var texture2D = new Texture2D(resolutionX, resolutionY, TextureFormat.RG16, 0, true);

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
                CurHeightArray = curHeightArray,
            };
            var calculateHeightHandle = calculateHeightJob.Schedule(pixelCount, 64);
            calculateHeightHandle.Complete();
            usedTriangleInfoList.Dispose();

            var curOffsetHeightArray = new NativeArray<float>(pixelCount, Allocator);
            var maxOffset = 0f;
            if(needHeightOffset) {
                var calculateOffsetHeightJob = new CalculateOffsetHeightJob
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
                    Bottom = mapBoundary.Bottom,
                    CurOffsetHeightArray = curOffsetHeightArray,
                };
                var calculateOffsetHeightHandle = calculateOffsetHeightJob.Schedule(pixelCount, 64);
                calculateOffsetHeightHandle.Complete();

                foreach(var offset in curOffsetHeightArray) {
                    if(maxOffset < offset) {
                        maxOffset = offset;
                    }
                }
            }

            var pixelIndex = 0;
            for(var y = 0; y < resolutionY; y++) {
                for(var x = 0; x < resolutionX; x++) {
                    texture2D.SetPixel(x, y,
                        new Color(curHeightArray[pixelIndex].Height / high, curOffsetHeightArray[pixelIndex] / maxOffset,
                            0));
                    pixelIndex++;
                }
            }

            curHeightArray.Dispose();
            curOffsetHeightArray.Dispose();

            var costTime = DateTime.Now.Subtract(curTime).TotalMilliseconds / 1000d;

            var texturePath = $"/{path}/{mapGo.name}.png";

            texture2D.Apply();
            //保存图片
            var dataBytes = texture2D.EncodeToPNG();
            var fileStream = File.Open($"{Application.dataPath}{texturePath}", FileMode.OpenOrCreate);
            fileStream.Write(dataBytes, 0, dataBytes.Length);
            fileStream.Close();

            //是否有材质球，没有则创建并保存
            if(mapMaterial == null) {
                var matPath = $"Assets/{path}/{mapGo.name}.mat";
                mapMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mapMaterial == null) {
                    mapMaterial = new Material(Shader.Find("Custom/LC/Shadow/ProjShadowURP"));
                    AssetDatabase.CreateAsset(mapMaterial, $"Assets/{path}/{mapGo.name}.mat");
                }
            }
            
            mapMaterial.SetTexture(HeightTex, AssetDatabase.LoadAssetAtPath<Texture2D>($"Assets{texturePath}"));
            mapMaterial.SetFloat(HeightTexLeft, mapBoundary.Left);
            mapMaterial.SetFloat(HeightTexLength, length);
            mapMaterial.SetFloat(HeightTexBack, mapBoundary.Back);
            mapMaterial.SetFloat(HeightTexWidth, width);
            mapMaterial.SetFloat(HeightTexBottom, mapBoundary.Bottom);
            mapMaterial.SetFloat(HeightTexHigh, high);
            mapMaterial.SetFloat(MaxOffset, maxOffset);
            
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"left:{mapBoundary.Left}_length:{length}_back:{mapBoundary.Back}_width:{width}_bottom:{mapBoundary.Bottom}_high:{high}_maxOffset:{maxOffset}_costTime:{costTime}");
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

                    if(curH < tempH) {
                        curH = tempH;
                    }

                    if(!offset && triangleInfo.Type == TriangleType.Offset) {
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
        private struct CalculateOffsetHeightJob : IJobParallelFor {
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
                if(curH > Bottom) {
                    for(var i = 1; i <= CheckLength; i++) {
                        if(xIndex - i >= 0) {
                            CalculateCurSin(ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index - i],
                                StepX, HeightOffsetSinMin, HeightOffsetSinMax);
                        }

                        if(xIndex + i < ResolutionX) {
                            CalculateCurSin(ref curSin, Bottom, curH, CheckHeightOffsetLayer, CurHeightArray[index + i],
                                StepX, HeightOffsetSinMin, HeightOffsetSinMax);
                        }

                        if(yIndex - i >= 0) {
                            CalculateCurSin(ref curSin, Bottom, curH, CheckHeightOffsetLayer,
                                CurHeightArray[index - i * ResolutionX], StepY, HeightOffsetSinMin, HeightOffsetSinMax);
                        }

                        if(yIndex + i < ResolutionY) {
                            CalculateCurSin(ref curSin, Bottom, curH, CheckHeightOffsetLayer,
                                CurHeightArray[index + i * ResolutionX], StepY, HeightOffsetSinMin, HeightOffsetSinMax);
                        }
                    }

                    var curOffset = math.round(curSin * HeightOffset * 100) / 100f;
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
        private static float3 LeftTwoDecimal(float3 worldPos) {
            worldPos.x = math.ceil(worldPos.x * 100) / 100f;
            worldPos.y = math.ceil(worldPos.y * 100) / 100f;
            worldPos.z = math.ceil(worldPos.z * 100) / 100f;
            return worldPos;
        }

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
