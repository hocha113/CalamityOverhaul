using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 充能爆破炮重铸：湮灭充能。材质身份：青蓝电浆（月总科技的湮灭态能量）。<br/>
    /// ①按住引导：低热喷电浆弹，白热升格聚能弧束脉冲；引导持续积热=充能；<br/>
    /// ②过载即「湮灭炮」：触顶自动轰出巨型电浆球（电弧舔舐近敌）再进锁——
    /// 过载是本炮的大招而非纯惩罚，充能拉满就是为了这一发；<br/>
    /// ③泄压：提前手动放出小型聚能弹（威力随充能），不想等满充时的止盈键
    /// </summary>
    internal class GsChargedBlasterCannon : GsHeatScheme
    {
        public override int TargetItemID => ItemID.ChargedBlasterCannon;

        protected override string GsDescFallback =>
            "Reforged: hold to channel; cold plasma spits bolts, white heat pulses focused arc lances" +
            "\nCharging never stops: cap the gauge and the cannon itself fires the Annihilator, a massive plasma sphere that licks arcs at everything nearby" +
            "\nRight click to cash out early as a lesser charge shot";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 0.6f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Lock;
        internal override int OverloadLockTicks => 100;
        internal override float VentMinHeat => 30f;
        internal override Color MuzzleTheme => GsChargedBlasterCannonHeldProj.PlasmaMain;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            if (HeldAlive<GsChargedBlasterCannonHeldProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //全接管：左键只负责唤起引导 held（动画法由 held 的族层持械姿态达标）
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsChargedBlasterCannonHeldProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsChargedBlasterCannonHeldProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            //湮灭炮：过载不是白挨的——充能全部化为一发巨型电浆球（owner 端路径）
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * 2.2f));
            Projectile.NewProjectile(player.GetSource_Misc("GsBlasterAnnihilate"),
                player.MountedCenter + GsAimUnit(player) * 30f, GsAimUnit(player) * 4.5f,
                ModContent.ProjectileType<GsChargedBlasterCannonOrbProj>(), damage, 8f, player.whoAmI);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item92 with { Volume = 1f, Pitch = -0.5f }, player.Center);
            }
        }

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //提前止盈：小型聚能弹，威力随充能（0.6~1.6 倍）
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.6f + 1.0f * frac)));
            int idx = Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"),
                player.MountedCenter + GsAimUnit(player) * 26f, GsAimUnit(player) * 9f,
                ProjectileID.ChargedBlasterOrb, damage, 5f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Main.projectile[idx].scale *= 0.8f + 0.6f * frac;
                Main.projectile[idx].netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item91 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            }
        }
    }
}
