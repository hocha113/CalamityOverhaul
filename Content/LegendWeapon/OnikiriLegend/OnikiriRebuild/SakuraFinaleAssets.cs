using System;
using System.Text;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild.Geometry;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraFinaleAssets : ModSystem
{
    internal const string Path = "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/";
    internal const string TexturePath = "CalamityOverhaul/" + Path;
    internal RibbonDefinition RangedDefinition { get; private set; }
    internal RibbonMesh RangedMesh { get; private set; }
    internal SakuraSlashPetalBurst RangedPetals { get; private set; }
    internal Texture2D Weapon { get; private set; }
    internal Texture2D Streak { get; private set; }
    internal Texture2D ShotLine { get; private set; }
    internal Texture2D Noise { get; private set; }
    internal Texture2D Halo { get; private set; }
    internal Texture2D KiriTrail { get; private set; }
    internal Texture2D ChargeTrail { get; private set; }
    internal Texture2D SlashTrail { get; private set; }
    internal Texture2D SlashEdge { get; private set; }
    internal Texture2D SlashNoise { get; private set; }
    internal Effect FuzzyEdge { get; private set; }
    internal Effect PolarFlow { get; private set; }
    internal Effect KiriMesh { get; private set; }
    internal Effect SlashShader { get; private set; }
    internal Effect RadialBlur { get; private set; }
    internal Effect ShockColor { get; private set; }
    internal Texture2D BurstTexture { get; private set; }
    internal Texture2D ShockTexture { get; private set; }
    private SakuraSlashRenderer rangedRenderer;
    private Effect readyStarEffect;
    private Texture2D glowMask;
    private SakuraKiriChargeFlowRenderer chargeRenderer;

    public override void Load()
    {
        RangedDefinition = RibbonDefinition.Parse(Encoding.UTF8.GetString(Mod.GetFileBytes(Path + "SakuraRangedSlash.json")));
        RangedMesh = RibbonMeshBuilder.Build(RangedDefinition);
        if (Main.dedServ) return;
        RangedPetals = new SakuraSlashPetalBurst(RangedDefinition);
        Weapon = Texture("OnikiriItem");
        Streak = Texture("Eff_Streak_5555");
        ShotLine = Texture("ShotLine");
        Noise = Texture("voronoiOrigin");
        Halo = Texture("noi_1");
        KiriTrail = Texture("Eff_Trail_518_02");
        ChargeTrail = Texture("Eff_Trail_2015");
        SlashTrail = Texture("Eff_Trail_63_03");
        SlashEdge = Texture("Eff_Trail_63_03_Blade");
        SlashNoise = Texture("PerlinX");
        FuzzyEdge = Shader("FuzzyEdge");
        PolarFlow = Shader("PolarFlow");
        KiriMesh = Shader("SakuraKiriMesh");
        SlashShader = Shader("SakuraSlash");
        RadialBlur = Shader("RadialBlur");
        ShockColor = Shader("ReColor");
        BurstTexture = Texture("SakuraFinaleBurstDust");
        ShockTexture = Texture("SakuraFinaleShockDust");
    }

    private Texture2D Texture(string name) => Mod.Assets.Request<Texture2D>(Path + name, AssetRequestMode.ImmediateLoad).Value;
    private Effect Shader(string name) => Mod.Assets.Request<Effect>(Path + name, AssetRequestMode.ImmediateLoad).Value;

    private Effect ReadyStarEffect => readyStarEffect ??= FuzzyEdge.Clone();

    internal Texture2D GlowMask() => glowMask ??=
        SakuraReadyStanceWeaponVisual.CreateGlowMask(Main.instance.GraphicsDevice, Weapon);

    internal SakuraKiriChargeFlowRenderer ChargeRenderer() => chargeRenderer ??=
        new SakuraKiriChargeFlowRenderer(Main.instance.GraphicsDevice);

    internal static void RestoreWorldBatch()
    {
        // A deferred Begin alone leaves the custom vertex shader active for player draw buffers.
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        Main.spriteBatch.End();
        Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
    }

    internal SakuraSlashRenderer RangedRenderer()
    {
        if (rangedRenderer != null) return rangedRenderer;
        rangedRenderer = new SakuraSlashRenderer(Main.instance.GraphicsDevice, RangedDefinition,
            SlashTrail, SlashShader, SlashNoise, SlashEdge);
        SakuraSlashStyle.Apply(rangedRenderer);
        return rangedRenderer;
    }

    internal void DrawReadyStar(Vector2 center, float projectileRotation, int age, int facing, int gravity, float playerRotation)
    {
        if (age < 0 || age > 20) return;
        Effect readyStarEffect = ReadyStarEffect;
        float progress = age / 20f;
        float rotation = MathHelper.Lerp(2.2f, 4.2f, progress);
        Vector2 scale = new(MathHelper.Lerp(.2f, .9f, progress), MathHelper.Lerp(.3f, 0f, progress));
        float fade = MathHelper.Lerp(4.5f, .2f, progress);
        float erosion = age < 5 ? 0f : MathHelper.Lerp(0f, .6f, 1f - MathF.Pow(1f - (age - 5f) / 20f, 2f));
        SpriteEffects flip = (facing < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None)
            | (gravity < 0 ? SpriteEffects.FlipVertically : SpriteEffects.None);
        GraphicsDevice device = Main.instance.GraphicsDevice;
        Texture oldNoise = device.Textures[1];
        SamplerState oldSampler = device.SamplerStates[1];
        Main.spriteBatch.End();
        try
        {
            for (int layer = 0; layer < 3; layer++)
            {
                Texture2D texture = layer == 1 ? ShotLine : Streak;
                Vector2 size = scale * (layer == 1 ? new Vector2(4.8f, 4.6f)
                    : layer == 2 ? new Vector2(.8f, .4f) : Vector2.One);
                readyStarEffect.Parameters["ImageColor"].SetValue(layer == 0 ? new Vector4(0f, 0f, 0f, 1f)
                    : layer == 1 ? new Vector4(fade, 0f, 0f, fade) : new Vector4(1.5f));
                readyStarEffect.Parameters["Intensity"].SetValue(layer == 1 ? 0f : erosion);
                readyStarEffect.Parameters["NoiseScale"].SetValue(new Vector2(1f, .2f));
                readyStarEffect.Parameters["UseErosion"].SetValue(true);
                readyStarEffect.Parameters["Symmetric"].SetValue(false);
                Main.spriteBatch.Begin(SpriteSortMode.Immediate,
                    layer == 1 ? BlendState.Additive : BlendState.NonPremultiplied,
                    Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone,
                    readyStarEffect, Main.GameViewMatrix.TransformationMatrix);
                for (int i = 0; i < 2; i++)
                {
                    readyStarEffect.Parameters["NoiseOffset"].SetValue(new Vector2(i == 0 ? 0f : .5f, 0f));
                    device.Textures[1] = Noise;
                    device.SamplerStates[1] = SamplerState.LinearWrap;
                    readyStarEffect.CurrentTechnique.Passes[0].Apply();
                    float angle = (projectileRotation + rotation + i * MathHelper.PiOver2) * facing * gravity + playerRotation;
                    Main.spriteBatch.Draw(texture, center - Main.screenPosition, null,
                        layer == 1 ? Color.White : layer == 0 ? new Color(0, 0, 0, 255) : Color.Black,
                        angle, new Vector2(texture.Width, texture.Height) * .5f, size, flip, 0f);
                }
                Main.spriteBatch.End();
            }
        }
        finally
        {
            device.Textures[1] = oldNoise;
            device.SamplerStates[1] = oldSampler;
            RestoreWorldBatch();
        }
    }

    public override void Unload()
    {
        var oldRenderer = rangedRenderer;
        var oldStar = readyStarEffect;
        var oldMask = glowMask;
        var oldCharge = chargeRenderer;
        Main.QueueMainThreadAction(() =>
        {
            oldRenderer?.Dispose();
            oldStar?.Dispose();
            oldMask?.Dispose();
            oldCharge?.Dispose();
        });
        rangedRenderer = null;
        readyStarEffect = null;
        glowMask = null;
        chargeRenderer = null;
        RangedDefinition = null;
        RangedMesh = null;
        RangedPetals = null;
        Weapon = Streak = ShotLine = Noise = Halo = KiriTrail = ChargeTrail = SlashTrail = SlashEdge = SlashNoise = null;
        FuzzyEdge = PolarFlow = KiriMesh = SlashShader = RadialBlur = ShockColor = null;
        BurstTexture = ShockTexture = null;
    }
}
