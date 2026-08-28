using CalamityOverhaul.Content.NPCs.BloomsandSerpents;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Ranged.Thornstrings
{
    /// <summary>
    /// 棘箭：任意箭矢经棘弦强化后的形态。命中敌人向两侧崩出短针；
    /// 重箭（ai[0]=1）贯穿多目标，落点绽放花瓣圈
    /// </summary>
    internal class ThornstringArrow : BssModProjectile
    {
        public override string Texture => CWRConstant.Projectile_Ranged + "ThornstringArrow";

        /// <summary>1 为右键满蓄重箭</summary>
        private ref float Heavy => ref Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.arrow = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 270;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;
        }

        public override void AI() {
            //首帧按重箭档补齐贯穿与体型（ai 随生成包同步，各端一致）
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                if (Heavy > 0f) {
                    Projectile.penetrate = 3;
                    Projectile.scale = 1.15f;
                }
            }

            if (++Projectile.localAI[1] > 20f) {
                Projectile.velocity.Y = MathHelper.Clamp(Projectile.velocity.Y + 0.12f, -20f, 15f);
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!Main.dedServ && Main.rand.NextBool(Heavy > 0f ? 4 : 9)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, Heavy > 0f ? DustID.RedTorch : DustID.JunglePlants,
                    -Projectile.velocity * 0.05f, 140, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //向两侧各崩一根短针
            Vector2 side = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * 7.5f;
            BloomArsenal.ShedNeedle(Projectile, Projectile.Center, side, (int)(Projectile.damage * 0.4f), 1f, gravity: false);
            BloomArsenal.ShedNeedle(Projectile, Projectile.Center, -side, (int)(Projectile.damage * 0.4f), 1f, gravity: false);

            //重箭每次命中都绽一圈花瓣
            if (Heavy > 0f) {
                BloomArsenal.PetalRing(Projectile, target.Center, 6, (int)(Projectile.damage * 0.5f), 1f);
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (Heavy > 0f) {
                BloomArsenal.PetalRing(Projectile, Projectile.Center, 6, (int)(Projectile.damage * 0.5f), 1f);
            }
            return true;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            Collision.HitTiles(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.JunglePlants,
                    Main.rand.NextVector2Circular(1.4f, 1.4f), 120, default, 0.7f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            Color trailTint = Heavy > 0f
                ? lightColor.MultiplyRGB(BloomArsenal.Bloom)
                : lightColor.MultiplyRGB(BloomArsenal.Leaf);
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, trailTint * (0.28f * t), Projectile.rotation,
                    origin, Projectile.scale * (0.85f + 0.15f * t), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
