using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 暗影焰咒娃娃重铸。材质身份：巫毒暗焰（缝偶针脚间渗出的影火）。<br/>
    /// ①rider：暗影焰触须命中钉下「咒偶印」10 秒，被印之敌受本武器伤害 +8%；②施法有举偶起手
    /// </summary>
    internal class GsShadowFlameHexDoll : GsMorphScheme
    {
        public override int TargetItemID => ItemID.ShadowFlameHexDoll;

        protected override string GsDescFallback =>
            "Reforged: shadowflame tendrils pin a hex brand into whatever they touch; branded foes take more from this doll";
        protected override float BaseDamageMult => 1.05f;

        /// <summary>咒偶印的伤害加成</summary>
        private const float HexBonus = 0.08f;

        /// <summary>原版暗影焰触须弹类型</summary>
        private static int TendrilType => ContentSamples.ItemsByType[ItemID.ShadowFlameHexDoll].shoot;

        //==================== 动画法：举偶 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举偶：出手瞬间咒偶举高 4px 微斜，随动画进度垂回（绝对剖面 0.14·p，差分施加防累积漂移；本偶动画三发，中途 snap 由差分清账）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.14f * progress, 0.14f * ((player.itemAnimation + 1) / n));
        }

        //==================== rider：咒偶印 ====================

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //咒偶印消费：被印之敌受本武器伤害 +8%（印是攻击方本地量，命中链天然攻击方端）
            GsShadowFlameHexNPC hex = target.GetGlobalNPC<GsShadowFlameHexNPC>();
            if (Main.GameUpdateCount < hex.HexUntil) {
                modifiers.FinalDamage *= 1f + HexBonus;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != TendrilType || !proj.IsOwnedByLocalPlayer()) {
                return;
            }
            target.AddBuff(BuffID.ShadowFlame, 240);
            //钉印：咒偶印 10 秒
            target.GetGlobalNPC<GsShadowFlameHexNPC>().HexUntil = Main.GameUpdateCount + 600;
        }
    }

    /// <summary>
    /// 咒偶印（攻击方本地量：命中钩子只在攻击方端执行，加成只在攻击方端结算）
    /// </summary>
    internal class GsShadowFlameHexNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>咒偶印失效时刻</summary>
        internal uint HexUntil;
    }
}
