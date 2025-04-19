using Course;
using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SocialPlatforms;

namespace Reference
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
    [RequireComponent(typeof(Fluid3D_ref))]
    public class FluidRender_ref : MonoBehaviour
    {
        [SerializeField] RenderOption renderOption = RenderOption.Particle;
        [SerializeField] Material fluidMat;
        [SerializeField] Color col;
        [SerializeField] float isoLevel = -2;
        #region Parameters
        // 描画するオブジェクトのスケール
        public Vector3 ObjectScale = new Vector3(0.1f, 0.1f, 0.1f);
        #endregion

        #region Script References
        // Boids スクリプトの参照
        public Fluid3D_ref Solver;
        #endregion

        #region Built-in Resources
        // 描画するメッシュの参照
        public Mesh InstanceMesh;
        // 描画のためのマテリアルの参照
        public Material InstanceRenderMat;
        private static readonly int THREAD_SIZES = 8;
        #endregion

        #region Private Valiables

        // GPUインスタンシングのための引数 (ComputeBuffer への転送用)
        // インスタンスあたりのインデックス数，インスタンス数，
        // 開始インデックス位置，ベース頂点位置，インスタンスの開始位置
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // GPUインスタンシングのための引数バッファ
        ComputeBuffer argsBuffer;

        ComputeShader marchingCubeCS;

        ComputeBuffer lutBuffer;
        ComputeBuffer triangleBuffer;
        ComputeBuffer renderArgsBuffer;
        ComputeBuffer debugDensityMapBuffer;
        #endregion


        #region MonoBehabior Functions
        // Start is called before the first frame update
        void Start()
        {
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

            marchingCubeCS = Resources.Load<ComputeShader>("MarchingCube_ref");
            string lutString = Resources.Load<TextAsset>("MarchingCubesLUT").text;
            Debug.Log(lutString);
            int[] lutValus = lutString.Trim().Split(',').Select(x => int.Parse(x)).ToArray();
            lutBuffer = new ComputeBuffer(lutValus.Length, Marshal.SizeOf(typeof(int)));
            lutBuffer.SetData(lutValus);
            CreateTriangleBuffer(Solver.densityMapResolution);
            renderArgsBuffer = new ComputeBuffer(5, Marshal.SizeOf(typeof(int)), ComputeBufferType.IndirectArguments);
            debugDensityMapBuffer = new ComputeBuffer(Solver.densityMapResolution * Solver.densityMapResolution, Marshal.SizeOf(typeof(float)));
        }

        // Update is called once per frame
        void Update()
        {
            if (renderOption == RenderOption.Particle)
            {
                // メッシュをインスタンシング
                RenderInstancedMesh();
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
            if (argsBuffer != null)
                argsBuffer.Release();
            argsBuffer = null;
        }
        #endregion

        #region Private Functions
        void RenderInstancedMesh()
        {
            if (InstanceRenderMat == null || Solver == null || !SystemInfo.supportsInstancing)
                return;

            uint numIndecies = (InstanceMesh != null) ? (uint)InstanceMesh.GetIndexCount(0) : 0;
            args[0] = numIndecies;
            args[1] = (uint)Solver.NumParticles;
            argsBuffer.SetData(args);

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
                argsBuffer
            );
        }

        void RunMarchingCube()
        {
            triangleBuffer.SetCounterValue(0);
            int kernelID = -1;

            int numVoxelsPerX = Solver.densityMapTexture.width - 1;
            int numVoxelsPerY = Solver.densityMapTexture.height - 1;
            int numVoxelsPerZ = Solver.densityMapTexture.volumeDepth - 1;

            kernelID = marchingCubeCS.FindKernel("MarchingCubeCS");

            marchingCubeCS.SetInts("_DensityMapSize", Solver.densityMapTexture.width, Solver.densityMapTexture.height, Solver.densityMapTexture.volumeDepth);
            marchingCubeCS.SetFloat("_IsoLevel", isoLevel);
            marchingCubeCS.SetVector("_Range", Solver.range);

            marchingCubeCS.SetBuffer(kernelID, "_TrianglesBuffer", triangleBuffer);
            marchingCubeCS.SetBuffer(kernelID, "_LutBuffer", lutBuffer);
            marchingCubeCS.SetTexture(kernelID, "_DensityMapTexture", Solver.densityMapTexture);
            marchingCubeCS.SetBuffer(kernelID, "_DebugDensityMapBuffer", debugDensityMapBuffer);
            //marchingCubeCS.SetFloats("_Range", Solver.range.x, Solver.range.y, Solver.range.z);
            //marchingCubeCS.SetVector("_Range", new Vector3(Solver.range.x, Solver.range.y, Solver.range.z));
            //marchingCubeCS.SetVector("_Range", new Vector4(Solver.range.x, Solver.range.y, Solver.range.z, 1.0f));
            //marchingCubeCS.SetInts("_DensityMapSize", Solver.densityMapTexture.width, Solver.densityMapTexture.height, Solver.densityMapTexture.volumeDepth);
            //marchingCubeCS.SetFloat("_IsoLevel", isoLevel);
            //marchingCubeCS.SetVector("_Range", Solver.range);
            //marchingCubeCS.SetFloats("_Range", Solver.range.x, Solver.range.y, Solver.range.z);

            marchingCubeCS.Dispatch(kernelID, numVoxelsPerX / THREAD_SIZES + 1, numVoxelsPerY / THREAD_SIZES + 1, numVoxelsPerZ / THREAD_SIZES + 1);
        }

        void RenderIsoMesh()
        {
            fluidMat.SetBuffer("_VertexBuffer", triangleBuffer);
            fluidMat.SetColor("col", col);

            int kernelID = -1;
            kernelID = marchingCubeCS.FindKernel("SetRenderArgsCS");
            marchingCubeCS.SetBuffer(kernelID, "_RenderArgsBuffer", renderArgsBuffer);
            marchingCubeCS.SetBuffer(kernelID, "_TrianglesBuffer", triangleBuffer);

            // 境界領域を定義
            Vector3 center = Solver.GetSimulationAreaSize() * 0.5f;
            var bounds = new Bounds
            (
                center,
                Solver.GetSimulationAreaSize() * 10f
            );

            ComputeBuffer.CopyCount(triangleBuffer, renderArgsBuffer, 0);
            marchingCubeCS.Dispatch(kernelID, 1, 1, 1);
            Graphics.DrawProceduralIndirect(fluidMat, bounds, MeshTopology.Triangles, renderArgsBuffer);
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
            DeleteBuffer(debugDensityMapBuffer);
        }
        #endregion

    }
}
