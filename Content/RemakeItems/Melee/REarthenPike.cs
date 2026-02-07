using CalamityOverhaul.Content.MeleeModify.Core;
using CalamityOverhaul.Content.Projectiles.Weapons.Rogue.HeldProjs;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.RemakeItems.Melee
{
    internal class REarthenPike : CWRItemOverride
    {
        public override void SetStaticDefaults() => ItemID.Sets.ItemsThatAllowRepeatedRightClick[TargetID] = true;
        public override void SetDefaults(Item item) {
            item.UseSound = null;
            item.damage = 120;
            item.SetKnifeHeld<EarthenPikeHeld>();
        }
        public override bool? AltFunctionUse(Item item, Player player) => true;
        public override void ModifyShootStats(Item item, Player player, ref ItemShootState shootStats) {
            item.useTime = 25;
            if (player.altFunctionUse == 2) {
                item.useTime = 40;
                shootStats.Type = ModContent.ProjectileType<EarthenPikeAlt>();
            }
        }
    }

    internal class EarthenPikeAlt : BaseThrowable
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "EarthenPike";
        private bool onTIle;
        private float tileRot;
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 1;
            ItemID.Sets.ItemsThatAllowRepeatedRightClick[Type] = true;
        }
        public override void SetThrowable() {
            Projectile.DamageType = DamageClass.Melee;
            Projectile.width = Projectile.height = 24;
            Projectile.alpha = 255;
            HandOnTwringMode = -15;
            OnThrowingGetRotation = (a) => ToMouseA;
            OnThrowingGetCenter = (armRotation)
                => Owner.GetPlayerStabilityCenter() + Vector2.UnitY.RotatedBy(armRotation * Owner.gravDir)
                * HandOnTwringMode * Owner.gravDir + UnitToMouseV * 6;
        }

        public override void FlyToMovementAI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (++Projectile.ai[2] > 60 && !onTIle) {
                Projectile.velocity.Y += 0.3f;
                Projectile.velocity.X *= 0.99f;
            }
            if (onTIle) {
                Projectile.rotation = tileRot;
                Player player = CWRUtils.GetPlayerInstance(Projectile.owner);
                if (player != null) {
                    if (player.Distance(Projectile.Center) < Projectile.width) {
                        player.CWR().ReceivingPlatformTime = 2;
                    }
                }
            }
        }

        public override bool PreThrowOut() {
            SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            Projectile.velocity = UnitToMouseV * 17.5f;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.localAI[0] = 1;
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            for (int i = 0; i < 16; i++) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                , DustID.Sand, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!onTIle) {
                Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
                SoundEngine.PlaySound(SoundID.Dig, Projectile.position);
                Projectile.velocity = Vector2.Zero;
                tileRot = Projectile.rotation;
                onTIle = true;
            }

            Projectile.alpha -= 15;
            return Projectile.alpha < 15;
        }

        public override void DrawThrowable(Color lightColor) {
            float drawRot = Projectile.rotation + (MathHelper.PiOver4 + OffsetRoting) * (Projectile.velocity.X > 0 ? 1 : -1);
            if (Projectile.localAI[0] == 1) {
                for (int k = 0; k < Projectile.oldPos.Length; k++) {
                    Vector2 drawPos = Projectile.oldPos[k] - Main.screenPosition + Projectile.Size / 2;
                    Color color = lightColor * (float)((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length / 2);
                    Main.EntitySpriteDraw(TextureValue, drawPos, null, color
                    , drawRot, TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
                }
            }
            Main.EntitySpriteDraw(TextureValue, Projectile.Center - Main.screenPosition, null, lightColor * (Projectile.alpha / 255f)
                , drawRot, TextureValue.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.None : SpriteEffects.FlipVertically, 0);
        }
    }

    internal class EarthenPikeHeld : BaseKnife
    {
        public override void SetKnifeProperty() {
            Projectile.width = Projectile.height = 64;
            Length = 52;
            autoSetShoot = true;
        }

        public override bool PreSwingAI() {
            if (Time == 0) {
                SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
            }
            StabBehavior(initialLength: 60, lifetime: 26, scaleFactorDenominator: 220f, minLength: 60, maxLength: 100, ignoreUpdateCount: true);
            return false;
        }

        public override void Shoot() {
            Projectile.NewProjectile(Source, ShootSpanPos, AbsolutelyShootVelocity,
                CWRID.Proj_FossilShard, (int)(Projectile.damage * 0.5), Projectile.knockBack * 0.85f, Projectile.owner);
            Item.useTime = 40;
        }

        public override void MeleeEffect() {
            if (Main.rand.NextBool(3)) {
                Dust.NewDust(Projectile.position + Projectile.velocity, Projectile.width, Projectile.height
                , DustID.Sand, Projectile.velocity.X * 0.5f, Projectile.velocity.Y * 0.5f);
            }
        }

        public override void KnifeHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(CWRID.Buff_ArmorCrunch, 60);
        }
    }
}
