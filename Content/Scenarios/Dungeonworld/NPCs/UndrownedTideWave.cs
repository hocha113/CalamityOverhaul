using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Dungeonworld.NPCs
{
    /// <summary>
    /// 泄洪浪：砸地起浪的地浪/水面浪。浪高 3 格恒定、速度恒定不加速（可读性阀门），
    /// 撞墙撞柱即碎（柱间分段=场地自然分档）。ai[0]=行进方向，ai[1]=主人 whoAmI，
    /// ai[2]=浪脊骑行线世界 Y（地浪=地板线，水面浪=水面线），全部随 spawn 包原子过线。
    /// 浪是"水"不是"光"：本体层实色遮挡，白沫走加色（材质身份：狱水）
    /// </summary>
    internal class UndrownedTideWave : UndrownedModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float RideY => Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.7391f % 3.7f;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = (int)Undrowned.WaveHeight;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            Life++;
            //恒速推进 + 浪脊贴线（骑行线随 spawn 包过线，不追地形）
            Projectile.velocity.X = Dir * Undrowned.WaveSpeed;
            Projectile.velocity.Y = 0f;
            Projectile.Center = new Vector2(Projectile.Center.X, RideY - Undrowned.WaveHeight * 0.5f + 4f);

            //浪冠白沫（速度拉伸的水沫，客户端）
            if (!Main.dedServ && (int)Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_SumpSpray>(
                    Projectile.Center + new Vector2(Dir * 10f, -Undrowned.WaveHeight * 0.5f + Main.rand.NextFloat(0f, 10f)),
                    new Vector2(Dir * Main.rand.NextFloat(1f, 2.4f), -Main.rand.NextFloat(0.8f, 2.2f)),
                    Color.Lerp(Undrowned.BogWater, Undrowned.FoamWhite, Main.rand.NextFloat(0.4f, 0.9f)),
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(Main.rand.Next(10, 18));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞墙撞柱即碎：跳越即空窗，浪不叠层不穿柱
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
            for (int k = 0; k < 6; k++) {
                PRTLoader.NewParticle<PRT_SumpSpray>(Projectile.Center + Main.rand.NextVector2Circular(10f, 16f),
                    new Vector2(-Dir * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(1f, 3.5f)),
                    Color.Lerp(Undrowned.BogWater, Undrowned.FoamWhite, Main.rand.NextFloat(0.6f)),
                    Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：暗水裙边 → 实色浪体 → 加色浪冠 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 bOrigin = blob.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Life / 5f, 0f, 1f);
            float wobble = 1f + 0.06f * MathF.Sin(Life * 0.4f + Seed);

            //浪体两层（真 alpha 血珠布，实色遮挡）：暗裙边略宽，主体沼靛
            sb.Draw(blob, pos + new Vector2(0f, 6f), null, Undrowned.BogDeep * (0.85f * fade),
                0f, bOrigin, new Vector2(0.34f, 0.5f) * wobble, SpriteEffects.None, 0f);
            sb.Draw(blob, pos, null,
                lightColor.MultiplyRGB(Undrowned.BogWater) * (0.95f * fade),
                Dir * 0.08f, bOrigin, new Vector2(0.28f, 0.44f) * wobble, SpriteEffects.None, 0f);

            //浪冠白沫（加色层：强度写进色乘，永不 A=0 染色）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            Vector2 gOrigin = glow.Size() * 0.5f;
            sb.Draw(glow, pos + new Vector2(Dir * 6f, -Undrowned.WaveHeight * 0.42f), null,
                Undrowned.FoamWhite * (0.5f * fade),
                0f, gOrigin, new Vector2(26f * 2f / glow.Width, 12f * 2f / glow.Height), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }

    /// <summary>狱水水沫：飞溅即坠的水珠沫，速度拉伸、尾段收芯转暗（真 alpha 布纹贴图）</summary>
    internal class PRT_SumpSpray : BasePRT
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";
        public override bool CanPool => true;
        public override int InGame_World_MaxCount => 400;

        private Color initialColor;

        public PRT_SumpSpray Configure(int lifetime) {
            Lifetime = lifetime;
            initialColor = Color;
            return this;
        }

        public override void Reset() {
            base.Reset();
            initialColor = default;
        }

        public override void SetProperty() {
            PRTDrawMode = PRTDrawModeEnum.AlphaBlend;
            if (Lifetime <= 0) {
                Lifetime = 16;
            }
            if (initialColor == default) {
                initialColor = Color;
            }
        }

        public override void AI() {
            //水珠：重力坠落 + 横向阻尼
            Velocity.X *= 0.97f;
            Velocity.Y = MathF.Min(Velocity.Y + 0.22f, 6f);
            float t = LifetimeCompletion;
            Scale *= 0.98f;
            Color = Color.Lerp(initialColor, Undrowned.BogDeep, MathF.Pow(t, 1.5f) * 0.8f);
            Opacity = 1f - MathF.Pow(t, 2.2f);
            Rotation = Velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Texture2D tex = PRTLoader.PRT_IDToTexture[ID];
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Position - Main.screenPosition;
            float stretch = MathHelper.Clamp(Velocity.Length() * 0.09f, 0f, 0.9f);
            Vector2 scale = new Vector2(0.2f * (1f - stretch * 0.3f), 0.26f * (1f + stretch * 1.7f)) * Scale;

            spriteBatch.Draw(tex, pos, null,
                Color.Lerp(Color, Undrowned.BogDeep, 0.5f) * Opacity, Rotation, origin,
                scale * new Vector2(1.25f, 1.05f), SpriteEffects.None, 0f);
            spriteBatch.Draw(tex, pos, null, Color * Opacity, Rotation, origin, scale, SpriteEffects.None, 0f);
            //新鲜期泡沫白芯（A=0 加色写法只用于预乘补光点缀）
            float fresh = 1f - MathHelper.Clamp(LifetimeCompletion * 2.2f, 0f, 1f);
            if (fresh > 0.05f) {
                spriteBatch.Draw(tex, pos, null,
                    (Undrowned.FoamWhite with { A = 0 }) * (0.4f * fresh * Opacity), Rotation, origin,
                    scale * new Vector2(0.45f, 0.7f), SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
