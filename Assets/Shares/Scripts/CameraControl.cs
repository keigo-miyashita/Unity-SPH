using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Reference
{
    public class CameraControl : MonoBehaviour
    {
        private Camera cam;
        private Vector3 startPos;
        private Vector3 startAngle;

        // Start is called before the first frame update
        void Start()
        {
            cam = GetComponent<Camera>();
        }

        // Update is called once per frame
        void Update()
        {
            if (cam == null)
                return;

            float sensitiveRotate = 5f;
            float sensitiveZoom = 10f;

            if (Input.GetMouseButton(1))
            {
                float rotateX = Input.GetAxis("Mouse X") * sensitiveRotate;
                float rotateY = Input.GetAxis("Mouse Y") * sensitiveRotate;
                cam.transform.RotateAround(new Vector3(2, 2, 2), Vector3.up, rotateX);
                cam.transform.RotateAround(new Vector3(2, 2, 2), cam.transform.right, rotateY);
            }

            float moveZ = Input.GetAxis("Mouse ScrollWheel") * sensitiveZoom;
            cam.transform.position += cam.transform.forward * moveZ;
        }
    }

}
