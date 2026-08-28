using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Magic.Everdeeps;
using CalamityOverhaul.Content.Items.Melee.Abyssrends;
using CalamityOverhaul.Content.NPCs.SeaShrimp.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.SeaShrimp.Projectiles
{
    /// <summary>
    /// 间歇泉柱：行军链的一节。预告即本体——预告期画真实高度的低透水影
    /// （可见范围=将来的判定范围）+向心水滴，随后喷发-驻留-收回。
    /// 伤害窗=喷发+驻留；判定芯窄于可见柱。
    /// ai[0]=预告帧数(含行军错帧)，ai[1]=柱高；Center=柱底地面点
    /// </summary>
    internal class SeaShrimpGeyserSpout : SeaShrimpModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        [VaultLoaden(CWRConstant.Masking + "PerlinNoise")]
        private static Asset<Texture2D> noiseTex = null;

        /// <summary>名义可见宽 px</summary>
        private const float VisualWidth = 64f;
        /// <summary>判定芯半宽（判定藏在可见体内）</summary>
        private const float CoreHalfWidth = 20f;
        private const int EruptFrames = 8;
        private const int HoldFrames = 18;
        private const int RetractFrames = 14;

        private int OmenFrames => (int)Projectile.ai[0];
        private float SpoutHeight => Projectile.ai[1];
        private int TotalLife => OmenFrames + EruptFrames + HoldFrames + RetractFrames;

        /// <summary>本地帧龄：逐端计数，迟入端不重播预告</summary>
        private int Age => (int)Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 420;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 150;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>可见柱高比例：预告 0，喷发急升，驻留 1，收回缓落</summary>
        private float Height01() {
            int age = Age;
            if (age < OmenFrames) {
                return 0f;
            }
            if (age < OmenFrames + EruptFrames) {
                float t = (age - OmenFrames) / (float)EruptFrames;
                return 1f - (1f - t) * (1f - t) * (1f - t);
            }
            if (age < OmenFrames + EruptFrames + HoldFrames) {
                return 1f;
            }
            float r = (age - OmenFrames - EruptFrames - HoldFrames) / (float)RetractFrames;
            return 1f - r;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            int age = Age;
            if (age >= TotalLife) {
                Projectile.Kill();
                return;
            }
            float h01 = Height01();
            if (h01 > 0.1f) {
                Lighting.AddLight(Projectile.Center - new Vector2(0f, SpoutHeight * h01 * 0.5f),
                    0.08f * h01, 0.18f * h01, 0.34f * h01);
            }

            if (Main.dedServ) {
                return;
            }
            if (age < OmenFrames && Main.GameUpdateCount % 3 == 0) {
                //预告：地表水珠被向心吸入（要喷了）
                Vector2 from = Projectile.Center + new Vector2(Main.rand.NextFloat(-46f, 46f), -Main.rand.NextFloat(0f, 14f));
                PRTLoader.NewParticle<PRT_AbyssGlob>(from,
                    (Projectile.Center - from) * 0.09f,
                    Color.Lerp(SeaShrimpVFX.Deep, SeaShrimpVFX.Body, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.22f, 0.38f))?.Configure(12, 1.5f);
            }
            if (age == OmenFrames) {
                //喷发帧：水花上抛 + 闷响
                SoundEngine.PlaySound(SoundID.Item21 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 4 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.SplashWeak with { Volume = 0.7f, Pitch = 0.1f, MaxInstances = 4 }, Projectile.Center);
                EverdeepVFX.SplashBurst(Projectile.Center, Vector2.UnitY * 9f, 0.9f);
            }
            //驻留期顶冠甩滴
            if (h01 >= 0.9f && Main.GameUpdateCount % 4 == 0) {
                EverdeepVFX.ShedDroplet(Projectile.Center - new Vector2(Main.rand.NextFloat(-20f, 20f), SpoutHeight * h01),
                    new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), -Main.rand.NextFloat(1f, 2.6f)), 0.8f);
            }
        }

        /// <summary>伤害窗=可见窗：喷发与驻留有伤，预告与收回无害</summary>
        public override bool? CanDamage() {
            int age = Age;
            return age >= OmenFrames && age < OmenFrames + EruptFrames + HoldFrames ? null : false;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float h = SpoutHeight * Height01();
            if (h < 10f) {
                return false;
            }
            Rectangle column = new((int)(Projectile.Center.X - CoreHalfWidth),
                (int)(Projectile.Center.Y - h), (int)(CoreHalfWidth * 2f), (int)h);
            return column.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            int age = Age;
            float h01 = Height01();

            Effect fx = EffectLoader.SeaShrimpJet?.Value;
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null) {
                return false;
            }
            //画布契约：quad 高 = 柱高×1.30(顶冠飞屑余量) + 底埋 8px；宽 = 名义宽×3
            float quadH = SpoutHeight * 1.30f + 8f;
            float quadW = VisualWidth * 3.0f;

            if (fx == null || noiseTex == null) {
                //回退：暗柱+亮芯（预告期低透真高鬼影）
                float env = age < OmenFrames ? 0.2f : h01;
                Rectangle src = new(0, 0, 1, 1);
                Vector2 basePos = Projectile.Center - new Vector2(0f, SpoutHeight * env) - Main.screenPosition;
                Main.spriteBatch.Draw(pixel, basePos, src, SeaShrimpVFX.Deep * (0.7f * env), 0f,
                    new Vector2(0.5f, 0f), new Vector2(VisualWidth * 0.8f, SpoutHeight * env), SpriteEffects.None, 0f);
                return false;
            }

            //预告期：真实高度的低透水影（可见范围=将来判定范围）+呼吸脉动
            float alpha;
            float heightPx;
            float life;
            if (age < OmenFrames) {
                float omenT = age / MathF.Max(OmenFrames, 1f);
                alpha = (0.14f + 0.14f * omenT)
                    * (0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 16f + Projectile.identity));
                heightPx = SpoutHeight;
                life = 0.3f;
            }
            else {
                alpha = 1f;
                heightPx = SpoutHeight * MathF.Max(h01, 0.02f);
                //收回段进 uLife 尾段撕裂消散区(0.74..1)
                bool retract = age >= OmenFrames + EruptFrames + HoldFrames;
                life = retract
                    ? 0.74f + 0.26f * ((age - OmenFrames - EruptFrames - HoldFrames) / (float)RetractFrames)
                    : 0.3f;
            }

            fx.CurrentTechnique = fx.Techniques["TechGeyser"];
            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Projectile.identity * 0.71f);
            fx.Parameters["fadeAlpha"]?.SetValue(alpha);
            fx.Parameters["uQuadWPx"]?.SetValue(quadW);
            fx.Parameters["uQuadHPx"]?.SetValue(quadH);
            fx.Parameters["uHeightPx"]?.SetValue(heightPx);
            fx.Parameters["uGeyserWPx"]?.SetValue(VisualWidth);
            fx.Parameters["uLife"]?.SetValue(life);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noiseTex.Value;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            fx.CurrentTechnique.Passes[0].Apply();

            //底埋地下 8px：根部球根不露平切口
            Vector2 drawCenter = Projectile.Center + new Vector2(0f, 8f) - new Vector2(0f, quadH * 0.5f);
            Rectangle srcRect = new(0, 0, 1, 1);
            sb.Draw(pixel, drawCenter - Main.screenPosition, srcRect, Color.White, 0f,
                new Vector2(0.5f, 0.5f), new Vector2(quadW, quadH), SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }
    }
}
