using System;
using System.Collections.Generic;
using InnoVault.RenderHandles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OnikiriRebuild;

internal sealed class SakuraFinaleFeedback : ModSystem
{
    private sealed class Spark
    {
        internal Vector2 Position, Velocity, Scale;
        internal Color Color;
        internal Entity Attached;
        internal int Age, Lifetime;
        internal bool Shock;
    }
    private sealed class Blur
    {
        internal Vector2 Position;
        internal float Strength;
        internal int Age, Lifetime;
    }
    private readonly List<Spark> sparks = new();
    private readonly List<Blur> blurs = new();
    private RenderTarget2D blurScratch;

    internal bool HasBlur => blurs.Count > 0;

    internal static void ReadyBurst(Vector2 center, Vector2 starCenter, Player player)
    {
        if (Main.dedServ || Main.gameMenu) return;
        var system = ModContent.GetInstance<SakuraFinaleFeedback>();
        system.AddBlur(center, .004f, 30);
        system.Circle(starCenter, 15, 20f, 16, new Color(100, 20, 40, 255), new Vector2(2.5f, .3f),
            10f, 30f, new Vector2(.3f), 3, .3f, false, player);
        system.Circle(starCenter, 15, 20f, 16, new Color(255, 50, 50, 0), new Vector2(2.5f, .3f),
            10f, 30f, new Vector2(.3f), 3, .3f, false, player);
    }

    internal static void KiriBurst(Vector2 center)
    {
        if (Main.dedServ || Main.gameMenu) return;
        var system = ModContent.GetInstance<SakuraFinaleFeedback>();
        system.AddBlur(center, .01f, 30);
        system.Circle(center, 50, 30f, 30, new Color(255, 100, 180, 255), new Vector2(5f, 1f),
            100f, 600f, Vector2.Zero, 3, .3f, false, null);
        system.Circle(center, 100, 30f, 30, new Color(255, 100, 180), Vector2.One,
            100f, 600f, Vector2.Zero, 4, .3f, true, null);
    }

    private void Circle(Vector2 center, int count, float speed, int lifetime, Color color, Vector2 scale,
        float distance, float offset, Vector2 scaleOffset, int lifeOffset, float speedOffset, bool shock, Entity attached)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Main.rand.NextFloat(-3.14f, 3.14f);
            Vector2 direction = angle.ToRotationVector2();
            var spark = new Spark
            {
                Position = center + direction * (distance + Main.rand.NextFloat(0f, offset)),
                Velocity = direction * speed * Main.rand.NextFloat(1f - speedOffset, 1f + speedOffset) * (shock ? 10.81f : 1f),
                Scale = scale + Main.rand.NextVector2Circular(scaleOffset.X, scaleOffset.Y),
                Color = color, Lifetime = lifetime + Main.rand.Next(-lifeOffset, lifeOffset),
                Shock = shock, Attached = attached
            };
            Advance(spark);
            sparks.Add(spark);
        }
    }

    private void AddBlur(Vector2 center, float strength, int lifetime) =>
        blurs.Add(new Blur { Position = center, Strength = strength, Lifetime = lifetime });

    private static void Advance(Spark spark)
    {
        spark.Age++;
        spark.Velocity *= .92f;
        spark.Position += spark.Velocity;
        if (spark.Attached is { active: true } entity)
            spark.Position += entity.position - entity.oldPosition;
        if (spark.Shock) Lighting.AddLight(spark.Position, spark.Color.ToVector3());
    }

    public override void PostUpdateDusts()
    {
        if (Main.dedServ || Main.gamePaused) return;
        for (int i = sparks.Count - 1; i >= 0; i--)
        {
            Advance(sparks[i]);
            if (sparks[i].Age > sparks[i].Lifetime) sparks.RemoveAt(i);
        }
        for (int i = blurs.Count - 1; i >= 0; i--)
            if (++blurs[i].Age >= blurs[i].Lifetime) blurs.RemoveAt(i);
    }

    internal void DrawSparks(SpriteBatch batch)
    {
        if (sparks.Count == 0) return;
        var assets = ModContent.GetInstance<SakuraFinaleAssets>();
        // Dust remains a local visual list, not a projectile or a persistent player state.
        for (int layer = 0; layer < 2; layer++)
        {
            batch.Begin(layer == 0 ? SpriteSortMode.Deferred : SpriteSortMode.Immediate,
                layer == 0 ? BlendState.AlphaBlend : BlendState.Additive,
                Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            try
            {
                foreach (Spark spark in sparks)
                {
                    if (spark.Shock != (layer == 1)) continue;
                    float progress = spark.Age / (float)spark.Lifetime;
                    Texture2D texture = spark.Shock ? assets.ShockTexture : assets.BurstTexture;
                    int columns = spark.Shock ? 4 : 2, rows = spark.Shock ? 1 : 4;
                    int frame = Math.Clamp((int)(progress * (spark.Shock ? 4 : 8)), 0, columns * rows - 1);
                    Rectangle rect = new(frame % columns * (texture.Width / columns), frame / columns * (texture.Height / rows),
                        texture.Width / columns, texture.Height / rows);
                    Vector2 scale = spark.Scale * (spark.Shock ? MathHelper.Lerp(1f, 2f, progress) : 1f);
                    if (spark.Shock)
                    {
                        assets.ShockColor.Parameters["newColor"].SetValue(new Vector4(1.5f * MathHelper.Lerp(1f, .5f, progress)));
                        assets.ShockColor.CurrentTechnique.Passes[0].Apply();
                    }
                    batch.Draw(texture, spark.Position - Main.screenPosition, rect, spark.Color,
                        spark.Velocity.ToRotation() + (spark.Shock ? 3.14f / 2f : 0f),
                        new Vector2(rect.Width, rect.Height) * .5f, scale, SpriteEffects.None, 0f);
                }
            }
            finally { batch.End(); }
        }
        batch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
            DepthStencilState.None, Main.Rasterizer, null, Main.Transform);
        batch.End();
    }

    internal void DrawBlur(SpriteBatch batch, GraphicsDevice device)
    {
        if (blurs.Count == 0 || !RenderHandleLoader.IsScreenCaptured || !RenderHandleLoader.ScreenTargetAvailable)
            return;

        RenderTarget2D screen = Main.screenTarget;
        if (screen == null || screen.IsDisposed || screen.Width <= 0 || screen.Height <= 0)
            return;

        // KL can replace the main target with an HDR surface whose actual dimensions
        // differ from Main.screenWidth/Height. Never use the framework's screenSwap here.
        if (blurScratch == null || blurScratch.IsDisposed || blurScratch.Width != screen.Width
            || blurScratch.Height != screen.Height || blurScratch.Format != screen.Format)
        {
            var old = blurScratch;
            blurScratch = new RenderTarget2D(device, screen.Width, screen.Height, false,
                screen.Format, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
            old?.Dispose();
        }

        Effect effect = ModContent.GetInstance<SakuraFinaleAssets>().RadialBlur;
        if (effect == null || effect.IsDisposed) return;
        foreach (Blur blur in blurs)
        {
            // KL's world-to-screen conversion uses ZoomMatrix; normalize against the
            // actual source texture rather than the UI-scaled logical screen size.
            Vector2 screenPosition = Vector2.Transform(blur.Position - Main.screenPosition,
                Main.GameViewMatrix.ZoomMatrix);
            effect.Parameters["center"].SetValue(screenPosition / new Vector2(screen.Width, screen.Height));
            effect.Parameters["Iteration"].SetValue(30f);
            effect.Parameters["strength"].SetValue(blur.Strength * (1f - blur.Age / (float)blur.Lifetime));
            RenderHandleLoader.ApplyScreenEffect(batch, device, blurScratch, effect);
        }
    }

    public override void OnWorldUnload() { sparks.Clear(); blurs.Clear(); }
    public override void Unload()
    {
        OnWorldUnload();
        var old = blurScratch;
        blurScratch = null;
        if (old != null) Main.QueueMainThreadAction(old.Dispose);
    }
}
