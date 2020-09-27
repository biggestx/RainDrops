using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class CutomRendererFeature : ScriptableRendererFeature
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
    }

    public RainDropsSettings settings = new RainDropsSettings();

    class CustomRenderPass : ScriptableRenderPass
    {
        public Material blurMaterial;
        public int passes;
        public bool copyToFramebuffer;
        public string targetName;
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

            cmd.SetGlobalFloat("_offset", 1.5f);
            cmd.Blit(source, tmpRT1, blurMaterial);
            cmd.Blit(tmpRT1, source, blurMaterial);

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
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var src = renderer.cameraColorTarget;
        scriptablePass.Setup(src);
        renderer.EnqueuePass(scriptablePass);
    }
}


