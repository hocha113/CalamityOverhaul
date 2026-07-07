using CalamityOverhaul.Content.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Magic.AriaofTheCosmoses
{
    /// 寰宇咏叹调手持 左键蓄力掷盘 右键压扁领域 Q/R见<see cref="AriaofTheCosmos.HoldItem"/>
    internal class AriaofTheCosmosHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Magic + "AriaofTheCosmos";
        public override int TargetID => ModContent.ItemType<AriaofTheCosmos>();
        public override bool CanRightClick => true;

        //蓄力阶段分界
        private const int Stage1 = 60;
        private const int Stage2 = 120;
        private const int MaxChargeTime = 180;
        private const int MinChargeTimeR = 30;

        //左键蓄力(按键由基类同步 本地累积驱动表现；黑洞演出由 AccretionDisk 自驱)
        private int chargeTime;
        private float chargeProgress;
        private int accretionDiskIndex = -1;
        private bool isCharging;

        //右键蓄力状态
        private int chargeTimeR;
        private float chargeProgressR;
        private int flattenedDiskIndex = -1;
        private bool isChargingR;

        //蓄力中不自毁 松键下一帧由释放逻辑收尾
        public override bool StayAlive() => isCharging || isChargingR;

        public override void SetGunProperty() {
            Projectile.DamageType = DamageClass.Magic;
            HandFireDistanceX = 25;
            HandFireDistanceY = -8;
            MuzzleForwardOffset = 30;
            GunPressure = 0;
            ControlForce = 0;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(CanFire);

            //左键蓄力
            if (WantsFireLeft) {
                isCharging = true;
                chargeTime = Math.Min(chargeTime + 1, MaxChargeTime);
                chargeProgress = chargeTime / (float)MaxChargeTime;
                UpdateChargeEffects();
                UpdateAccretionDisk();
            }
            else if (isCharging) {
                ReleaseAttack();
                ResetCharge();
            }

            //右键蓄力
            if (WantsFireRight) {
                isChargingR = true;
                chargeTimeR = Math.Min(chargeTimeR + 1, MaxChargeTime);
                chargeProgressR = chargeTimeR / (float)MaxChargeTime;
                UpdateChargeEffectsR();
                UpdateFlattenedDisk();
                PlayChargeSoundR();
            }
            else if (isChargingR) {
                ReleaseAttackR();
                ResetChargeR();
            }

            Time++;
        }

        #region 左键：黑洞
        private void UpdateChargeEffects() {
            //黑洞自身演出(恒星→坍缩→视界)由 AccretionDisk 自驱；这里只做持械者侧反馈
            if (!VaultUtils.isServer && chargeTime > AccretionDisk.CollapseEnd && Main.rand.NextBool(6)) {
                //法杖口飘向黑洞的引力尘
                Vector2 particlePos = ShootPos + Main.rand.NextVector2Circular(20, 20);
                PRTLoader.NewParticle<PRT_Spark>(particlePos,
                    (DiskAnchorPos - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f),
                    Color.Lerp(AccretionDisk.ColGold, AccretionDisk.ColHot, Main.rand.NextFloat()) * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(10, 16), Owner);
            }

            //满蓄低频压迫感
            if (chargeTime >= MaxChargeTime - 1) {
                Owner.CWR().GetScreenShake(1.2f);
            }
        }

        /// <summary>黑洞锚点：法杖前方随蓄力推远，给成长的天体让位</summary>
        private Vector2 DiskAnchorPos => ShootPos + ToMouse.SafeNormalize(Vector2.Zero) * (40f + chargeProgress * 90f);

        private void UpdateAccretionDisk() {
            //黑洞仅主人端生成操控 远端靠弹幕同步
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (accretionDiskIndex == -1 || !Main.projectile[accretionDiskIndex].active
                || Main.projectile[accretionDiskIndex].type != ModContent.ProjectileType<AccretionDisk>()) {
                accretionDiskIndex = Projectile.NewProjectile(Source, DiskAnchorPos, Vector2.Zero
                    , ModContent.ProjectileType<AccretionDisk>()
                    , WeaponDamage * 2, WeaponKnockback, Owner.whoAmI);
            }

            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];
                disk.Center = Vector2.Lerp(disk.Center, DiskAnchorPos, 0.35f);
                disk.timeLeft = 10;//保持存活
            }
        }

        private void ReleaseAttack() {
            //视界尚未诞生就松手：恒星溃散,不掷出不耗蓝
            if (chargeTime < AccretionDisk.MinThrowCharge) {
                return;
            }

            PlayReleaseSound();
            Owner.CWR().GetScreenShake(5f + chargeProgress * 9f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            //黑洞转入掷出态
            if (accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active) {
                Projectile disk = Main.projectile[accretionDiskIndex];
                if (disk.ModProjectile is AccretionDisk accretion) {
                    float damageMultiplier = MathHelper.Lerp(1f, 3.5f, chargeProgress);
                    disk.damage = (int)(WeaponDamage * damageMultiplier);
                    disk.knockBack = WeaponKnockback * (1f + chargeProgress);
                    disk.friendly = true;
                    disk.timeLeft = (int)(120 + chargeProgress * 180);//2-5秒
                    disk.velocity = (InMousePos - disk.Center).SafeNormalize(Vector2.Zero) * (8f + chargeProgress * 12f);
                    accretion.ThrownState = 1f;
                    disk.netUpdate = true;
                }
            }

            //掷出的反作用力与魔力支付
            Owner.velocity -= ShootVelocity.SafeNormalize(Vector2.Zero) * (3f + chargeProgress * 5f);
            Owner.statMana = Math.Max(Owner.statMana - (int)(Item.mana * (1f + chargeProgress)), 0);
            HoldManaRegenDelay();
        }

        private void PlayReleaseSound() {
            float volume = 0.8f + chargeProgress * 0.4f;
            float pitch = -0.3f + chargeProgress * 0.5f;

            SoundEngine.PlaySound(SoundID.Item109 with { Volume = volume, Pitch = pitch }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_ExplosiveTrapExplode with { Volume = volume * 0.6f, Pitch = pitch }, Projectile.Center);

            if (chargeProgress >= 0.66f) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
            }
        }

        private void ResetCharge() {
            chargeTime = 0;
            chargeProgress = 0;
            isCharging = false;

            //清理未掷出黑洞(已掷出 friendly=true 不受影响)
            if (Projectile.IsOwnedByLocalPlayer()
                && accretionDiskIndex >= 0 && Main.projectile[accretionDiskIndex].active
                && Main.projectile[accretionDiskIndex].type == ModContent.ProjectileType<AccretionDisk>()
                && !Main.projectile[accretionDiskIndex].friendly) {
                Main.projectile[accretionDiskIndex].Kill();
            }
            accretionDiskIndex = -1;
        }
        #endregion

        #region 右键：事件视界领域
        private void UpdateChargeEffectsR() {
            //领域自身演出由 FlattenedAccretionDisk 自驱;这里只做持械者侧反馈
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Vector2 particlePos = ShootPos + Main.rand.NextVector2Circular(20, 20);
                PRTLoader.NewParticle<PRT_Spark>(particlePos,
                    (DomainAnchorPos - particlePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4f),
                    Color.Lerp(GammaRayBeam.ColViolet, GammaRayBeam.ColCheren, Main.rand.NextFloat()) * 0.8f,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(false, Main.rand.Next(10, 16), Owner);
            }

            if (chargeTimeR >= Stage2) {
                Owner.CWR().GetScreenShake(chargeProgressR * 1.5f);
            }
        }

        /// <summary>领域锚点：法杖前方随蓄力推远</summary>
        private Vector2 DomainAnchorPos => ShootPos + ToMouse.SafeNormalize(Vector2.Zero) * (50f + chargeProgressR * 110f);

        private void UpdateFlattenedDisk() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            if (flattenedDiskIndex == -1 || !Main.projectile[flattenedDiskIndex].active
                || Main.projectile[flattenedDiskIndex].type != ModContent.ProjectileType<FlattenedAccretionDisk>()) {
                flattenedDiskIndex = Projectile.NewProjectile(Source, DomainAnchorPos, Vector2.Zero
                    , ModContent.ProjectileType<FlattenedAccretionDisk>()
                    , WeaponDamage, WeaponKnockback, Owner.whoAmI);
            }

            if (flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active) {
                Projectile disk = Main.projectile[flattenedDiskIndex];
                disk.Center = Vector2.Lerp(disk.Center, DomainAnchorPos, 0.3f);
                disk.timeLeft = 10;
                disk.rotation = ToMouseA;

                if (disk.ModProjectile is FlattenedAccretionDisk flattenedDisk) {
                    flattenedDisk.ChargeProgress = chargeProgressR;
                }
            }
        }

        private void PlayChargeSoundR() {
            if (chargeTimeR == 1) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            }
            else if (chargeTimeR == Stage1) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 0.7f, Pitch = -0.5f }, Projectile.Center);
            }
            else if (chargeTimeR == Stage2) {
                SoundEngine.PlaySound(SoundID.DD2_DarkMageHealImpact with { Volume = 0.8f, Pitch = -0.3f }, Projectile.Center);
            }
        }

        private void ReleaseAttackR() {
            if (chargeTimeR < MinChargeTimeR) {
                return;
            }

            PlayReleaseSoundR();
            Owner.CWR().GetScreenShake(3f + chargeProgressR * 8f);

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }

            Owner.statMana = Math.Max(Owner.statMana - (int)(Item.mana * 0.8f * (1f + chargeProgressR * 0.5f)), 0);
            HoldManaRegenDelay();
        }

        private void PlayReleaseSoundR() {
            float volume = 0.7f + chargeProgressR * 0.3f;
            float pitch = 0.1f + chargeProgressR * 0.4f;

            SoundEngine.PlaySound(SoundID.Item84 with { Volume = volume, Pitch = pitch }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.DD2_LightningBugZap with { Volume = volume * 0.5f, Pitch = pitch }, Projectile.Center);

            if (chargeProgressR >= 0.66f) {
                SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.6f, Pitch = 0.3f }, Projectile.Center);
            }
        }

        private void ResetChargeR() {
            chargeTimeR = 0;
            chargeProgressR = 0;
            isChargingR = false;

            //压扁吸积盘仅蓄力期存在 结束即清理
            if (Projectile.IsOwnedByLocalPlayer()
                && flattenedDiskIndex >= 0 && Main.projectile[flattenedDiskIndex].active
                && Main.projectile[flattenedDiskIndex].type == ModContent.ProjectileType<FlattenedAccretionDisk>()) {
                Main.projectile[flattenedDiskIndex].Kill();
            }
            flattenedDiskIndex = -1;
        }
        #endregion

        public override void OnKill(int timeLeft) {
            ResetCharge();
            ResetChargeR();
        }
    }
}
