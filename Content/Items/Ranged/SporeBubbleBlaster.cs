using CalamityOverhaul.Common;
using CalamityOverhaul.Content.RangedModify.Core;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged
{
    internal class SporeBubbleBlaster : ModItem
    {
        public override string Texture => CWRConstant.Item_Ranged + "SporeBubbleBlaster";
        public override void SetDefaults() {
            Item.width = Item.height = 32;
            Item.damage = 16;
            Item.DamageType = DamageClass.Ranged;
            Item.useAnimation = Item.useTime = 10;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.rare = ItemRarityID.Orange;
            Item.value = 600;
            Item.shootSpeed = 12;
            Item.shoot = ModContent.ProjectileType<SporeBobo>();
            Item.UseSound = null;//开火音效由手持弹幕负责
            Item.noMelee = true;
            Item.noUseGraphic = true;
            Item.autoReuse = true;
        }

        public override bool CanUseItem(Player player)
            => player.ownedProjectileCounts[ModContent.ProjectileType<SporeBubbleBlasterHeld>()] == 0;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source
            , Vector2 position, Vector2 velocity, int type, int damage, float knockback)
            => BaseHeldGun.SpawnHeldProj<SporeBubbleBlasterHeld>(player, source);
    }

    internal class SporeBubbleBlasterHeld : BaseHeldGun
    {
        public override string Texture => CWRConstant.Item_Ranged + "SporeBubbleBlasterHeld";
        public override int TargetID => ModContent.ItemType<SporeBubbleBlaster>();
        public override SoundStyle? ShootSound => CWRSound.SporeBubble;
        //单次点射的发数与节奏
        private const int BurstCount = 10;
        private const int ShotInterval = 2;
        private const int BurstCooldown = 32;
        private int frame;
        private int frameConter;
        public override void SetGunProperty() {
            Onehanded = true;
            AlwaysAimPose = true;
        }

        public override void AI() {
            UpdateHeldPose(WantsFireLeft);

            //点射期间的喷口帧动画
            if (WantsFireLeft && FireCooldown < 4) {
                if (++frameConter > 2) {
                    if (++frame > 2) {
                        frame = 0;
                    }
                    frameConter = 0;
                }
            }
            else {
                frame = 0;
            }

            if (WantsFireLeft && FireCooldown <= 0) {
                Fire();
            }
            Time++;
        }

        private void Fire() {
            //每轮点射开始时播放一次音效
            if (fireIndex == 0) {
                PlayShootSound();
            }

            SnapToAimPose();

            if (Projectile.IsOwnedByLocalPlayer()) {
                //孢子泡泡带少量散布，并奔向远处的标记点
                Vector2 orgVelocity = ShootVelocity;
                Vector2 velocity = orgVelocity * Main.rand.NextFloat(0.8f, 1f);
                velocity = velocity.RotatedByRandom(0.12f);
                Vector2 targetPos = ShootPos + orgVelocity * 300;
                Projectile.NewProjectile(Source, ShootPos, velocity
                    , ModContent.ProjectileType<SporeBobo>(), WeaponDamage, WeaponKnockback, Owner.whoAmI, targetPos.X, targetPos.Y);
            }

            //打满一轮后进入较长的换气冷却
            if (++fireIndex >= BurstCount) {
                fireIndex = 0;
                frame = 0;
                FireCooldown = BurstCooldown;
            }
            else {
                FireCooldown = ShotInterval;
            }
        }

        public override void GunDraw(Vector2 drawPos, ref Color lightColor) {
            float offsetRot = DrawGunBodyRotOffset * (DirSign > 0 ? 1 : -1);
            Rectangle rectangle = TextureValue.GetRectangle(frame, 3);
            Main.EntitySpriteDraw(TextureValue, drawPos, rectangle, lightColor
                , Projectile.rotation + offsetRot, rectangle.Size() / 2, Projectile.scale
                , DirSign > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically);
        }
    }

    internal class SporeBobo : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 32;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.timeLeft = 300;
            Projectile.friendly = true;
            Projectile.extraUpdates = 13;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            if (++Projectile.ai[2] > 30) {
                Projectile.SmoothHomingBehavior(new Vector2(Projectile.ai[0], Projectile.ai[1]), 1, 0.1f);
            }
            Projectile.velocity *= 0.99f;

            if (Projectile.ai[2] > 1 && Main.rand.NextBool(3) && Projectile.velocity.Length() > 1f) {
                var prt = PRTLoader.NewParticle<PRT_SporeBobo>(Projectile.Center, Projectile.velocity / 3);
                prt.shader = GameShaders.Armor.GetShaderFromItemId(Projectile.CWR().DyeItemID);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 30);
            Projectile.damage = (int)(Projectile.damage * 0.9f);
        }
    }

    internal class PRT_SporeBobo : BasePRT
    {
        public override string Texture => CWRConstant.Other + "SporeBobo";
        public override void SetProperty() {
            Scale = Main.rand.NextFloat(0.8f, 1.22f);
            Lifetime = Main.rand.Next(18, 36);
            Frame = TexValue.GetRectangle(Main.rand.Next(4), 4);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Opacity = Main.rand.NextFloat(0.2f, 0.6f);
        }

        public override void AI() {
            Lighting.AddLight(Position, Color.White.ToVector3() * 0.2f);//孢子需要微微发光
            Rotation += Velocity.X * 0.1f;
            if (Framing.GetTileSafely(Position.ToTileCoordinates16()).HasTile) {//如果和物块接触，迅速变小消失
                Scale -= 0.1f;
                if (Scale <= 0) {
                    Kill();
                }
                Scale = MathHelper.Clamp(Scale, 0, 2f);
            }

            if (LifetimeCompletion > 0.6f) {
                if (Opacity > 0f) {
                    Opacity -= 0.1f;
                }
            }
            else {
                if (Opacity < 1f) {
                    Opacity += 0.1f;
                }
            }

            Opacity = MathHelper.Clamp(Opacity, 0, 1f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch) {
            Color drawColor = Lighting.GetColor(Position.ToTileCoordinates()) * Opacity;
            spriteBatch.Draw(TexValue, Position - Main.screenPosition, Frame
                , drawColor, Rotation, Frame.Size() / 2, Scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
