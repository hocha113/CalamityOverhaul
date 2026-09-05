using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 水果蛋糕查克拉姆重铸。材质：糖霜蛋糕环刃。签名行为：①命中给目标涂上糖渍，至多三层
    /// ②回程再命中有糖渍的目标时引爆全部糖渍，每层加伤 25%
    /// </summary>
    internal class GsFruitcakeChakram : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.FruitcakeChakram;

        internal override int BoomerProjType => ModContent.ProjectileType<GsFruitcakeChakramProj>();

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Outbound hits frost the target with sugar glaze, up to three coats\nHitting a glazed target on the return detonates every coat, 25% bonus damage per coat";
    }

    /// <summary>蛋糕环刃：去程涂糖渍，回程引爆</summary>
    internal class GsFruitcakeChakramProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.FruitcakeChakram;

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
                    if (!VaultUtils.isServer) {
                        SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = -0.15f }, target.Center);
                    }
                }
                return;
            }
            //去程与冲刺涂糖渍
            glaze.TryGetValue(target.whoAmI, out int cur);
            glaze[target.whoAmI] = System.Math.Min(3, cur + 1);
        }
    }
}
