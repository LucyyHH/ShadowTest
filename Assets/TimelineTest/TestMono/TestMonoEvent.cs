using UnityEngine;
using UnityEngine.Events;

public class TestMonoEvent : Object {
    public UnityEvent<float> startEvent;
    public UnityEvent stopEvent;

    public void StartEvent(float duration) {
        startEvent?.Invoke(duration);
    }
    
    public void StopEvent() {
        stopEvent?.Invoke();
    }
}
