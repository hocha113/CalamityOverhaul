using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 木回旋镖重铸。材质：生木弯镖。签名行为：①去程命中后向最近的另一名敌人回弹折飞一次
    /// ②折飞瞬间木屑迸溅与闷响 ③命中木屑纷飞，音色是钝木声不是金属声
    /// </summary>
    internal class GsWoodenBoomerang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.WoodenBoomerang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsWoodenBoomerangProj>();

        internal override float DamageMul => 1.10f;   //公认弱势起点武器，补一成底伤

        protected override string GsDescFallback =>
            "Living wood remembers the throw: on its first hit it rebounds toward another nearby foe\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>木镖体：去程命中回弹折飞一次，木质命中反馈</summary>
    internal class GsWoodenBoomerangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.WoodenBoomerang;

        protected override Color GlowColor => new(196, 152, 92);

        protected override Color TrailColor => new(178, 134, 80);

        protected override float GhostBaseAlpha => 0.18f;

        protected override SoundStyle HitSound => SoundID.Dig with { Volume = 0.55f, Pitch = -0.2f };

        /// <summary>回弹折飞是否已用（owner 权威，远端跟随相位同步）</summary>
        private bool ricochetUsed;

        protected override bool HoverOnFirstHit => false;

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase != PhaseOut && Phase != PhaseDash) {
                return;
            }
            //生木回弹：首次命中向最近的另一敌折飞（借冲刺相位，不消耗玩家指令次数）
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

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            //木屑纷飞：原版木家具尘垫底 + 主题色火星
            for (int i = 0; i < 6; i++) {
                Dust d = Dust.NewDustPerfect(target.Center,
                    DustID.WoodFurniture, Main.rand.NextVector2Circular(3.5f, 3.5f),
                    60, default, Main.rand.NextFloat(0.9f, 1.3f));
                d.noGravity = Main.rand.NextBool();
            }
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.18f)?.Configure(8, 0.7f);
        }
    }
}
