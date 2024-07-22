# 平坦地形固定镜头视角的影子绘制

## 性能数据对比
|   mesh个数   | 无影子和优化后的平面阴影性能数据                                                        | 投影器阴影普通版本和SRP合批版本性能数据                                                                     |
|:----------:|:------------------------------------------------------------------------|:------------------------------------------------------------------------------------------|
|  500个mesh  | ![Nop_500.gif](Resources%2FNop%2FNop_500.gif)<br/>无影子                   | ![PS_500.gif](Resources%2FProjectorShadow%2FPS_500.gif)<br/>ProjectShadow                 |
|     \      | ![Custom_500.gif](Resources%2FCustom%2FCustom_500.gif)<br/>优化后的平面阴影     | ![PS_SRP_500.gif](Resources%2FProjectorShadow%2FPS_SRP_500.gif)<br/>ProjectShadow_SRP     |
| 1000个mesh  | ![Nop_1000.gif](Resources%2FNop%2FNop_1000.gif)<br/>无影子                 | ![PS_1000.gif](Resources%2FProjectorShadow%2FPS_1000.gif)<br/>ProjectShadow               |
|     \      | ![Custom_1000.gif](Resources%2FCustom%2FCustom_1000.gif)<br/>优化后的平面阴影   | ![PS_SRP_1000.gif](Resources%2FProjectorShadow%2FPS_SRP_1000.gif)<br/>ProjectShadow_SRP   |
| 2000个mesh  | ![Nop_2000.gif](Resources%2FNop%2FNop_2000.gif)<br/>无影子                 | ![PS_2000.gif](Resources%2FProjectorShadow%2FPS_2000.gif)<br/>ProjectShadow               |
|     \      | ![Custom_2000.gif](Resources%2FCustom%2FCustom_2000.gif)<br/>优化后的平面阴影   | ![PS_SRP_2000.gif](Resources%2FProjectorShadow%2FPS_SRP_2000.gif)<br/>ProjectShadow_SRP   |
| 5000个mesh  | ![Nop_5000.gif](Resources%2FNop%2FNop_5000.gif)<br/>无影子                 | ![PS_5000.gif](Resources%2FProjectorShadow%2FPS_5000.gif)<br/>ProjectShadow               |
|     \      | ![Custom_5000.gif](Resources%2FCustom%2FCustom_5000.gif)<br/>优化后的平面阴影   | ![PS_SRP_5000.gif](Resources%2FProjectorShadow%2FPS_SRP_5000.gif)<br/>ProjectShadow_SRP   |
| 10000个mesh | ![Nop_10000.gif](Resources%2FNop%2FNop_10000.gif)<br/>无影子               | ![PS_10000.gif](Resources%2FProjectorShadow%2FPS_10000.gif)<br/>ProjectShadow             |
|     \      | ![Custom_10000.gif](Resources%2FCustom%2FCustom_10000.gif)<br/>优化后的平面阴影 | ![PS_SRP_10000.gif](Resources%2FProjectorShadow%2FPS_SRP_10000.gif)<br/>ProjectShadow_SRP |