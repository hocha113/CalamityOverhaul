using CalamityOverhaul.Common;
using CalamityOverhaul.Content.MeleeModify.Core;
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
                        CWRRef.FadingGloryRapierHitDustEffect(Projectile, npc);
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
