using System.Collections.Generic;
using UnityEngine;

namespace ShadowTest {
    public interface IGenerateComplete {
        public void Callback(List<GameObject> gameObjects);
    }
}