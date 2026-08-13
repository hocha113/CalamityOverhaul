using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Projectiles
{
    /// <summary>血凝块：重力抛物的湿黏活体弹，落地爆浆。速度拉伸+表面抖动</summary>
    internal class WofBloodClot : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 6;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            //重力抛物，湿重下坠
            Projectile.velocity.Y += 0.24f;
            if (Projectile.velocity.Y > 15f) {
                Projectile.velocity.Y = 15f;
            }
            Projectile.velocity.X *= 0.998f;
            Projectile.rotation = Projectile.velocity.ToRotation();

            Lighting.AddLight(Projectile.Center, WofMotionFX.BloodMid.ToVector3() * 0.35f);

            if (VaultUtils.isServer) {
                return;
            }
            //飞行途中甩落血珠(飞行期不许死寂)
            if (Main.rand.NextBool(4)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.8f, 0.8f),
                    WofMotionFX.BloodMid, Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 24), 0.3f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCHit13 with { Volume = 0.6f, Pitch = -0.2f, MaxInstances = 4 }, Projectile.Center);
            WofMotionFX.SpawnBloodBurst(Projectile.Center, 0.55f, -Projectile.velocity.SafeNormalize(Vector2.UnitY));
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D drop = CWRAsset.Extra_98.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Vector2 screenPos = Projectile.Center - Main.screenPosition;

            //残影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                Main.EntitySpriteDraw(drop, ghostPos, null, WofMotionFX.BloodDark * (0.3f * fade),
                    Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f,
                    new Vector2(0.2f, 0.3f) * fade, SpriteEffects.None, 0);
            }

            //速度拉伸的湿黏本体：快成梭、慢成珠，表面高频抖动
            float speed = Projectile.velocity.Length();
            float stretch = MathHelper.Clamp(speed * 0.05f, 0f, 0.9f);
            float wobble = 1f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 42f + Projectile.whoAmI * 2.4f);
            Vector2 scale = new Vector2(0.42f * (1f + stretch * 1.4f), 0.4f * (1f - stretch * 0.3f) * wobble);

            //底衬幽光(under-layer)：只作湿气衬底，直径压到本体级别防光球感
            Main.EntitySpriteDraw(glow, screenPos, null, WofMotionFX.BloodDark with { A = 0 } * 0.32f, 0f,
                glow.Size() / 2f, 0.45f, SpriteEffects.None, 0);
            //暗肉核
            Main.EntitySpriteDraw(drop, screenPos, null, WofMotionFX.BloodDark, Projectile.rotation + MathHelper.PiOver2,
                drop.Size() / 2f, scale, SpriteEffects.None, 0);
            //湿亮偏移高光
            Main.EntitySpriteDraw(drop, screenPos - new Vector2(2f, 3f), null, WofMotionFX.BloodHot * 0.65f,
                Projectile.rotation + MathHelper.PiOver2, drop.Size() / 2f, scale * 0.62f, SpriteEffects.None, 0);
            return false;
        }
    }
}
