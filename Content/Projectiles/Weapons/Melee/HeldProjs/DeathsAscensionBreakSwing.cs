using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.HeldProjs
{
    internal class DeathsAscensionBreakSwing : BaseHeldProj
    {
        public override string Texture => CWRConstant.Cay_Proj_Melee + "DeathsAscensionSwing";
        public int frameX = 0;
        public int frameY = 0;

        public int CurrentFrame {
            get => frameX * 6 + frameY;
            set {
                frameX = value / 6;
                frameY = value % 6;
            }
        }

        public override void SetDefaults() {
            Projectile.width = 159;
            Projectile.height = 230;
            Projectile.scale = 2.2f;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.DamageType = CWRRef.GetTrueMeleeNoSpeedDamageClass();
            Projectile.ownerHitCheck = true;
            Projectile.usesIDStaticNPCImmunity = true;
            Projectile.idStaticNPCHitCooldown = 5;
            Projectile.frameCounter = 0;
        }

        public override void AI() {
            Projectile.frameCounter++;
            if (Projectile.frameCounter % 3 == 0) {
                CurrentFrame++;
                if (frameX >= 2) {
                    CurrentFrame = 0;
                }
            }

            if (frameX == 0 && frameY == 3 && Projectile.frameCounter % 3 == 0) {
                PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center
                    , (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
                Main.instance.CameraModifiers.Add(modifier);
                SoundEngine.PlaySound("CalamityMod/Sounds/Item/MurasamaHitOrganic".GetSound() with { Pitch = 0.35f, Volume = 0.45f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.6f, Volume = 1.25f, MaxInstances = 2 }, Projectile.position);
                Projectile.timeLeft = 30;
            }

            if ((frameX == 0 && frameY >= 3) || (frameX == 1 && frameY <= 1)) {
                Projectile.idStaticNPCHitCooldown = 8;
            }
            else if (frameX == 1 && frameY > 1) {
                Projectile.idStaticNPCHitCooldown = 12;
            }

            Projectile.direction = (Projectile.velocity.X > 0).ToDirectionInt();

            Projectile.spriteDirection = Projectile.direction;
            if (Projectile.direction == 1) {
                Projectile.Left = Owner.MountedCenter;
            }
            else {
                Projectile.Right = Owner.MountedCenter;
            }

            Projectile.Center = Owner.GetPlayerStabilityCenter() + new Vector2(Owner.direction * 86, 0);
            Owner.ChangeDir(Projectile.direction);

            Owner.itemRotation = (Projectile.velocity * Projectile.direction).ToRotation();
            SetHeld();
            Owner.itemTime = 2;
            Owner.itemAnimation = 2;
        }

        public override bool PreDraw(ref Color lightColor) {
            if (Projectile.frameCounter <= 1)
                return false;
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            Vector2 position = Projectile.Center - Main.screenPosition + (Projectile.spriteDirection == -1 ? new Vector2(90, 0) : new Vector2(-90, 0));
            Vector2 origin = texture.Size() / new Vector2(2f, 6f) * 0.5f;
            Rectangle frame = texture.Frame(2, 6, frameX, frameY);
            SpriteEffects spriteEffects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(texture, position, frame, Color.White, Projectile.rotation, origin, Projectile.scale, spriteEffects, 0);
            return false;
        }

        public override Color? GetAlpha(Color lightColor) => new Color(200, 200, 200, 170);

        public override bool? CanDamage() => ((frameX == 0 && frameY >= 3) || frameX == 1) && Projectile.frameCounter > 6;
    }
}
