using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 晨星「链锤惯性」：慢重鞭给 18f 宽窗；三层转印。<br/>
    /// 独有「抡锤蓄势」：每记踩拍挥击都在鞭梢落点砸出 80px 贴地震荡
    /// （0.5x + 轻屏震）；<br/>
    /// 处决 = 星坠锤：目标头顶砸落流星锤 2.0x（锤体 = 原版晨星鞭梢贴图放大 + 拖尾），
    /// 落点 120px 震波 0.6x，总账 2.6x。强度目标 118%
    /// </summary>
    internal class GsMorningStar : GsWhipScheme
    {
        public override int TargetItemID => ItemID.MaceWhip;

        public override int WhipProjType => ProjectileID.MaceWhip;

        public override int BaseWindowFrames => 18;

        public override int MarkCap => 3;

        public override float DamageTweak => 1.05f;

        /// <summary>星金白</summary>
        public override Color MarkColor => new(255, 226, 150);

        protected override string GsDescFallback =>
            "Reforged: every on-beat swing slams a quake at the whip tip; " +
            "3 scars seal the mark, and the next on-beat hit calls a falling star mace " +
            "down on the target's head";

        /// <summary>抡锤蓄势：踩拍挥击的鞭梢落点震荡</summary>
        protected override void OnWhipApex(Player player, Projectile whipProj, GodSmithProjRouter router, Vector2 tipPos) {
            if (router.MarkData2 < 1f) {
                return;
            }
            int dmg = Math.Max(1, (int)MathF.Round(whipProj.damage * 0.5f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                FindGroundBelow(tipPos), Vector2.Zero,
                ModContent.ProjectileType<GsWhipMaceQuakeProj>(), dmg, 4f, player.whoAmI);
        }

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int maceDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 2.0f));
            int quakeDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            //锤从目标上方 250px 起坠，横向按 identity 微偏错开重复处决的弹道
            float xJitter = ((whipProj?.identity ?? target.whoAmI) % 7 - 3) * 10f;
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                target.Center + new Vector2(xJitter, -250f), Vector2.Zero,
                ModContent.ProjectileType<GsWhipMorningStarFallProj>(), maceDmg, 8f,
                player.whoAmI, target.whoAmI, quakeDmg);
        }

        /// <summary>鞭梢落点向下最多找 10 格地表，悬空即在原位震荡</summary>
        private static Vector2 FindGroundBelow(Vector2 pos) {
            Point tile = pos.ToTileCoordinates();
            for (int j = 0; j < 10; j++) {
                int ty = tile.Y + j;
                if (ty < 10 || ty >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, ty)) {
                    return new Vector2(pos.X, ty * 16f);
                }
            }
            return pos;
        }
    }
}
