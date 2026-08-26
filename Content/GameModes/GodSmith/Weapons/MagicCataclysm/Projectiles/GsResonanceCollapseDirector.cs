using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 共鸣权杖灾变「谐振崩解」：锚定光标区。蓄势 40t 三连同心波纹收缩；
    /// 爆发 130t 五道驻波束交叉驻留，波节点阵持续打击（×0.5/15t，节点判定与可见同源）；
    /// 余韵 90t 残响波纹外扩（无判定）
    /// </summary>
    internal class GsResonanceCollapseDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 40;
        public override int MainTicks => 130;
        public override int AftermathTicks => 90;

        protected override int HitTickRate => 15;

        protected override float TickDamageMul => 0.5f;

        /// <summary>驻波束半长</summary>
        private const float BeamHalf = 210f;
        /// <summary>节点判定半径</summary>
        private const float NodeRadius = 26f;
        /// <summary>每束节点位置（-1~1 归一）</summary>
        private static readonly float[] NodeFractions = [-0.66f, -0.22f, 0.22f, 0.66f];

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        [VaultLoaden(CWRConstant.Masking + "StarTexture_White")]
        internal static Asset<Texture2D> StarTex = null;

        internal static readonly Color ResonPink = new(255, 170, 200);
        internal static readonly Color ResonGold = new(255, 214, 130);
        internal static readonly Color ResonDeep = new(150, 80, 120);

        /// <summary>束阵基准朝向（identity 定相，判定与绘制同源）</summary>
        private float BaseAngle => Hash01(7) * MathHelper.TwoPi;

        private Vector2 BeamDir(int i) => (BaseAngle + MathHelper.TwoPi / 5f * i).ToRotationVector2();

        protected override void OmenUpdate(int t) {
            //三环先后起振
            if (!VaultUtils.isServer && t % 12 == 0 && t < 36) {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.55f, Pitch = -0.3f + t * 0.02f }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, ResonPink.ToVector3() * 0.35f * (t / (float)OmenTicks));
        }

        protected override void MainUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            }
            //驻波 hum
            if (!VaultUtils.isServer && t % 30 == 15) {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.4f, Pitch = -0.15f }, Projectile.Center);
            }
            //节点微光尘（约 1/4 帧）
            if (!VaultUtils.isServer && t % 4 == 0) {
                int i = Main.rand.Next(5);
                float f = NodeFractions[Main.rand.Next(NodeFractions.Length)];
                Vector2 pos = Projectile.Center + BeamDir(i) * (f * BeamHalf);
                PRTLoader.NewParticle<PRT_Sparkle>(pos + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(0.8f, 0.8f),
                    Color.Lerp(ResonPink, ResonGold, Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(ResonGold, 20);
            }
            Lighting.AddLight(Projectile.Center, ResonGold.ToVector3() * 0.5f);
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item25 with { Volume = 0.5f, Pitch = -0.5f }, Projectile.Center);
            }
        }

        /// <summary>只有爆发段有判定：二十个驻波节点圆</summary>
        public override bool? CanDamage() => Phase == 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 1) {
                return false;
            }
            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            float reach = NodeRadius + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            for (int i = 0; i < 5; i++) {
                Vector2 dir = BeamDir(i);
                for (int n = 0; n < NodeFractions.Length; n++) {
                    Vector2 node = Projectile.Center + dir * (NodeFractions[n] * BeamHalf);
                    if (Vector2.Distance(node, targetCenter) < reach) {
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            int e = Elapsed;

            //蓄势：三连同心波纹收缩（错相 8t）
            if (Phase == 0) {
                for (int i = 0; i < 3; i++) {
                    int age = e - i * 8;
                    if (age < 0) {
                        continue;
                    }
                    float prog = MathHelper.Clamp(age / 32f, 0f, 1f);
                    float r = MathHelper.Lerp(320f, 70f, VaultUtils.EaseOutQuad(prog));
                    ShockRingDraw.Draw(sb, Projectile.Center, r, 9f, ResonGold, ResonPink, ResonDeep,
                        0.5f * (0.4f + 0.6f * prog), tearPx: 8f, timeSeed: i * 1.3f);
                }
            }
            //余韵：残响波纹外扩
            else if (Phase == 2) {
                int aftT = e - OmenTicks - MainTicks;
                for (int i = 0; i < 2; i++) {
                    int age = (aftT + i * 45) % 90;
                    float prog = age / 90f;
                    ShockRingDraw.Draw(sb, Projectile.Center, MathHelper.Lerp(60f, 330f, VaultUtils.EaseOutQuad(prog)), 8f,
                        ResonPink, ResonDeep, ResonDeep, (1f - prog) * 0.4f * (1f - aftT / 90f * 0.5f), timeSeed: i * 2.1f);
                }
            }

            Texture2D glow = GlowTex?.Value;
            Texture2D star = StarTex?.Value;
            if (Phase != 1 || glow == null || star == null) {
                return false;
            }

            //爆发：五道驻波虚线束 + 节点脉冲（节点与判定同位同源）
            int mainT = e - OmenTicks;
            float fadeIn = MathHelper.Clamp(mainT / 14f, 0f, 1f);
            float fadeOut = MathHelper.Clamp((MainTicks - mainT) / 16f, 0f, 1f);
            float env = fadeIn * fadeOut;
            float waveT = Main.GlobalTimeWrappedHourly * 6f;
            for (int i = 0; i < 5; i++) {
                Vector2 dir = BeamDir(i);
                //十二段虚线，亮度按驻波驻点分布流动
                for (int s = 0; s < 12; s++) {
                    float f = MathHelper.Lerp(-1f, 1f, s / 11f);
                    Vector2 pos = Projectile.Center + dir * (f * BeamHalf) - Main.screenPosition;
                    float standing = Math.Abs((float)Math.Sin(f * MathHelper.Pi * 2.2f - waveT + i * 1.1f));
                    Color tint = Color.Lerp(ResonPink, ResonGold, standing) with { A = 0 };
                    Main.EntitySpriteDraw(glow, pos, null, tint * (0.35f * env * (0.35f + 0.65f * standing)),
                        dir.ToRotation(), glow.Size() * 0.5f,
                        new Vector2(34f, 7f + 5f * standing) / glow.Width, SpriteEffects.None, 0);
                }
                //节点脉冲星
                for (int n = 0; n < NodeFractions.Length; n++) {
                    Vector2 node = Projectile.Center + dir * (NodeFractions[n] * BeamHalf) - Main.screenPosition;
                    float pulse = 0.7f + 0.3f * (float)Math.Sin(waveT * 1.6f + i * 1.7f + n * 2.3f);
                    Main.EntitySpriteDraw(star, node, null, ResonGold with { A = 0 } * (0.65f * env * pulse),
                        waveT * 0.4f + n, star.Size() * 0.5f, 0.2f * pulse, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
