using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 战斗扳手重铸。材质：镀铜工具钢。签名行为：①同一目标每第三次命中触发敲栓重击，加伤 80%
    /// ②敲栓命中钢质构装体再加 25%，专拆机械 ③敲栓瞬间螺栓火花四射与响亮金属铛声
    /// </summary>
    internal class GsCombatWrench : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.CombatWrench;

        internal override int BoomerProjType => ModContent.ProjectileType<GsCombatWrenchProj>();

        internal override float DamageMul => 1.05f;

        protected override string GsDescFallback =>
            "Every third hit on the same target is a bolt-knock: 80% bonus damage\n" +
            "Bolt-knocks against steel constructs gain another 25%\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>工具钢镖体：敲栓重击</summary>
    internal class GsCombatWrenchProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.CombatWrench;

        protected override Color GlowColor => new(235, 155, 70);

        protected override Color TrailColor => new(200, 130, 70);

        protected override SoundStyle HitSound => SoundID.Tink with { Volume = 0.5f, Pitch = 0.1f };

        /// <summary>目标 whoAmI → 命中计数（owner 判定端本地量）</summary>
        private readonly Dictionary<int, int> hitCount = [];

        private bool IsKnockHit(NPC target) {
            hitCount.TryGetValue(target.whoAmI, out int cur);
            return (cur + 1) % 3 == 0;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (!IsKnockHit(target)) {
                return;
            }
            float mul = 1.8f;
            if (CWRLoad.NPCValue.ISTheofSteel(target)) {
                mul += 0.25f;   //专拆机械
            }
            modifiers.FinalDamage *= mul;
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            hitCount.TryGetValue(target.whoAmI, out int cur);
            cur++;
            bool knock = cur % 3 == 0;
            hitCount[target.whoAmI] = cur;
            if (knock && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Tink with { Volume = 0.8f, Pitch = -0.35f }, target.Center);
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, GlowColor, 0.38f)?.Configure(12, 0.9f);
                for (int i = 0; i < 8; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center,
                        Main.rand.NextVector2Circular(6f, 6f), GlowColor,
                        Main.rand.NextFloat(0.4f, 0.65f))?.Configure(true, Main.rand.Next(14, 22));
                }
            }
        }
    }
}
