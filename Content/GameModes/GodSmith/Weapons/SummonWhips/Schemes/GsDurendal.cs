using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 杜兰达尔「骑士剑鞭」：12f 基准窗；五层转印；原版祝福攻速与踩拍升速
    /// 经动态窗口换算天然叠算。<br/>
    /// 独有「剑意」：节拍连击蓄满三层后，之后每记踩拍挥击都在鞭梢释放
    /// 横断剑气（0.9x，穿透 3）；<br/>
    /// 处决 = 圣剑审判：目标头顶金色下落斩 1.8x + 落地金环 0.6x，总账 2.4x。
    /// 强度目标 115%
    /// </summary>
    internal class GsDurendal : GsWhipScheme
    {
        public override int TargetItemID => ItemID.SwordWhip;

        public override int WhipProjType => ProjectileID.SwordWhip;

        public override int BaseWindowFrames => 12;

        public override int MarkCap => 5;

        public override float DamageTweak => 1.05f;

        /// <summary>圣金</summary>
        public override Color MarkColor => new(255, 214, 120);

        protected override string GsDescFallback =>
            "Reforged: after three on-beat lashes, every further on-beat swing " +
            "looses a golden sword-wave from the whip tip; " +
            "5 scars seal the mark, and the next on-beat hit calls down a holy verdict blade";

        /// <summary>剑意：连击蓄满三层后的踩拍挥击，鞭梢横断剑气</summary>
        protected override void OnWhipApex(Player player, Projectile whipProj, GodSmithProjRouter router, Vector2 tipPos) {
            if (router.MarkData < 4f || router.MarkData2 < 1f) {
                return;
            }
            Vector2 dir = (tipPos - player.Center).SafeNormalize(Vector2.UnitX * player.direction);
            int dmg = Math.Max(1, (int)MathF.Round(whipProj.damage * 0.9f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), tipPos, dir * 13f,
                ModContent.ProjectileType<GsWhipDurendalArcProj>(), dmg, 3f, player.whoAmI);
        }

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 1.8f));
            int ringDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                target.Center - new Vector2(0f, 210f), Vector2.Zero,
                ModContent.ProjectileType<GsWhipDurendalVerdictProj>(), dmg, 5f,
                player.whoAmI, target.whoAmI, ringDmg);
        }
    }
}
