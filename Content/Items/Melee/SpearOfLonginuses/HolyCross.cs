using CalamityOverhaul.Common;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.SpearOfLonginuses
{
    /// <summary>
    /// 玩家手持朗基努斯之枪时悬浮于身下的圣神十字架，
    /// shader 光十字自下而上点亮显示能量，座环刻度显示立场层数，与盗贼系统解耦
    /// </summary>
    internal class HolyCross : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float spawnProgress;
        private float pulsePhase;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
        }

        public override bool ShouldUpdatePosition() => false;

        public override bool? CanDamage() => false;

        private Player Owner => Main.player[Projectile.owner];

        public override void AI() {
            Player player = Owner;

            //玩家不再持枪、死亡或离开时立即销毁
            if (!player.active || player.dead
                || player.HeldItem?.type != SpearOfLonginus.ID
                || player.CountProjectilesOfID<LonginusHeld>() == 0) {
                Projectile.Kill();
                return;
            }

            //出现动画
            if (spawnProgress < 1f) {
                spawnProgress = MathHelper.Clamp(spawnProgress + 0.05f, 0f, 1f);
            }
            pulsePhase += 0.08f;

            //悬浮于玩家身下，随重力方向调整
            float verticalOffset = 70f * player.gravDir;
            float bob = (float)Math.Sin(Main.GameUpdateCount * 0.05f) * 3f;
            Projectile.Center = player.Center + new Vector2(0, verticalOffset + bob * player.gravDir);
            Projectile.timeLeft = 2;

            //简单的环境光照
            if (player.HeldItem.ModItem is SpearOfLonginus longinus) {
                float fill = longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                    ? 1f
                    : longinus.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax;
                float lightIntensity = 0.4f + fill * 0.8f;
                Lighting.AddLight(Projectile.Center
                    , 1.0f * lightIntensity, 0.85f * lightIntensity, 0.35f * lightIntensity);

                //当能量接近上限时偶尔散发金色尘屑
                if (fill > 0.85f && Main.rand.NextBool(4)) {
                    Vector2 dustPos = Projectile.Center + Main.rand.NextVector2Circular(22f, 22f);
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.GoldCoin, Main.rand.NextVector2Circular(1f, 1f) - new Vector2(0, 1f), 0
                        , default, Main.rand.NextFloat(0.7f, 1.1f));
                    d.noGravity = true;
                    d.fadeIn = 0.6f;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Owner?.HeldItem?.ModItem is not SpearOfLonginus longinus) {
                return;
            }

            //圣神能量进度（达到最大立场后保持满）
            float fill = longinus.ChargeGrade >= SpearOfLonginus.MaxChargeGrade
                ? 1f
                : MathHelper.Clamp(longinus.HolyEnergy / (float)SpearOfLonginus.HolyEnergyMax, 0f, 1f);

            float appear = MathHelper.Clamp(spawnProgress * 1.25f, 0f, 1f);
            float dir = Owner.gravDir;
            Vector2 up = new Vector2(0, -dir);
            Vector2 ringCenter = Projectile.Center + new Vector2(0, 10f * dir);
            float breathe = 0.5f + 0.5f * (float)Math.Sin(pulsePhase);

            //座环
            LonginusVFX.DrawHalo(ringCenter, 30f, 0.38f, appear, fill * 0.45f + breathe * 0.2f, 0.6f);

            //光十字计量，自下而上点亮
            LonginusVFX.DrawCross(Projectile.Center, up, 40f, 22f, appear, 0f, 0.8f
                , 0.12f, fill >= 1f ? 0.35f * breathe : 0.08f, fill);

            //层数刻度：座环上的小光环
            int grade = longinus.ChargeGrade;
            if (grade > 0) {
                float baseRot = pulsePhase * 0.6f;
                for (int i = 0; i < grade; i++) {
                    float angle = baseRot + MathHelper.TwoPi * i / SpearOfLonginus.MaxChargeGrade;
                    Vector2 offset = angle.ToRotationVector2() * 34f;
                    offset.Y *= 0.38f * dir;
                    float ph = 0.5f + 0.5f * (float)Math.Sin(pulsePhase + i);
                    LonginusVFX.DrawHalo(ringCenter + offset, 5.5f, 0.9f, appear, ph, 0.85f, LonginusVFX.HolyGold);
                }
            }
        }
    }
}
