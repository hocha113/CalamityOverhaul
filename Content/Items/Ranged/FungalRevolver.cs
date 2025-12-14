using CalamityMod;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class FungalRevolver : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "FungalRevolver";
        public override void SetDefaults() {
            Item.CloneDefaults(ItemID.Revolver);
            Item.damage = 18;
            Item.shoot = ModContent.ProjectileType<FungiAmmo>();
            Item.SetCartridgeGun<FungalRevolverHeld>(6);
            Item.CWR().CartridgeType = CartridgeUIEnum.Magazines;
        }
    }

    internal class FungalRevolverHeld : BaseFeederGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "FungalRevolver";
        public override int TargetID => ModContent.ItemType<FungalRevolver>();
        public override void SetRangedProperty() {
            KreloadMaxTime = 30;
            FireTime = 8;
            HandIdleDistanceX = 18;
            HandIdleDistanceY = 3;
            HandFireDistanceX = 18;
            HandFireDistanceY = -2;
            ShootPosNorlLengValue = -5;
            ShootPosToMouLengValue = 10;
            RepeatedCartridgeChange = true;
            CanCreateCaseEjection = false;
            GunPressure = 0.1f;
            ControlForce = 0.05f;
            Recoil = 0.9f;
            RangeOfStress = 25;
            CanCreateSpawnGunDust = false;
            Onehanded = true;
            InOwner_HandState_AlwaysSetInFireRoding = true;
            ForcedConversionTargetAmmoFunc = () => AmmoTypes == ProjectileID.Bullet;
            ToTargetAmmo = ModContent.ProjectileType<FungiAmmo>();
            LoadingAmmoAnimation = LoadingAmmoAnimationEnum.Revolver;
            if (!MagazineSystem) {
                FireTime += 5;
            }
        }

        public override void KreloadSoundloadTheRounds() {
            base.KreloadSoundloadTheRounds();
            for (int i = 0; i < 6; i++) {
                CaseEjection();
            }
        }
    }

    public class FungiAmmo : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            AIType = ProjectileID.Bullet;
            Projectile.extraUpdates = 2;
        }

        public override void AI() {
            Lighting.AddLight(Projectile.Center, new Vector3(0, 244, 252) * (1.2f / 255));

            if (++Projectile.localAI[0] > 4f) {
                Vector2 dspeed = -Projectile.velocity * 0.5f;
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueFairy, 0f, 0f, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = dspeed;
                Vector2 spawnPos = Projectile.Center + Projectile.velocity.GetNormalVector() * MathF.Sin(Projectile.localAI[0] * 0.6f) * 10;
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(spawnPos, Projectile.velocity / 6);
                prt.Scale /= 2f;
            }

            if (Projectile.localAI[1] < 22) {
                Projectile.localAI[1]++;
            }
        }

        public override void OnKill(int timeLeft) {
            if (Projectile.IsOwnedByLocalPlayer()) {
                Vector2 velocity = CalamityUtils.RandomVelocity(100f, 60f, 85f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity
                    , ModContent.ProjectileType<SporeBurst>(), Projectile.damage / 2, 0f, Projectile.owner);
            }
            SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.position);
            for (int k = 0; k < 5; k++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                    , DustID.BlueFairy, Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);
            }
        }
    }

    public class SporeBurst : ModProjectile, ILocalizedModType
    {
        public override string Texture => CWRConstant.Projectile + "Glomushroom";
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }
        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
        }

        public override bool? CanHitNPC(NPC target) => Projectile.timeLeft < 150 && target.CanBeChasedBy(Projectile);

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.5f + 1f;
            Lighting.AddLight(Projectile.Center, 0f, 0.7f * pulse, 1f * pulse);

            Projectile.localAI[0] += 1f;
            if (Projectile.localAI[0] > 4f) {
                Vector2 dspeed = -Projectile.velocity * 0.5f;
                int dust = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.BlueFairy, 0f, 0f, 100, default, 1f);
                Main.dust[dust].noGravity = true;
                Main.dust[dust].velocity = dspeed;
            }

            if (Projectile.timeLeft == 150) {
                for (int k = 0; k < 20; k++) {
                    Vector2 offset = Main.rand.NextVector2Circular(3f, 3f);
                    int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.MagicMirror, offset.X, offset.Y, 150, default, 1.2f);
                    Main.dust[d].noGravity = true;
                    Main.dust[d].velocity *= 1.5f;
                }
            }

            bool isHoming = false;
            if (Projectile.timeLeft < 150) {
                isHoming = true;
                CWRRef.HomeInOnNPC(Projectile, !Projectile.tileCollide, 450f, 6.5f, 20f);
            }

            if (!isHoming) {
                Projectile.velocity.Y += 0.14f;
            }

            if (isHoming && Main.rand.NextBool(3)) {
                int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.BlueFairy, 0, 0, 150, Color.Cyan, 1.3f);
                Main.dust[d].velocity = -Projectile.velocity * 0.3f + Main.rand.NextVector2Circular(1f, 1f);
                Main.dust[d].noGravity = true;
            }
        }

        public override void OnKill(int timeLeft) {
            Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                , DustID.BlueFairy, Projectile.oldVelocity.X * 0.5f, Projectile.oldVelocity.Y * 0.5f);

            for (int i = 0; i < 30; i++) {
                Vector2 speed = Main.rand.NextVector2CircularEdge(3f, 3f);
                int d = Dust.NewDust(Projectile.Center, 0, 0, DustID.MagicMirror, speed.X, speed.Y, 100, Color.Cyan, 1.5f);
                Main.dust[d].noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
                Color color = Color.Cyan * fade * 0.7f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None);
            }
            return false;
        }
    }
}
