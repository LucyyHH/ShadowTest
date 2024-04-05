using System.Collections.Generic;
using ShadowTest;
using UnityEngine;

public class GenerateComplete : MonoBehaviour, IGenerateComplete
{
    public void Callback(List<GameObject> gameObjects) {
        if(gameObjects == null || gameObjects.Count == 0) {
            return;
        }

        var shadowRenderer = GetComponent<SetupProjectorShadow>().projectorShadow.ShadowRenderer;
        shadowRenderer.Clear();
        foreach(var go in gameObjects) {
            foreach(var componentsInChild in go.transform.GetComponentsInChildren<Renderer>()) {
                shadowRenderer.AddLast(componentsInChild);
            }
        }
    }
}
