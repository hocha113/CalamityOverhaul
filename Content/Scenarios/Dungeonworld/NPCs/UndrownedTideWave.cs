using CalamityOverhaul.Common;
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
    /// 本体=UndrownedTideWall.fx 涌浪 quad（起浪几何生长/卷唇/冠沫/背坡拖裙/溃散蚀顶），
    /// 撞墙不瞬灭：判定当帧关死，浪体再花 14f 自冠塌回水里（碰撞各端对同步 tile 确定性同判）。
    /// 着色器缺编回退旧双层水团贴图。浪是"水"不是"光"：本体层实色遮挡，白沫走加色
    /// </summary>
    internal class UndrownedTideWave : UndrownedModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>撞墙后的塌浪帧数（判定已死，纯表现）</summary>
        private const int CollapseFrames = 14;
        /// <summary>浪体画布（可见浪宽于命中盒：判定藏在可见体内读作公平）</summary>
        private const float CanvasW = 180f;
        private const float CanvasH = 118f;

        private float Dir => Projectile.ai[0] >= 0f ? 1f : -1f;
        private float RideY => Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];
        /// <summary>&gt;0=塌浪中（撞墙表现尾声，判定恒关）</summary>
        private ref float Collapsing => ref Projectile.localAI[1];

        private float Seed => Projectile.identity * 0.7391f % 3.7f;

        public override void SetStaticDefaults() {
            //浪冠抛沫画布高出命中盒，出屏余量不足会整浪瞬灭
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 220;
        }

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

            if (Collapsing > 0f) {
                //塌浪尾声：原地蚀顶，判定死透
                Collapsing++;
                Projectile.velocity = Vector2.Zero;
                if (Collapsing >= CollapseFrames) {
                    Projectile.Kill();
                }
                return;
            }

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
            //浪脚湿沫余痕：浪走过之后仍留在地上（活得比浪久的痕迹）
            if (!Main.dedServ && (int)Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_SumpSpray>(
                    Projectile.Center + new Vector2(-Dir * 20f, Undrowned.WaveHeight * 0.5f - 6f),
                    new Vector2(-Dir * 0.3f, -Main.rand.NextFloat(0.2f, 0.7f)),
                    Undrowned.BogWater * 0.55f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(30, 50));
            }
        }

        /// <summary>塌浪期判定恒关（撞墙当帧浪就"死了"，尾声是纯表现）</summary>
        public override bool? CanDamage() => Collapsing > 0f ? false : null;

        public override bool OnTileCollide(Vector2 oldVelocity) {
            //撞墙撞柱：判定即碎，浪体走 14f 塌浪尾声；跳越即空窗，浪不叠层不穿柱
            if (Collapsing <= 0f) {
                Collapsing = 1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = CollapseFrames + 4;
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.5f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
                if (!Main.dedServ) {
                    for (int k = 0; k < 6; k++) {
                        PRTLoader.NewParticle<PRT_SumpSpray>(Projectile.Center + Main.rand.NextVector2Circular(10f, 16f),
                            new Vector2(-Dir * Main.rand.NextFloat(0.5f, 2f), -Main.rand.NextFloat(1f, 3.5f)),
                            Color.Lerp(Undrowned.BogWater, Undrowned.FoamWhite, Main.rand.NextFloat(0.6f)),
                            Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(12, 22));
                    }
                }
            }
            return false;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //寿命自然到点（没撞墙）也给一记小碎沫收尾
            for (int k = 0; k < 4; k++) {
                PRTLoader.NewParticle<PRT_SumpSpray>(Projectile.Center + Main.rand.NextVector2Circular(10f, 14f),
                    new Vector2(Main.rand.NextFloat(-1f, 1f), -Main.rand.NextFloat(0.5f, 2f)),
                    Undrowned.BogWater * 0.8f, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：涌浪 shader 本体（缺编回退双层水团）====================

        public override bool PreDraw(ref Color lightColor) {
            float grow = MathHelper.Clamp(Life / 12f, 0f, 1f);
            grow = grow * grow * (3f - 2f * grow);
            float collapse = Collapsing > 0f
                ? MathHelper.Clamp(Collapsing / CollapseFrames, 0f, 1f)
                : 1f - MathHelper.Clamp(Projectile.timeLeft / 20f, 0f, 1f);
            float intensity = 0.5f + 0.5f * MathHelper.Clamp(Life / 10f, 0f, 1f);

            Effect effect = EffectLoader.UndrownedTideWall?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (effect == null || noise == null || pixel == null) {
                DrawSpriteFallback(lightColor);
                return false;
            }

            //浪底锚在骑行线；顶部四成画布留给冠口抛沫
            Vector2 bottom = new(Projectile.Center.X, RideY + 6f);
            Vector2 drawCenter = bottom - new Vector2(0f, CanvasH * 0.5f);

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Seed);
            effect.Parameters["uIntensity"]?.SetValue(intensity);
            effect.Parameters["uGrowth"]?.SetValue(grow);
            effect.Parameters["uCollapse"]?.SetValue(collapse);
            effect.Parameters["uDir"]?.SetValue(Dir);
            effect.Parameters["uSeed"]?.SetValue(Seed);
            effect.Parameters["uDeepColor"]?.SetValue(Undrowned.BogDeep.ToVector3());
            effect.Parameters["uSeaColor"]?.SetValue(Undrowned.BogWater.ToVector3());
            effect.Parameters["uFoamColor"]?.SetValue(Undrowned.FoamWhite.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            //噪声显式绑 s1：SpriteBatch.Draw 会把 s0 覆写成画布贴图（合同同 FishronTsunamiWallProj）
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            effect.CurrentTechnique.Passes[0].Apply();

            sb.Draw(pixel, drawCenter - Main.screenPosition, null, Color.White, 0f,
                pixel.Size() * 0.5f, new Vector2(CanvasW / pixel.Width, CanvasH / pixel.Height),
                SpriteEffects.None, 0f);

            sb.End();
            gd.Textures[1] = null;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>着色器缺编兜底：旧双层水团 + 加色浪冠</summary>
        private void DrawSpriteFallback(Color lightColor) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (blob == null || glow == null) {
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 bOrigin = blob.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float fade = MathHelper.Clamp(Life / 5f, 0f, 1f) * (Collapsing > 0f ? 1f - Collapsing / CollapseFrames : 1f);
            float wobble = 1f + 0.06f * MathF.Sin(Life * 0.4f + Seed);

            sb.Draw(blob, pos + new Vector2(0f, 6f), null, Undrowned.BogDeep * (0.85f * fade),
                0f, bOrigin, new Vector2(0.34f, 0.5f) * wobble, SpriteEffects.None, 0f);
            sb.Draw(blob, pos, null,
                lightColor.MultiplyRGB(Undrowned.BogWater) * (0.95f * fade),
                Dir * 0.08f, bOrigin, new Vector2(0.28f, 0.44f) * wobble, SpriteEffects.None, 0f);
            //浪冠白沫（A=0 加色写法只用于预乘批补光）
            sb.Draw(glow, pos + new Vector2(Dir * 6f, -Undrowned.WaveHeight * 0.42f), null,
                (Undrowned.FoamWhite with { A = 0 }) * (0.5f * fade),
                0f, glow.Size() * 0.5f, new Vector2(26f * 2f / glow.Width, 12f * 2f / glow.Height), SpriteEffects.None, 0f);
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
