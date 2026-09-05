using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Launchers
{
    /// <summary>
    /// 近战雷发射器重铸：智能雷场。敌人触发原样自爆；
    /// 布设超过 60 秒未爆的雷自动引爆并返还 1 发所用火箭（每分钟封顶 8 发防囤积）。<br/>
    /// 原版落雷 AI 一概不动；MarkData = 布设序号，MarkData2 = 所耗弹药物品 ID
    /// </summary>
    internal class GsProximityMineLauncher : GsLauncherScheme
    {
        public override int TargetItemID => ItemID.ProximityMineLauncher;

        protected override string GsDescFallback =>
            "Reforged: mines show their trigger radius to you; mines idle for 60s refund a rocket and self-clear";
        /// <summary>地雷主弹全家</summary>
        internal static readonly HashSet<int> MineTypes = [
            ProjectileID.ProximityMineI, ProjectileID.ProximityMineII,
            ProjectileID.ProximityMineIII, ProjectileID.ProximityMineIV,
            ProjectileID.ClusterMineI, ProjectileID.ClusterMineII,
            ProjectileID.WetMine, ProjectileID.LavaMine, ProjectileID.HoneyMine,
            ProjectileID.MiniNukeMineI, ProjectileID.MiniNukeMineII, ProjectileID.DryMine,
        ];

        /// <summary>雷场红（回收漂字着色）</summary>
        internal static readonly Color MineRed = new(255, 96, 70);

        /// <summary>最近一次 PickAmmo 的弹药物品 ID，owner 端射击链内消费</summary>
        private int pendingAmmoType;

        private LocalizedText tipRecycle;

        /// <summary>每雷本地包：布设龄计数（回收判定）</summary>
        private class MineState
        {
            public int age;
        }

        public override void GsSetStaticDefaults()
            => tipRecycle = this.GetLocalization("TipRecycle", () => "Mine reclaimed");

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        public override void GsPickAmmo(Item weapon, Item ammo, Player player,
            ref int type, ref float speed, ref StatModifier damage, ref float knockback) {
            if (player.whoAmI == Main.myPlayer) {
                pendingAmmoType = ammo.type;
            }
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            if (!MineTypes.Contains(proj.type)) {
                return;
            }
            GsLaunchersPlayer mp = Main.player[proj.owner].GetModPlayer<GsLaunchersPlayer>();
            router.MarkData = ++mp.mineSeq;
            router.MarkData2 = pendingAmmoType;
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            LaunchRecoil(player, velocity, 1.2f);
            return null;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!MineTypes.Contains(proj.type) || proj.timeLeft <= 3) {
                return;
            }
            //回收计龄：60 秒未爆自动引爆并返还弹药（owner 端权威）
            MineState st = router.GetOrCreateState<MineState>();
            st.age++;
            if (st.age < 3600 || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsLaunchersPlayer mp = player.GetModPlayer<GsLaunchersPlayer>();
            int ammoType = (int)router.MarkData2;
            if (mp.mineRecycleBudget > 0 && ammoType > ItemID.None) {
                mp.mineRecycleBudget--;
                player.QuickSpawnItem(proj.GetSource_FromThis(), ammoType);
                LocalTip(player, tipRecycle, MineRed);
            }
            GsDetonate(proj);
        }
    }
}
