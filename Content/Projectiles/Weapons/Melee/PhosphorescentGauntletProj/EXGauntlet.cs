using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.PhosphorescentGauntletProj
{
    internal class EXGauntlet : ModProjectile
    {
        public override string Texture => CWRConstant.Cay_Wap_Melee + "PhosphorescentGauntlet";
        public override void AutoStaticDefaults() => AutoProj.AutoStaticDefaults(this);
        public Vector2 TargetPos {
            get => new Vector2(Projectile.ai[0], Projectile.ai[1]);
            set { Projectile.ai[0] = value.X; Projectile.ai[1] = value.Y; }
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 13;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 122;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.MaxUpdates = 5;
            Projectile.scale = 5;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 156;
            Projectile.penetrate = -1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.Center.Distance(TargetPos) < 12) {
                Projectile.Kill();
            }
        }

        public override bool? CanDamage() => Projectile.numHits > 0 ? false : base.CanDamage();

        public override void OnKill(int timeLeft) {
            SoundEngine.PlaySound("CalamityMod/Sounds/Item/SupernovaBoom".GetSound() with { Volume = 0.9f }, Projectile.Center);
            GauntletInAltShoot.SpanDust(Projectile, 3);

            Vector2 normVr = Projectile.velocity.GetNormalVector();

            for (int i = 0; i < 136; i++) {
                Vector2 vr = normVr * (Main.rand.NextBool() ? -1 : 1) * Main.rand.Next(3, 113);
                Dust sulphurousAcid = Dust.NewDustPerfect(Projectile.Center + VaultUtils.RandVr(13), DustID.JungleTorch);
                sulphurousAcid.velocity = vr;
                sulphurousAcid.noGravity = true;
                sulphurousAcid.scale = Main.rand.NextFloat(1, 2.2f);
            }
            PunchCameraModifier modifier = new PunchCameraModifier(Projectile.Center
                , (Main.rand.NextFloat() * ((float)Math.PI * 2f)).ToRotationVector2(), 20f, 6f, 20, 1000f, FullName);
            Main.instance.CameraModifiers.Add(modifier);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Vector2 pos = Projectile.oldPos[i];
                Main.EntitySpriteDraw(value, pos - Main.screenPosition + Projectile.Size / 2, null, Color.White * (Projectile.timeLeft / 15f) * (i / 36f)
                    , Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale * (i / 16f), Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            }
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, null, Color.White * (Projectile.timeLeft / 15f)
                , Projectile.rotation + MathHelper.PiOver4 + MathHelper.Pi + (Projectile.velocity.X > 0 ? MathHelper.PiOver2 : 0)
                , value.Size() / 2, Projectile.scale, Projectile.velocity.X > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
            return false;
        }
    }
}
