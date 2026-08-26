using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 荆棘鞭「荆棘缠打」：16f 基准窗；四层转印；
    /// 处决 = 荆棘爆裂（0.6x 主爆）+ 四根追踪棘刺（各 0.4x，挂原版中毒），总账 2.2x。
    /// 原版丛林之怒攻速 buff 与踩拍升速天然叠算：窗口按实际动画帧换算，节奏不漂。
    /// 强度目标 125%
    /// </summary>
    internal class GsSnapthorn : GsWhipScheme
    {
        public override int TargetItemID => ItemID.ThornWhip;

        public override int WhipProjType => ProjectileID.ThornWhip;

        public override int BaseWindowFrames => 16;

        public override int MarkCap => 4;

        public override float DamageTweak => 1.08f;

        /// <summary>毒藤绿</summary>
        public override Color MarkColor => new(110, 196, 64);

        protected override string GsDescFallback =>
            "Reforged: on-beat lashes grow thorn scars on the prey; " +
            "4 scars seal the mark, and the next on-beat hit bursts it into " +
            "a thorn explosion plus 4 homing venom barbs";

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int burstDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            int dartDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.4f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipThornBurstProj>(), burstDmg, 2f, player.whoAmI);
            //四根棘刺按 identity 微差的四向散开，飘 10f 后转追踪
            float baseRot = whipProj.identity * 0.37f;
            for (int i = 0; i < 4; i++) {
                Vector2 vel = (baseRot + MathHelper.PiOver4 + i * MathHelper.PiOver2).ToRotationVector2() * 6.5f;
                Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, vel,
                    ModContent.ProjectileType<GsWhipThornDartProj>(), dartDmg, 1.5f, player.whoAmI);
            }
        }
    }
}
