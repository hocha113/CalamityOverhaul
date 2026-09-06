using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 月耀重铸（P13 抬档 B→A，终局件）。材质身份：月蚀冷辉（蚀盘边缘淌下的月焰）。<br/>
    /// ①左键 rider：「第四落」，每第 4 次施放自天穹补落一道 0.5× 月焰（复刻原版天降参数）
    /// ②施法有举书响应。第四落 0.5×/(4×3) ≈ +4%，底伤加成保持 5%
    /// </summary>
    internal class GsLunarFlare : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.LunarFlareBook;

        protected override string GsDescFallback =>
            "Reforged: every 4th cast pours a pale fourth flare from the sky";
        public override int ChargePerHit => 2;

        protected override float PassiveDamageBonus => 0.05f;

        /// <summary>原版月焰弹类型</summary>
        private static int FlareType => ContentSamples.ItemsByType[ItemID.LunarFlareBook].shoot;

        /// <summary>施放计数与第四落旗标（GsShoot 只在 owner 端执行，本机契约）</summary>
        private int castCounter;

        private bool pendingPale;

        //==================== 动画法：举书 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //举书诵月：书身抬升 4px 微后仰再缓落（绝对剖面 0.11·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 2f, -4f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.11f * progress, 0.11f * ((player.itemAnimation + 1) / n));
        }

        //==================== 左键 rider：第四落 ====================

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //每第 4 次施放补落一道苍白月焰：复刻原版天降参数（天窗高 600px、ai[1]=准星深度）
            castCounter++;
            if (castCounter % 4 != 0) {
                return null;
            }
            Vector2 aim = Main.MouseWorld;
            Vector2 sky = new((aim.X + player.Center.X) * 0.5f + Main.rand.Next(-200, 201),
                player.MountedCenter.Y - 600f);
            Vector2 delta = aim - sky;
            delta.Y = MathF.Max(MathF.Abs(delta.Y), 20f);
            Vector2 vel = delta.SafeNormalize(Vector2.UnitY) * velocity.Length() * 0.5f;
            pendingPale = true;
            Projectile.NewProjectile(source, sky, vel, type,
                Math.Max(1, damage / 2), knockback, player.whoAmI, 0f, aim.Y);
            pendingPale = false;
            //原版三落照常放行
            return null;
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            //苍白第四落：出生窗打角色标（先于生成包，远端同见）
            if (pendingPale && proj.type == FlareType) {
                router.MarkData = 1f;
            }
        }
    }
}
