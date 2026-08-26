using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonWhips.Schemes
{
    /// <summary>
    /// 暗黑收割「收割轮舞」：12f 基准窗；四层转印；原版黑暗能量与收割时刻攻速全保留。<br/>
    /// 深化 = 黑暗能量跳劈命中带鞭痕目标时按层数传染邻敌（每层 +1 记补劈，
    /// 上限 4；不改原版跳劈弹幕的内部计数，传染走自建闪劈真弹幕）；<br/>
    /// 处决 = 大镰收魂：紫魂涌出 1.6x + 魂爆 0.6x，并对全场带鞭痕目标补 0.5x 闪劈
    /// （封顶 6 个防粒子超预算）。强度目标 115%
    /// </summary>
    internal class GsDarkHarvest : GsWhipScheme
    {
        public override int TargetItemID => ItemID.ScytheWhip;

        public override int WhipProjType => ProjectileID.ScytheWhip;

        public override int BaseWindowFrames => 12;

        public override int MarkCap => 4;

        public override float DamageTweak => 1.04f;

        /// <summary>幽紫</summary>
        public override Color MarkColor => new(150, 80, 220);

        protected override string GsDescFallback =>
            "Reforged: dark energy leaps spread to nearby foes for each reap scar on the victim; " +
            "4 scars seal the mark, and the next on-beat hit reaps its soul, " +
            "flash-scything every scarred enemy on the field";

        public override void GsSetStaticDefaults()
            //黑暗能量跳劈由仆从命中触发生成，无打标源，走类型通道
            => GsRegisterProjChannel(ProjectileID.ScytheWhipProj);

        protected override void OnExecute(Player player, NPC target, Projectile whipProj, WhipMarkState st) {
            int soulDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 1.6f));
            int burstDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.6f));
            Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsWhipReapSoulProj>(), soulDmg, 3f, player.whoAmI, burstDmg);
            //全场闪劈：对其余带鞭痕目标各补一记短镰光（≤6）
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            int flashDmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.5f));
            int budget = 6;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (budget <= 0) {
                    break;
                }
                if (npc.whoAmI == target.whoAmI
                    || !mp.TryGetMark(npc, out WhipMarkState other) || other.Stacks <= 0) {
                    continue;
                }
                Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"), npc.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsWhipReapFlashProj>(), flashDmg, 2f, player.whoAmI, npc.whoAmI);
                budget--;
            }
        }

        /// <summary>跳劈弹幕的每弹幕闩：一枚只传染一次</summary>
        private sealed class ChainLocal
        {
            public bool Chained;
        }

        /// <summary>深化传染：跳劈命中带鞭痕目标，按层数对邻敌补劈</summary>
        protected override void OnWhipProjHit(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.ScytheWhipProj || proj.owner != Main.myPlayer) {
                return;
            }
            ChainLocal local = router.GetOrCreateState<ChainLocal>();
            if (local.Chained) {
                return;
            }
            local.Chained = true;
            Player player = Main.player[proj.owner];
            GsWhipPlayer mp = player.GetModPlayer<GsWhipPlayer>();
            if (!mp.TryGetMark(target, out WhipMarkState st) || st.Stacks <= 0) {
                return;
            }
            int chains = Math.Min(st.Stacks, 4);
            int dmg = Math.Max(1, (int)MathF.Round(st.MarkDamage * 0.35f));
            //取 160px 内最近的 chains 个邻敌
            List<NPC> nearby = [];
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == target.whoAmI || npc.friendly || !npc.CanBeChasedBy()
                    || Vector2.Distance(npc.Center, target.Center) > 160f) {
                    continue;
                }
                nearby.Add(npc);
            }
            nearby.Sort((a, b) => Vector2.DistanceSquared(a.Center, target.Center)
                .CompareTo(Vector2.DistanceSquared(b.Center, target.Center)));
            for (int i = 0; i < nearby.Count && i < chains; i++) {
                Projectile.NewProjectile(player.GetSource_Misc("GsWhipExecute"),
                    nearby[i].Center, Vector2.Zero,
                    ModContent.ProjectileType<GsWhipReapFlashProj>(), dmg, 2f,
                    player.whoAmI, nearby[i].whoAmI);
            }
        }
    }
}
