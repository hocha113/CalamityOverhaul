using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalLunaticCultist.Projectiles
{
    /// <summary>
    /// 霜牢冰枪：凝晶前摇（锁定前跟瞄，末12帧锁死）→急速刺出；
    /// ai[0]=前摇帧 ai[1]=刺出速度；出生时 velocity 为归一化瞄准方向
    /// </summary>
    internal class CultistIceLance : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private const int AimLockLead = 12;
        private int TelegraphTime => Math.Max((int)Projectile.ai[0], 10);
        private float LaunchSpeed => Projectile.ai[1] > 0f ? Projectile.ai[1] : 19f;

        private bool launched;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 500;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];

            if (t == 1) {
                //缓存伤害，前摇期归零（公平阀）
                Projectile.localAI[1] = Projectile.damage;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item30 with { Volume = 0.5f, Pitch = 0.35f, MaxInstances = 6 }, Projectile.Center);
                }
            }

            if (!launched && t <= TelegraphTime) {
                //前摇期无伤害
                Projectile.damage = 0;
                Projectile.position -= Projectile.velocity;

                //锁定前服务端跟瞄
                if (!VaultUtils.isClient && t < TelegraphTime - AimLockLead) {
                    int idx = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
                    Player target = Main.player[idx];
                    if (target.Alives()) {
                        Vector2 aim = (target.Center + target.velocity * 10f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        Projectile.velocity = aim * 0.0001f;
                        if ((int)t % 10 == 0) {
                            Projectile.netUpdate = true;
                        }
                    }
                }

                Projectile.rotation = Projectile.velocity.ToRotation();

                //凝晶闪点
                if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                    PRTLoader.NewParticle<PRT_CultistFrost>(
                        Projectile.Center + Main.rand.NextVector2Circular(26f, 26f),
                        Main.rand.NextVector2Circular(0.5f, 0.5f),
                        CultistPalette.IceBright, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(14, 24));
                }
                return;
            }

            //刺出帧：恢复伤害
            if (!launched) {
                launched = true;
                Projectile.damage = (int)Projectile.localAI[1];
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
                Projectile.velocity = dir * LaunchSpeed;
                Projectile.timeLeft = 240;
                if (!VaultUtils.isClient) {
                    Projectile.netUpdate = true;
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.75f, Pitch = 0.2f, MaxInstances = 6 }, Projectile.Center);
                    CultistRenderHelper.CastBurst(Projectile.Center, dir, CultistElement.Ice, 1f);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            //航迹霜雾
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_CultistFrost>(Projectile.Center,
                    -Projectile.velocity * 0.05f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    CultistPalette.IceMain, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(18, 30));
            }
            Lighting.AddLight(Projectile.Center, CultistPalette.IceMain.ToVector3() * 0.5f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Chilled, 120);
        }

        public override void OnKill(int timeLeft) {
            if (!VaultUtils.isServer) {
                CultistRenderHelper.ElementImpact(Projectile.Center, CultistElement.Ice, 1f);
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f, MaxInstances = 6 }, Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch sb = Main.spriteBatch;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D star = CWRAsset.StarGlow01.Value;
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float t = Projectile.localAI[0];
            float assemble = MathHelper.Clamp(t / TelegraphTime, 0f, 1f);

            CultistRenderHelper.BeginAdditive(sb);

            if (!launched) {
                //凝晶体：菱形渐显+锁定前摇末端收缩闪
                float lockFlash = t > TelegraphTime - AimLockLead ? (t - (TelegraphTime - AimLockLead)) / AimLockLead : 0f;
                Color body = Color.Lerp(CultistPalette.IceMain, CultistPalette.IceBright, lockFlash);
                sb.Draw(star, drawPos, null, body * (0.35f + assemble * 0.6f),
                    Projectile.rotation, star.Size() / 2f,
                    new Vector2(1.5f * assemble + lockFlash * 0.4f, 0.24f), SpriteEffects.None, 0f);
                sb.Draw(glow, drawPos, null, CultistPalette.IceDeep * (0.4f * assemble),
                    0f, glow.Size() / 2f, 0.5f * assemble, SpriteEffects.None, 0f);

                //瞄准预示线（薄）
                Texture2D line = CWRAsset.LightShot.Value;
                sb.Draw(line, drawPos, null, CultistPalette.IceBright * (0.22f * assemble),
                    Projectile.rotation, new Vector2(0f, line.Height / 2f),
                    new Vector2(3.4f * assemble, 0.1f), SpriteEffects.None, 0f);
            }
            else {
                //刺出体：速度拉伸晶枪，亮芯+冷缘
                float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.1f, 1.2f, 2.4f);
                sb.Draw(star, drawPos, null, CultistPalette.IceDeep * 0.8f,
                    Projectile.rotation, star.Size() / 2f, new Vector2(2f * stretch, 0.4f), SpriteEffects.None, 0f);
                sb.Draw(star, drawPos, null, CultistPalette.IceBright * 0.95f,
                    Projectile.rotation, star.Size() / 2f, new Vector2(1.5f * stretch, 0.2f), SpriteEffects.None, 0f);
            }

            CultistRenderHelper.EndAdditive(sb);
            return false;
        }
    }
}
