using UnityEngine;
using System.Runtime.InteropServices;

namespace Course
{


    public class Fluid3D: FluidBase<FluidParticle>
    {
        [SerializeField] private float ballRadius = 0.5f;           // 粒子位置初期化時の球半径．
        [SerializeField] private float MouseInteractionRadius = 1f; // マウスインタラクションの範囲の広さ．

        private bool isMouseDown;
        private Vector4 hittedYPlanePos;

        /// <summary>
        /// パーティクル初期位置の設定
        /// </summary>
        /// <param name="particles"></param>
        protected override void InitParticleData(ref FluidParticle[] particles)
        {
            Vector3 position;
            for (int i = 0; i < NumParticles; i++)
            {
                position = range / 2f + Random.insideUnitSphere * ballRadius;  // 球形に粒子を初期化する．
                
                particles[i].Position = new Vector4(position.x, position.y, position.z, 0f);
                particles[i].Velocity = Vector4.zero;

            }
        }

        /// <summary>
        /// ComputeShader の定数バッファに追加する
        /// </summary>
        /// <param name="cs"></param>
        protected override void AdditionalCSParams(ComputeShader cs)
        {
            hittedYPlanePos = Vector4.zero;
            if (Input.GetMouseButtonDown(0))
            {
                isMouseDown = true;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isMouseDown = false;
            }

            if (isMouseDown)
            {
                Vector3 mousePos = Input.mousePosition;
                Ray ray = Camera.main.ScreenPointToRay(mousePos);
                float t = -ray.origin.y / ray.direction.y;
                hittedYPlanePos = ray.origin + t * ray.direction;
                Debug.Log(hittedYPlanePos);
            }

            cs.SetVector("_MousePos", hittedYPlanePos);
            cs.SetFloat("_MouseRadius", MouseInteractionRadius);
            cs.SetBool("_MouseDown", isMouseDown);
        }

    }
}
