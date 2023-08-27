

using UnityEngine;

/// <summary>
/// 全局的数据
/// </summary>
public class GlobalData : Singleton<GlobalData> {
    /// <summary>
    /// 灯光方向
    /// </summary>
    public Vector4 lightDir = Vector4.one;
}
