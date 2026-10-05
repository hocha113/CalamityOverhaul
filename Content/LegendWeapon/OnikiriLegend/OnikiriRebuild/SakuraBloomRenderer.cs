using System;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Prepare before the scene; composite later without changing its render target.</summary>
public sealed class SakuraBloomRenderer : IDisposable
{
    public const int Padding = 32;
    private readonly GraphicsDevice device;
    private readonly SpriteBatch batch;
    private readonly Effect blur;
    private readonly EffectParameter sampleStep;
    private readonly BlendState compositeBlend;
    private readonly RenderTargetBinding[][] restoreTargets = [null, new RenderTargetBinding[1],
        new RenderTargetBinding[2], new RenderTargetBinding[3], new RenderTargetBinding[4]];
    private RenderTarget2D source, temporary;
    private bool ready;
    private int sceneWidth, sceneHeight;
    public float Strength { get; set; } = .85f;
    public int DrawCalls { get; private set; }
    public int BufferWidth => source?.Width ?? 0;
    public int BufferHeight => source?.Height ?? 0;

    public SakuraBloomRenderer(GraphicsDevice device, Effect blurEffect)
    {
        this.device = device;
        blur = blurEffect.Clone();
        sampleStep = blur.Parameters["SampleStep"];
        batch = new SpriteBatch(device);
        compositeBlend = new BlendState {
            ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
            AlphaSourceBlend = Blend.Zero, AlphaDestinationBlend = Blend.One
        };
    }

    public void Reset()
    {
        ready = false;
        DrawCalls = 0;
    }

    public void Prepare(SakuraParticleRenderer particles, SakuraParticleWorld world,
        Vector2 camera, int width, int height)
    {
        Reset();
        if (width <= 0 || height <= 0 || world.Count == 0 || Strength <= 0) return;
        // Source stays in camera space. The final game transform is applied once at composite time.
        if (!particles.HasBloomSource(world, camera, width, height, Padding)) return;
        int targetCount = device.GetRenderTargetsNoAllocEXT(null);
        RenderTargetBinding[] targets = restoreTargets[targetCount];
        if (targetCount > 0) device.GetRenderTargetsNoAllocEXT(targets);
        Viewport viewport = device.Viewport;
        Rectangle scissor = device.ScissorRectangle;
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState rasterizer = device.RasterizerState;
        Texture texture = device.Textures[0];
        SamplerState sampler = device.SamplerStates[0];
        try
        {
            EnsureTargets(width, height);
            device.Textures[0] = null;
            device.SetRenderTarget(source);
            device.Clear(Color.Transparent);
            Matrix sourceTransform = Matrix.CreateTranslation(Padding, Padding, 0)
                * Matrix.CreateScale(source.Width / (float)(width + Padding * 2), source.Height / (float)(height + Padding * 2), 1);
            particles.DrawBloomSource(world, camera, sourceTransform, source.Width, source.Height);
            DrawCalls = particles.SourceDrawCalls;
            Blur(source, temporary, new Vector2(1f / source.Width, 0));
            Blur(temporary, source, new Vector2(0, 1f / source.Height));
            ready = true;
        }
        finally
        {
            device.Textures[0] = null;
            device.SetRenderTargets(targets);
            device.Viewport = viewport;
            device.ScissorRectangle = scissor;
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = rasterizer;
            device.Textures[0] = texture;
            device.SamplerStates[0] = sampler;
        }
    }

    private void EnsureTargets(int width, int height)
    {
        int bufferWidth = (width + Padding * 2 + 1) / 2;
        int bufferHeight = (height + Padding * 2 + 1) / 2;
        sceneWidth = width; sceneHeight = height;
        if (source != null && !source.IsDisposed && !temporary.IsDisposed
            && source.Width == bufferWidth && source.Height == bufferHeight) return;
        device.Textures[0] = null;
        source?.Dispose(); temporary?.Dispose();
        source = new RenderTarget2D(device, bufferWidth, bufferHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
        temporary = new RenderTarget2D(device, bufferWidth, bufferHeight, false, SurfaceFormat.HdrBlendable, DepthFormat.None);
    }

    private void Blur(RenderTarget2D input, RenderTarget2D output, Vector2 step)
    {
        device.Textures[0] = null;
        device.SetRenderTarget(output);
        sampleStep.SetValue(step);
        batch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
            DepthStencilState.None, RasterizerState.CullNone, blur);
        try { batch.Draw(input, new Rectangle(0, 0, output.Width, output.Height), Color.White); }
        finally { batch.End(); }
        DrawCalls++;
    }

    public void Composite(Matrix transform)
    {
        if (!ready || source == null || source.IsDisposed) return;
        BlendState blend = device.BlendState;
        DepthStencilState depth = device.DepthStencilState;
        RasterizerState rasterizer = device.RasterizerState;
        Texture texture = device.Textures[0];
        SamplerState sampler = device.SamplerStates[0];
        try
        {
            batch.Begin(SpriteSortMode.Immediate, compositeBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, transform);
            try { batch.Draw(source, new Rectangle(-Padding, -Padding, sceneWidth + Padding * 2, sceneHeight + Padding * 2),
                Color.White * MathHelper.Clamp(Strength, 0, 1)); }
            finally { batch.End(); }
            DrawCalls++;
        }
        finally
        {
            device.BlendState = blend;
            device.DepthStencilState = depth;
            device.RasterizerState = rasterizer;
            device.Textures[0] = texture;
            device.SamplerStates[0] = sampler;
        }
    }

    public void Dispose()
    {
        source?.Dispose(); temporary?.Dispose();
        blur.Dispose(); batch.Dispose(); compositeBlend.Dispose();
    }
}
