using System.Collections;
using UnityEngine;

namespace Course
{
    [RequireComponent(typeof(Fluid3D))]
    public class FluidRender : MonoBehaviour
    {
        #region Parameters
        // 描画するオブジェクトのスケール
        public Vector3 ObjectScale = new Vector3(0.1f, 0.1f, 0.1f);
        #endregion

        #region Script References
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

        // GPUインスタンシングのための引数 (ComputeBuffer への転送用)
        // インスタンスあたりのインデックス数，インスタンス数，
        // 開始インデックス位置，ベース頂点位置，インスタンスの開始位置
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        // GPUインスタンシングのための引数バッファ
        ComputeBuffer argsBuffer;
        #endregion


        #region MonoBehabior Functions
        // Start is called before the first frame update
        void Start()
        {
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        }

        // Update is called once per frame
        void Update()
        {
            // メッシュをインスタンシング
            RenderInstancedMesh();
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
        #endregion
        
    }
}
