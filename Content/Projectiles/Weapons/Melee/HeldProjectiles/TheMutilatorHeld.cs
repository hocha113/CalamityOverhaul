﻿using CalamityMod.CalPlayer;
using CalamityMod.Items.Weapons.Melee;
using CalamityMod.Projectiles.Melee;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjectiles
{
    internal class TheMutilatorHeld : BaseKnife
    {
        public override int TargetID => ModContent.ItemType<TheMutilator>();
        public override string gradientTexturePath => CWRConstant.ColorBar + "BloodRed_Bar";
        public override void SetKnifeProperty() {
            canDrawSlashTrail = true;
            SwingData.starArg = 54;
            SwingData.baseSwingSpeed = 5f;
            distanceToOwner = 64;
            trailTopWidth = 30;
        }

        public override void Shoot() {
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (target.life <= (target.lifeMax * 0.2f) && target.canGhostHeal) {
                if (!CalamityPlayer.areThereAnyDamnBosses || Main.rand.NextBool()) {
                    int heartDrop = CalamityPlayer.areThereAnyDamnBosses ? 1 : Main.rand.Next(1, 3);
                    for (int i = 0; i < heartDrop; i++) {
                        Item.NewItem(Owner.GetSource_OnHit(target), (int)target.position.X, (int)target.position.Y, target.width, target.height, 58, 1, false, 0, false, false);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item14, target.Center);
                target.position.X += target.width / 2;
                target.position.Y += target.height / 2;
                target.position.X -= target.width / 2;
                target.position.Y -= target.height / 2;
                for (int i = 0; i < 30; i++) {
                    int bloodDust = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 5, 0f, 0f, 100, default, 2f);
                    Main.dust[bloodDust].velocity *= 3f;
                    if (Main.rand.NextBool()) {
                        Main.dust[bloodDust].scale = 0.5f;
                        Main.dust[bloodDust].fadeIn = 1f + Main.rand.Next(10) * 0.1f;
                    }
                }
                for (int j = 0; j < 50; j++) {
                    int bloodDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 5, 0f, 0f, 100, default, 3f);
                    Main.dust[bloodDust2].noGravity = true;
                    Main.dust[bloodDust2].velocity *= 5f;
                    bloodDust2 = Dust.NewDust(new Vector2(target.position.X, target.position.Y), target.width, target.height, 5, 0f, 0f, 100, default, 2f);
                    Main.dust[bloodDust2].velocity *= 2f;
                }
            }
        }
    }
}
