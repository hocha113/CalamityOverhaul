using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalBrainOfCthulhu.Projectiles
{
    /// <summary>
    /// 血珠弹：ai[0]=0 直线搏动弹 / 1 重力血滴（血雨）
    /// 血材质：速度拉伸液滴本体+雾尾+落地溅斑，非能量线
    /// </summary>
    internal class BrainBloodShard : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private bool IsGravityGlob => Projectile.ai[0] == 1f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 7;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 600;
        }

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.alpha = 255;
        }

        public override void AI() {
            //首帧湿滑出膛声
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with {
                        Volume = 0.3f,
                        Pitch = -0.45f + Main.rand.NextFloat(-0.1f, 0.1f),
                        MaxInstances = 8,
                        SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
                    }, Projectile.Center);
                }
            }

            if (Projectile.alpha > 0) {
                Projectile.alpha = Math.Max(Projectile.alpha - 30, 0);
            }

            if (IsGravityGlob) {
                Projectile.velocity.Y += 0.24f;
                if (Projectile.velocity.Y > 17f) {
                    Projectile.velocity.Y = 17f;
                }
                //下坠中轻微左右摇曳（identity 哈希，各端一致）
                float wobblePhase = Projectile.identity * 1.7f + Projectile.timeLeft * 0.06f;
                Projectile.velocity.X += (float)Math.Sin(wobblePhase) * 0.02f;
            }
            else {
                //直线弹在心跳节拍上轻微搏动加速
                float pulse = 1f + BrainHeartbeat.Pulse * 0.028f;
                Projectile.velocity *= pulse;
                if (Projectile.velocity.Length() > 15.5f) {
                    Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitY) * 15.5f;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, BrainMotion.BloodDark.ToVector3() * 0.34f);

            //飞行血雾尾
            if (!VaultUtils.isServer && Main.rand.NextBool(5) && BrainMotion.OnScreen(Projectile.Center)) {
                var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(Projectile.Center,
                    -Projectile.velocity * 0.08f, BrainMotion.BloodDark * 0.55f, Main.rand.NextFloat(0.3f, 0.5f));
                mist?.Configure(Main.rand.Next(16, 26));
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer || !BrainMotion.OnScreen(Projectile.Center)) {
                return;
            }
            //溅斑：液滴迸散
            for (int i = 0; i < 4; i++) {
                Vector2 vel = -Projectile.velocity.SafeNormalize(Vector2.Zero)
                    .RotatedBy(Main.rand.NextFloat(-1.1f, 1.1f)) * Main.rand.NextFloat(1.5f, 5f);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(Projectile.Center, vel,
                    Color.Lerp(BrainMotion.BloodBright, BrainMotion.BloodDark, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(18, 32), 0.36f);
            }
            var mist = PRTLoader.NewParticle<PRT_BrainBloodMist>(Projectile.Center, Vector2.Zero,
                BrainMotion.BloodDark * 0.6f, Main.rand.NextFloat(0.5f, 0.8f));
            mist?.Configure(Main.rand.Next(20, 30));
            SoundEngine.PlaySound(SoundID.NPCHit1 with {
                Volume = 0.22f,
                Pitch = -0.7f,
                MaxInstances = 6,
                SoundLimitBehavior = SoundLimitBehavior.ReplaceOldest
            }, Projectile.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = CWRAsset.Extra_98.Value;
            Vector2 origin = tex.Size() * 0.5f;
            float fade = 1f - Projectile.alpha / 255f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            //速度拉伸：快成线慢成珠
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.05f, 0.1f, 1f);
            Vector2 bodyScale = new Vector2(0.4f * (1f - stretch * 0.3f), 0.62f * (1f + stretch * 1.5f));

            //拖尾液滴影
            for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    continue;
                }
                float t = 1f - i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                Main.EntitySpriteDraw(tex, pos, null, BrainMotion.BloodDark * (0.4f * t * fade),
                    Projectile.rotation, origin, bodyScale * (0.55f + 0.35f * t), SpriteEffects.None, 0);
            }

            //本体双层：暗边+亮芯
            Main.EntitySpriteDraw(tex, drawPos, null, BrainMotion.BloodDark * (0.95f * fade),
                Projectile.rotation, origin, bodyScale * 1.12f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(tex, drawPos, null, BrainMotion.BloodBright * (0.9f * fade),
                Projectile.rotation, origin, bodyScale * new Vector2(0.66f, 0.9f), SpriteEffects.None, 0);

            return false;
        }
    }
}
