using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace TrackExample
{
    [TrackClipType(typeof(TestMonoPlayableAsset))] //  数据对象
    public class TestMonoControlTrack : TrackAsset {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            return ScriptPlayable<TestPlayableBehaviour>.Create(graph, inputCount);
        }
    }	// 继承 TrackAsset 
}