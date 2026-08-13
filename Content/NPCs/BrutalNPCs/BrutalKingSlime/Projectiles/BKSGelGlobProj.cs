using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>凝胶炮弹，重力弧线；ai[0]=1落地生成滞留池；服务端生成</summary>
    internal class BKSGelGlobProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private bool SpawnsPool => Projectile.ai[0] == 1f;

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //重力弧线，凝胶偏重
            Projectile.velocity.Y += 0.34f;
            if (Projectile.velocity.Y > 17f) {
                Projectile.velocity.Y = 17f;
            }
            Projectile.velocity.X *= 0.998f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //飞行滴落
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_BKSGelBead>(
                    Projectile.Center + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, Main.rand.NextFloat()) * 0.7f,
                    Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(14, 24));
            }

            Lighting.AddLight(Projectile.Center, KingSlimeGelFX.GelMid.ToVector3() * 0.28f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 120);
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            Projectile.Kill();
            return false;
        }

        public override void OnKill(int timeLeft) {
            //落点飞溅
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.2f, Volume = 0.5f, MaxInstances = 5 }, Projectile.Center);
                KingSlimeGelFX.GelSplatter(Projectile.Center, -Vector2.UnitY, 6, 5f, 0.8f);
            }
            //滞留池，服务端
            if (SpawnsPool && !VaultUtils.isClient) {
                Vector2 ground = KingSlimeGelFX.FindGroundBelow(Projectile.Center, 8);
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), ground, Vector2.Zero,
                    ModContent.ProjectileType<BKSGelPoolProj>(), (int)(Projectile.damage * 0.6f), 0f, Main.myPlayer,
                    130f, 200f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = tex.Size() * 0.5f;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.3f) * 0.88f;

            //速度拉伸的凝胶团，双层厚度
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0f, 0.8f);
            Vector2 scale = new Vector2(0.9f * (1f - stretch * 0.3f), 1.05f * (1f + stretch * 1.2f));

            //拖影
            for (int i = 1; i <= 3; i++) {
                Vector2 ghostPos = pos - Projectile.velocity * (i * 0.6f);
                DrawBlob(tex, ghostPos, gel * (0.3f - i * 0.08f), Projectile.rotation, origin, scale * (1f - i * 0.1f));
            }

            DrawBlob(tex, pos, gel, Projectile.rotation, origin, scale);
            DrawBlob(tex, pos, gel * 0.75f, Projectile.rotation, origin, scale * new Vector2(0.55f, 0.9f));
            //顶部一点高光
            DrawBlob(tex, pos - new Vector2(0, 4f), KingSlimeGelFX.GelFoam with { A = 0 } * 0.35f,
                Projectile.rotation, origin, scale * 0.32f);
            return false;
        }

        private static void DrawBlob(Texture2D tex, Vector2 pos, Color color, float rot, Vector2 origin, Vector2 scale) {
            Main.EntitySpriteDraw(tex, pos, null, color, rot, origin, scale, SpriteEffects.None, 0);
        }
    }
}
