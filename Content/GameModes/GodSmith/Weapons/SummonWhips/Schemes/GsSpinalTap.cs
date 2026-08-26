using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 波尼鞭「敲骨节拍」：15f 基准窗；四层转印；低衰减群鞭定位。<br/>
    /// 独有「连骨振」：踩拍挥击单次挥中三个敌人时，鞭梢追加一记 60px 横扫余振
    /// （0.6x 固定面板，只打本挥未中之敌，不再乘多段衰减）；<br/>
    /// 处决 = 脊骨突刺：目标脚下窜出骨刺柱 1.6x + 顶端迸裂 0.6x，轻击飞。
    /// 强度目标 122%
    /// </summary>
    internal class GsSpinalTap : GsWhipScheme
    {
        public override int TargetItemID => ItemID.BoneWhip;

        public override int WhipProjType => ProjectileID.BoneWhip;

        public override int BaseWindowFrames => 15;

        public override int MarkCap => 4;

        public override float DamageTweak => 1.06f;

        /// <summary>骨白</summary>
        public override Color MarkColor => new(226, 222, 200);

        protected override string GsDescFallback =>
            "Reforged: an on-beat sweep that tags 3 foes cracks a bone echo " +
            "into everything it missed; 4 scars seal the mark, " +
            "and the next on-beat hit erupts a bone spire from below";

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int spireDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 1.6f));
            int crackDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                FindGroundBelow(target), Vector2.Zero,
                ModContent.ProjectileType<GsWhipBoneSpireProj>(), spireDmg, 7f,
                player.whoAmI, crackDmg);
        }

        /// <summary>连骨振：踩拍挥击命中第三个新目标的瞬间触发一次</summary>
        protected override void OnWhipProjHit(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != WhipProjType || !router.IsMarked || router.MarkData2 < 1f) {
                return;
            }
            Player player = Main.player[proj.owner];
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            if (mp.SwingHitCount != 3) {
                return;
            }
            int dmg = Math.Max(1, (int)MathF.Round(proj.damage * 0.6f));
            Projectile echo = Projectile.NewProjectileDirect(player.GetSource_Misc("GsWhipExecute"),
                target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipBoneEchoProj>(), dmg, 3f, player.whoAmI);
            if (echo.ModProjectile is GsWhipBoneEchoProj echoProj) {
                //排除表只服务 owner 端命中判定，生成后填实例字段即可，无需过线
                echoProj.CaptureExclusions(mp.SwingHitNPCs);
            }
        }

        /// <summary>从目标脚下向下最多找 12 格地表，悬空则贴脚底</summary>
        private static Vector2 FindGroundBelow(NPC target) {
            Point tile = target.Bottom.ToTileCoordinates();
            for (int j = 0; j < 12; j++) {
                int ty = tile.Y + j;
                if (ty < 10 || ty >= Main.maxTilesY - 10) {
                    break;
                }
                if (WorldGen.SolidTile(tile.X, ty)) {
                    return new Vector2(target.Bottom.X, ty * 16f);
                }
            }
            return target.Bottom;
        }
    }
}
