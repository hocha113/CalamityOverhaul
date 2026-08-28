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
    /// 水果蛋糕查克拉姆重铸。材质：糖霜蛋糕环刃。签名行为：①命中给目标涂上糖渍，至多三层
    /// ②回程再命中有糖渍的目标时引爆全部糖渍，每层加伤 25% ③引爆时红绿糖屑与奶白闪光四溅
    /// </summary>
    internal class GsFruitcakeChakram : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.FruitcakeChakram;

        internal override int BoomerProjType => ModContent.ProjectileType<GsFruitcakeChakramProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Outbound hits frost the target with sugar glaze, up to three coats\n" +
            "Hitting a glazed target on the return detonates every coat, 25% bonus damage per coat\n" +
            "Right click while it flies: command it to dash toward your cursor";
    }

    /// <summary>蛋糕环刃：去程涂糖渍，回程引爆</summary>
    internal class GsFruitcakeChakramProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.FruitcakeChakram;

        protected override Color GlowColor => new(255, 120, 130);

        protected override Color TrailColor => new(140, 220, 130);

        /// <summary>奶白糖霜色</summary>
        private static readonly Color SugarWhite = new(255, 246, 225);

        /// <summary>目标 whoAmI → 糖渍层数（判定端本地量，命中判定只在 owner 端跑）</summary>
        private readonly Dictionary<int, int> glaze = [];

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            //回程咬糖：结算这一击时吃掉全部糖渍层
            if (Phase == PhaseReturn && glaze.TryGetValue(target.whoAmI, out int coats) && coats > 0) {
                modifiers.FinalDamage *= 1f + (0.25f * coats);
            }
        }

        protected override void OnHitEffects(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Phase == PhaseReturn) {
                if (glaze.TryGetValue(target.whoAmI, out int coats) && coats > 0) {
                    glaze[target.whoAmI] = 0;
                    DetonateFX(target, coats);
                }
                return;
            }
            //去程与冲刺涂糖渍
            glaze.TryGetValue(target.whoAmI, out int cur);
            glaze[target.whoAmI] = System.Math.Min(3, cur + 1);
        }

        private void DetonateFX(NPC target, int coats) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.15f }, target.Center);
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, SugarWhite, 0.3f + (0.12f * coats))?.Configure(12, 0.9f);
            for (int i = 0; i < 4 + (coats * 3); i++) {
                Color c = Main.rand.NextBool() ? GlowColor : TrailColor;
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(5f, 5f), c,
                    Main.rand.NextFloat(0.35f, 0.6f))?.Configure(true, Main.rand.Next(14, 22));
            }
        }

        protected override void HitBurstFX(NPC target, NPC.HitInfo hit) {
            //涂糖：糖霜白点光 + 红绿糖屑
            PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, SugarWhite, 0.2f)?.Configure(9, 0.75f);
            for (int i = 0; i < 3; i++) {
                Color c = Main.rand.NextBool() ? GlowColor : TrailColor;
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Circular(3f, 3f), c,
                    Main.rand.NextFloat(0.3f, 0.45f))?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        protected override void FlightFX(Player owner) {
            base.FlightFX(owner);
            //悬停期糖霜滴落
            if (Phase == PhaseHover && PhaseTimer % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(
                    Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(0f, Main.rand.NextFloat(0.5f, 1.2f)), SugarWhite,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(true, Main.rand.Next(12, 18));
            }
        }
    }
}
