using CalamityMod;
using CalamityMod.NPCs.Yharon;
using CalamityOverhaul.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Melee.DawnshatterAzureProj
{
    internal class TheEndSun : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "DawnshatterAzureBeam";
        public override bool IsLoadingEnabled(Mod mod) {
            if (!CWRServerConfig.Instance.AddExtrasContent) {
                return false;
            }
            return base.IsLoadingEnabled(mod);
        }
        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 13;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.MaxUpdates = 6;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            CWRDust.SpanCycleDust(Projectile, DustID.InfernoFork, DustID.Flare);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                target.CWR().TheEndSunOnHitNum = true;
                SoundEngine.PlaySound(Yharon.ShortRoarSound, target.position);
                for (int i = 0; i < 3; i++) {
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center + new Vector2(Main.rand.Next(-260, 260), -520 + Main.rand.Next(-160, 160)), new Vector2(0, 26)
                    , ModContent.ProjectileType<TheDaybreak>(), Projectile.damage, Projectile.knockBack, Projectile.owner, 0, 32);
                }
            }
        }

        public override void OnKill(int timeLeft) {
            base.OnKill(timeLeft);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D value = TextureAssets.Projectile[Type].Value;
            Vector2 orig = value.Size() / 2;
            Vector2 offset = Projectile.Size / 2f - Main.screenPosition;
            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                Main.EntitySpriteDraw(value, Projectile.oldPos[i] + offset, null, Color.White * (i / (float)Projectile.oldPos.Length), Projectile.rotation, orig, Projectile.scale, 0, 0);
            }
            return false;
        }
    }
}
