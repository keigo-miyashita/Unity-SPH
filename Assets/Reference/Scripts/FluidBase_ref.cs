using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;


namespace Reference
{
    public struct FluidParticle
    {
        public Vector4 Position;
        public Vector4 Velocity;
    }
    // 
    public enum NumParticleEnum
    {
        NUM_1K = 1024,
        NUM_4K = 1024 * 4,
        NUM_16K = 1024 * 16
    }

    // 構造体の定義．
    // コンピュートシェーダ側とバイト幅を合わせる．
    struct FluidParticleDensity
    {
        public float Density;
    }

    struct FluidParticlePressure
    {
        public float pressure;
    }

    struct FluidParticleForce
    {
        public Vector4 Acceleration;
    }

    public abstract class FluidBase_ref<T> : MonoBehaviour where T : struct
    {
        [SerializeField] protected NumParticleEnum particleNum = NumParticleEnum.NUM_16K;   // パーティクルの個数．
        [SerializeField] protected float smoothlen = 0.5f;                               // 粒子半径． 
        [SerializeField] private float pressureStiffness = 200.0f;                          // 圧力項係数．
        [SerializeField] protected float restDensity = 1000.0f;                             // 静止密度．
        [SerializeField] protected float particleMass = 0.0002f;                           // 粒子質量．
        [SerializeField] protected float viscosity = 0.1f;
        [SerializeField] protected float maxAllowableTimestep = 0.005f;
        [SerializeField] protected float wallStiffness = 3000.0f;
        [SerializeField] protected int iterations = 4;
        [SerializeField] protected Vector3 gravity = new Vector3(0f, -9.8f, 0f);
        [SerializeField] protected Vector3 range = new Vector3(1f, 1f, 1f);

        private int numParticles;                                                           // パーティクルの個数．
        private float timeStep;                                                             // 時間刻み幅．
        private float densityCoef;                                                          // Poly6 カーネルの密度係数．            
        private float gradPressureCoef;                                                     // Spiky カーネルの密度係数．
        private float lapViscosityCoef;                                                     // Laplacian カーネルの密度係数．

        #region DirectConpute
        private ComputeShader fluidCS;
        private static readonly int THREAD_SIZE_X = 64;
        private ComputeBuffer particlesBufferRead;
        private ComputeBuffer particlesBufferWrite;
        private ComputeBuffer particlesPressureBuffer;
        private ComputeBuffer particlesDensityBuffer;
        private ComputeBuffer particlesForceBuffer;

        #endregion

        #region Accessor
        public int NumParticles
        {
            get { return numParticles; }
        }
        public ComputeBuffer ParticlesBufferRead
        {
            get { return particlesBufferRead; }
        }
        public Vector3 GetSimulationAreaSize()
        {
            return this.range;
        }
        #endregion

        #region Mono
        protected virtual void Awake()
        {
            fluidCS = (ComputeShader)Resources.Load("SPH3D_ref");
            numParticles = (int)particleNum;
        }

        protected virtual void Start()
        {
            InitBuffers();
        }

        private void Update()
        {
            timeStep = Mathf.Min(maxAllowableTimestep, Time.deltaTime);

            // カーネル関数における係数の計算（3D）．
            // 参考：Muller et al. Particle-based fluid simulation for interactive applications, SCA, July 2003
            // https://dl.acm.org/doi/10.5555/846276.846298
            densityCoef = particleMass * 315f / (64f * Mathf.PI * Mathf.Pow(smoothlen, 9));
            gradPressureCoef = particleMass * -45f / (Mathf.PI * Mathf.Pow(smoothlen, 6));
            lapViscosityCoef = particleMass * 45f / (Mathf.PI * Mathf.Pow(smoothlen, 6));

            // シェーダ定数の転送．
            fluidCS.SetInt("_NumParticles", numParticles);
            fluidCS.SetFloat("_TimeStep", timeStep);
            fluidCS.SetFloat("_Smoothlen", smoothlen);
            fluidCS.SetFloat("_PressureStiffness", pressureStiffness);
            fluidCS.SetFloat("_RestDensity", restDensity);
            fluidCS.SetFloat("_Viscosity", viscosity);
            fluidCS.SetFloat("_DensityCoef", densityCoef);
            fluidCS.SetFloat("_GradPressureCoef", gradPressureCoef);
            fluidCS.SetFloat("_LapViscosityCoef", lapViscosityCoef);
            fluidCS.SetFloat("_WallStiffness", wallStiffness);
            fluidCS.SetVector("_Range", range);
            fluidCS.SetVector("_Gravity", gravity);

            AdditionalCSParams(fluidCS);

            // 複数回イテレーション．
            for (int i = 0; i < iterations; i++)
            {
                RunFluidSolver();
            }

            
        }

        private void OnDestroy()
        {
            DeleteBuffer(particlesBufferRead);
            DeleteBuffer(particlesBufferWrite);
            DeleteBuffer(particlesPressureBuffer);
            DeleteBuffer(particlesDensityBuffer);
            DeleteBuffer(particlesForceBuffer);
        }
        #endregion Mono

        /// <summary>
        /// 流体シミュレーションメインルーチン
        /// </summary>
        private void RunFluidSolver()
        {
            int kernelID = -1;
            int threadGroupX = numParticles / THREAD_SIZE_X;

            // 密度の計算
            kernelID = fluidCS.FindKernel("DensityCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferWrite", particlesDensityBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // 圧力の計算
            kernelID = fluidCS.FindKernel("PressureCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferWrite", particlesPressureBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // 外力の計算
            kernelID = fluidCS.FindKernel("ForceCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferRead", particlesPressureBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferWrite", particlesForceBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // 位置更新
            kernelID = fluidCS.FindKernel("IntegrateCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferRead", particlesForceBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferWrite", particlesBufferWrite);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // バッファの入れ替え
            SwapComputeBuffer(ref particlesBufferRead, ref particlesBufferWrite);
        }

        /// <summary>
        /// 子クラスでシェーダ定数の転送を追加する場合はこのメソッドを利用する．
        /// </summary>
        /// <param name="shader"></param>
        protected virtual void AdditionalCSParams(ComputeShader shader) { }

        /// <summary>
        /// パーティクルの初期位置と初速の設定．
        /// </summary>
        /// <param name="particles"></param>
        protected abstract void InitParticleData(ref T[] particles);

        /// <summary>
        /// バッファの初期化
        /// </summary>
        private void InitBuffers()
        {
            particlesBufferRead = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(T)));
            var particles = new T[numParticles];
            InitParticleData(ref particles);
            particlesBufferRead.SetData(particles);
            particles = null;

            particlesBufferWrite = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(T)));
            particlesPressureBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticlePressure)));
            particlesDensityBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticleDensity)));
            particlesForceBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(FluidParticleForce)));

        }

        /// <summary>
        /// 引数に指定されたバッファの入れ替え
        /// </summary>
        private void SwapComputeBuffer(ref ComputeBuffer ping, ref ComputeBuffer pong)
        {
            ComputeBuffer temp = ping;
            ping = pong;
            pong = temp;
        }

        /// <summary>
        /// バッファの開放
        /// </summary>
        /// <param name="buffer"></param>
        private void DeleteBuffer(ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }
    }
}
