using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns;
using InnoVault.GameContent.BaseEntity;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 链式机枪重铸（L3 手持接管）：转管姿态由手持弹幕承载，「最快枪」身份保留。<br/>
    /// [全域压制] 按住约 0.5 秒转管起转（爬音），随后 4t/发 热身至 3t/发，散布大、
    /// 持续压制 8 秒枪管红热，强制 12t/发 直到松手散热；开火期间移速下降。<br/>
    /// 子弹逐发走 <see cref="Player.PickAmmo"/>（原版 50% 省弹保留），弹药身份不灭
    /// </summary>
    internal class GsChainGun : GodSmithScheme
    {
        public override int TargetItemID => ItemID.ChainGun;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: hold to spin up, ramping to the fastest fire rate in the land until the barrels glow red-hot after 8s of sustained fire, forcing a slowdown until you let go\nFiring slows your movement while you suppress";
        public override bool? GsCanUseItem(Item item, Player player) {
            if (HeldAlive<GsChainGunHeld>(player)) {
                return false;
            }
            if (player.whoAmI == Main.myPlayer) {
                Projectile.NewProjectile(player.GetSource_ItemUse(item), player.Center, GsAimUnit(player),
                    ModContent.ProjectileType<GsChainGunHeld>(),
                    player.GetWeaponDamage(item), item.knockBack, player.whoAmI);
            }
            return false;
        }
    }

    /// <summary>
    /// 链式机枪手持弹幕：转管、射速热身、红热惩罚、逐发耗弹全部自管。<br/>
    /// ai[2]=干仓旗标；热量与转管由同步的 DownLeft 输入流各端确定性积分
    /// </summary>
    internal class GsChainGunHeld : BaseHeldProj
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override LocalizedText DisplayName => Language.GetText("ItemName.ChainGun");

        /// <summary>转管起转时长</summary>
        private const int SpinUpTicks = 30;
        /// <summary>热身时长：起转完成后射速从 4t 渐进到 3t</summary>
        private const int WarmupTicks = 120;
        /// <summary>红热阈值（8 秒持续压制）</summary>
        private const float HeatMax = 480f;
        /// <summary>停火收枪延时</summary>
        private const int IdleKillDelay = 40;

        private int spinTimer;
        private float warmth;
        private float heat;
        private bool overheated;
        private int fireTimer;
        private int idleTimer;
        private int dryTimer;
        private float recoilAnim;
        private bool spinSoundLatch;

        private bool Dry => Projectile.ai[2] > 0f;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
            Projectile.hide = true;
            Projectile.timeLeft = 60;
            Projectile.CWR().NotSubjectToSpecialEffects = true;
        }

        public override bool? CanDamage() => false;

        public override void AI() {
            //模式被关立刻收枪；换武器/死亡同判
            if (!GameModeSystem.GodSmithActive || Item.type != ItemID.ChainGun
                || Owner.dead || !Owner.active || Owner.noItems) {
                Projectile.Kill();
                return;
            }
            Projectile.timeLeft = 2;

            UpdatePose();

            bool wantFire = DownLeft && !Owner.CCed && !Dry;

            //转管：起转完成才开火
            if (wantFire) {
                if (spinTimer < SpinUpTicks) {
                    spinTimer++;
                    HandleSpinUpAudio();
                }
            }
            else {
                spinTimer = Math.Max(0, spinTimer - 2);
                spinSoundLatch = false;
            }

            bool readyToFire = wantFire && spinTimer >= SpinUpTicks;
            if (readyToFire) {
                idleTimer = 0;
                warmth = MathF.Min(warmth + 1f, WarmupTicks);
                heat = MathF.Min(heat + 1f, HeatMax);
                if (!overheated && heat >= HeatMax) {
                    overheated = true;
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item34 with { Volume = 0.7f, Pitch = -0.6f }, Projectile.Center);
                    }
                }
                //压制开火期间的负重减速，owner 权威位置随原生同步
                if (Projectile.IsOwnedByLocalPlayer() && !Owner.mount.Active) {
                    Owner.velocity.X *= 0.93f;
                }
                float atkSpeed = Owner.GetWeaponAttackSpeed(Item);
                if (atkSpeed <= 0f) {
                    atkSpeed = 1f;
                }
                float baseInterval = overheated ? 12f : MathHelper.Lerp(4f, 3f, warmth / WarmupTicks);
                int interval = Math.Max(1, (int)MathF.Round(baseInterval / atkSpeed));
                if (++fireTimer >= interval) {
                    fireTimer = 0;
                    FireOnce();
                }
            }
            else {
                idleTimer++;
                warmth = MathF.Max(0f, warmth - 2f);
                fireTimer = 99;
                if (Dry && Projectile.IsOwnedByLocalPlayer()) {
                    SetDry(false);
                }
                if (idleTimer > IdleKillDelay) {
                    Projectile.Kill();
                    return;
                }
            }

            //松手快速散热，热量归零解除红热（迟滞回环）
            if (!readyToFire) {
                heat = MathF.Max(0f, heat - 5f);
            }
            if (overheated && heat <= 0f) {
                overheated = false;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = -0.1f }, Projectile.Center);
                }
            }

            if (Dry) {
                if (++dryTimer > 30 && Projectile.IsOwnedByLocalPlayer()) {
                    Projectile.Kill();
                    return;
                }
            }
            else {
                dryTimer = 0;
            }

            recoilAnim = MathF.Max(0f, recoilAnim - 0.2f);
        }

        /// <summary>转管爬音：起转期每 6 tick 一声，音调随进度上爬</summary>
        private void HandleSpinUpAudio() {
            if (VaultUtils.isServer) {
                return;
            }
            if (spinTimer % 6 == 1) {
                float progress = spinTimer / (float)SpinUpTicks;
                SoundEngine.PlaySound(SoundID.Item22 with {
                    Volume = 0.35f + progress * 0.2f,
                    Pitch = -0.5f + progress * 0.9f,
                    MaxInstances = 3
                }, Projectile.Center);
            }
            if (!spinSoundLatch && spinTimer >= SpinUpTicks) {
                spinSoundLatch = true;
                SoundEngine.PlaySound(SoundID.Item149 with { Volume = 0.45f, Pitch = 0.4f }, Projectile.Center);
            }
        }

        private void UpdatePose() {
            //转管与全速开火时枪身高频微震，identity 定相
            float shake = 0f;
            if (spinTimer > 0) {
                float intensity = MathHelper.Clamp(spinTimer / (float)SpinUpTicks, 0f, 1f);
                shake = MathF.Sin(Main.GameUpdateCount * 2.3f + Projectile.identity) * 0.014f * intensity;
            }
            GsGunPose.Update(this, 22f, -6f, recoilAnim * 0.045f + shake, recoilAnim * 2.6f, 0.4f);
        }

        /// <summary>一发：owner 端走原版弹药链生成子弹，各端播音与后坐</summary>
        private void FireOnce() {
            recoilAnim = 1f;

            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item11 with {
                    Volume = 0.32f,
                    Pitch = (overheated ? -0.35f : 0.1f) + (Projectile.identity % 5) * 0.01f,
                    MaxInstances = 4
                }, Projectile.Center);
            }

            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            if (!Owner.PickAmmo(Item, out int projToShoot, out float speed, out int damage,
                out float knockback, out _, false)) {
                SetDry(true);
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.4f, Pitch = -0.4f }, Projectile.Center);
                }
                return;
            }
            SetDry(false);

            const float Spread = 0.14f;
            Vector2 muzzle = GsGunPose.MuzzlePos(Projectile, DirSign, 34f, -2f);
            Vector2 vel = (ToMouseA + Main.rand.NextFloat(-Spread, Spread)).ToRotationVector2() * speed;
            Projectile.NewProjectile(Owner.GetSource_ItemUse(Item), muzzle, vel,
                projToShoot, Math.Max(1, (int)(damage * 0.95f)), knockback, Owner.whoAmI);
        }

        private void SetDry(bool value) {
            if (Dry != value) {
                Projectile.ai[2] = value ? 1f : 0f;
                NetUpdate();
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //手持接管期间原版不绘制枪体，此为必要的一笔
            GsGunPose.DrawGunBody(ItemID.ChainGun, Projectile.Center, Projectile.rotation, DirSign, lightColor);
            return false;
        }
    }
}
