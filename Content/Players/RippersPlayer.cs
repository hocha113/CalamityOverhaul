﻿using CalamityMod;
using CalamityMod.CalPlayer;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.LegendWeapon.MurasamaLegend.MurasamaProj;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.StormGoddessSpearProj;
using CalamityOverhaul.Content.RangedModify;
using InnoVault.GameSystem;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Players
{
    internal class RippersPlayer : PlayerOverride, ICWRLoader
    {
        public static List<int> noRippersProj = [];

        public static RippersPlayer Instance;
        void ICWRLoader.SetupData() {
            Instance = this;
            noRippersProj = [
                ModContent.ProjectileType<CosmicDischargeFlail>(),
                ModContent.ProjectileType<CosmicIceBurst>(),
                ModContent.ProjectileType<MuraExecutionCut>(),
                ModContent.ProjectileType<StormGoddessSpearHeld>(),
                ModContent.ProjectileType<StormArc>(),
                ModContent.ProjectileType<StormLightning>(),
            ];
        }

        void ICWRLoader.UnLoadData() {
            Instance = null;
            noRippersProj?.Clear();
        }

        public override bool On_ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            CalamityPlayer mp = Player.Calamity();
            if (mp.adrenalineModeActive) {
                if (noRippersProj.Contains(proj.type)) {
                    return false;
                }
                GlobalGun.AdrenalineByGunDamageAC(Player, ref modifiers);
            }
            return base.On_ModifyHitNPCWithProj(proj, target, ref modifiers);
        }
    }
}
