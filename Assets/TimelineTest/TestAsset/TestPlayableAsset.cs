using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;

[System.Serializable]
public class TestPlayableAsset : PlayableAsset {
    public BasePlayableEvent playableEvent;
    
    // Factory method that generates a playable based on this asset
    public override Playable CreatePlayable(PlayableGraph graph, GameObject go)
    {
        var playable = ScriptPlayable<TestPlayableBehaviour>.Create(graph);
			
        var behaviour = playable.GetBehaviour();
        //注意：如果是引用场景的对象，需要用
        //lightControlBehaviour.light = light.Resolve(graph.GetResolver());
        //lightControlBehaviour.color = color;
        //lightControlBehaviour.intensity = intensity;
        behaviour.playableEvent = playableEvent;
        
        return playable;
    }
}
