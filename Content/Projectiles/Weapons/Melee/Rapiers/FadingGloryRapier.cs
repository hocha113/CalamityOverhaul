using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.Rapiers
{
    internal class FadingGloryRapier : BaseRapiers
    {
        public override string Texture => CWRConstant.Item_Melee + "FadingGlory";
        public override string GlowPath => CWRConstant.Item_Melee + "FadingGloryGlow";
        public override void SetRapiers() {
            overHitModeing = 93;
            SkialithVarSpeedMode = 23;//非常快的残影移动!
            StabbingSpread = 0.15f;
            ShurikenOut = CWRSound.ShurikenOut with { Pitch = 0.24f };
        }

        public override void ExtraShoot() {
            if (HitNPCs.Count > 0) {
                Owner.HealEffect(2);
                foreach (var npc in HitNPCs) {
                    if (npc.active) {
                        SoundEngine.PlaySound(SoundID.NPCHit18, Projectile.Center);
                        Vector2 bloodSpawnPosition = npc.Center + Main.rand.NextVector2Circular(npc.width, npc.height) * 0.04f;
                        Vector2 splatterDirection = (Projectile.Center - bloodSpawnPosition).SafeNormalize(Vector2.UnitY);
                        if (CWRLoad.NPCValue.ISTheofSteel(npc)) {
                            for (int j = 0; j < 3; j++) {
                                float sparkScale = Main.rand.NextFloat(1.2f, 2.33f);
                                int sparkLifetime = Main.rand.Next(22, 36);
                                Color sparkColor = Color.Lerp(Color.Silver, Color.Gold, Main.rand.NextFloat(0.7f));
                                Vector2 sparkVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(19f, 34.5f);
                                PRT_Spark spark = new PRT_Spark(bloodSpawnPosition, sparkVelocity, true, sparkLifetime, sparkScale, sparkColor);
                                PRTLoader.AddParticle(spark);
                            }
                        }
                        else {
                            for (int i = 0; i < 6; i++) {
                                int bloodLifetime = Main.rand.Next(22, 36);
                                float bloodScale = Main.rand.NextFloat(0.6f, 0.8f);
                                Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat());
                                bloodColor = Color.Lerp(bloodColor, new Color(51, 22, 94), Main.rand.NextFloat(0.65f));

                                if (Main.rand.NextBool(20))
                                    bloodScale *= 2f;

                                Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.81f) * Main.rand.NextFloat(11f, 23f);
                                bloodVelocity.Y -= 12f;
                                PRT_Spark spark = new PRT_Spark(bloodSpawnPosition, bloodVelocity, true, bloodLifetime, bloodScale, bloodColor);
                                PRTLoader.AddParticle(spark);
                            }
                            for (int i = 0; i < 3; i++) {
                                float bloodScale = Main.rand.NextFloat(0.2f, 0.33f);
                                Color bloodColor = Color.Lerp(Color.Red, Color.DarkRed, Main.rand.NextFloat(0.5f, 1f));
                                Vector2 bloodVelocity = splatterDirection.RotatedByRandom(0.9f) * Main.rand.NextFloat(9f, 14.5f);
                                PRT_Spark spark = new PRT_Spark(bloodSpawnPosition, bloodVelocity, true, 22, bloodScale, bloodColor);
                                PRTLoader.AddParticle(spark);
                            }
                        }
                    }
                }
                return;
            }
            int proj = Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, Projectile.velocity.UnitVector() * 13
                , ModContent.ProjectileType<FadingGloryBeam>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
            Main.projectile[proj].rotation = Main.projectile[proj].velocity.ToRotation();
        }

        public override void Draw1(Texture2D tex, Vector2 imgsOrig, Vector2 off, float fade, SkialithStruct afterImage, ref Color lightColor) {
            Main.spriteBatch.Draw(tex, imgsOrig + off, null, Color.White * fade * 0.3f
                , afterImage.rot - MathHelper.ToRadians(30) * (Projectile.velocity.X > 0 ? 1 : -1)
                , tex.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        }

        public override void Draw2(Texture2D tex, Vector2 imgsOrig, Vector2 off, float opacity, SkialithStruct afterImage, ref Color lightColor) {
            Main.spriteBatch.Draw(tex, imgsOrig + off, null, Color.White * opacity * 0.5f
                , Projectile.rotation - MathHelper.ToRadians(30) * (Projectile.velocity.X > 0 ? 1 : -1)
                , tex.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        }

        public override void Draw3(Texture2D tex, Vector2 off, float fade, Color lightColor) {
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition + off + Projectile.velocity * 83
                , null, lightColor * fade, Projectile.rotation - MathHelper.ToRadians(30) * (Projectile.velocity.X > 0 ? 1 : -1)
                , tex.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
        }
    }
}
