using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 木回旋镖重铸。材质：生木弯镖。签名行为：①去程命中后向最近的另一名敌人回弹折飞一次
    /// ②折飞与命中的音色是钝木声不是金属声
    /// </summary>
    internal class GsWoodenBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.WoodenBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsWoodenBoomerangProj>();

        internal override float DamageMul => 1.10f;   //公认弱势起点武器，补一成底伤

        protected override string GsDescFallback =>
            "Living wood remembers the throw: on its first hit it rebounds toward another nearby foe";
    }

    /// <summary>木镖体：去程命中回弹折飞一次，木质命中反馈</summary>
    internal class GsWoodenBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.WoodenBoomerang;

        protected override SoundStyle HitSound => SoundID.Dig with { Volume = 0.55f, Pitch = -0.2f };

        /// <summary>回弹折飞是否已用（owner 权威，远端跟随相位同步）</summary>
        private bool ricochetUsed;

        protected override bool HoverOnFirstHit => false;

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase != PhaseOut && Phase != PhaseDash) {
                return;
            }
            //生木回弹：首次命中向最近的另一敌折飞（借冲刺相位续飞）
            if (!ricochetUsed) {
                ricochetUsed = true;
                NPC next = FindNextTarget(target);
                if (next != null) {
                    Projectile.velocity = (next.Center - Projectile.Center)
                        .SafeNormalize(Vector2.UnitX * spinDir) * DashSpeed;
                    EnterPhase(PhaseDash, Owner);
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.6f, Pitch = 0.35f }, Projectile.Center);
                    }
                    return;
                }
            }
            EnterPhase(PhaseHover, Owner);
        }

        private NPC FindNextTarget(NPC exclude) {
            NPC best = null;
            float bestDist = 430f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == exclude.whoAmI || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.Distance(Projectile.Center);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }
    }
}
