// 创建一个可序列化的子弹类 Bullet.CS

using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// 可序列化
/// </summary>
[Serializable]
public class BasePlayableEvent : ScriptableObject {

    public virtual void StartEvent(float duration) {
        
    }
    
    public virtual void StopEvent() {
        
    }
}