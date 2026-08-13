using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>焚地：坠地驻燃的火舌区域，区域拒止</summary>
    internal class CultistCinderPatch : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int BurnTime = 260;
        private bool Landed => Projectile.ai[1] >= 1f;

        public override void SetDefaults() {
            Projectile.width = 52;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 90 + BurnTime;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;

            if (Projectile.localAI[0] == 1) {
                //缓存伤害，坠落期归零
                Projectile.localAI[1] = Projectile.damage;
            }

            if (!Landed) {
                //坠落寻地
                Projectile.velocity.X *= 0.98f;
                Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.42f, 13f);
                Projectile.damage = 0;
                return;
            }

            Projectile.damage = (int)Projectile.localAI[1];
            Projectile.velocity = Vector2.Zero;

            //驻燃期
            float life = Projectile.timeLeft / (float)BurnTime;
            if (!VaultUtils.isServer) {
                if (Main.rand.NextBool(3)) {
                    PRTLoader.NewParticle<PRT_CultistEmber>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-22f, 22f), 4f),
                        new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(1.2f, 3f)),
                        CultistPalette.FireBright, Main.rand.NextFloat(0.6f, 1.1f) * life)?.Configure(Main.rand.Next(18, 30));
                }
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.FireMain.ToVector3() * (0.8f * life));
        }

        public override bool OnTileCollide(Vector2 oldVelocity) {
            if (!Landed) {
                Projectile.ai[1] = 1f;
                Projectile.velocity = Vector2.Zero;
                Projectile.timeLeft = BurnTime;
                Projectile.netUpdate = true;
                if (!VaultUtils.isServer) {
                    CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Fire, 0.8f);
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            if (!Landed) {
                //坠落中的火种
                SpriteBatch sbFall = Main.spriteBatch;
                CultistRenderHelper.BeginAdditive(sbFall);
                Texture2D g = CWRAsset.SoftGlow.Value;
                sbFall.Draw(g, Projectile.Center - Main.screenPosition, null,
                    CultistPalette.FireMain * 0.8f, 0f, g.Size() / 2f, 0.42f, SpriteEffects.None, 0f);
                CultistRenderHelper.EndAdditive(sbFall);
                return false;
            }

            float life = Projectile.timeLeft / (float)BurnTime;
            SpriteBatch sb = Main.spriteBatch;
            Vector2 basePos = Projectile.Center - Main.screenPosition + new Vector2(0f, 8f);
            Texture2D flame = CultistRenderHelper.TearFlame01?.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            if (flame == null) {
                return false;
            }

            CultistRenderHelper.BeginAdditive(sb);

            //底部余光
            sb.Draw(glow, basePos, null, CultistPalette.FireDeep * (0.7f * life),
                0f, glow.Size() / 2f, new Vector2(1.1f, 0.4f), SpriteEffects.None, 0f);

            //三条火舌，错相闪变（噪声撕裂端）
            for (int i = 0; i < 3; i++) {
                float phase = Main.GlobalTimeWrappedHourly * 9f + i * 2.1f + Projectile.whoAmI;
                float sway = (float)Math.Sin(phase) * 0.16f;
                float h = (0.55f + 0.2f * (float)Math.Sin(phase * 1.7f)) * life;
                Vector2 pos = basePos + new Vector2((i - 1) * 15f, 0f);
                SpriteEffects fx = (i + Projectile.whoAmI) % 2 == 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                sb.Draw(flame, pos, null, CultistPalette.FireMain * (0.85f * life),
                    -MathHelper.PiOver2 + sway, new Vector2(0f, flame.Height / 2f),
                    new Vector2(h, 0.34f), fx, 0f);
            }

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
