using CalamityMod.NPCs.Yharon;
using CalamityMod.Particles;
using CalamityMod.Sounds;
using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Items.Melee;
using InnoVault.GameContent.BaseEntity;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Utilities;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class DawnshatterOut : BaseHeldProj
    {
        public override LocalizedText DisplayName => CWRUtils.SafeGetItemName<DawnshatterAzure>();
        public override string Texture => CWRConstant.Item_Melee + "DawnshatterAzure";
        private SlotId roar;
        private bool spanSwing = true;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 40;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 190;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.ownerHitCheck = true;
            Projectile.MaxUpdates = 3;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 7 * Projectile.MaxUpdates;
            Projectile.hide = true;
        }

        public override bool PreUpdate() {
            if (Projectile.ai[0] == 0) {
                roar = SoundEngine.PlaySound(Yharon.RoarSound with { Pitch = 0.2f }, Owner.Center);
            }
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);

            if (SoundEngine.TryGetActiveSound(roar, out ActiveSound activeSound)) {
                activeSound.Position = Projectile.Center;
            }

            SetHeld();
            return true;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Owner.direction = Math.Sign(Projectile.velocity.X);
            //卸载掉玩家的所有钩爪
            Owner.RemoveAllGrapplingHooks();
            //卸载掉玩家的所有坐骑
            Owner.mount.Dismount(Owner);

            if (Projectile.IsOwnedByLocalPlayer()) {//发射衍生弹幕和进行位移的代码只能交由主人玩家执行
                Owner.Center = Vector2.Lerp(Owner.Center, Projectile.Center, 0.1f);
                Owner.velocity = Projectile.velocity.UnitVector();
                if (CWRServerConfig.Instance.LensEasing) {
                    Main.SetCameraLerp(0.1f, 10);
                }
                float projToOwnerLeng = Projectile.Center.Distance(Owner.Center);
                if (projToOwnerLeng < 233) {
                    Owner.GivePlayerImmuneState(5, false);
                }
                if (!DownRight) {
                    Projectile.NewProjectile(Owner.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                        , ModContent.ProjectileType<DawnshatterSwing>(), Projectile.damage, 0, Owner.whoAmI);
                    spanSwing = false;
                    Projectile.Kill();
                }
            }
            Vector2 origPos = Projectile.Center + Projectile.velocity.UnitVector() * -132;
            Lighting.AddLight(Projectile.Center, Color.Red.ToVector3() * 0.2f);
            AltSparkParticle spark = new(origPos, Projectile.velocity * 0.05f, false, 22, 3.3f, Color.DarkRed);
            GeneralParticleHandler.SpawnParticle(spark);
            Particle orb = new GenericBloom(origPos + Main.rand.NextVector2Circular(10, 10), Projectile.velocity * Main.rand.NextFloat(0.05f, 0.5f)
                , Color.Gold, Main.rand.NextFloat(0.2f, 0.45f), Main.rand.Next(9, 12), true, false);
            GeneralParticleHandler.SpawnParticle(orb);
            LineParticle spark2 = new(origPos, -Projectile.velocity * 0.05f, false, 27, 1.7f, Color.Red);
            GeneralParticleHandler.SpawnParticle(spark2);

            float armRotSengsFront, armRotSengsBack;
            armRotSengsBack = armRotSengsFront = (Projectile.rotation - MathHelper.PiOver2) / CWRUtils.atoR * -DirSign;
            Owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsFront) * -DirSign);
            Owner.SetCompositeArmBack(true, Player.CompositeArmStretchAmount.Full, MathHelper.ToRadians(armRotSengsBack) * -DirSign);
            Projectile.ai[0]++;
        }

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound(CommonCalamitySounds.MeatySlashSound with { Pitch = 0.6f }, Projectile.Center);
            if (Projectile.IsOwnedByLocalPlayer() && spanSwing) {
                Projectile.NewProjectile(Owner.FromObjectGetParent(), Projectile.Center, Vector2.Zero
                        , ModContent.ProjectileType<DawnshatterSwing>(), Projectile.damage, 0, Owner.whoAmI);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Type].Value;
            float rot = Projectile.rotation + MathHelper.PiOver4 + (Owner.direction > 0 ? 0 : MathHelper.PiOver2);
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;
            Vector2 origin = CWRUtils.GetOrig(texture, 4);
            Main.EntitySpriteDraw(texture, drawPosition, CWRUtils.GetRec(texture, Projectile.frame, 4), Projectile.GetAlpha(lightColor)
                , rot, origin, Projectile.scale * 0.7f, Owner.direction > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0);
            return false;
        }
    }
}
