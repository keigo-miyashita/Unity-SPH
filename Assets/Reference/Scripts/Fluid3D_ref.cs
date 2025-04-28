using UnityEngine;
using System.Runtime.InteropServices;
using static UnityEngine.EventSystems.EventTrigger;
using System.Drawing;
using Course;
using Unity.Mathematics;
using System.Text;
using System.IO;

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
        [SerializeField] private int densityMapResolution = 150;

        private static readonly int THREAD_SIZES = 8;
        private bool isMouseDown;
        private Vector4 hittedYPlanePos;
        private GameObject cube = null; // シミュレーション領域可視化用
        private RenderTexture densityMapTexture; // 密度場を格納する3Dテクスチャ
        private int3 densityMapSize; // 密度場のxyz方向の分割数

        #region Accessor
        public RenderTexture DensityMapTexture
        {
            get { return densityMapTexture; }
        }

        public int3 DensityMapSize
        {
            get { return densityMapSize; }
        }

        public int DensityMapResolution
        {
            get { return densityMapResolution; }
        }

        #endregion

        #region MonoBehabior Functions
        protected override void Awake()
        {
            base.Awake();
            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.position = new Vector3(range.x / 2, range.y / 2, range.z / 2);
            cube.transform.localScale = new Vector3(range.x, range.y, range.z);
            cube.GetComponent<MeshRenderer>().material = gridMaterial;
        }

        protected override void Start()
        {
            base.Start();
            InitDensityTex();
        }

        #endregion

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
        /// パーティクルの初期位置，初速を初期化．
        /// </summary>
        /// <param name="particles"></param>
        protected override void InitParticleData(ref FluidParticle[] particles)
        {
            if (initOption == InitOption.Sphere)
            {
                Vector3 position;
                for (int i = 0; i < NumParticles; i++)
                {
                    position = range / 2f + UnityEngine.Random.insideUnitSphere * ballRadius;  // 球形に粒子を初期化する．

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
                            //Debug.Log("position = " + particles[i].Position);
                            particles[i].Velocity = initVelocity;
                            //Debug.Log("i = " + i);
                            i++;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 密度バッファを初期化する
        /// </summary>
        /// <param name=""></param>
        void InitDensityTex()
        {
            float maxAxis = Mathf.Max(range.x, range.y, range.z);
            int width = Mathf.RoundToInt(range.x / maxAxis * densityMapResolution);
            int height = Mathf.RoundToInt(range.y / maxAxis * densityMapResolution);
            int depth = Mathf.RoundToInt(range.z / maxAxis * densityMapResolution);
            densityMapSize = new int3(width, height, depth);

            densityMapTexture = new RenderTexture(width, height, 0);
            densityMapTexture.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_SFloat;
            densityMapTexture.volumeDepth = depth;
            densityMapTexture.enableRandomWrite = true;
            densityMapTexture.dimension = UnityEngine.Rendering.TextureDimension.Tex3D;
            densityMapTexture.useMipMap = false;
            densityMapTexture.autoGenerateMips = false;
            densityMapTexture.Create();

            densityMapTexture.wrapMode = TextureWrapMode.Clamp;
            densityMapTexture.filterMode = FilterMode.Bilinear;
        }

        /// <summary>
        /// 密度場計算用のメソッド
        /// </summary>
        protected override void UpdateDensityMap(ComputeShader cs)
        {
            int kernelID = -1;

            CreateLookUpTable();

            // 密度の計算
            kernelID = cs.FindKernel("DensityMapCS");
            cs.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            cs.SetBuffer(kernelID, "_KeyBuffer", keyBuffer);
            cs.SetBuffer(kernelID, "_LUTBuffer", LUTBuffer);
            cs.SetTexture(kernelID, "_DensityMapTexure", densityMapTexture);
            cs.SetInts("_DensityMapSize", densityMapSize.x, densityMapSize.y, densityMapSize.z);
            cs.SetVector("_Range", range);
            cs.Dispatch(kernelID, densityMapSize.x / THREAD_SIZES + 1, densityMapSize.y / THREAD_SIZES + 1 , densityMapSize.z / THREAD_SIZES + 1);
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
            cs.SetInts("_DensityMapSize", densityMapSize.x, densityMapSize.y, densityMapSize.z);
        }
    }
}