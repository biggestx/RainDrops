using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class Deferred2DRendererFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class RainDropsSettings
    {
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
        public Material blurMaterial = null;

        [Range(2, 15)]
        public int blurPasses = 1;

        [Range(1, 4)]
        public int downsample = 1;
        public bool copyToFramebuffer;
        public string targetName = "_blurTexture";

        public Vector3 TempPos;
        public Mesh SphereMesh;
        public Material SimpleMaterial;
    }

    public RainDropsSettings settings = new RainDropsSettings();

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blurMaterial;
        public int passes;
        public bool copyToFramebuffer;
        public string targetName;

        public Vector3 TempPos;
        public Mesh SphereMesh;
        public Material SimpleMaterial;


        string profilerTag;

        int tmpId1;
        int tmpId2;



        RenderTargetIdentifier tmpRT1;
        RenderTargetIdentifier tmpRT2;

        private RenderTargetIdentifier source { get; set; }

        public void Setup(RenderTargetIdentifier source)
        {
            this.source = source;
        }

        public CustomRenderPass(string profilerTag)
        {
            this.profilerTag = profilerTag;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var width = cameraTextureDescriptor.width;
            var height = cameraTextureDescriptor.height;

            tmpId1 = Shader.PropertyToID("tmpBlurRT1");
            tmpId2 = Shader.PropertyToID("tmpBlurRT2");
            cmd.GetTemporaryRT(tmpId1, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);
            cmd.GetTemporaryRT(tmpId2, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.ARGB32);

            tmpRT1 = new RenderTargetIdentifier(tmpId1);
            tmpRT2 = new RenderTargetIdentifier(tmpId2);

            ConfigureTarget(tmpRT1);
            ConfigureTarget(tmpRT2);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            CommandBuffer cmd = CommandBufferPool.Get(profilerTag);

            RenderTextureDescriptor opaqueDesc = renderingData.cameraData.cameraTargetDescriptor;
            opaqueDesc.depthBufferBits = 0;

            cmd.SetRenderTarget(tmpRT1);
            cmd.ClearRenderTarget(true, true, Color.black);
            cmd.BeginSample("DrawSphere");
            cmd.DrawMesh(SphereMesh, Matrix4x4.TRS(TempPos, Quaternion.identity, Vector3.one), SimpleMaterial);
            cmd.EndSample("DrawSphere");

            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.SetGlobalTexture("_DiffuseTex", tmpRT1);

            cmd.SetRenderTarget(tmpRT2);
            cmd.Blit(source, tmpRT2,blurMaterial);

            cmd.Blit(tmpRT2, source);

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();

            CommandBufferPool.Release(cmd);
        }

        public override void FrameCleanup(CommandBuffer cmd)
        {
        }
    }

    CustomRenderPass scriptablePass;

    public override void Create()
    {
        scriptablePass = new CustomRenderPass("RainDrops");
        scriptablePass.blurMaterial = settings.blurMaterial;
        scriptablePass.passes = settings.blurPasses;
        scriptablePass.targetName = settings.targetName;

        scriptablePass.renderPassEvent = settings.renderPassEvent;

        scriptablePass.TempPos = settings.TempPos;
        scriptablePass.SphereMesh = settings.SphereMesh;
        scriptablePass.SimpleMaterial = settings.SimpleMaterial;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var src = renderer.cameraColorTarget;
        scriptablePass.Setup(src);
        renderer.EnqueuePass(scriptablePass);
    }
}


