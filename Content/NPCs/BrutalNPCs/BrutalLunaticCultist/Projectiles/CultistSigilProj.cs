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
    /// 悬空法阵炮台：展开→逐发齐射当前元素弹→收拢；
    /// ai[0]=元素 ai[1]=首发延迟(涟漪错拍) ai[2]=齐射发数；伤害经 Projectile.damage 携带
    /// </summary>
    internal class CultistSigilProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int GrowTime = 30;
        private const int ShotInterval = 26;
        private const int FadeTime = 22;

        private CultistElement Element => (CultistElement)(int)Projectile.ai[0];
        private int FireDelay => (int)Projectile.ai[1];
        private int ShotCount => Math.Max((int)Projectile.ai[2], 1);

        private float spin;

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 60;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GrowTime + 600;
        }

        /// <summary>展开进度 0-1</summary>
        private float GrowProgress => MathHelper.Clamp(Projectile.localAI[0] / GrowTime, 0f, 1f);

        public override void AI() {
            Projectile.localAI[0]++;
            float t = Projectile.localAI[0];
            spin += 0.03f + GrowProgress * 0.02f;

            //齐射结束后收拢
            int volleyEnd = GrowTime + FireDelay + ShotCount * ShotInterval;
            if (t > volleyEnd && Projectile.timeLeft > FadeTime) {
                Projectile.timeLeft = FadeTime;
            }

            if (t == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item78 with { Volume = 0.55f, Pitch = 0.3f, MaxInstances = 5 }, Projectile.Center);
            }

            //面向最近玩家（表现层，本地各自算）
            Player nearest = Main.player[Player.FindClosest(Projectile.position, Projectile.width, Projectile.height)];
            if (nearest.Alives()) {
                Vector2 aim = (nearest.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.rotation = Projectile.rotation.AngleLerp(aim.ToRotation(), 0.1f);
            }

            //齐射（服务端裁决）
            if (!VaultUtils.isClient && t > GrowTime + FireDelay) {
                int shotTimer = (int)(t - GrowTime - FireDelay);
                if (shotTimer % ShotInterval == 0 && shotTimer / ShotInterval < ShotCount) {
                    FireBolt(nearest);
                }
            }

            //齐射节拍的表现帧（各端按同一节拍本地放）
            if (!VaultUtils.isServer && t > GrowTime + FireDelay) {
                int shotTimer = (int)(t - GrowTime - FireDelay);
                bool volleyActive = shotTimer / ShotInterval < ShotCount;
                if (shotTimer % ShotInterval == 0 && volleyActive) {
                    CultistRenderHelper.CastBurst(Projectile.Center, Projectile.rotation.ToRotationVector2(), Element, 1.1f);
                    SoundEngine.PlaySound(SoundID.Item72 with { Volume = 0.6f, Pitch = 0.15f, MaxInstances = 6 }, Projectile.Center);
                    //开火后坐：法阵沿出弹反向弹一记（localAI[1]=后坐强度，绘制消费）
                    Projectile.localAI[1] = 1f;
                }
                //出弹前8帧：中心凝聚元素微球+符文急速收束（出弹的仪式感衔接）
                int untilShot = ShotInterval - shotTimer % ShotInterval;
                if (volleyActive && untilShot <= 8 && Main.rand.NextBool(2)) {
                    Vector2 start = Projectile.Center + Main.rand.NextVector2CircularEdge(80f, 80f);
                    PRTLoader.NewParticle<PRT_CultistRune>(start, Vector2.Zero,
                        CultistPalette.Main(Element), Main.rand.NextFloat(0.5f, 0.9f))
                        ?.Configure(Projectile.Center, 0.3f, 8);
                }
            }
            //后坐衰减
            Projectile.localAI[1] = Math.Max(Projectile.localAI[1] - 0.09f, 0f);

            //展开期与常驻微粒
            if (!VaultUtils.isServer && Main.rand.NextBool(GrowProgress > 0.95f ? 7 : 3)) {
                CultistRenderHelper.ConvergeRunes(Projectile.Center, 110f, Element, 0.8f);
            }

            Lighting.AddLight(Projectile.Center, CultistPalette.Main(Element).ToVector3() * 0.6f * GrowProgress);
        }

        private void FireBolt(Player target) {
            if (!target.Alives()) {
                return;
            }
            Vector2 aim = (target.Center + target.velocity * 14f - Projectile.Center).SafeNormalize(Vector2.UnitY);
            int damage = Projectile.damage;
            var source = Projectile.GetSource_FromAI();
            switch (Element) {
                case CultistElement.Fire:
                    Projectile.NewProjectile(source, Projectile.Center, aim * 6.4f,
                        ModContent.ProjectileType<CultistFireBolt>(), damage, 0f, Main.myPlayer, 0f, 0f);
                    break;
                case CultistElement.Ice:
                    Projectile.NewProjectile(source, Projectile.Center, aim,
                        ModContent.ProjectileType<CultistIceLance>(), damage, 0f, Main.myPlayer, 14f, 19f);
                    break;
                default:
                    Projectile.NewProjectile(source, Projectile.Center, aim * 7.4f,
                        ModContent.ProjectileType<CultistArcSpark>(), damage, 0f, Main.myPlayer,
                        (float)CultistElement.Thunder, 0f);
                    break;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = Projectile.timeLeft < FadeTime ? Projectile.timeLeft / (float)FadeTime : 1f;
            float flash = 0f;
            float chargeOrb = 0f;
            float t = Projectile.localAI[0];
            if (t > GrowTime + FireDelay) {
                int shotTimer = (int)(t - GrowTime - FireDelay);
                bool volleyActive = shotTimer / ShotInterval < ShotCount;
                int phase = shotTimer % ShotInterval;
                //开火帧短闪
                if (phase < 5) {
                    flash = 1f - phase / 5f;
                }
                //出弹前8帧：中心元素球凝聚可见（蓄力包络）
                int untilShot = ShotInterval - phase;
                if (volleyActive && untilShot <= 8) {
                    chargeOrb = 1f - untilShot / 8f;
                }
            }

            //开火后坐：法阵沿出弹反向位移+微缩（recoil语义）
            float recoil = Projectile.localAI[1];
            Vector2 recoilOff = -Projectile.rotation.ToRotationVector2() * (recoil * 8f);
            float radius = 92f * (1f - 0.08f * recoil);

            CultistRenderHelper.DrawSigil(Main.spriteBatch, Projectile.Center + recoilOff, radius, Element,
                GrowProgress, spin, flash, 0f, fade);

            //蓄力微弹：法阵中心凝聚的下一发弹丸（原版元素真实纹理，从无到有成形）
            if (chargeOrb > 0.05f) {
                CultistRenderHelper.DrawElementCore(Main.spriteBatch, Projectile.Center + recoilOff, Element,
                    0.2f + 0.35f * chargeOrb, chargeOrb, Projectile.localAI[0], Projectile.identity * 1.31f);
            }
            return false;
        }
    }
}
