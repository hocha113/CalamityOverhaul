using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 崩落的大型牙齿碎片。沉重贯穿数敌，重力弧更迟更陡，沿途撒血珠与骨渣，触物大碎裂
    /// </summary>
    internal class ShatterfangBigShard : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "ShatterfangBigShard";

        private const int GravityDelay = 12;

        private ref float Life => ref Projectile.localAI[0];
        private bool burstDone;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 3;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        public override void AI() {
            Life++;
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.26f, 16f);
            }
            Projectile.velocity.X *= 0.997f;
            //沉重的翻摆，牙尖大体朝前微微晃
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2
                + MathF.Sin(Life * 0.3f) * 0.16f;

               if (Main.dedServ) {
                return;
            }
            //断口滴血+掉骨渣，断下来的牙还带着血
            if (Life % 4 == 0) {
                Vector2 back = Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(6f, 16f);
                PRTLoader.NewParticle<Content.PRTTypes.PRT_HeartcarverDroplet>(back
                    , Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(0.8f, 0.8f)
                    , Main.rand.NextBool() ? ShatterfangFX.BloodMain : ShatterfangFX.BloodDeep
                    , Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(14, 24), 0.28f);
            }
            if (Life % 6 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Bone
                    , Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f), 100, default, 0.9f);
                d.noGravity = true;
            }
            Lighting.AddLight(Projectile.Center, ShatterfangFX.ScarletBright.ToVector3() * 0.22f);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            burstDone = true;
            ShatterBurst(oldVelocity, onTile: true);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //贯穿衰减，沉重但不无赖
            Projectile.damage = (int)(Projectile.damage * 0.8f);
            ShatterfangFX.BloodBurst(target.Center, Projectile.velocity, 0.8f);
            ShatterfangFX.ChipBurst(target.Center, -Projectile.velocity, 0.3f, 0.4f);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.55f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
        }

        public override void OnKill(int timeLeft) {
            if (!burstDone) {
                ShatterBurst(Projectile.velocity, onTile: false);
            }
        }

        /// <summary>大碎裂：牙屑扇+骨雾+双层脆响</summary>
        private void ShatterBurst(Vector2 impactVel, bool onTile) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            ShatterfangFX.ChipBurst(Projectile.Center, normal, 0.85f, 0.35f);
            ShatterfangFX.BonePuff(Projectile.Center, 3);
            SoundEngine.PlaySound(SoundID.NPCHit2 with { Volume = 0.6f, Pitch = -0.35f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = -0.1f, MaxInstances = 3 }, Projectile.Center);
            if (onTile) {
                ShatterfangFX.Punch(Projectile.Center, normal, 3f, 5f, 5);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ShatterfangAssets.BigShard?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;

            //沉重残影，比小碎片更浓
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float a = 0.32f * (1f - i / (float)Projectile.oldPos.Length);
                Color ghost = Color.Lerp(ShatterfangFX.BoneLead, ShatterfangFX.ScarletBright, i / (float)Projectile.oldPos.Length) * a;
                ghost.A = 0;
                Main.EntitySpriteDraw(tex, Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition
                    , null, ghost, Projectile.oldRot[i], origin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor
                , Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
