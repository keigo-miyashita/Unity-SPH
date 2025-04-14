using UnityEngine;
using System.Runtime.InteropServices;
using static UnityEngine.EventSystems.EventTrigger;
using System.Drawing;
using Course;

namespace Reference
{

    public enum InitOption
    {
        Sphere,
        Cube
    };
    public class Fluid3D_ref: FluidBase_ref<FluidParticle>
    {
        [SerializeField] private InitOption initOption = InitOption.Cube;
        [SerializeField] protected float initDensity = 1000.0f;
        [SerializeField] private float ballRadius = 0.5f;           // 粒子位置初期化時の球半径．
        [SerializeField] protected Vector3 initBoxRegion = new Vector3(3.0f, 3.0f, 3.0f);
        [SerializeField] protected Vector3 initBoxCenter = new Vector3(0.1f, 0.1f, 0.1f);
        [SerializeField] private float MouseInteractionRadius = 1f; // マウスインタラクションの範囲の広さ．
        [SerializeField] private float jitterStrength = 0.035f;
        [SerializeField] private Vector3 initVelocity = new Vector3(0f, 0f, 0f);
        [SerializeField] private Material gridMaterial = null;
        [SerializeField] private int particlesPerAxis = 0;
        

        private bool isMouseDown;
        private Vector4 hittedYPlanePos;
        //private int particlesPerAxis;
        private GameObject cube = null;

        protected override void Awake()
        {
            base.Awake();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(range.x / 2, range.y / 2, range.z / 2);
            cube.transform.localScale = new Vector3(range.x, range.y, range.z);
            cube.GetComponent<MeshRenderer>().material = gridMaterial;
        }

        /// <summary>
        /// パーティクル数を求める．
        /// </summary>
        /// <param name="particles"></param>
        protected override int CalculateNumParticles()
        {
            int targetParticleCount = 0;
            if (initOption == InitOption.Sphere)
            {
                targetParticleCount = (int)(4 / 3 * Mathf.PI * initDensity);
            } else if (initOption == InitOption.Cube)
            {
                targetParticleCount = (int)(initBoxRegion.x * initBoxRegion.y * initBoxRegion.z * initDensity);
                particlesPerAxis = (int)System.Math.Cbrt(targetParticleCount);
                targetParticleCount = particlesPerAxis * particlesPerAxis * particlesPerAxis;
            }

            return targetParticleCount;
        }

        /// <summary>
        /// パーティクルの初期位置を球形に，初速を0に初期化．
        /// </summary>
        /// <param name="particles"></param>
        protected override void InitParticleData(ref FluidParticle[] particles)
        {
            if (initOption == InitOption.Sphere)
            {
                Vector3 position;
                for (int i = 0; i < NumParticles; i++)
                {
                    position = range / 2f + Random.insideUnitSphere * ballRadius;  // 球形に粒子を初期化する．

                    particles[i].Position = new Vector4(position.x, position.y, position.z, 0f);
                    particles[i].Velocity = Vector4.zero;

                }
            }
            else if (initOption == InitOption.Cube)
            {
                int i = 0;
                for (int x = 0; x < particlesPerAxis; x++)
                {
                    for (int y = 0; y < particlesPerAxis; y++)
                    {
                        for (int z = 0; z < particlesPerAxis; z++)
                        {
                            float tx = x / (particlesPerAxis - 1f);
                            float ty = y / (particlesPerAxis - 1f);
                            float tz = z / (particlesPerAxis - 1f);

                            float px = (tx - 0.5f) * initBoxRegion.x + initBoxRegion.x / 2 + initBoxCenter.x;
                            float py = (ty - 0.5f) * initBoxRegion.y + initBoxRegion.y / 2 + initBoxCenter.y;
                            float pz = (tz - 0.5f) * initBoxRegion.z + initBoxRegion.z / 2 + initBoxCenter.z;
                            Vector3 jitter = UnityEngine.Random.insideUnitSphere * jitterStrength;
                            particles[i].Position = new Vector3(px, py, pz) + jitter;
                            particles[i].Velocity = initVelocity;
                            //Debug.Log("i = " + i);
                            i++;
                        }
                    }
                }
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
