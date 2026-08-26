using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 皮鞭「驯兽教鞭」：全族教学位。最宽 on-beat 窗（20f 基准）、
    /// 空挥不罚（唯一 None）、三层转印；处决 = 皮革响鞭冲击（2.0x 单段爆），
    /// 处决追加两秒驯兽令（自家仆从对该目标 +15%）。强度目标 135%（最弱鞭上限档）
    /// </summary>
    internal class GsLeatherWhip : GsWhipScheme
    {
        public override int TargetItemID => ItemID.BlandWhip;

        public override int WhipProjType => ProjectileID.BlandWhip;

        public override int BaseWindowFrames => 20;

        public override int MarkCap => 3;

        public override MissPolicyKind MissPolicy => MissPolicyKind.None;

        public override float DamageTweak => 1.10f;

        /// <summary>鞍革棕金</summary>
        public override Color MarkColor => new(214, 154, 82);

        protected override string GsDescFallback =>
            "Reforged: chain swings on the beat to build tempo and lash speed; " +
            "3 lash scars seal the mark, and the next on-beat hit cracks it for 200% damage, " +
            "then your minions maul the target for 2 seconds";

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 2.0f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipLeatherCrackProj>(), dmg, 4f, player.whoAmI);
            //驯兽令：处决后两秒自家仆从对该目标再 +15%（余韵 +10% 之外的皮鞭专属）
            st.LeatherBoostUntil = Main.GameUpdateCount + 120;
        }
    }
}
