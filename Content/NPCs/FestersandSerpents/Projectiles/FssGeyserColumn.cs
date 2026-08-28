using CalamityOverhaul.Common;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents.Projectiles
{
    /// <summary>
    /// 灼金脓泉柱；ai[0]=喷发前延迟 ai[1]=高度档 0常规 1高柱；中心锚在基点。
    /// 借世吞 EowGeyser 共享着色器（TechColumn/TechOmen），色板换污沙+灵液金。
    /// 判定随视觉柱体升降，喷发前走预兆盘。
    /// </summary>
    internal class FssGeyserColumn : FssModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int EruptRise = 9;
        private const int EruptHold = 24;
        private const int EruptFade = 12;

        private int Delay => (int)Projectile.ai[0];
        private float ColumnHeight => Projectile.ai[1] == 1f ? 380f : 280f;
        private const float ColumnWidth = 62f;
        /// <summary>画布宽放大：根部裙摆与两侧雾羽化余量（柱芯仍约一个ColumnWidth）</summary>
        private const float CanvasWidthScale = 1.9f;
        /// <summary>画布高放大：补偿shader顶部护栏渐隐段</summary>
        private const float CanvasHeightScale = 1.18f;

        private int Age => (int)Projectile.localAI[0];
        /// <summary>0未喷→1满柱→回落</summary>
        private float RiseT => MathHelper.Clamp((Age - Delay) / (float)EruptRise, 0f, 1f);
        private float FadeT => MathHelper.Clamp((Age - Delay - EruptRise - EruptHold) / (float)EruptFade, 0f, 1f);
        private bool Erupting => Age >= Delay;

        private Vector2 basePoint;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 720;

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 24;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            //首帧锚定基点
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                basePoint = Projectile.Center;
                Projectile.timeLeft = Delay + EruptRise + EruptHold + EruptFade + 4;
            }
            basePoint = basePoint == Vector2.Zero ? Projectile.Center : basePoint;
            Projectile.localAI[0]++;

            if (!Erupting) {
                UpdateOmen();
                return;
            }

            //喷发帧：金浆冲天 + 就近震屏
            if (Age == Delay && !VaultUtils.isServer) {
                FssVfx.IchorBurst(basePoint, 1.3f, -Vector2.UnitY);
                SoundEngine.PlaySound(SoundID.Item95 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 6 }, basePoint);
                SoundEngine.PlaySound(SoundID.WormDig with { Volume = 0.8f, Pitch = 0.2f, MaxInstances = 6 }, basePoint);
                FssVfx.Shake(basePoint, 3f, 1000f);
            }

            float coverage = RiseT * (1f - FadeT);
            int hitHeight = Math.Max((int)(ColumnHeight * coverage), 24);
            Projectile.hostile = coverage > 0.25f;
            Vector2 keepBase = basePoint;
            //判定比视觉柱芯略窄，避免顶端收窄段"被空气打中"
            Projectile.Resize((int)(ColumnWidth * 0.66f), hitHeight);
            Projectile.Center = keepBase - new Vector2(0f, hitHeight * 0.5f);

            //柱内粒子（客户端）：污沙浆 + 金珠溅
            if (!VaultUtils.isServer && FadeT < 1f && OnScreen(basePoint)) {
                int per = RiseT < 1f ? 4 : 2;
                for (int i = 0; i < per; i++) {
                    Vector2 dustPos = basePoint + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f) * ColumnWidth * 0.7f, -2f);
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Sand, 0, 0, 80, FssVfx.TaintedSand, Main.rand.NextFloat(1.2f, 2f));
                    dust.velocity = new Vector2(Main.rand.NextFloat(-0.8f, 0.8f),
                        -Main.rand.NextFloat(8f, 15f) * coverage);
                    dust.noGravity = Main.rand.NextBool(3);
                }
                if (Main.rand.NextBool(2)) {
                    Dust gold = Dust.NewDustDirect(basePoint + new Vector2(-10f, -4f), 20, 4,
                        DustID.Ichor, 0, 0, 40, default, Main.rand.NextFloat(0.9f, 1.4f));
                    gold.velocity = new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(7f, 12f) * coverage);
                    gold.noGravity = false;
                }
                //根部碎浆侧抛：重力弧线砸向两侧，衔接喷发口
                if (RiseT < 1f || Main.rand.NextBool(4)) {
                    Dust chunk = Dust.NewDustDirect(basePoint + new Vector2(-6f, -6f), 12, 6,
                        DustID.CorruptGibs, 0, 0, 30, default, Main.rand.NextFloat(1.2f, 1.9f));
                    chunk.velocity = new Vector2(Main.rand.NextFloat(2.5f, 6f) * (Main.rand.NextBool() ? 1f : -1f),
                        -Main.rand.NextFloat(3f, 7f));
                    chunk.noGravity = false;
                }
                Lighting.AddLight(basePoint - new Vector2(0f, ColumnHeight * 0.4f * coverage),
                    FssVfx.IchorGold.ToVector3() * 0.45f * coverage);
            }
        }

        /// <summary>喷发前基点预兆：汇聚金屑+微光（脓池引爆时池子已先冒泡，这里是通用兜底）</summary>
        private void UpdateOmen() {
            Projectile.hostile = false;
            if (VaultUtils.isServer || !OnScreen(basePoint)) {
                return;
            }
            float t = Age / (float)Math.Max(Delay, 1);
            if (Main.rand.NextBool(2)) {
                Vector2 dustPos = basePoint + new Vector2(Main.rand.NextFloat(-46f, 46f), Main.rand.NextFloat(-4f, 4f));
                Dust dust = Dust.NewDustDirect(dustPos, 4, 4, DustID.Ichor, 0, 0, 60, default, Main.rand.NextFloat(0.8f, 1.3f));
                dust.velocity = (basePoint - dustPos).SafeNormalize(Vector2.Zero) * (1.5f + t * 3f) - Vector2.UnitY * 1.2f;
                dust.noGravity = true;
            }
            Lighting.AddLight(basePoint, FssVfx.IchorGold.ToVector3() * (0.2f + 0.4f * t));
        }

        private static bool OnScreen(Vector2 pos) {
            Vector2 screen = Main.screenPosition;
            return pos.X > screen.X - 300f && pos.X < screen.X + Main.screenWidth + 300f
                && pos.Y > screen.Y - 500f && pos.Y < screen.Y + Main.screenHeight + 300f;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (FadeT >= 1f) {
                return false;
            }

            Effect effect = EffectLoader.EowGeyser?.Value;
            Vector2 baseDraw = basePoint - Main.screenPosition;

            if (effect == null) {
                return false; //回退时粒子密度已足够
            }

            //喷发前：基点小预兆盘
            if (!Erupting) {
                DrawOmenDisc(effect, baseDraw);
                return false;
            }

            effect.CurrentTechnique = effect.Techniques["TechColumn"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 83 * 0.131f);
            effect.Parameters["uProgress"]?.SetValue(RiseT);
            effect.Parameters["uFade"]?.SetValue(FadeT);
            //传实际画布高宽比：shader据此做各向同性噪声取样
            effect.Parameters["uAspect"]?.SetValue(ColumnHeight * CanvasHeightScale / (ColumnWidth * CanvasWidthScale));
            effect.Parameters["uDirtColor"]?.SetValue(FssVfx.TaintedSand.ToVector3());
            effect.Parameters["uAcidColor"]?.SetValue(FssVfx.IchorGold.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(ColumnWidth * CanvasWidthScale / pixel.Width,
                ColumnHeight * CanvasHeightScale / pixel.Height);
            //底边锚基点：origin取贴图底中
            sb.Draw(pixel, baseDraw, null, Color.White, 0f,
                new Vector2(pixel.Width / 2f, pixel.Height), scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            return false;
        }

        /// <summary>喷发前基点预兆盘（TechOmen 小尺寸，金色）</summary>
        private void DrawOmenDisc(Effect effect, Vector2 baseDraw) {
            float chargeT = MathHelper.Clamp(Age / (float)Math.Max(Delay, 1), 0f, 1f);

            effect.CurrentTechnique = effect.Techniques["TechOmen"];
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 83 * 0.131f);
            effect.Parameters["uProgress"]?.SetValue(chargeT);
            effect.Parameters["uFade"]?.SetValue(0f);
            effect.Parameters["uAspect"]?.SetValue(1f);
            effect.Parameters["uDirtColor"]?.SetValue(FssVfx.TaintedSand.ToVector3());
            effect.Parameters["uAcidColor"]?.SetValue(FssVfx.IchorGold.ToVector3());

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            effect.CurrentTechnique.Passes[0].Apply();

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 scale = new Vector2(150f / pixel.Width, 46f / pixel.Height);
            sb.Draw(pixel, baseDraw, null, Color.White, 0f, pixel.Size() / 2f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
