using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Destroyer
{
    /// <summary>
    /// 探针追踪激光：ai[0]追踪目标。飞行期复利续力+收口转率，
    /// 命中标定目标时向所有者标定进度入账
    /// </summary>
    internal class ProbeLockBolt : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        private ref float Age => ref Projectile.localAI[0];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
        }

        public override void SetDefaults() {
            Projectile.width = 6;
            Projectile.height = 6;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.extraUpdates = 1;
            Projectile.timeLeft = 140;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            //出膛拍：口闪+短音，各端在自己收到弹幕的首帧演
            if (Age == 0f && !VaultUtils.isServer) {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center, dir * 2f,
                    ProbeDroneProj.ThemeAmber, 0.24f)?.Configure(8, opacity: 1.2f, squishStrenght: 2.6f);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                        dir.RotatedBy(Main.rand.NextFloat(-0.5f, 0.5f)) * Main.rand.NextFloat(2f, 5f),
                        ProbeDroneProj.ThemeBlood, Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(false, Main.rand.Next(8, 14));
                }
                SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.32f, Pitch = 0.55f, MaxInstances = 3 }, Projectile.Center);
            }
            Age++;

            //追踪：转率随接近收紧，速度复利续力(飞行期有演化)
            int targetIdx = (int)Projectile.ai[0];
            if (targetIdx >= 0 && targetIdx < Main.maxNPCs) {
                NPC target = Main.npc[targetIdx];
                if (target.active && !target.friendly) {
                    float dist = Projectile.Distance(target.Center);
                    float turnRate = 0.03f + 0.05f * MathHelper.Clamp(1f - dist / 700f, 0f, 1f);
                    float desired = Projectile.AngleTo(target.Center);
                    float heading = Projectile.velocity.ToRotation().AngleTowards(desired, turnRate);
                    float speed = Math.Min(Projectile.velocity.Length() * 1.012f, 34f);
                    Projectile.velocity = heading.ToRotationVector2() * speed;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();

            //飞行沿途微量火花
            if (!VaultUtils.isServer && Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    -Projectile.velocity * 0.06f + Main.rand.NextVector2Circular(0.6f, 0.6f),
                    ProbeDroneProj.ThemeBlood, Main.rand.NextFloat(0.4f, 0.7f))
                    ?.Configure(false, Main.rand.Next(6, 12));
            }

            Lighting.AddLight(Projectile.Center, ProbeDroneProj.ThemeBlood.ToVector3() * 0.32f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //命中钩子只在所有者端跑，标定入账天然归属所有者
            Main.player[Projectile.owner].GetModPlayer<ProbeMatrixPlayer>().RegisterBoltHit(target);
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }

            //命中爆点
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2.5f, 7f),
                    Color.Lerp(ProbeDroneProj.ThemeBlood, ProbeDroneProj.ThemeAmber, Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.7f, 1.1f))?.Configure(true, Main.rand.Next(10, 18));
            }
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                ProbeDroneProj.ThemeAmber, 0.3f)?.Configure(10, opacity: 1.1f);

            //尾迹余韵：沿旧位置补几粒渐隐光，拖尾比弹体多活一拍
            for (int i = 1; i < Projectile.oldPos.Length; i += 2) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 pos = Projectile.oldPos[i] + Projectile.Size / 2f;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                PRTLoader.NewParticle<PRT_Light>(pos, Vector2.Zero,
                    ProbeDroneProj.ThemeBlood * fade, 0.16f)?.Configure(8 + i, opacity: 0.7f * fade);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float rot = Projectile.rotation;

            //旧位置残影，速度拉伸淡出
            for (int i = 1; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) {
                    break;
                }
                Vector2 ghostPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                float fade = 1f - i / (float)Projectile.oldPos.Length;
                //实体批内A=0加色技法
                Main.EntitySpriteDraw(glow, ghostPos, null, new Color(180, 30, 20, 0) * (0.34f * fade),
                    rot, glow.Size() / 2f, new Vector2(0.42f * fade, 0.1f), SpriteEffects.None, 0);
            }

            //三层弹体：深红宽鞘→琥珀中层→白热细芯，全部沿速度拉伸
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(200, 30, 18, 0) * 0.85f,
                rot, glow.Size() / 2f, new Vector2(0.62f, 0.17f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, drawPos, null, new Color(255, 120, 50, 0) * 0.9f,
                rot, glow.Size() / 2f, new Vector2(0.44f, 0.11f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(pixel, drawPos, null, new Color(255, 230, 195, 0),
                rot, pixel.Size() / 2f, new Vector2(20f / pixel.Width, 2f / pixel.Height), SpriteEffects.None, 0);
            return false;
        }
    }
}
