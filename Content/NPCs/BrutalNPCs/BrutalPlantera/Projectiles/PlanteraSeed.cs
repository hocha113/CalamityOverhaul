using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalPlantera.Projectiles
{
    /// <summary>加特林种子：高速直射，后段坠落；速度拉伸拖光+残影</summary>
    internal class PlanteraSeed : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_275";

        internal static int GetDamage(NPC boss) => Math.Max((int)(boss.defDamage * 0.3f), 12);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
        }

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.aiStyle = -1;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            //40帧后进入坠落段
            if (Projectile.timeLeft < 200) {
                Projectile.velocity.Y += 0.12f;
                if (Projectile.velocity.Y > 18f) {
                    Projectile.velocity.Y = 18f;
                }
            }

            Lighting.AddLight(Projectile.Center, PlanteraRenderHelper.SporeGreen.ToVector3() * 0.22f);

            //飞行草屑，稀疏
            if (!VaultUtils.isServer && Main.rand.NextBool(6)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Plantera_Green, 0f, 0f, 130, default, 0.9f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.08f;
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //撞击残留：汁液飞溅+短命余光(比弹体多活一拍)
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height,
                    DustID.Plantera_Green, 0f, 0f, 100, default, Main.rand.NextFloat(1f, 1.5f));
                dust.velocity = -Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(Main.rand.NextFloat(-1f, 1f))
                    * Main.rand.NextFloat(1f, 3.5f);
            }
            InnoVault.PRT.PRTLoader.NewParticle<PRT_PlanteraSporeMote>(Projectile.Center,
                -Projectile.velocity * 0.05f, PlanteraRenderHelper.SporeGreen, 1f)?.SetLife(18);
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadProjectile(Type);
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float speed = Projectile.velocity.Length();

            //速度拉伸拖光(暗外层+亮芯两段)
            float stretch = MathHelper.Clamp(speed / 26f, 0.3f, 1.3f);
            Color tracer = PlanteraRenderHelper.SporeGreen with { A = 0 };
            Main.EntitySpriteDraw(glow, drawPos, null, tracer * 0.55f,
                Projectile.rotation, glow.Size() / 2f, new Vector2(0.14f, 0.62f * stretch), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, Color.White with { A = 0 } * 0.3f,
                Projectile.rotation, glow.Size() / 2f, new Vector2(0.07f, 0.4f * stretch), SpriteEffects.None, 0);

            //残影
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float fade = (1f - i / (float)Projectile.oldPos.Length) * 0.35f;
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, ghostPos, null, tracer * fade,
                    Projectile.rotation, tex.Size() / 2f, Projectile.scale * 0.92f, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, null, lightColor,
                Projectile.rotation, tex.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
