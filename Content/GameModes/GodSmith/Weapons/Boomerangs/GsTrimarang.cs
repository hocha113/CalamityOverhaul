using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Boomerangs
{
    /// <summary>
    /// 三重回旋镖重铸。材质：三合板复合镖。签名行为：同场至多三镖，每多一镖在空中全体加伤 8%
    /// </summary>
    internal class GsTrimarang : GsBoomerScheme
    {
        public override int TargetItemID => ItemID.Trimarang;

        internal override int BoomerProjType => ModContent.ProjectileType<GsTrimarangProj>();

        internal override int MaxAirborne => 3;   //与原版三镖上限对齐

        internal override float DamageMul => 1.0f;

        protected override string GsDescFallback =>
            "Up to three boomerangs airborne; each extra one in flight grants all of them 8% damage";
    }

    /// <summary>复合镖体：三相编队</summary>
    internal class GsTrimarangProj : GsBoomerProjBase
    {
        internal override int SourceItemID => ItemID.Trimarang;

        /// <summary>同场镖数（含自身），判定端直读 ownedProjectileCounts</summary>
        private int SquadCount => Math.Clamp(Owner.ownedProjectileCounts[Type], 1, 3);

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            int extra = SquadCount - 1;
            if (extra > 0) {
                modifiers.FinalDamage *= 1f + (0.08f * extra);
            }
        }
    }
}
