using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles
{
    /// <summary>
    /// 幽灵法杖灾变「魂渊潮汐」：锚定光标。蓄势 45t 魂涡收拢入渊；
    /// 爆发 156t 三涌潮汐环外扩扫荡（环带判定与可见潮头同源），
    /// 潮头渡魂归疗施法者（1HP/跳，6HP/s 封顶）并放出扑猎迷魂；
    /// 余韵 90t 魂雾弥散加三缕残魂离渊
    /// </summary>
    internal class GsSpectreStaffTideDirector : GsCataclysmDirectorProj
    {
        public override int OmenTicks => 45;
        public override int MainTicks => 156;
        public override int AftermathTicks => 90;

        protected override int HitTickRate => 18;
        protected override float TickDamageMul => 0.55f;

        /// <summary>单涌周期</summary>
        private const int WavePeriod = 52;
        /// <summary>潮汐最大半径</summary>
        private const float TideRadius = 380f;
        /// <summary>潮头环带半宽</summary>
        private const float BandHalf = 30f;

        /// <summary>本秒渡魂归疗量（owner 本地预算）</summary>
        private int healBudget;
        private uint healBudgetTick;

        /// <summary>当前潮头半径；无在场潮涌返回 -1</summary>
        private float WaveRadius {
            get {
                if (Phase != 1) {
                    return -1f;
                }
                int waveT = (Elapsed - OmenTicks) % WavePeriod;
                if (waveT > WavePeriod - 8) {
                    return -1f;
                }
                return 24f + (TideRadius - 24f) * VaultUtils.EaseOutQuad(waveT / (float)(WavePeriod - 8));
            }
        }

        protected override void OmenUpdate(int t) {
            if (t == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.8f, Pitch = -0.5f }, Projectile.Center);
            }
            if (VaultUtils.isServer) {
                return;
            }
            //魂涡收拢：外圈魂缕螺旋入渊（无伤 telegraph）
            if (t % 2 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                float dist = MathHelper.Lerp(TideRadius * 0.7f, 40f, t / (float)OmenTicks) + Main.rand.NextFloat(-20f, 20f);
                Vector2 pos = Projectile.Center + ang.ToRotationVector2() * dist;
                Vector2 vel = (Projectile.Center - pos).SafeNormalize(Vector2.UnitY) * Main.rand.NextFloat(2.5f, 5f)
                    + ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 1.6f;
                PRTLoader.NewParticle<PRT_SoulLight>(pos, vel, GsSpectreStaff.SpectreCyan, Main.rand.NextFloat(0.3f, 0.5f));
            }
            Lighting.AddLight(Projectile.Center, GsSpectreStaff.SpectreCyan.ToVector3() * 0.5f * (t / (float)OmenTicks));
        }

        protected override void MainUpdate(int t) {
            int waveT = t % WavePeriod;
            //涌起帧：潮鸣与破渊闪
            if (waveT == 0 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.85f, Pitch = -0.2f + 0.15f * (t / WavePeriod) }, Projectile.Center);
                for (int i = 0; i < 10; i++) {
                    float ang = MathHelper.TwoPi * i / 10f;
                    PRTLoader.NewParticle<PRT_SoulFire>(Projectile.Center + ang.ToRotationVector2() * 26f,
                        ang.ToRotationVector2() * Main.rand.NextFloat(2f, 4f),
                        GsSpectreStaff.SpectreCyan, Main.rand.NextFloat(0.4f, 0.65f));
                }
            }
            //潮头魂沫：沿当前潮头环撒魂缕
            float radius = WaveRadius;
            if (!VaultUtils.isServer && radius > 0f && Main.GameUpdateCount % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    PRTLoader.NewParticle<PRT_SoulLight>(Projectile.Center + ang.ToRotationVector2() * radius,
                        ang.ToRotationVector2() * Main.rand.NextFloat(0.8f, 1.8f),
                        GsSpectreStaff.SpectreCyan, Main.rand.NextFloat(0.26f, 0.42f));
                }
            }
            Lighting.AddLight(Projectile.Center, GsSpectreStaff.SpectreCyan.ToVector3() * 0.6f);

            //潮中扑猎：每涌中段放出两缕迷魂咬住界内之敌（owner 端生成）
            if (OwnerSide && waveT == 20) {
                int spawned = 0;
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (!npc.CanBeChasedBy() || npc.Distance(Projectile.Center) > TideRadius) {
                        continue;
                    }
                    Vector2 vel = (npc.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY) * 6f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                        GsSpectreStaff.SoulType, ScaledDamage(0.6f), 2f, Projectile.owner);
                    if (++spawned >= 2) {
                        break;
                    }
                }
            }
        }

        protected override void AftermathUpdate(int t) {
            if (t == 0) {
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item104 with { Volume = 0.6f, Pitch = -0.35f }, Projectile.Center);
                }
                //三缕残魂离渊（owner 端生成，收尾馈赠）
                if (OwnerSide) {
                    for (int i = 0; i < 3; i++) {
                        Vector2 vel = (-Vector2.UnitY).RotatedBy((i - 1) * 0.7f) * 4.5f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                            GsSpectreStaff.SoulType, ScaledDamage(0.4f), 1.5f, Projectile.owner);
                    }
                }
            }
            if (VaultUtils.isServer) {
                return;
            }
            //魂雾弥散：低频缓升的余魂
            if (t % 5 == 0) {
                PRTLoader.NewParticle<PRT_SoulLight>(
                    Projectile.Center + Main.rand.NextVector2Circular(120f, 70f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.6f, 1.4f)),
                    GsSpectreStaff.SpectreDeep, Main.rand.NextFloat(0.24f, 0.4f));
            }
            Lighting.AddLight(Projectile.Center, GsSpectreStaff.SpectreCyan.ToVector3() * 0.4f * (1f - t / (float)AftermathTicks));
        }

        /// <summary>潮头环带判定：与可见潮头同半径（爆发段外无伤）</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float radius = WaveRadius;
            if (radius <= 0f) {
                return false;
            }
            float dist = (targetHitbox.Center.ToVector2() - Projectile.Center).Length();
            return Math.Abs(dist - radius) <= BandHalf + Math.Min(targetHitbox.Width, targetHitbox.Height) * 0.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            //渡魂归疗：潮头命中回 1HP，6HP/s 封顶（命中只在 owner 端结算，生命客户端权威）
            if (Projectile.owner == Main.myPlayer && !Owner.dead) {
                if (Main.GameUpdateCount - healBudgetTick >= 60) {
                    healBudgetTick = Main.GameUpdateCount;
                    healBudget = 0;
                }
                if (healBudget < 6 && Owner.statLife < Owner.statLifeMax2) {
                    healBudget++;
                    Owner.Heal(1);
                }
            }
            if (!VaultUtils.isServer) {
                //渡魂缕：命中处一缕魂光飘向施法者
                Vector2 toOwner = (Owner.MountedCenter - target.Center).SafeNormalize(-Vector2.UnitY);
                PRTLoader.NewParticle<PRT_SoulLight>(target.Center, toOwner * 3.5f,
                    GsSpectreStaff.SpectreCyan, 0.45f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 center = Projectile.Center - Main.screenPosition;
            //包络：蓄势渐显、爆发满辉、余韵消散
            float env = Phase == 0
                ? VaultUtils.EaseOutQuad(Elapsed / (float)OmenTicks)
                : Phase == 1 ? 1f
                : 1f - VaultUtils.EaseInQuad((Elapsed - OmenTicks - MainTicks) / (float)AftermathTicks);
            if (env <= 0.02f) {
                return false;
            }

            //魂涡三层：外渊、中涡、白芯（identity 定相双向缓旋）
            float t1 = Main.GlobalTimeWrappedHourly * 1.7f + Projectile.identity * 0.37f;
            Main.EntitySpriteDraw(glow, center, null, GsSpectreStaff.SpectreDeep with { A = 0 } * (0.6f * env), t1,
                glow.Size() / 2f, 2.4f + 0.15f * MathF.Sin(t1 * 2.2f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, center, null, GsSpectreStaff.SpectreCyan with { A = 0 } * (0.55f * env), -t1 * 1.4f,
                glow.Size() / 2f, 1.5f + 0.1f * MathF.Sin(t1 * 3.1f + 1.7f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, center, null, Color.White with { A = 0 } * (0.7f * env),
                t1 * 2.6f, star.Size() / 2f, 0.5f, SpriteEffects.None, 0);

            //涡缘魂珠：八点缓旋（渊界读数）
            for (int i = 0; i < 8; i++) {
                float ang = MathHelper.TwoPi * i / 8f + t1 * 0.8f;
                Vector2 pos = center + ang.ToRotationVector2() * 60f;
                Main.EntitySpriteDraw(glow, pos, null, GsSpectreStaff.SpectreCyan with { A = 0 } * (0.35f * env), 0f,
                    glow.Size() / 2f, 0.12f, SpriteEffects.None, 0);
            }

            //潮头环：沿当前潮头画光珠环 + 前缘白晕
            float radius = WaveRadius;
            if (radius > 0f) {
                int beads = 26;
                for (int i = 0; i < beads; i++) {
                    float ang = MathHelper.TwoPi * i / beads + t1 * 0.5f;
                    Vector2 pos = center + ang.ToRotationVector2() * radius;
                    float fade = 1f - radius / TideRadius * 0.5f;
                    Main.EntitySpriteDraw(glow, pos, null, GsSpectreStaff.SpectreCyan with { A = 0 } * (0.5f * fade * env), 0f,
                        glow.Size() / 2f, 0.2f, SpriteEffects.None, 0);
                    Main.EntitySpriteDraw(glow, pos, null, Color.White with { A = 0 } * (0.28f * fade * env), 0f,
                        glow.Size() / 2f, 0.1f, SpriteEffects.None, 0);
                }
            }
            return false;
        }
    }
}
