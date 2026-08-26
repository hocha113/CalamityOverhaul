using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph.Projectiles;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicMorph
{
    /// <summary>
    /// 彩虹枪重铸：小领域「棱彩领域」。经典不毁：原版驻留彩虹全程原样保留，只做增强层。<br/>
    /// 彩虹存续期间：弧上方 80px 带内本玩家弹幕 +8% 伤害（攻击方端结算）；
    /// 彩虹头每 2s 滑出 1 枚微追踪彩虹脉冲；右键收回彩虹并返还一半蓝耗。<br/>
    /// 弧位置链存 LocalState（各端自记，由弹幕位置同步驱动）
    /// </summary>
    internal class GsRainbowGun : GsMorphScheme
    {
        public override int TargetItemID => ItemID.RainbowGun;

        protected override string GsDescFallback =>
            "Reforged: while the rainbow stands, your projectiles inside the band above the arc deal 8% more damage, and the arc sheds homing prism pulses.\nRight click recalls the rainbow and refunds half its mana";

        protected override float BaseDamageMult => 1.05f;

        /// <summary>弧位置链（每弹幕本地状态包，各端自记）</summary>
        private class RainbowTrail
        {
            public List<Vector2> Points = [];
        }

        private static bool IsRainbow(int type) => type == ProjectileID.RainbowFront || type == ProjectileID.RainbowBack;

        /// <summary>右键：收回彩虹并返还一半蓝耗（owner 端 Kill 广播全端）</summary>
        protected override void OnAltTrigger(Item item, Player player) {
            bool recalled = false;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == player.whoAmI && IsRainbow(p.type)) {
                    p.Kill();
                    recalled = true;
                }
            }
            if (!recalled) {
                SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.7f, Volume = 0.5f }, player.Center);
                return;
            }
            int refund = item.mana / 2;
            player.statMana = Utils.Clamp(player.statMana + refund, 0, player.statManaMax2);
            player.ManaEffect(refund);
            SoundEngine.PlaySound(SoundID.Item9 with { Volume = 0.8f, Pitch = 0.4f }, player.Center);
        }

        /// <summary>本武器无蓄力形态，右键已改为瞬发收回</summary>
        protected override void FireMorphB(Item item, Player player) { }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (!IsRainbow(proj.type)) {
                return;
            }
            //弧位置链：每 6t 记一点，上限 120 点（覆盖整条彩虹）
            RainbowTrail trail = router.GetOrCreateState<RainbowTrail>();
            if (proj.timeLeft % 6 == 0) {
                trail.Points.Add(proj.Center);
                if (trail.Points.Count > 120) {
                    trail.Points.RemoveAt(0);
                }
            }
            //彩虹脉冲：owner 端由弧头（Front）每 2s 滑出一枚，沿当前行进方向离弧
            if (proj.owner == Main.myPlayer && proj.type == ProjectileID.RainbowFront
                && proj.timeLeft % 120 == 0) {
                Vector2 dir = proj.velocity.Length() > 0.5f
                    ? proj.velocity.SafeNormalize(Vector2.UnitX)
                    : (Main.MouseWorld - proj.Center).SafeNormalize(Vector2.UnitX);
                Projectile.NewProjectile(proj.GetSource_FromAI(), proj.Center, dir * 6f,
                    ModContent.ProjectileType<GsRainbowPulseProj>(),
                    (int)(proj.damage * 0.6f), 1f, proj.owner);
            }
        }

        /// <summary>
        /// 棱彩领域增益：本玩家弹幕命中时若正处于任一自有彩虹弧上方 80px 带内则 +8%。
        /// 由 GsMorphPlayer.ModifyHitNPCWithProj 转发（攻击方端执行，模式闸门已在调用方查过）
        /// </summary>
        internal static void TryBandBonus(Player player, Projectile hitter, ref NPC.HitModifiers modifiers) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != player.whoAmI || !IsRainbow(p.type)) {
                    continue;
                }
                if (!p.TryGetGlobalProjectile(out GodSmithProjRouter router)
                    || router.LocalState is not RainbowTrail trail) {
                    continue;
                }
                foreach (Vector2 pt in trail.Points) {
                    if (MathHelper.Distance(hitter.Center.X, pt.X) < 24f
                        && hitter.Center.Y > pt.Y - 80f && hitter.Center.Y <= pt.Y) {
                        modifiers.FinalDamage *= 1.08f;
                        return;
                    }
                }
            }
        }
    }
}
