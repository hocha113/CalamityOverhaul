using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

/// <summary>Client-local petals; particles never occupy projectile slots or send packets.</summary>
internal sealed class SakuraParticleSystem : ModSystem
{
    private SakuraParticleWorld world;
    private SakuraParticleRenderer renderer;
    private Effect bloomEffect;

    public override void Load()
    {
        if (Main.dedServ) return;
        bloomEffect = Mod.Assets.Request<Effect>(
            "Content/LegendWeapon/OnikiriLegend/OnikiriRebuild/SakuraBloom",
            AssetRequestMode.ImmediateLoad).Value;
        world = new SakuraParticleWorld();
        On_Main.CheckMonoliths += PrepareBloom;
        On_Main.DrawCachedProjs += DrawOverPlayers;
    }

    internal static void EmitBurst(Vector2 position, Vector2 direction, SakuraEmissionSettings settings)
    {
        if (Main.dedServ || Main.gameMenu) return;
        ModContent.GetInstance<SakuraParticleSystem>().world?.EmitBurst(position, direction, settings);
    }

    public override void PostUpdateDusts()
    {
        if (!Main.dedServ && !Main.gamePaused) world?.Update(1f / 60f);
    }

    private void PrepareBloom(On_Main.orig_CheckMonoliths orig)
    {
        orig();
        renderer?.Bloom?.Reset();
        if (Main.dedServ || Main.gameMenu || world == null || world.Count == 0) return;
        renderer ??= new SakuraParticleRenderer(Main.instance.GraphicsDevice, bloomEffect: bloomEffect);
        renderer.PrepareBloom(world, Main.screenPosition, Main.screenWidth, Main.screenHeight);
    }

    private void DrawOverPlayers(On_Main.orig_DrawCachedProjs orig, Main self, List<int> cache,
        bool startSpriteBatch)
    {
        orig(self, cache, startSpriteBatch);
        if (Main.dedServ || Main.gameMenu || world == null || world.Count == 0 ||
            !ReferenceEquals(cache, self.DrawCacheProjsOverPlayers)) return;
        if (!startSpriteBatch) Main.spriteBatch.End();
        try
        {
            renderer ??= new SakuraParticleRenderer(Main.instance.GraphicsDevice, bloomEffect: bloomEffect);
            renderer.Draw(world, Main.screenPosition, Main.GameViewMatrix.TransformationMatrix,
                Main.instance.GraphicsDevice.Viewport.Width, Main.instance.GraphicsDevice.Viewport.Height);
        }
        finally
        {
            // Restore the default vertex shader before Terraria draws its player buffer.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
            Main.spriteBatch.End();
            if (!startSpriteBatch)
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        }
    }

    public override void OnWorldUnload()
    {
        world?.Clear();
        renderer?.Bloom?.Reset();
    }

    public override void Unload()
    {
        if (!Main.dedServ)
        {
            On_Main.CheckMonoliths -= PrepareBloom;
            On_Main.DrawCachedProjs -= DrawOverPlayers;
        }
        world = null;
        SakuraParticleRenderer old = renderer;
        renderer = null;
        bloomEffect = null;
        if (old != null) Main.QueueMainThreadAction(old.Dispose);
    }
}
