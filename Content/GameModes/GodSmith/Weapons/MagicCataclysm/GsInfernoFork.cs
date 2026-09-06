using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using Terraria;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 地狱叉重铸：燎原。材质身份：炉底狱火（地牢炉渣里舀出来的一叉火）。<br/>
    /// ①左键 rider：命中挂狱火并积「积炎」；②施法有掷叉后坐
    /// </summary>
    internal class GsInfernoFork : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.InfernoFork;

        protected override string GsDescFallback =>
            "Reforged: fireballs corkscrew with furnace slag";
        protected override float PassiveDamageBonus => 0.08f;

        /// <summary>原版狱火弹 / 狱火场弹类型</summary>
        private static int BoltType => ContentSamples.ItemsByType[ItemID.InfernoFork].shoot;
        private const int FieldType = ProjectileID.InfernoFriendlyBlast;

        //==================== 动画法：掷叉后坐 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //掷叉后坐：出手瞬间叉身后坐 4px 上踢，随动画进度回坐（绝对剖面 0.1·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation -= new Vector2(player.direction * 4f, 1f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.1f * progress, 0.1f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：狱火 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积炎（计量是攻击方本地量）
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != BoltType && proj.type != FieldType) {
                return;
            }
            if (proj.IsOwnedByLocalPlayer()) {
                target.AddBuff(BuffID.OnFire3, proj.type == BoltType ? 180 : 120);
            }
        }
    }
}
