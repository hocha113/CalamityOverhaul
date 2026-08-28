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
    /// 星籁灾变「星海终章」：跟随玩家。蓄势 30t 五线谱光带绕体；
    /// 爆发 128t 按 4/4 拍每 16t 向最近敌自动奏三星和弦（×0.9），拍点星芒炸裂，
    /// 演奏期间移速 +10%；余韵 90t 星尘绕体。伤害全在星弹，主控无自身判定
    /// </summary>
    internal class GsStellarFinaleDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 30;
        public override int MainTicks => 128;
        public override int AftermathTicks => 90;

        protected override bool FollowOwner => true;

        /// <summary>拍间隔（4/4 拍，共 8 拍）</summary>
        private const int BeatTicks = 16;

        /// <summary>八拍五声音阶上行</summary>
        private static readonly float[] BeatPitch = [-0.2f, -0.05f, 0.1f, 0.25f, 0.4f, 0.25f, 0.1f, 0.55f];

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        [VaultLoaden(CWRConstant.Masking + "StarTexture_White")]
        internal static Asset<Texture2D> StarTex = null;

        internal static readonly Color StarPink = new(255, 160, 220);
        internal static readonly Color StarBlue = new(140, 190, 255);

        private static int NoteType => ContentSamples.ItemsByType[ItemID.SparkleGuitar].shoot;

        /// <summary>五线谱可见度</summary>
        private float StaveEnvelope() {
            if (Phase == 0) {
                return VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks);
            }
            if (Phase == 1) {
                return 1f;
            }
            return MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / 55f, 0f, 1f);
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.6f, Pitch = -0.3f }, Projectile.Center);
            }
            if (!VaultUtils.isServer && t % 8 == 0) {
                PRTLoader.NewParticle<PRT_Note>(Projectile.Center + Main.rand.NextVector2Circular(70f, 70f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.5f, 1.2f)),
                    Color.Lerp(StarPink, StarBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.6f, 0.9f))?.Configure(34);
            }
        }

        protected override void MainUpdate(int t) {
            //演奏会状态：各端刷新 owner 的移速增益
            Owner.GetModPlayer<GsCataclysmPlayer>().StellarConcert = true;

            if (t % BeatTicks != 0) {
                return;
            }
            int beat = Math.Min(t / BeatTicks, BeatPitch.Length - 1);
            //拍点：全端音与星芒，owner 端奏和弦
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item26 with { Volume = 0.62f, Pitch = BeatPitch[beat] }, Projectile.Center);
                for (int i = 0; i < 2; i++) {
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(Projectile.Center + Main.rand.NextVector2Circular(40f, 40f),
                        Main.rand.NextVector2Circular(2f, 2f) - new Vector2(0f, 1.5f),
                        beat % 2 == 0 ? StarPink : StarBlue, Main.rand.NextFloat(0.45f, 0.7f))?.Configure(false, 22);
                }
            }
            if (!OwnerSide) {
                return;
            }
            Vector2 targetPos = FindChordTarget();
            Vector2 baseDir = (targetPos - Projectile.Center).SafeNormalize(Vector2.UnitX);
            for (int i = -1; i <= 1; i++) {
                Vector2 vel = baseDir.RotatedBy(i * MathHelper.ToRadians(12f)) * 11f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    NoteType, ScaledDamage(0.9f), Projectile.knockBack, Projectile.owner);
            }
        }

        /// <summary>owner 端选和弦目标：700px 内最近可追踪敌，无则朝光标</summary>
        private Vector2 FindChordTarget() {
            int picked = -1;
            float best = 700f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || !npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    picked = i;
                }
            }
            return picked >= 0 ? Main.npc[picked].Center : Main.MouseWorld;
        }

        protected override void AftermathUpdate(int t) {
            //星尘绕体螺旋
            if (!VaultUtils.isServer && t % 3 == 0) {
                float angle = t * 0.19f + Projectile.identity;
                Vector2 pos = Projectile.Center + angle.ToRotationVector2() * (46f + 26f * (float)Math.Sin(t * 0.05f));
                PRTLoader.NewParticle<PRT_Sparkle>(pos, new Vector2(0f, -0.5f),
                    Color.Lerp(StarPink, StarBlue, Main.rand.NextFloat()), Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(StarBlue, 24);
            }
        }

        /// <summary>伤害全在星弹，主控恒无判定</summary>
        public override bool? CanDamage() => false;

        public override bool PreDraw(ref Color lightColor) {
            float env = StaveEnvelope();
            Texture2D glow = GlowTex?.Value;
            Texture2D star = StarTex?.Value;
            if (env <= 0.02f || glow == null || star == null) {
                return false;
            }
            //五线谱：五条同心虚线弧绕体缓旋（identity 定相）
            float spin = Timer * 0.017f + Projectile.identity * 0.29f;
            for (int line = 0; line < 5; line++) {
                float radius = 56f + line * 12f;
                int dots = 16;
                for (int d = 0; d < dots; d++) {
                    //弧占 220 度，缺口朝下旋转
                    float angle = spin + MathHelper.ToRadians(220f) * (d / (float)(dots - 1)) - MathHelper.ToRadians(110f) - MathHelper.PiOver2;
                    Vector2 pos = Projectile.Center + angle.ToRotationVector2() * radius - Main.screenPosition;
                    float tw = 0.65f + 0.35f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f + d * 0.9f + line * 1.4f);
                    Color tint = Color.Lerp(StarPink, StarBlue, line / 4f) with { A = 0 };
                    Main.EntitySpriteDraw(glow, pos, null, tint * (0.4f * env * tw), 0f,
                        glow.Size() * 0.5f, 9f / glow.Width, SpriteEffects.None, 0);
                }
            }
            //拍点脉冲星（主演出相，节拍呼吸与音同步）
            if (Phase == 1) {
                float beatPulse = 1f - (Elapsed - OmenTicks) % BeatTicks / (float)BeatTicks;
                Main.EntitySpriteDraw(star, Projectile.Center - Main.screenPosition, null,
                    Color.White with { A = 0 } * (0.5f * beatPulse * env), spin * 2f,
                    star.Size() * 0.5f, 0.34f * (0.7f + 0.5f * beatPulse), SpriteEffects.None, 0);
            }
            return false;
        }
    }
}
