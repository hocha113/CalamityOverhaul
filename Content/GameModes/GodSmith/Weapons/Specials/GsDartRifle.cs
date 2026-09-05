using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Guns;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Specials
{
    /// <summary>
    /// 镖步枪重铸（L1 弹口层 + 蓄力时序闸）：镖种特效完整保留。<br/>
    /// [贯穿蓄射] 按住蓄力，松开放镖：满蓄 60 tick 时镖速 ×2、穿透 +2、伤害 +50%，
    /// 未满按比例给部分加成。原版 use 流程被压掉，发射由松开触发、手动走 PickAmmo（1 镖）
    /// </summary>
    internal class GsDartRifle : GodSmithScheme
    {
        public override int TargetItemID => ItemID.DartRifle;

        public override string GsFamily => "Specials";

        protected override string GsDescFallback =>
            "Reforged: hold to charge, the aim line fades in, release to loose; a full 1s draw gives x2 dart speed, +2 pierce and +50% damage\nDart ammo effects are fully preserved";
        /// <summary>满蓄所需帧数</summary>
        private const int ChargeFull = 60;

        //以下瞬时字段全部只在本地玩家路径消费（方案单例的 owner 契约）
        private int charge;
        private uint lastHoldTick;
        private float pendingChargeRatio;

        /// <summary>蓄射压掉原版 use：发射改由「松开」在 GsHoldItem 里触发</summary>
        public override bool? GsCanUseItem(Item item, Player player) => false;

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            //断持重置：换走武器再切回来，残留的蓄力作废
            if (Main.GameUpdateCount - lastHoldTick > 2) {
                charge = 0;
            }
            lastHoldTick = Main.GameUpdateCount;

            //压掉 use 流后 itemAnimation 恒 0，原版不绘制枪体：持枪姿态件常驻补位
            GsGunHoldPoseProj.Ensure(player, TargetItemID, 0f);
            if (player.controlUseItem && !player.mouseInterface) {
                int prev = charge;
                charge = Math.Min(charge + 1, ChargeFull);
                if (prev < ChargeFull && charge >= ChargeFull && !VaultUtils.isServer) {
                    //满蓄咔哒
                    SoundEngine.PlaySound(SoundID.Unlock with { Volume = 0.5f, Pitch = 0.6f }, player.Center);
                }
            }
            else if (charge > 0) {
                int released = charge;
                charge = 0;
                if (released >= 4) {
                    FireChargedDart(item, player, released);
                }
            }
        }

        /// <summary>松开放镖：手动走 PickAmmo 原版弹药链（1 镖），按蓄力比给加成</summary>
        private void FireChargedDart(Item item, Player player, int released) {
            if (!player.PickAmmo(item, out int projType, out float speed, out int damage,
                out float knockback, out int usedAmmoID, false)) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item16 with { Volume = 0.4f, Pitch = -0.4f }, player.Center);
                }
                return;
            }
            float ratio = MathHelper.Clamp(released / (float)ChargeFull, 0f, 1f);
            pendingChargeRatio = ratio;
            Vector2 aim = GsAimUnit(player);
            Vector2 vel = aim * speed * (1f + ratio);
            int dmg = (int)(damage * (1f + 0.5f * ratio));
            Projectile.NewProjectile(player.GetSource_ItemUse_WithPotentialAmmo(item, usedAmmoID),
                player.Center + aim * 20f, vel, projType, dmg, knockback, player.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item98 with {
                    Volume = 0.55f + 0.25f * ratio,
                    Pitch = -0.1f + 0.35f * ratio
                }, player.Center);
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //满蓄贯穿 +2，带 >0 守卫防 -1 无限穿被写坏
            if (pendingChargeRatio >= 0.99f && proj.penetrate > 0) {
                proj.penetrate += 2;
            }
            pendingChargeRatio = 0f;
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.05f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
