using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 掷瓶共享:环境转换功能原样保留(经典不毁的功能版),增强层只在爆点追加一片 3s 领域。
    /// 掷瓶不吃远程伤害体系,不参与连投轴,经济只走两成不消耗
    /// </summary>
    internal abstract class GsWaterScheme : GsThrowScheme
    {
        /// <summary>爆点领域类型</summary>
        protected abstract int ZoneKind { get; }
        /// <summary>领域半径</summary>
        protected virtual float ZoneRadius => 60f;
        /// <summary>领域覆盖 ≥3 敌返还的物品(0=不返还)</summary>
        protected virtual int ZoneRefundItem => 0;

        protected override float NoConsumeChance => 0.20f;
        protected override bool JoinsCombo => false;

        protected override void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //碎瓶起域:owner 权威;原版环境转换已在原版 Kill 流程完成,这里只追加领域
            if (proj.owner != Main.myPlayer || router.LocalState is not GsThrowProjState { IsPrimary: true }) {
                return;
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                ModContent.ProjectileType<GsZoneProj>(), 0, 0f, proj.owner,
                ZoneKind, ZoneRadius, ZoneRefundItem);
        }
    }

    /// <summary>圣水:爆点 3s 圣辉域,域内敌受所有来源 +10%,域内玩家每秒回 1 生命;域覆盖 3 敌返还一瓶</summary>
    internal class GsHolyWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.HolyWater;
        protected override int ZoneKind => GsZoneProj.KindHoly;
        protected override int ZoneRefundItem => ItemID.HolyWater;
        protected override string GsDescFallback =>
            "Reforged: still hallows the land; the burst also raises a 3s radiant field\nFoes inside take 10% more from everything, allies inside mend 1 life per second; covering 3 foes refunds a flask";
    }

    /// <summary>邪水:爆点 3s 邪雾域,域内敌持续暗影焰并微微迟滞</summary>
    internal class GsUnholyWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.UnholyWater;
        protected override int ZoneKind => GsZoneProj.KindUnholy;
        protected override string GsDescFallback =>
            "Reforged: still corrupts the land; the burst also raises a 3s miasma\nFoes inside smolder with shadowflame and wade as if through tar";
    }

    /// <summary>血水:爆点 3s 血雾域,域内玩家的命中吸血(每秒至多 3 点)</summary>
    internal class GsBloodWater : GsWaterScheme
    {
        public override int TargetItemID => ItemID.BloodWater;
        protected override int ZoneKind => GsZoneProj.KindBlood;
        protected override string GsDescFallback =>
            "Reforged: still spreads the crimson; the burst also raises a 3s blood haze\nWhile you stand inside, your strikes leech 1 life, up to 3 per second";
    }
}
