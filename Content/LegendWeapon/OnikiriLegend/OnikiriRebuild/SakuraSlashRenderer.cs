using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Shared game/preview renderer. Texture and Effect are borrowed, not owned.</summary>
public sealed class SakuraSlashRenderer : IDisposable
{
    private readonly GraphicsDevice device;
    private readonly Effect effect;
    private readonly Texture2D texture;
    private readonly Texture2D noise;
    private readonly Texture2D bladeTexture;
    private readonly VertexPositionTexture[] vertices;
    private readonly int[] indices;
    private readonly RibbonMaterial material;
    private readonly Vector3 materialTint;
    private readonly BlendState additive = new()
    {
        ColorSourceBlend = Blend.One, ColorDestinationBlend = Blend.One,
        AlphaSourceBlend = Blend.One, AlphaDestinationBlend = Blend.InverseSourceAlpha
    };

    public Vector3 TintColor { get; set; } = Vector3.One;
    public Vector3 BladeColor { get; set; } = Vector3.One;
    public bool ForceAlphaBlend { get; set; }
    public string[] Warnings { get; }
    public int TriangleCount => indices.Length / 3;

    public SakuraSlashRenderer(GraphicsDevice device, RibbonDefinition definition, Texture2D texture, Effect effect, Texture2D noise,
        Texture2D bladeTexture = null)
    {
        this.device = device; this.texture = texture; this.effect = effect;
        this.noise = noise ?? throw new ArgumentNullException(nameof(noise));
        this.bladeTexture = bladeTexture;
        material = definition.Material;
        materialTint = new Vector3(material.Tint[0], material.Tint[1], material.Tint[2]);
        var mesh = RibbonMeshBuilder.Build(definition);
        Warnings = mesh.Warnings; indices = mesh.Indices;
        vertices = new VertexPositionTexture[mesh.Vertices.Length / 4];
        for (int i = 0; i < vertices.Length; i++)
            vertices[i] = new(new Vector3(mesh.Vertices[i * 4], mesh.Vertices[i * 4 + 1], 0),
                new Vector2(mesh.Vertices[i * 4 + 2], mesh.Vertices[i * 4 + 3]));
    }

    /// <summary>Draw the complete instance using its scaled animation clock.</summary>
    public void Draw(SakuraSlashInstance slash, Vector2 camera, Matrix gameView) =>
        Draw(slash.GetScreenTransform(camera, gameView), slash.Age, slash.DrawOpacity,
            reveal: slash.Reveal, dissolve: slash.Dissolve, dissolveUvScale: slash.DissolveUvScale,
            dissolveNoiseStrength: slash.DissolveNoiseStrength, dissolveEdgeWidth: slash.DissolveEdgeWidth,
            dissolveEdgeColor: slash.DissolveEdgeColor, dissolveEdgeIntensity: slash.DissolveEdgeIntensity,
            dissolveRimWidth: slash.DissolveRimWidth, attackUvOffset: slash.AttackUvOffset,
            noiseOnlyDissolve: slash.NoiseOnlyDissolve, dissolveDirection: slash.DissolveDirection);

    /// <summary>Call at a draw boundary without an active SpriteBatch. Time is seconds.</summary>
    public void Draw(Matrix localToScreen, float time, float opacity = 1, float emission = 1, float reveal = 1, float dissolve = 0,
        Vector2? dissolveUvScale = null, float dissolveNoiseStrength = .22f, float dissolveEdgeWidth = .055f,
        Vector3? dissolveEdgeColor = null, float dissolveEdgeIntensity = 1, float dissolveRimWidth = .025f,
        Vector2? attackUvRange = null, float? attackUvOffset = null, bool noiseOnlyDissolve = false,
        Vector2? dissolveDirection = null)
    {
        var p = effect.Parameters;
        // Rotate in 3D first, then discard depth for the 2D world. Otherwise the
        // usual 0..1 orthographic depth range clips tilted vertices on either side.
        localToScreen.M13 = localToScreen.M23 = localToScreen.M33 = localToScreen.M43 = 0;
        p["Transform"].SetValue(localToScreen * Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, 0, 1));
        p["TintColor"].SetValue(TintColor * materialTint);
        p["BladeColor"].SetValue(BladeColor);
        p["BladeEnabled"].SetValue(bladeTexture == null ? 0f : 1f);
        p["Opacity"].SetValue(MathHelper.Clamp(opacity * material.Opacity, 0, 1));
        p["Emission"].SetValue(Math.Max(0, emission));
        p["Reveal"].SetValue(MathHelper.Clamp(reveal, 0, 1));
        // A CPU-evaluated offset supports multi-stage motion beyond the strike endpoint.
        p["AttackUvRange"].SetValue(attackUvOffset.HasValue
            ? new Vector2(attackUvOffset.Value) : attackUvRange ?? new Vector2(-1.02f, 0));
        p["Dissolve"].SetValue(Math.Max(0, dissolve));
        p["DissolveUvScale"].SetValue(dissolveUvScale ?? SakuraSlashInstance.DefaultDissolveUvScale);
        p["DissolveNoiseStrength"].SetValue(MathHelper.Clamp(dissolveNoiseStrength, 0, 1));
        p["DissolveDirection"].SetValue(dissolveDirection ?? new Vector2(0, -1));
        p["NoiseOnlyDissolve"].SetValue(noiseOnlyDissolve);
        p["DissolveEdgeWidth"].SetValue(MathHelper.Clamp(dissolveEdgeWidth, .001f, .25f));
        p["DissolveRimWidth"].SetValue(MathHelper.Clamp(dissolveRimWidth, .001f, .5f));
        p["DissolveEdgeColor"].SetValue(dissolveEdgeColor ?? new Vector3(1f, .48f, .72f));
        p["DissolveEdgeIntensity"].SetValue(Math.Max(0, dissolveEdgeIntensity));
        p["RepeatU"].SetValue(material.Repeat); p["OffsetU"].SetValue(material.Offset + time * material.ScrollSpeed);
        p["WrapU"].SetValue(material.Wrap ? 1f : 0f); p["Vertical"].SetValue(material.FrameAxis == "vertical" ? 1f : 0f);
        p["FlipAlong"].SetValue(material.FlipAlong ? 1f : 0f); p["FlipAcross"].SetValue(material.FlipAcross ? 1f : 0f);
        var cell = material.GetAtlasCell(time);
        p["AtlasSize"].SetValue(new Vector2(material.AtlasColumns, material.AtlasRows));
        p["AtlasCell"].SetValue(new Vector2(cell.X, cell.Y)); p["TexelSize"].SetValue(new Vector2(1f / texture.Width, 1f / texture.Height));
        var blend = device.BlendState; var depth = device.DepthStencilState; var raster = device.RasterizerState;
        var sampler = device.SamplerStates[0]; var oldTexture = device.Textures[0];
        var noiseSampler = device.SamplerStates[1]; var oldNoise = device.Textures[1];
        var bladeSampler = device.SamplerStates[2]; var oldBlade = device.Textures[2];
        try
        {
            device.BlendState = !ForceAlphaBlend && material.Blend == "additive" ? additive : BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.None; device.RasterizerState = RasterizerState.CullNone;
            effect.CurrentTechnique = effect.Techniques["SakuraGradient"];
            foreach (var pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply(); device.Textures[0] = texture; device.SamplerStates[0] = SamplerState.LinearClamp;
                device.Textures[1] = noise;
                device.SamplerStates[1] = SamplerState.LinearWrap;
                device.Textures[2] = bladeTexture ?? texture;
                device.SamplerStates[2] = SamplerState.LinearClamp;
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, indices.Length / 3);
            }
        }
        finally
        {
            device.BlendState = blend; device.DepthStencilState = depth; device.RasterizerState = raster;
            device.SamplerStates[0] = sampler; device.Textures[0] = oldTexture;
            device.SamplerStates[1] = noiseSampler; device.Textures[1] = oldNoise;
            device.SamplerStates[2] = bladeSampler; device.Textures[2] = oldBlade;
        }
    }
    public void Dispose() => additive.Dispose();
}
