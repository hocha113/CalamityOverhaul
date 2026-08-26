using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.WallOfFlesh
{
    /// <summary>
    /// 逆流血珠：吸血池的治疗载体。ai[0]=治疗量(生成参数随出生包同步) ai[1]=形态种子。
    /// 凝珠短悬(反向漂离) → 加速逆流归体(垂线摆动的曲线路径，禁匀速直线) → 触体回血。
    /// 回血只在拥有者端写自身生命(玩家自写生命无需网络包，逐帧差量同步收尾)，
    /// 其余端只演出；到体绿字由 HealEffect 广播
    /// </summary>
    internal class GluttonousLeechOrb : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        /// <summary>凝珠悬滞时长(tick)</summary>
        private const int CondenseTicks = 8;
        /// <summary>触体吸收距离 px</summary>
        private const float AbsorbDist = 30f;
        /// <summary>最高回流速度 px/t</summary>
        private const float MaxSpeed = 26f;

        private ref float HealAmount => ref Projectile.ai[0];
        private ref float Seed => ref Projectile.ai[1];
        private ref float Timer => ref Projectile.localAI[0];

        private Player Owner => Main.player[Projectile.owner];

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 12;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Player owner = Owner;
            if (!owner.active || owner.dead || owner.ghost) {
                //拥有者不在场：血珠失去归所，散作坠地血滴
                Fizzle();
                return;
            }
            Timer++;

            if (Timer <= CondenseTicks) {
                //凝珠：先违背直觉地漂离玩家一小段(逆流前的蓄势)，尺寸自 0 撑起；
                //漂移相位走种子确定性，端间轨迹一致
                Vector2 away = (Projectile.Center - owner.Center).SafeNormalize(Vector2.UnitY * -1f);
                float drift = MathF.Sin(Timer * 0.8f + Seed) * 0.4f;
                Projectile.velocity = away * 1.6f + away.RotatedBy(MathHelper.PiOver2) * drift;
                Projectile.position += Projectile.velocity;
                Projectile.scale = Timer / CondenseTicks;
                return;
            }

            //逆流：向玩家加速汇聚，垂线正弦摆动随接近收敛
            Vector2 toOwner = owner.Center - Projectile.Center;
            float dist = toOwner.Length();
            if (dist < AbsorbDist) {
                Absorb(owner);
                return;
            }
            Vector2 dir = toOwner / dist;
            float speedRamp = MathHelper.Clamp((Timer - CondenseTicks) / 26f, 0f, 1f);
            float speed = MathHelper.Lerp(2.5f, MaxSpeed, speedRamp * speedRamp);
            //蛇行摆幅：远处大、近处收(确定性相位，端间无判定依赖)
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float weave = MathF.Sin(Timer * 0.55f + Seed * 0.7f) * MathHelper.Clamp(dist / 260f, 0f, 1f) * 4.2f;
            Projectile.velocity = dir * speed + perp * weave;
            Projectile.position += Projectile.velocity;
            Projectile.scale = 1f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //回流尾迹：小血珠向后洒落
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    -Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    WofMotionFX.BloodMid, Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(10, 18), 0.2f);
            }
            if (!VaultUtils.isServer) {
                Lighting.AddLight(Projectile.Center, WofMotionFX.BloodHot.ToVector3() * 0.25f);
            }
        }

        //自管位移(凝珠段反向漂移与逆流段变速曲线都不走默认积分)
        public override bool ShouldUpdatePosition() => false;

        /// <summary>触体吸收：拥有者端写生命(独立通道：不动药水疲劳、不动原版吸血预算)</summary>
        private void Absorb(Player owner) {
            int amount = (int)HealAmount;
            if (Main.myPlayer == Projectile.owner && amount > 0 && owner.Alives()) {
                owner.statLife = Math.Min(owner.statLife + amount, owner.statLifeMax2);
                owner.HealEffect(amount);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item3 with { Pitch = 0.35f, Volume = 0.45f }, owner.Center);
                //吸入演出：血滴向体心收拢
                for (int i = 0; i < 6; i++) {
                    Vector2 off = Main.rand.NextVector2CircularEdge(20f, 20f);
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(owner.Center + off,
                        -off * 0.12f, WofMotionFX.BloodHot,
                        Main.rand.NextFloat(0.4f, 0.7f))?.Configure(Main.rand.Next(8, 14), 0.1f);
                }
            }
            Projectile.Kill();
        }

        /// <summary>失所血珠：无人可治，散作重坠血滴</summary>
        private void Fizzle() {
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                        Main.rand.NextVector2Circular(2f, 1f) + Vector2.UnitY * 2f,
                        WofMotionFX.BloodDark, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(20, 32), 0.4f);
                }
            }
            Projectile.Kill();
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D drop = CWRAsset.Extra_98.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            //快成线、慢成珠：速度拉伸各向异性
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0f, 1.1f);
            Vector2 bodyScale = new Vector2(0.4f * (1f - stretch * 0.3f), 0.55f * (1f + stretch * 1.6f))
                * Projectile.scale;

            //暗珠体(真 alpha，可承载暗色)
            Main.EntitySpriteDraw(drop, screenPos, null, WofMotionFX.BloodMid, Projectile.rotation,
                drop.Size() / 2f, bodyScale, SpriteEffects.None, 0);
            //湿核
            Main.EntitySpriteDraw(drop, screenPos - new Vector2(1f, 2f) * Projectile.scale, null,
                WofMotionFX.BloodHot * 0.85f, Projectile.rotation,
                drop.Size() / 2f, bodyScale * 0.55f, SpriteEffects.None, 0);
            //辉光(A=0 加色技法，AlphaBlend 批内合法)
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Main.EntitySpriteDraw(glow, screenPos, null, new Color(255, 60, 45, 0) * (0.45f * Projectile.scale),
                0f, glow.Size() / 2f, 0.5f * Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
