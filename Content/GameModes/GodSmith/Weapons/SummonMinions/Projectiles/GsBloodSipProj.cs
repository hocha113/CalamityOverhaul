using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles
{
    /// <summary>
    /// 血髓珠：吸血蛙吸髓协同的回血载体。从命中点飞向 owner 玩家，
    /// 真弹幕全端可见；回血只在 owner 本地结算（客户端写自己血量合法且自动同步），
    /// 远端只看到珠子被吸收
    /// </summary>
    internal class GsBloodSipProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithSummonMinionsA";

        private static readonly Color BloodBright = new(255, 110, 96);
        private static readonly Color BloodMain = new(196, 40, 44);
        private static readonly Color BloodDeep = new(110, 16, 26);

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.9151f % MathHelper.TwoPi;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //先散开一小段再咬向玩家，加速渐强
            if (Life > 8f) {
                Vector2 want = (owner.Center - Projectile.Center)
                    .SafeNormalize(Vector2.UnitY) * MathHelper.Clamp(4f + Life * 0.22f, 4f, 13f);
                float turn = MathHelper.Clamp((Life - 8f) / 20f, 0.06f, 0.2f);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //到手：owner 本地回血，各端本地消珠
            if (Projectile.Center.Distance(owner.Center) <= 26f) {
                if (Projectile.owner == Main.myPlayer) {
                    owner.Heal(1);
                }
                Projectile.Kill();
                return;
            }

            if (!VaultUtils.isServer && Life % 4 == 0) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(
                    Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f, BloodMain,
                    Main.rand.NextFloat(0.25f, 0.4f))?.Configure(Main.rand.Next(8, 14), 0.1f);
                Lighting.AddLight(Projectile.Center, BloodMain.ToVector3() * 0.12f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item3 with { Volume = 0.3f, Pitch = 0.6f },
                Projectile.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(0.8f, 2f),
                    Main.rand.NextBool() ? BloodMain : BloodBright,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(Main.rand.Next(10, 18));
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D soft = CWRAsset.Extra_98?.Value;
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (soft == null || glow == null) {
                return false;
            }
            float fadeIn = MathHelper.Clamp(Life / 5f, 0f, 1f);
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0.05f, 0.5f);
            float wob = 0.1f * (float)Math.Sin(Life * 0.5f + Seed);
            //液珠三层：深缘/主体/湿反光
            Main.EntitySpriteDraw(soft, pos, null, BloodDeep * (0.85f * fadeIn),
                Projectile.rotation, soft.Size() / 2f,
                new Vector2(0.13f + stretch, 0.11f - wob * 0.03f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(soft, pos, null, BloodMain * (0.8f * fadeIn),
                Projectile.rotation, soft.Size() / 2f,
                new Vector2(0.09f + stretch * 0.7f, 0.08f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, (BloodBright with { A = 0 }) * (0.5f * fadeIn),
                0f, glow.Size() / 2f, 0.16f + wob * 0.02f, SpriteEffects.None, 0);
            return false;
        }
    }
}
