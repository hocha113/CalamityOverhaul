using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Projectiles
{
    /// <summary>
    /// 投技·生吞入腹的专属预兆：巨口盘。双层收缩环+向心蚀土流+加速心跳闷响，
    /// 明确区别于普通破土预兆(更大、更慢、更饿)。ai[0]=持续帧；锚地表点，无伤害
    /// </summary>
    internal class EowDevourOmen : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>盘半径(抓取判定110px的可视放大，覆盖逃逸提示区)</summary>
        private const float RadiusPx = 300f;

        private int Duration => Math.Max((int)Projectile.ai[0], 10);
        private int Age => (int)Projectile.localAI[0];
        private float ChargeT => MathHelper.Clamp(Age / (float)Duration, 0f, 1f);

        /// <summary>下一次心跳的时刻(本地节拍器)</summary>
        private int nextThumpAge;
        /// <summary>已播心跳数(音高爬升用)</summary>
        private int thumpCount;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 700;

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 12;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                Projectile.timeLeft = Duration;
                nextThumpAge = 2;
            }
            Projectile.localAI[0]++;

            if (VaultUtils.isServer || !EowMotionFX.OnScreen(Projectile.Center, 500f)) {
                return;
            }

            float t = ChargeT;

            //加速心跳：间隔22→8帧收紧，音高随之爬升；末20%静默蓄势
            if (t < 0.8f && Age >= nextThumpAge) {
                float squeeze = t / 0.8f;
                nextThumpAge = Age + (int)MathHelper.Lerp(22f, 8f, squeeze * squeeze);
                thumpCount++;
                SoundEngine.PlaySound(SoundID.WormDigQuiet with {
                    Volume = 0.55f + squeeze * 0.5f,
                    Pitch = -0.85f + Math.Min(thumpCount * 0.06f, 0.5f),
                    MaxInstances = 4
                }, Projectile.Center);
            }

            //向心蚀土流：两族运动，径向吸入+切向旋涡(比普通预兆多一层"卷")
            if (t < 0.8f) {
                int count = 1 + (int)(t * 4f);
                for (int i = 0; i < count; i++) {
                    Vector2 dustPos = Projectile.Center
                        + new Vector2(Main.rand.NextFloat(-1f, 1f) * RadiusPx, Main.rand.NextFloat(-8f, 4f));
                    Dust dust = Dust.NewDustDirect(dustPos, 4, 4,
                        Main.rand.NextBool(3) ? DustID.CorruptGibs : DustID.Dirt,
                        0, 0, 110, default, Main.rand.NextFloat(1.1f, 1.9f));
                    Vector2 inward = (Projectile.Center - dustPos).SafeNormalize(Vector2.Zero);
                    Vector2 swirl = inward.RotatedBy(MathHelper.PiOver2) * 0.8f;
                    dust.velocity = inward * (1.6f + t * 3.8f) + swirl
                        - Vector2.UnitY * Main.rand.NextFloat(0.4f, 1.4f);
                    dust.noGravity = true;
                }
            }

            //震颤加深
            if (Age % 8 == 0 && t > 0.25f) {
                EowMotionFX.CameraPunch(Projectile.Center, 1.2f + t * 3f, 10, "EowDevourOmenPulse");
            }
            Lighting.AddLight(Projectile.Center, EowMotionFX.AcidGreen.ToVector3() * (0.3f + t * 1.0f));
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect effect = EffectLoader.EowGeyser?.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float t = ChargeT;

            if (effect != null) {
                effect.CurrentTechnique = effect.Techniques["TechOmen"];
                effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                effect.Parameters["uSeed"]?.SetValue(Projectile.whoAmI % 71 * 0.149f);
                effect.Parameters["uFade"]?.SetValue(0f);
                effect.Parameters["uAspect"]?.SetValue(1f);
                effect.Parameters["uDirtColor"]?.SetValue(EowMotionFX.DirtBrown.ToVector3());
                effect.Parameters["uAcidColor"]?.SetValue(EowMotionFX.AcidGreen.ToVector3());

                SpriteBatch sb = Main.spriteBatch;
                Texture2D pixel = VaultAsset.placeholder2.Value;

                sb.End();
                sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

                //外环：巨口全径
                effect.Parameters["uProgress"]?.SetValue(t);
                effect.CurrentTechnique.Passes[0].Apply();
                Vector2 outerScale = new Vector2(RadiusPx * 2f / pixel.Width, RadiusPx * 0.66f / pixel.Height);
                sb.Draw(pixel, drawPos, null, Color.White, 0f, pixel.Size() / 2f, outerScale, SpriteEffects.None, 0f);

                //内环：先行收拢的"喉口"，充能更快更亮
                effect.Parameters["uProgress"]?.SetValue(MathHelper.Clamp(t * t * 1.25f, 0f, 1f));
                effect.CurrentTechnique.Passes[0].Apply();
                Vector2 innerScale = new Vector2(RadiusPx * 1.05f / pixel.Width, RadiusPx * 0.4f / pixel.Height);
                sb.Draw(pixel, drawPos, null, Color.White, 0f, pixel.Size() / 2f, innerScale, SpriteEffects.None, 0f);

                sb.End();
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
            else {
                //回退：软光双层压扁呼吸(黑底贴图，A=0走加色路径)
                Texture2D softGlow = CWRAsset.SoftGlow.Value;
                float pulse = 0.6f + 0.4f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (5f + t * 14f));
                Color warn = EowMotionFX.AcidGreen with { A = 0 } * (t * 0.65f * pulse);
                Main.EntitySpriteDraw(softGlow, drawPos, null, warn, 0f, softGlow.Size() / 2f,
                    new Vector2(RadiusPx / softGlow.Width * 2.3f, 0.44f), SpriteEffects.None, 0);
                Main.EntitySpriteDraw(softGlow, drawPos, null, warn * 0.7f, 0f, softGlow.Size() / 2f,
                    new Vector2(RadiusPx / softGlow.Width * 1.2f, 0.3f), SpriteEffects.None, 0);
            }

            //中心巨口酸光核：随充能立方膨胀(隐形起步，惊悚收尾)
            Texture2D core = CWRAsset.SoftGlow.Value;
            float coreT = t * t * t;
            Color coreColor = EowMotionFX.AcidGreen with { A = 0 } * (coreT * 0.9f);
            Main.EntitySpriteDraw(core, drawPos, null, coreColor, 0f, core.Size() / 2f,
                new Vector2(0.28f + coreT * 0.85f, 0.16f + coreT * 0.5f), SpriteEffects.None, 0);

            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI) {
            behindNPCs.Add(index);
        }
    }
}
