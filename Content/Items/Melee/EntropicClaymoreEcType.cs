using CalamityMod.Items;
using CalamityMod.Items.Weapons.Melee;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using CalamityOverhaul.Content.Projectiles.Weapons;
using CalamityOverhaul.Content.Projectiles.Weapons.Melee.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee
{
    /// <summary>
    /// 熵之舞
    /// </summary>
    internal class EntropicClaymoreEcType : EctypeItem
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EntropicClaymore";
        public static readonly Color EntropicColor1 = new Color(25, 5, 9);
        public static readonly Color EntropicColor2 = new Color(25, 5, 9);
        public override void SetDefaults() => SetDefaultsFunc(Item);
        public static void SetDefaultsFunc(Item Item) {
            Item.damage = 92;
            Item.DamageType = DamageClass.Melee;
            Item.useAnimation = 38;
            Item.useTime = 28;
            Item.knockBack = 5.25f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.UseSound = SoundID.Item1;
            Item.useTurn = true;
            Item.autoReuse = true;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.value = CalamityGlobalItem.RarityCyanBuyPrice;
            Item.rare = ItemRarityID.Cyan;
            Item.shootSpeed = 12f;
            Item.SetKnifeHeld<EntropicClaymoreHeld>();
            Item.EasySetLocalTextNameOverride("EntropicClaymoreEcType");
        }
    }

    internal class EntropicClaymoreProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public ref float Time => ref Projectile.ai[0];
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.ignoreWater = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.extraUpdates = 10;
            Projectile.timeLeft = 67 * Projectile.extraUpdates;
            Projectile.tileCollide = false;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 30;
        }

        public override void AI() {
            Time++;
            Lighting.AddLight(Projectile.Center, Color.LightGreen.ToVector3() * 0.2f);
            Player Owner = Main.player[Projectile.owner];
            float targetDist = Vector2.Distance(Owner.Center, Projectile.Center);

            if (Projectile.timeLeft % 3 == 0 && Time > 12f && targetDist < 1400f) {
                DRK_Spark2 spark = new DRK_Spark2(Projectile.Center, -Projectile.velocity * 0.05f, false, 6, 1.6f, Color.Black) {
                    entity = Owner
                };
                DRKLoader.AddParticle(spark);
            }

            if (Projectile.timeLeft % 3 == 0 && Time > 12f && targetDist < 1400f) {
                DRK_Line_FormPlayer spark2 = new DRK_Line_FormPlayer(Projectile.Center, -Projectile.velocity * 0.05f, false, 6, 0.9f, Color.LightGreen) {
                    Owner = Owner
                };
                DRKLoader.AddParticle(spark2);
            }

            if (Time % (Projectile.extraUpdates + 1) == 0)
                Projectile.position += Owner.CWR().PlayerPositionChange;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Projectile.damage -= 15;
        }

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.6f;
            }
        }
    }

    internal class EntropicClaymoreHeld : BaseKnife, IDrawWarp
    {
        public override int TargetID => ModContent.ItemType<EntropicClaymore>();
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 96;
            canDrawSlashTrail = true;
            distanceToOwner = 20;
            drawTrailBtommWidth = 80;
            drawTrailTopWidth = 60;
            drawTrailCount = 46;
            Length = 122;
            unitOffsetDrawZkMode = -16;
            SwingAIType = SwingAITypeEnum.UpAndDown;
            overOffsetCachesRoting = MathHelper.ToRadians(16);
        }

        public override void Shoot() {
            SoundStyle sound = new("CalamityMod/Sounds/Item/ELRFire");
            SoundEngine.PlaySound(sound with { Pitch = Projectile.ai[2] * 0.15f, Volume = 0.45f }, Projectile.Center);
            int dir = -Math.Sign(rotSpeed);
            Vector2 spwanPos = ShootSpanPos + ShootVelocity.GetNormalVector() * (Projectile.ai[2] * 20 * dir) - ShootVelocity * (Projectile.ai[2] * 6) + ShootVelocity.UnitVector() * 100;
            Projectile.NewProjectile(Source, spwanPos, ShootVelocity, ModContent.ProjectileType<EntropicClaymoreProj>(), (int)(Projectile.damage * 0.65f), Projectile.knockBack, Projectile.owner);
        }

        public override bool PreInOwnerUpdate() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame);
            }
            if (++Projectile.ai[1] > (20 * SetSwingSpeed(1))) {
                canShoot = true;
                Projectile.ai[1] = 0;
                Projectile.ai[2]++;
            }
            return base.PreInOwnerUpdate();
        }

        bool IDrawWarp.canDraw() => true;

        bool IDrawWarp.noBlueshift() => true;

        void IDrawWarp.Warp() => WarpDraw();

        void IDrawWarp.costomDraw(SpriteBatch spriteBatch) {
            Texture2D texture = TextureValue;
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.FlipVertically : SpriteEffects.None;

            Vector2 offsetOwnerPos = safeInSwingUnit.GetNormalVector() * unitOffsetDrawZkMode * Projectile.spriteDirection;
            float drawRoting = Projectile.rotation;
            if (Projectile.spriteDirection == -1) {
                drawRoting += MathHelper.Pi;
            }
            //烦人的对角线翻转代码，我凑出来了这个效果，它很稳靠，但我仍旧不想细究这其中的数学逻辑
            if (inDrawFlipdiagonally) {
                effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                drawRoting += MathHelper.PiOver2;
                offsetOwnerPos *= -1;
            }

            Vector2 drawPosValue = Projectile.Center - RodingToVer(toProjCoreMode, (Projectile.Center - Owner.Center).ToRotation()) + offsetOwnerPos;

            Main.EntitySpriteDraw(texture, drawPosValue - Main.screenPosition + Vector2.UnitY * Projectile.gfxOffY, new Rectangle?(rect)
                , Projectile.GetAlpha(Lighting.GetColor(new Point((int)(Projectile.Center.X / 16), (int)(Projectile.Center.Y / 16))))
                , drawRoting, drawOrigin, Projectile.scale, effects, 0);
        }

        public override void DrawSwing(SpriteBatch spriteBatch, Color lightColor) { }
    }
}
