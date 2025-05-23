﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;
using System;
using Unity.Mathematics;
using System.Text;
using System.IO;

namespace Course
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

    struct Entry
    {
        public uint P_ID;
        public uint hash;
        public uint key;
    }

    public enum SearchType
    {
        Exhaustive,
        Neighborhood
    }

    public enum PressureType
    {
        Compressible,
        WeakCompressible
    }

    public abstract class FluidBase<T> : MonoBehaviour where T : struct
    {
        [SerializeField] protected int particleNum = 1024;           // パーティクルの個数．
        [SerializeField] protected float smoothlen = 0.15f;          // 粒子半径． 
        [SerializeField] protected float gasConstant = 10.0f;        // ガス定数．
        [SerializeField] protected float pressureStiffness = 200.0f; // 非圧縮性で計算する際利用する圧力項係数．
        [SerializeField] protected float restDensity = 1000.0f;      // 静止密度．
        [SerializeField] protected float particleMass = 0.02f;       // 粒子質量．
        [SerializeField] protected float viscosity = 1.04f;
        [SerializeField] protected float maxAllowableTimestep = 0.005f;
        [SerializeField] protected float wallStiffness = 3000.0f;
        [SerializeField] protected int iterations = 5;
        [SerializeField] protected Vector3 gravity = new Vector3(0f, -9.8f, 0f);
        [SerializeField] protected Vector3 range = new Vector3(6f, 6f, 6f);
        [SerializeField] protected bool drawSimulationGrid = true;
        [SerializeField] protected SearchType searchType = SearchType.Exhaustive;


        protected int numParticles;    // パーティクルの個数．
        protected float timeStep;      // 時間刻み幅．
        protected float poly6Coef;     // Poly6 カーネルの密度係数．            
        protected float spikyCoef;     // Spiky カーネルの密度係数．
        protected float viscosityCoef; // Laplacian カーネルの密度係数．

        // 圧力計算のオプション．弱圧縮性の式を実装した場合，ここをIncompressibleにする．
        private PressureType pressureType = PressureType.Compressible;

        #region Debug
        protected uint frame = 0;

        #endregion

        #region DirectConpute
        protected ComputeShader fluidCS;
        protected static readonly int THREAD_SIZE_X = 64;
        protected ComputeBuffer particlesBufferRead;
        protected ComputeBuffer particlesBufferWrite;
        protected ComputeBuffer particlesPressureBuffer;
        protected ComputeBuffer particlesDensityBuffer;
        protected ComputeBuffer particlesForceBuffer;
        protected ComputeBuffer keyBuffer;
        protected ComputeBuffer LUTBuffer;

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

        public SearchType SearchType
        {
            get { return searchType; }
        }
        #endregion

        #region MonoBehabior Functions
        protected virtual void Awake()
        {
            fluidCS = (ComputeShader)Resources.Load("Computes/SPH3D");
        }

        protected virtual void Start()
        {
            particleNum = CalculateNumParticles();
            numParticles = particleNum;
            InitBuffers();
        }

        protected virtual void Update()
        {
            timeStep = Mathf.Min(maxAllowableTimestep, Time.deltaTime);

            // NOTE : 
            // カーネル関数における係数の計算（3D）．
            // 参考：Muller et al. Particle-based fluid simulation for interactive applications, SCA, July 2003
            // https://dl.acm.org/doi/10.5555/846276.846298
            // poly6Coef = /*poly6カーネルの係数を計算*/;
            // spikyCoef = /*spikyカーネルの係数を計算*/;
            // viscosityCoef = /*viscosityカーネルの係数を計算*/;

            // シェーダ定数の転送．
            fluidCS.SetInt("_NumParticles", numParticles);
            fluidCS.SetFloat("_TimeStep", timeStep);
            fluidCS.SetFloat("_Smoothlen", smoothlen);
            fluidCS.SetFloat("_GasConstant", gasConstant);
            fluidCS.SetFloat("_PressureStiffness", pressureStiffness);
            fluidCS.SetFloat("_RestDensity", restDensity);
            fluidCS.SetFloat("_ParticleMass", particleMass);
            fluidCS.SetFloat("_Viscosity", viscosity);
            fluidCS.SetFloat("_Poly6Coef", poly6Coef);
            fluidCS.SetFloat("_SpikyCoef", spikyCoef);
            fluidCS.SetFloat("_ViscosityCoef", viscosityCoef);
            fluidCS.SetFloat("_WallStiffness", wallStiffness);
            fluidCS.SetVector("_Range", range);
            fluidCS.SetVector("_Gravity", gravity);

            AdditionalCSParams(fluidCS);

            // 複数回イテレーション．
            for (int i = 0; i < iterations; i++)
            {
                if (searchType == SearchType.Exhaustive)
                {
                    RunFluidSolver();
                } else if (searchType == SearchType.Neighborhood)
                {
                    RunFluidSolverWithNeighborhoodSearch();
                }
            }
            UpdateDensityMap(fluidCS);
            frame++;
        }

        private void OnDestroy()
        {
            DeleteBuffer(particlesBufferRead);
            DeleteBuffer(particlesBufferWrite);
            DeleteBuffer(particlesPressureBuffer);
            DeleteBuffer(particlesDensityBuffer);
            DeleteBuffer(particlesForceBuffer);
            DeleteBuffer(keyBuffer);
            DeleteBuffer(LUTBuffer);
        }
        #endregion Mono

        /// <summary>
        /// 粒子へのアクセスを高速化するためのルックアップテーブルを用意
        /// </summary>
        protected void CreateLookUpTable()
        {
            int kernelID = -1;
            int threadGroupX = numParticles / THREAD_SIZE_X + 1;

            // 密度の計算
            kernelID = fluidCS.FindKernel("RegisterHashCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_KeyBuffer", keyBuffer);
            fluidCS.SetBuffer(kernelID, "_LUTBuffer", LUTBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            Entry[] results = new Entry[numParticles];
            keyBuffer.GetData(results); // keyBufferからデータをリードバック
            Array.Sort(results, (a, b) => a.key.CompareTo(b.key)); // 読み込んだデータをソート
            keyBuffer.SetData(results); // ソート結果をアップロード

            kernelID = fluidCS.FindKernel("CalculateOffsetsCS");
            fluidCS.SetBuffer(kernelID, "_KeyBuffer", keyBuffer);
            fluidCS.SetBuffer(kernelID, "_LUTBuffer", LUTBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);
        }

        /// <summary>
        /// 流体シミュレーションメインルーチン
        /// </summary>
        private void RunFluidSolver()
        {
            int kernelID = -1;
            int threadGroupX = numParticles / THREAD_SIZE_X + 1;

            // 密度の計算
            kernelID = fluidCS.FindKernel("DensityCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferWrite", particlesDensityBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            if (pressureType == PressureType.Compressible)
            {
                // 圧縮性を仮定した圧力の計算
                kernelID = fluidCS.FindKernel("PressureCompressibleCS");
                fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
                fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferWrite", particlesPressureBuffer);
                fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);
            }
            else if (pressureType == PressureType.WeakCompressible)
            {
                // 非圧縮性を仮定した圧力の計算
                kernelID = fluidCS.FindKernel("PressureWeakCompressibleCS");
                fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
                fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferWrite", particlesPressureBuffer);
                fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);
            }

            // 圧力項・粘性項の計算
            kernelID = fluidCS.FindKernel("ForceCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferRead", particlesPressureBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferWrite", particlesForceBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // 外力項計算・位置更新
            kernelID = fluidCS.FindKernel("IntegrateCS");
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferRead", particlesForceBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferWrite", particlesBufferWrite);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // バッファの入れ替え
            SwapComputeBuffer(ref particlesBufferRead, ref particlesBufferWrite);
        }

        /// <summary>
        /// 流体シミュレーションメインルーチン（近傍探索版）
        /// </summary>
        private void RunFluidSolverWithNeighborhoodSearch()
        {
            CreateLookUpTable();

            int kernelID = -1;
            int threadGroupX = numParticles / THREAD_SIZE_X + 1;

            // 密度の計算
            kernelID = fluidCS.FindKernel("DensityNeighborCS");
            fluidCS.SetBuffer(kernelID, "_KeyBuffer", keyBuffer);
            fluidCS.SetBuffer(kernelID, "_LUTBuffer", LUTBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferWrite", particlesDensityBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            if (pressureType == PressureType.Compressible)
            {
                // 圧縮性を仮定した圧力の計算
                kernelID = fluidCS.FindKernel("PressureCompressibleCS");
                fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
                fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferWrite", particlesPressureBuffer);
                fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);
            }
            else if (pressureType == PressureType.WeakCompressible)
            {
                // 非圧縮性を仮定した圧力の計算
                kernelID = fluidCS.FindKernel("PressureWeakCompressibleCS");
                fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
                fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferWrite", particlesPressureBuffer);
                fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);
            }

            // 圧力項・粘性項の計算
            kernelID = fluidCS.FindKernel("ForceNeighborCS");
            fluidCS.SetBuffer(kernelID, "_KeyBuffer", keyBuffer);
            fluidCS.SetBuffer(kernelID, "_LUTBuffer", LUTBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesBufferRead", particlesBufferRead);
            fluidCS.SetBuffer(kernelID, "_ParticlesDensityBufferRead", particlesDensityBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesPressureBufferRead", particlesPressureBuffer);
            fluidCS.SetBuffer(kernelID, "_ParticlesForceBufferWrite", particlesForceBuffer);
            fluidCS.Dispatch(kernelID, threadGroupX, 1, 1);

            // 外力項計算・位置更新
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
        /// パーティクル数を求める．
        /// </summary>
        /// <param name="particles"></param>
        protected abstract int CalculateNumParticles();

        /// <summary>
        /// パーティクルの初期位置，初速を初期化．
        /// </summary>
        /// <param name="particles"></param>
        protected abstract void InitParticleData(ref T[] particles);

        /// <summary>
        /// 密度場計算用のメソッド
        /// </summary>
        /// <param name="shader"></param>
        protected virtual void UpdateDensityMap(ComputeShader shader) { }

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
            keyBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(uint3)));
            LUTBuffer = new ComputeBuffer(numParticles, Marshal.SizeOf(typeof(uint)));
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
