using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.Shatterfangs
{
    /// <summary>
    /// 小型牙齿碎片。短暂平直后吃重力走抛物线，飞行掉骨渣，触物碎成牙屑<br/>
    /// ai[1]=贴图变体 0/1
    /// </summary>
    internal class ShatterfangShard : ModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Melee + "ShatterfangShard1";

        /// <summary>出手后多少帧开始吃重力，碎片是甩出去的</summary>
        private const int GravityDelay = 8;

        private ref float Life => ref Projectile.localAI[0];
        private bool burstDone;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 150;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > GravityDelay) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.24f, 15f);
            }
            Projectile.velocity.X *= 0.998f;
            //牙尖朝向飞行方向(贴图尖端朝上)
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行掉骨渣
            if (!Main.dedServ && Life % 5 == 0) {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * 0.4f, DustID.Bone
                    , Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.5f, 0.5f), 120, default, 0.8f);
                d.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            burstDone = true;
            ShatterBurst(oldVelocity);
            return true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            ShatterfangFX.BloodBurst(target.Center, Projectile.velocity, 0.3f);
        }

        public override void OnKill(int timeLeft) {
            if (!burstDone) {
                ShatterBurst(Projectile.velocity);
            }
        }

        /// <summary>碎裂：几粒牙屑+骨粉+脆响</summary>
        private void ShatterBurst(Vector2 impactVel) {
            if (Main.dedServ) {
                return;
            }
            Vector2 normal = -impactVel.SafeNormalize(Vector2.UnitY);
            ShatterfangFX.ChipBurst(Projectile.Center, normal, 0.25f, 0.2f);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.32f, Pitch = 0.4f, MaxInstances = 3 }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = ((int)Projectile.ai[1] == 1 ? ShatterfangAssets.Shard2 : ShatterfangAssets.Shard1)?.Value;
            if (tex == null) {
                return false;
            }
            Vector2 origin = tex.Size() * 0.5f;

            //速度残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i -= 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float a = 0.26f * (1f - i / (float)Projectile.oldPos.Length);
                Color ghost = ShatterfangFX.BoneLead * a;
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
