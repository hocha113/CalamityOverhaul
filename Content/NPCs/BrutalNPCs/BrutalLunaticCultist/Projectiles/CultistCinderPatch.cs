using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
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
                //烟缕缓升（火床上方的热对流，尾声期烟多火少）
                if (Main.rand.NextBool(life > 0.4f ? 9 : 5)) {
                    PRTLoader.NewParticle<PRT_CultistSmoke>(
                        Projectile.Center + new Vector2(Main.rand.NextFloat(-20f, 20f), -4f),
                        new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.5f, 1.1f)),
                        new Color(170, 95, 55), Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(36, 58));
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
                    //着地帧：火花横溅+第一波烟（火种炸开成火床）
                    CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Fire, 0.8f);
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.4f, Pitch = 0.15f, MaxInstances = 5 }, Projectile.Center);
                    for (int i = 0; i < 8; i++) {
                        Vector2 vel = new(Main.rand.NextFloat(-4.5f, 4.5f), -Main.rand.NextFloat(1f, 4f));
                        PRTLoader.NewParticle<PRT_CultistEmber>(Projectile.Center + new Vector2(Main.rand.NextFloat(-18f, 18f), 2f),
                            vel, CultistPalette.FireBright, Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(18, 30));
                    }
                }
            }
            return false;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.OnFire, 150);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D fire = CWRAsset.Fire.Value;
            int fw = fire.Width / 4;
            int fh = fire.Height / 4;

            if (!Landed) {
                //坠落中的火种：本体=原版火球467真实纹理（速度拉伸+底晕叠加）
                float ft = Projectile.localAI[0];
                Main.instance.LoadProjectile(ProjectileID.CultistBossFireBall);
                Texture2D fireball = TextureAssets.Projectile[ProjectileID.CultistBossFireBall].Value;
                int bfh = fireball.Height / 4;
                Rectangle seedSrc = new(0, (int)(ft / 4f + Projectile.whoAmI) % 4 * bfh, fireball.Width, bfh);
                float fallStretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.07f, 0.8f, 1.5f);
                CultistRenderHelper.BeginAdditive(sb);
                sb.Draw(glow, Projectile.Center - Main.screenPosition, null,
                    CultistPalette.FireDeep * 0.45f, 0f, glow.Size() / 2f, 0.5f, SpriteEffects.None, 0f);
                CultistRenderHelper.EndAdditive(sb);
                sb.Draw(fireball, Projectile.Center - Main.screenPosition, seedSrc,
                    new Color(255, 255, 255, 255), ft * 0.24f,
                    new Vector2(fireball.Width / 2f, bfh / 2f), new Vector2(0.8f, 0.8f * fallStretch), SpriteEffects.None, 0f);
                return false;
            }

            float life = Projectile.timeLeft / (float)BurnTime;
            Vector2 basePos = Projectile.Center - Main.screenPosition + new Vector2(0f, 8f);
            Texture2D flame = CultistRenderHelper.TearFlame01?.Value;
            Texture2D scorch = CWRAsset.TearSpread01.Value;
            if (flame == null) {
                return false;
            }

            //落地焦痕：真alpha贴图走实体批直接染暗色（余韵的地面证据，随火床同淡出）
            float scorchFade = MathHelper.Clamp(life * 2.5f, 0f, 1f) * MathHelper.Clamp((1f - life) * 4f + 0.35f, 0.35f, 1f);
            sb.Draw(scorch, basePos + new Vector2(0f, 4f), null, new Color(52, 22, 14) * (0.7f * scorchFade),
                0f, scorch.Size() / 2f, new Vector2(0.62f, 0.2f), SpriteEffects.None, 0f);

            CultistRenderHelper.BeginAdditive(sb);

            //底部余光
            sb.Draw(glow, basePos, null, CultistPalette.FireDeep * (0.7f * life),
                0f, glow.Size() / 2f, new Vector2(1.1f, 0.4f), SpriteEffects.None, 0f);

            //烬床：沿地一排hash频闪烬点（烧透的炭在呼吸）
            for (int i = 0; i < 7; i++) {
                float ex = (i - 3) * 8.5f + (float)Math.Sin(Projectile.whoAmI * 2.7f + i * 13.7f) * 3f;
                float breath = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * (5f + i * 0.83f) + i * 2.9f + Projectile.whoAmI);
                float emberLum = (0.28f + 0.5f * breath * breath) * life;
                Color emberCol = Color.Lerp(CultistPalette.FireDeep, CultistPalette.FireBright, breath * 0.7f);
                sb.Draw(glow, basePos + new Vector2(ex, 5f), null, emberCol * emberLum,
                    0f, glow.Size() / 2f, 0.08f + 0.05f * breath, SpriteEffects.None, 0f);
            }

            //焰帧根层：两团滚卷的火根（帧动画时间签名）
            for (int i = 0; i < 2; i++) {
                int frameIdx = (int)(Main.GlobalTimeWrappedHourly * 13f + i * 6 + Projectile.whoAmI * 3) % 16;
                Rectangle src = new(frameIdx % 4 * fw, frameIdx / 4 * fh, fw, fh);
                Vector2 pos = basePos + new Vector2((i - 0.5f) * 17f, -2f);
                float s = (0.3f + 0.07f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 7f + i * 2.4f)) * life;
                sb.Draw(fire, pos, src, CultistPalette.FireMain * (0.8f * life),
                    (i - 0.5f) * 0.12f, new Vector2(fw / 2f, fh), s, SpriteEffects.None, 0f);
            }

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
