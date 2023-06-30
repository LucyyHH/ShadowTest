using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    // Start is called before the first frame update
    void Start()
    {
        var m_ProjectorShadow = new ProjectorShadow ();
        ProjectorShadow.ProjectorConfig config = new ProjectorShadow.ProjectorConfig
        {
	        ShadowTexHeight = Screen.height,
	        ShadowTexWidth = Screen.width,
	        CameraNearClipPlane = 0.3f,
	        CameraFarClipPlane = 10000f,
	        Orthographic = true,
	        CameraSize = 10,
	        ProjectorIgnoreLayers = (1 << LayerMask.NameToLayer("Fish3")) +
	                                (1 << LayerMask.NameToLayer("Fish2")) +
	                                (1 << LayerMask.NameToLayer("Fish1")) +
	                                (1 << LayerMask.NameToLayer("GameFishWithLight")),
	        ProjectorPos = new Vector3(0, -40f, 2.6f),
	        ShadowCameraPos = new Vector3(0, -40f, 2.6f),
	        ProjectorRotate = Quaternion.Euler(new Vector3(90, 0, 0)),
	        ShadowCameraRotate = Quaternion.Euler(new Vector3(90, 0, 0)),
	        ShadowIntensity = 0.6f,
        };
        m_ProjectorShadow.Init (config);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
