using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Players.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players
{
    internal class RippersPlayer : PlayerSet
    {
        public static List<int> noRippersProj = new List<int>();

        public static RippersPlayer Instance;

        public override void Load() {
            Instance = this;
            noRippersProj = new List<int>();
            noRippersProj = new List<int>() {
                ModContent.ProjectileType<CosmicDischargeFlail>(),
                ModContent.ProjectileType<CosmicIceBurst>(),

                ModContent.ProjectileType<StormGoddessSpearProj>(),
                ModContent.ProjectileType<StormArc>(),
                ModContent.ProjectileType<StormLightning>(),
            };
        }

        public override bool On_ModifyHitNPCWithItem(Player player, Item item, NPC target, ref NPC.HitModifiers modifiers) {
            //CalamityPlayer mp = player.Calamity();
            return base.On_ModifyHitNPCWithItem(player, item, target, ref modifiers);
        }

        public override bool On_ModifyHitNPCWithProj(Player player, Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            CalamityPlayer mp = player.Calamity();
            if (mp.adrenalineModeActive) {
                if (noRippersProj.Contains(proj.type)) {
                    return false;
                }
            }
            return base.On_ModifyHitNPCWithProj(player, proj, target, ref modifiers);
        }
    }
}
