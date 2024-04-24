using UnityEngine;

public class SetupProjectorShadow : MonoBehaviour {
	public Material material;
	public ProjectorShadow projectorShadow;
	public bool batcher;
	
    // Start is called before the first frame update
    void Start()
    {
        projectorShadow = new ProjectorShadow ();
        ProjectorShadow.ProjectorConfig config = new ProjectorShadow.ProjectorConfig
        {
	        ShadowTexHeight = Screen.height,
	        ShadowTexWidth = Screen.width,
	        CameraNearClipPlane = 0.3f,
	        CameraFarClipPlane = 10000f,
	        Orthographic = true,
	        CameraSize = 15,
	        ProjectorIgnoreLayers = 1 << LayerMask.NameToLayer("GenerateShadow"),
	        ProjectorPos = new Vector3(10, 100, 10),
	        ShadowCameraPos = new Vector3(10, 100, 10),
	        ProjectorRotate = Quaternion.Euler(new Vector3(90, 0, 0)),
	        ShadowCameraRotate = Quaternion.Euler(new Vector3(90, 0, 0)),
	        ShadowCameraLayerMask = 1 << LayerMask.NameToLayer("GenerateShadow"),
	        ShadowIntensity = 0.6f,
	        Batcher = batcher
        };
        projectorShadow.Init (config, material);

        projectorShadow.EnableShadow();
    }
}
