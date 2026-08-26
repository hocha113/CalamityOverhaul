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
    /// 星云奥秘灾变「星云漩臂」：跟随玩家。蓄势 30t 星云雾环聚拢；
    /// 爆发 150t 三条旋臂星云场绕自机公转（×0.6/18t，极角螺旋判定与可见臂同源，
    /// 小敌被缓拽向臂心）；余韵 120t 星云余雾
    /// </summary>
    internal class GsNebulaSpiralDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 30;
        public override int MainTicks => 150;
        public override int AftermathTicks => 120;

        protected override bool FollowOwner => true;

        protected override int HitTickRate => 18;

        protected override float TickDamageMul => 0.6f;

        /// <summary>旋臂径向范围</summary>
        private const float ArmInner = 40f;
        private const float ArmOuter = 230f;
        /// <summary>臂带弧向半宽 px</summary>
        private const float ArmHalf = 34f;
        /// <summary>螺旋弯曲率（弧度/px）</summary>
        private const float SpiralCurl = 0.0065f;
        /// <summary>公转角速度</summary>
        private const float SpinRate = 0.035f;

        [VaultLoaden(CWRConstant.Masking + "SoftGlow")]
        internal static Asset<Texture2D> GlowTex = null;

        internal static readonly Color NebulaPink = new(255, 130, 220);
        internal static readonly Color NebulaViolet = new(160, 90, 240);
        internal static readonly Color NebulaDeep = new(70, 30, 110);

        /// <summary>臂 k 在半径 r 处的极角（判定与绘制同源）</summary>
        private float ArmAngle(int k, float r)
            => Timer * SpinRate + MathHelper.TwoPi / 3f * k + r * SpiralCurl + Projectile.identity * 0.47f;

        /// <summary>旋臂强度包络</summary>
        private float ArmEnvelope() {
            if (Phase == 0) {
                return 0f;
            }
            if (Phase == 1) {
                return MathHelper.Clamp((Elapsed - OmenTicks) / 18f, 0f, 1f);
            }
            return MathHelper.Clamp(1f - (Elapsed - OmenTicks - MainTicks) / 70f, 0f, 1f) * 0.55f;
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.6f, Pitch = -0.15f }, Projectile.Center);
            }
            //雾环聚拢
            if (!VaultUtils.isServer && t % 2 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                    Color.Lerp(NebulaPink, NebulaViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(150f, 240f), 26);
            }
        }

        protected override void MainUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.7f, Pitch = -0.1f }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, NebulaViolet.ToVector3() * 0.6f);

            //小敌缓拽向臂心（权威端改速度，自然入同步；只拽非 Boss）
            if (Authoritative) {
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (!npc.active || npc.friendly || npc.boss || !npc.CanBeChasedBy() || npc.knockBackResist <= 0f) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist > ArmOuter + 60f || dist < 130f) {
                        continue;
                    }
                    npc.velocity += (Projectile.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.3f * npc.knockBackResist;
                    if (npc.velocity.Length() > 10f) {
                        npc.velocity *= 10f / npc.velocity.Length();
                    }
                }
            }
            //臂上星尘（约 2/3 帧）
            if (!VaultUtils.isServer && t % 3 != 0) {
                int k = Main.rand.Next(3);
                float r = Main.rand.NextFloat(ArmInner, ArmOuter);
                Vector2 pos = Projectile.Center + ArmAngle(k, r).ToRotationVector2() * r;
                PRTLoader.NewParticle<PRT_Sparkle>(pos, Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.Lerp(NebulaPink, NebulaViolet, Main.rand.NextFloat()), Main.rand.NextFloat(0.28f, 0.5f))
                    ?.Configure(NebulaViolet, 18);
            }
        }

        protected override void AftermathUpdate(int t) {
            //星云余雾
            if (!VaultUtils.isServer && t % 4 == 0) {
                PRTLoader.NewParticle<PRT_GravityVortex>(Projectile.Center, Vector2.Zero,
                    NebulaDeep, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(Main.rand.NextFloat(MathHelper.TwoPi), Main.rand.NextFloat(80f, 180f), 30);
            }
            Lighting.AddLight(Projectile.Center, NebulaDeep.ToVector3() * 1.2f * (1f - t / (float)AftermathTicks));
        }

        /// <summary>爆发段旋臂判定：极角螺旋带（与可见臂同源）</summary>
        public override bool? CanDamage() => Phase == 1 ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Phase != 1) {
                return false;
            }
            Vector2 offset = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = offset.Length();
            float reach = Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            if (dist < ArmInner - reach || dist > ArmOuter + reach) {
                return false;
            }
            float targetAngle = offset.ToRotation();
            for (int k = 0; k < 3; k++) {
                float delta = MathHelper.WrapAngle(targetAngle - MathHelper.WrapAngle(ArmAngle(k, dist)));
                //弧向距离 = 角差 × 半径
                if (Math.Abs(delta) * dist < ArmHalf + reach) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = GlowTex?.Value;
            float env = ArmEnvelope();
            if (glow == null) {
                return false;
            }
            //中心核心辉光（蓄势期也亮，作聚拢读数）
            float coreEnv = Phase == 0 ? Elapsed / (float)OmenTicks : env + 0.2f;
            Main.EntitySpriteDraw(glow, Projectile.Center - Main.screenPosition, null,
                NebulaViolet with { A = 0 } * (0.5f * coreEnv), 0f, glow.Size() * 0.5f,
                90f / glow.Width, SpriteEffects.None, 0);
            if (env <= 0.02f) {
                return false;
            }
            //三臂：沿臂骨架采样九个星云斑（identity 定相，尺寸随半径增）
            for (int k = 0; k < 3; k++) {
                for (int s = 0; s < 9; s++) {
                    float r = MathHelper.Lerp(ArmInner, ArmOuter, s / 8f);
                    Vector2 pos = Projectile.Center + ArmAngle(k, r).ToRotationVector2() * r - Main.screenPosition;
                    float size = MathHelper.Lerp(40f, 86f, s / 8f) * (0.85f + 0.3f * Hash01(k * 16 + s));
                    float breathe = 0.8f + 0.2f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3f + s * 0.8f + k * 2.1f);
                    Color tint = Color.Lerp(NebulaPink, NebulaDeep, s / 8f) with { A = 0 };
                    Main.EntitySpriteDraw(glow, pos, null, tint * (0.4f * env * breathe), 0f,
                        glow.Size() * 0.5f, size / glow.Width * breathe, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
