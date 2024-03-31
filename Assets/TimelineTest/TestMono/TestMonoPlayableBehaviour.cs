using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

// A behaviour that is attached to a playable
public class TestMonoPlayableBehaviour : PlayableBehaviour {
    public TestMonoEvent playableEvent;
    
    // Called when the owning graph starts playing
    public override void OnGraphStart(Playable playable)
    {
        /*if(bullet)
            Debug.Log($"OnGraphStart：{bullet.bulletType}");*/
    }

    // Called when the owning graph stops playing
    public override void OnGraphStop(Playable playable)
    {
        /*if(bullet)
            Debug.Log($"OnGraphStop：{bullet.bulletType}");*/
    }

    // Called when the state of the playable is set to Play
    public override void OnBehaviourPlay(Playable playable, FrameData info) {
        //Debug.Log($"OnBehaviourPlay");
        playableEvent?.StartEvent(info.deltaTime);
    }

    // Called when the state of the playable is set to Paused
    public override void OnBehaviourPause(Playable playable, FrameData info) {
        //Debug.Log($"OnBehaviourPause");
        playableEvent?.StopEvent();
    }

    // Called each frame while the state is set to Play
    public override void PrepareFrame(Playable playable, FrameData info)
    {
        /*int inputCount = playable.GetInputCount ();
        for (int i = 0; i < inputCount; i++)
        {
            // 拿到第n个滑条的数据
            var inputPlayable = (ScriptPlayable<TestPlayableBehaviour>)playable.GetInput(i);
            var input = inputPlayable.GetBehaviour();
        }*/
        
        /*if(bullet)
            Debug.Log($"PrepareFrame：{bullet.bulletType}");*/
    }
}
