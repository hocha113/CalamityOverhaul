using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 美杜莎头颅重铸：全接管为「蛇发凝视」通道（原版持握周身石化手感僵硬，公认最弱魔法武器之一，给足 135% 档）。<br/>
    /// 左键按住朝光标 70° 扇区持续凝视，叠石纹满 4 层小石化（Boss 豁免）；
    /// 白热带扇区收窄 40° 换伤害 ×1.4 聚焦；右键泄压「蛇发怒视」全向 8 条石化射线；
    /// 过载反噬自石（自身减速 0.8s）进过热锁
    /// </summary>
    internal class GsMedusaHead : GsHeatScheme
    {
        public override int TargetItemID => ItemID.MedusaHead;

        protected override string GsDescFallback =>
            "Reforged: hold to unleash a petrifying gaze cone; stone-mark stacks briefly petrify, white heat narrows the gaze into a focused glare\nRight click to vent all heat as an eight-way stone glare";

        internal override float HeatPerShot => 0f;
        internal override float CoolRatePerTick => 1.0f;
        internal override float WhiteHotDamageMult => 1.4f;
        internal override Color MuzzleTheme => GsConduitVFX.StoneMain;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (base.GsCanUseItem(item, player) == false) {
                return false;
            }
            //通道在场即冷却（含收尾相），防重复生成
            if (HeldAlive<GsGazeBeamProj>(player)) {
                return false;
            }
            return null;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //owner 端生成凝视通道（GsShoot 只在 owner 端执行），压掉原版弹幕
            if (player.whoAmI == Main.myPlayer && !HeldAlive<GsGazeBeamProj>(player)) {
                Projectile.NewProjectile(source, player.MountedCenter, GsAimUnit(player),
                    ModContent.ProjectileType<GsGazeBeamProj>(), damage, knockback, player.whoAmI);
            }
            return false;
        }

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //蛇发怒视：全向 8 条石化射线，威力随热量走（0 蓝、无锁）
            float power = 1.2f * (0.6f + 0.6f * hp.Heat / GsHeatPlayer.HeatMax);
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * power));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), player.MountedCenter, Vector2.Zero,
                ModContent.ProjectileType<GsConduitRayProj>(), damage, 4f, player.whoAmI,
                8f, GsAimUnit(player).ToRotation(), 0f);
        }

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            //反噬自石：凝视者被自己的蛇发盯上（owner 给自己上原版减速，走原生玩家 buff 同步）
            player.AddBuff(BuffID.Slow, 48);
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_MarbleChip>(player.MountedCenter + Main.rand.NextVector2Circular(12f, 18f),
                        Main.rand.NextVector2Circular(2f, 1f) - new Vector2(0f, 1.4f),
                        GsConduitVFX.StoneMain, Main.rand.NextFloat(0.5f, 0.9f));
                }
            }
        }
    }
}
