using Course;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Course
{
    public struct Vertex
    {
        public Vector4 position;
        public Vector4 normal;
    }

    public struct Triangle
    {
        public Vertex vertexA;
        public Vertex vertexB;
        public Vertex vertexC;
    }

    public enum RenderOption
    {
        Particle,
        MC
    };

    public enum RenderMaterial
    {
        Diffuse,
        Fresnel
    };
    [RequireComponent(typeof(Fluid3D))]
    public class FluidRender : MonoBehaviour
    {
        [SerializeField] RenderOption renderOption = RenderOption.Particle;
        [SerializeField] RenderMaterial renderMaterial = RenderMaterial.Diffuse;
        [SerializeField] Material diffuse;
        [SerializeField] Material fresnel;
        [SerializeField] Color col;
        [SerializeField] float isoLevel = -2;
        #region Parameters
        // 描画するオブジェクトのスケール
        public Vector3 ObjectScale = new Vector3(0.1f, 0.1f, 0.1f);
        #endregion

        #region Script Course
        // Boids スクリプトの参照
        public Fluid3D Solver;
        #endregion

        #region Built-in Resources
        // 描画するメッシュの参照
        public Mesh InstanceMesh;
        // 描画のためのマテリアルの参照
        public Material InstanceRenderMat;

        #endregion

        #region Private Valiables
        private static readonly int THREAD_SIZES = 8;
        // GPUインスタンシングのための引数 (ComputeBuffer への転送用)
        // インスタンスあたりのインデックス数，インスタンス数，
        // 開始インデックス位置，ベース頂点位置，インスタンスの開始位置
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // GPUインスタンシングのための引数バッファ
        ComputeBuffer particleArgsBuffer;

        ComputeShader marchingCubeCS;
        ComputeBuffer lutBuffer;
        ComputeBuffer triangleBuffer;
        ComputeBuffer renderArgsBuffer;
        #endregion


        #region MonoBehabior Functions
        void Start()
        {
            particleArgsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            marchingCubeCS = Resources.Load<ComputeShader>("Computes/MarchingCube");
            string lutString = Resources.Load<TextAsset>("Texts/MarchingCubesLUT").text;
            int[] lutValus = lutString.Trim().Split(',').Select(x => int.Parse(x)).ToArray();
            lutBuffer = new ComputeBuffer(lutValus.Length, Marshal.SizeOf(typeof(int)));
            lutBuffer.SetData(lutValus);
            CreateTriangleBuffer(Solver.DensityMapResolution);
            renderArgsBuffer = new ComputeBuffer(5, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
        }

        void Update()
        {
            if (Solver.SearchType == SearchType.Exhaustive)
            {
                renderOption = RenderOption.Particle;
            }
            if (renderOption == RenderOption.Particle)
            {
                // メッシュをインスタンシング
                RenderParticleMesh();
            }
            else if (renderOption == RenderOption.MC)
            {
                RunMarchingCube();
                RenderIsoMesh();
            }
        }

        void OnDisable()
        {
            // 引数バッファを開放
            if (particleArgsBuffer != null)
                particleArgsBuffer.Release();
            particleArgsBuffer = null;
        }
        #endregion

        #region Private Functions
        /// <summary>
        /// 粒子で可視化する関数
        /// </summary>
        void RenderParticleMesh()
        {
            if (InstanceRenderMat == null || Solver == null || !SystemInfo.supportsInstancing)
                return;

            uint numIndecies = (InstanceMesh != null) ? (uint)InstanceMesh.GetIndexCount(0) : 0;
            args[0] = numIndecies;
            args[1] = (uint)Solver.NumParticles;
            particleArgsBuffer.SetData(args);

            // Boidデータを格納したバッファをマテリアルにセット
            InstanceRenderMat.SetBuffer("_ParticleDataBuffer", Solver.ParticlesBufferRead);
            // Boidオブジェクトスケールをセット
            InstanceRenderMat.SetVector("_ObjectScale", ObjectScale);
            // 境界領域を定義
            var bounds = new Bounds
            (
                Vector3.zero,
                Solver.GetSimulationAreaSize()
            );
            // メッシュをインスタンシングして描画
            Graphics.DrawMeshInstancedIndirect
            (
                InstanceMesh,
                0,
                InstanceRenderMat,
                bounds,
                particleArgsBuffer
            );
        }

        /// <summary>
        /// マーチングキューブを実行する関数
        /// </summary>
        void RunMarchingCube()
        {
            triangleBuffer.SetCounterValue(0);
            int kernelID = -1;

            int numVoxelsPerX = Solver.DensityMapSize.x - 1;
            int numVoxelsPerY = Solver.DensityMapSize.y - 1;
            int numVoxelsPerZ = Solver.DensityMapSize.z - 1;

            kernelID = marchingCubeCS.FindKernel("MarchingCubeCS");

            marchingCubeCS.SetInts("_DensityMapSize", Solver.DensityMapSize.x, Solver.DensityMapSize.y, Solver.DensityMapSize.z);
            marchingCubeCS.SetFloat("_IsoLevel", isoLevel);
            marchingCubeCS.SetVector("_Range", Solver.GetSimulationAreaSize());

            marchingCubeCS.SetBuffer(kernelID, "_TrianglesBuffer", triangleBuffer);
            marchingCubeCS.SetBuffer(kernelID, "_LutBuffer", lutBuffer);
            marchingCubeCS.SetTexture(kernelID, "_DensityMapTexture", Solver.DensityMapTexture);

            marchingCubeCS.Dispatch(kernelID, numVoxelsPerX / THREAD_SIZES + 1, numVoxelsPerY / THREAD_SIZES + 1, numVoxelsPerZ / THREAD_SIZES + 1);
        }

        /// <summary>
        /// 生成したメッシュを描画する関数
        /// </summary>
        void RenderIsoMesh()
        {
            if (renderMaterial == RenderMaterial.Diffuse)
            {
                diffuse.SetBuffer("_VertexBuffer", triangleBuffer);
                diffuse.SetColor("col", col);
            }
            else
            {
                fresnel.SetBuffer("_VertexBuffer", triangleBuffer);
            }

            int kernelID = -1;
            kernelID = marchingCubeCS.FindKernel("SetRenderArgsCS");
            marchingCubeCS.SetBuffer(kernelID, "_RenderArgsBuffer", renderArgsBuffer);
            marchingCubeCS.SetBuffer(kernelID, "_TrianglesBuffer", triangleBuffer);

            // 境界領域を定義
            Vector3 center = Solver.GetSimulationAreaSize() * 0.5f;
            var bounds = new Bounds
            (
                center,
                Solver.GetSimulationAreaSize()
            );

            ComputeBuffer.CopyCount(triangleBuffer, renderArgsBuffer, 0);
            marchingCubeCS.Dispatch(kernelID, 1, 1, 1);
            if (renderMaterial == RenderMaterial.Diffuse)
            {
                Graphics.DrawProceduralIndirect(diffuse, bounds, MeshTopology.Triangles, renderArgsBuffer);
            } else
            {
                Graphics.DrawProceduralIndirect(fresnel, bounds, MeshTopology.Triangles, renderArgsBuffer);
            }
        }

        void CreateTriangleBuffer(int resolution)
        {
            int numVoxlesPerAxis = resolution - 1;
            int numVoxels = numVoxlesPerAxis * numVoxlesPerAxis * numVoxlesPerAxis;
            // マーチングキューブ法では1セルに対して最大5ポリゴンだから？
            int maxTriangleCount = numVoxels * 5;
            triangleBuffer = new ComputeBuffer(maxTriangleCount, Marshal.SizeOf(typeof(Vertex)), ComputeBufferType.Append);
            triangleBuffer.SetCounterValue(0);
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

        private void OnDestroy()
        {
            DeleteBuffer(lutBuffer);
            DeleteBuffer(triangleBuffer);
            DeleteBuffer(renderArgsBuffer);
        }
        #endregion

    }
}
