using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthCamera_ref : MonoBehaviour
{
    protected new Camera camera;
    public DepthTextureMode depthTextureMode;
    // Start is called before the first frame update
    void Start()
    {
        this.camera = GetComponent<Camera>();
        this.camera.depthTextureMode = this.depthTextureMode;
    }

    protected virtual void OnValidate()
    {
        if (this.camera != null)
        {
            this.camera.depthTextureMode = this.depthTextureMode;
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
