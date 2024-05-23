using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Particles;
using CalamityOverhaul.Content.Particles.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Projectiles.Weapons.Ranged
{
    internal class ElectrosphereLauncherOrb : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;
        public List<Projectile> Orbs = new List<Projectile>();
        public ElectrosphereLauncherOrb[] orbList = new ElectrosphereLauncherOrb[] { };
        public override void SetDefaults() {
            Projectile.width = Projectile.height = 42;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = false;
            Projectile.timeLeft = 320;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1;
        }

        public override bool PreAI() {
            CWRUtils.ClockFrame(ref Projectile.frame, 5, 3);
            return true;
        }

        public override void AI() {
            Projectile.scale = 0.6f + Math.Abs(MathF.Sin(Projectile.ai[0] * 0.1f) * 0.2f);
            Projectile.rotation += Projectile.velocity.X * 0.1f;
            if (Projectile.ai[0] > 30) {
                Projectile.velocity *= 0.9f;
                if (Projectile.ai[0] > 40) {
                    if (Main.rand.NextBool(13)) {
                        foreach (ElectrosphereLauncherOrb p in orbList) {
                            if (p.Projectile == Projectile || !p.Projectile.Alives() || p.Projectile.type != Projectile.type) {
                                continue;
                            }
                            Vector2 pos = p.Projectile.Center;
                            Vector2 toPos = Projectile.Center.To(pos);
                            Vector2 toPosNor = toPos.UnitVector();
                            float rot = toPosNor.ToRotation();
                            float leng = toPos.Length();
                            int wid = 5;
                            int num = (int)(leng / wid) + 1;
                            for (int i = 0; i < num; i++) {
                                Vector2 pos2 = toPosNor * (i * wid) + Projectile.Center;
                                Vector2 vr = CWRUtils.randVr(3, 7);
                                var sparkier = Dust.NewDust(pos2, 1, 1, DustID.UnusedWhiteBluePurple, vr.X, vr.Y, 100, default, 1f);
                                Main.dust[sparkier].scale += 0.3f + (Main.rand.Next(50) * 0.01f);
                                Main.dust[sparkier].noGravity = false;
                                Main.dust[sparkier].velocity *= 0.1f;
                            }
                        }
                    }
                    Projectile.ai[1] = 1;
                }
            }
            Projectile.ai[0]++;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Projectile.ai[1] != 0) {
                float point = 0f;
                foreach (ElectrosphereLauncherOrb p in orbList) {
                    if (p.Projectile == Projectile || !p.Projectile.Alives() || p.Projectile.type != Projectile.type) {
                        continue;
                    }
                    Vector2 pos = p.Projectile.Center;
                    if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, pos, 3, ref point)) {
                        return true;
                    }
                }
            }

            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Projectile.numHits == 0) {
                Projectile.ai[1] = 1;
                Projectile.velocity *= 0.5f;
                if (Projectile.ai[0] < 40) {
                    Projectile.ai[0] = 40;
                }
            }
            Color light = Lighting.GetColor((int)(Projectile.position.X + (Projectile.width * 0.5)) / 16, (int)((Projectile.position.Y + (Projectile.height * 0.5)) / 16.0));
            for (int i = 0; i < Main.rand.Next(3, 16); i++) {
                Vector2 pos = target.Center + Main.rand.NextVector2Unit() * Main.rand.Next(target.width);
                Vector2 particleSpeed = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2() * Main.rand.NextFloat(15.5f, 37.7f);
                CWRParticle energyLeak = new LightParticle(pos, particleSpeed
                    , 0.3f, light, 6 + Main.rand.Next(5), 1, 1.5f, hueShift: 0.0f);
                CWRParticleHandler.AddParticle(energyLeak);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.Electrosphere);
            Main.instance.LoadProjectile(ProjectileID.CultistBossLightningOrb);
            Texture2D value = TextureAssets.Projectile[ProjectileID.Electrosphere].Value;
            Texture2D value2 = CWRUtils.GetT2DValue(CWRConstant.Projectile_Ranged + "ElectricBolt");
            Texture2D value3 = TextureAssets.Projectile[ProjectileID.CultistBossLightningOrb].Value;

            if (Projectile.ai[1] != 0) {
                foreach (ElectrosphereLauncherOrb p in orbList) {
                    if (p.Projectile == Projectile || !p.Projectile.Alives() || p.Projectile.type != Projectile.type) {
                        continue;
                    }
                    Vector2 pos = p.Projectile.Center;
                    Vector2 toPos = Projectile.Center.To(pos);
                    Vector2 toPosNor = toPos.UnitVector();
                    float rot = toPosNor.ToRotation();
                    float leng = toPos.Length();
                    int wid = value2.Width / 2;
                    int num = (int)(leng / wid) + 1;
                    for (int i = 0; i < num; i++) {
                        Vector2 drawPos = toPosNor * (i * wid) + Projectile.Center - Main.screenPosition;
                        Main.EntitySpriteDraw(value2, drawPos, CWRUtils.GetRec(value2, Projectile.frame, 4)
                        , Color.White, rot, CWRUtils.GetOrig(value2, 4), Projectile.scale, SpriteEffects.None, 0);
                    }
                }
            }
            Vector2 org = CWRUtils.GetOrig(value, 4);
            Rectangle rec = CWRUtils.GetRec(value, Projectile.frame, 4);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rec
                , Color.White, 0, CWRUtils.GetOrig(value, 4), 1, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rec
                , Color.White, 0, CWRUtils.GetOrig(value, 4), 1.01f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rec
                , Color.White, 0, CWRUtils.GetOrig(value, 4), 1.02f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rec
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 4), Projectile.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(value, Projectile.Center - Main.screenPosition, rec
                , Color.White, Projectile.rotation, CWRUtils.GetOrig(value, 4), 1.2f - Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
