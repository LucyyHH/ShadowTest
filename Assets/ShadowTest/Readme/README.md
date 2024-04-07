# 较平坦地形的影子绘制

## 两种方式对比
ProjectShadow不光需要一个额外的相机去生成RT图，同时当前这种方式生成RT图时，每个需要影子的mesh都需要一个DrawCall（包括SetPassCall），而且生成影子时接受影子mesh中每个不能合批的都会增加一个DrawCall（包括SetPassCall）   
可以修改ProjectorShadowPass来实现SRPBatching批量绘制RT图，但后面生成影子的可能要修改投影器插件的逻辑（URP没有自带的投影器）

Custom方式，先在编辑器下生成一个贴图来近似当前场景的高度图，同时使用SRPBatching批量生成影子，根据数量不同会有几个SetPassCall（几千个mesh只需要三四个），每个需要影子的mesh需要一个DrawCall（但不包括SetPassCall）

### 具体数据对比
#### ProjectShadow方式
###### DrawCall
###### SetPassCall
###### 帧数
###### 耗时
#### Custom
###### DrawCall
###### SetPassCall
###### 帧数
###### 耗时

## 实现思路
### 第一种方式，用垂直方向高度图生成，可以旋转灯光方向，不太准确
#### 生成高度图
使用多线程计算缩短生成时间

1. 遍历整个场景需要接收影子的mesh，将mesh的三角形各顶点转化到世界坐标系，并存储到meshInfoVoList中
    ```csharp
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
    ```
2. HandleMeshVerticesJob()任务处理从1中获取到meshInfoVoList的三角形信息，存储到triangleInfoArray列表中，主要任务是生成三角形的边界信息及法线，并标记超过设置边界的三角形
   ```csharp
    // 计算法线
    triangleInfo.Normal = math.normalizesafe(math.cross(triangleInfo.WorldPos2 - triangleInfo.WorldPos1,
                    triangleInfo.WorldPos3 - triangleInfo.WorldPos2));
    // 生成边界信息
    CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos1);
    CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos2);
    CheckBounds(ref triangleInfo.Boundary, triangleInfo.WorldPos3);
   ```
3. 遍历从2得到的triangleInfoArray列表，将在边界内的三角形存储到usedTriangleInfoList中
4. CalculateHeightJob()任务计算场景高度，存到curHeightArray中
   ```csharp
    // 计算当前像素世界坐标
    var xIndex = index % ResolutionX;
    var yIndex = index / ResolutionX;
    var curPosX = Left + (0.5f + xIndex) * StepX;
    var curPosY = Back + (0.5f + yIndex) * StepY;
    var curPoint = new float3(curPosX, Bottom, curPosY);
    // ...
    // 遍历三角形列表，判断当前点是否在三角形y方向的投影内，是则计算高度
    foreach(var triangleInfo in TriangleInfoArray) {
        // 检测是否在三角形内
        if(!IsInsideTriangleMy(curConvertPoint,
               new float3(triangleInfo.ConvertWorldPos1.x, curConvertPoint.y, triangleInfo.ConvertWorldPos1.z),
               new float3(triangleInfo.ConvertWorldPos2.x, curConvertPoint.y, triangleInfo.ConvertWorldPos2.z),
               new float3(triangleInfo.ConvertWorldPos3.x, curConvertPoint.y, triangleInfo.ConvertWorldPos3.z),
               CheckInInsideTriangleTolerance)) {
            continue;
            // ...
            // 计算高度
            var tempH = -((curConvertPoint.x - tempPoint.x) * triangleInfo.Normal.x + (curConvertPoint.z - tempPoint.z) * triangleInfo.Normal.z) /
                        triangleInfo.Normal.y + tempPoint.y - ConvertBottom;
            // ...
        }
        // ... 存储高度到CurHeightArray
    }
   ```
5. CalculateHeightOtherJob()任务计算场景高度偏移及边界信息，分别存到curOffsetHeightArray和curHideHeightArray
   ```csharp
    if(NeedHeightOffset) {
        CurOffsetHeightArray[index] = CalculateCurHeightOffset(index, xIndex, yIndex, CurHeightArray, StepX, StepY, ConvertBottom, ResolutionX, ResolutionY, CheckLength, CheckHeightOffsetLayer, HeightOffsetSinMin, HeightOffsetSinMax, HeightOffset);
    }
    CurEdgeHeightArray[index] = CalculateCurHeightHide(index, xIndex, yIndex, HeightCheckHideDiff, CurHeightArray, ConvertBottom, ResolutionX, ResolutionY);
   ```
6. 保存材质球信息，把4中得到的curHeightArray存到r通道，curOffsetHeightArray存到g通道，curHideHeightArray存到b通道
   ```csharp
   var pixelIndex = 0;
   for(var y = 0; y < resolutionY; y++) {
       for(var x = 0; x < resolutionX; x++) {
           texture2D.SetPixel(x, y,
               new Color( curHeightArray[pixelIndex].Height / high, curOffsetHeightArray[pixelIndex] / maxOffset, curEdgeHeightArray[pixelIndex]));
           pixelIndex++;
       }
   }
   ```

##### 绘制影子

### 第二种方式，根据灯光方向生成高度图，需要固定定光方向，更准确