using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalQueenBee.Projectiles
{
    /// <summary>
    /// 女王毒刺：ai[0]=0直射(双倍更新) 1=幕帘(重力抛坠) 2=炮台射(直射慢速)<br/>
    /// 伤害传入基准11(原版蜂后毒刺对齐)
    /// </summary>
    internal class BrutalBeeStinger : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>基准伤害，与原版蜂后毒刺持平</summary>
        internal const int BaseDamage = 11;

        private int Mode => (int)Projectile.ai[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
            Projectile.aiStyle = -1;
        }

        public override void AI() {
            //幕帘模式：初速衰减+重力，坠成刺雨
            if (Mode == 1) {
                Projectile.velocity.X *= 0.985f;
                Projectile.velocity.Y += 0.16f;
                if (Projectile.velocity.Y > 11f) {
                    Projectile.velocity.Y = 11f;
                }
            }
            else if (Projectile.extraUpdates < 1 && Mode == 0) {
                Projectile.extraUpdates = 1;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, QueenBeeMotion.HoneyGold.ToVector3() * 0.16f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            //中毒，修罗模式更久
            bool asura = CWRWorld.Asura;
            target.AddBuff(BuffID.Poisoned, asura ? 360 : 210);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(Main.rand.NextFloat(-0.9f, 0.9f))
                        * Main.rand.NextFloat(1.5f, 4f),
                    QueenBeeMotion.AmberDeep, Main.rand.NextFloat(0.6f, 1f))?.Configure(true, 14);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(ProjectileID.QueenBeeStinger);
            Texture2D tex = TextureAssets.Projectile[ProjectileID.QueenBeeStinger].Value;
            Vector2 origin = tex.Size() * 0.5f;

            //琥珀残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, new Color(255, 180, 70, 0) * (0.32f * fade),
                    Projectile.oldRot[i], origin, Projectile.scale * (0.75f + 0.25f * fade), SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            //刺尖热点
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null,
                new Color(255, 210, 110, 0) * 0.4f, Projectile.rotation, origin,
                Projectile.scale * 1.12f, SpriteEffects.None, 0);
            return false;
        }
    }
}
