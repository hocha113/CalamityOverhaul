using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Rendering;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.States;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalDukeFishron.Projectiles
{
    /// <summary>
    /// 投技大漩涡舞台（纯演出，无接触伤害——危险是"被卷"本身）。
    /// ai[0]=Boss whoAmI；相位/强度各端本地从 Boss 覆写状态推导，
    /// Boss 离开投技状态即本地渐隐，服务端到点 Kill 收尸
    /// </summary>
    internal class FishronVortexProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private int BossIndex => (int)Projectile.ai[0];

        /// <summary>本地渐隐计时（Boss 离开投技状态后启动）</summary>
        private ref float FadeOut => ref Projectile.localAI[0];
        /// <summary>本地寿命计时（纯表现相位）</summary>
        private ref float LiveTime => ref Projectile.localAI[1];

        private const int FadeTime = 26;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1600;

        public override void SetDefaults() {
            Projectile.width = 64;
            Projectile.height = 64;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            //硬上限兜底；正常生命周期由 Boss 状态驱动
            Projectile.timeLeft = 1200;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        /// <summary>
        /// 从 Boss 覆写读涡强度与转速：蓄涡爬升→抽吸增压→连段稳态→深潜渐平→其余渐隐。
        /// 全部由同步的状态索引+本地状态计时推导，各端一致
        /// </summary>
        private (float intensity, float spin, bool alive) ReadStagePhase() {
            if (!BossIndex.TryGetNPC(out NPC boss) || !boss.Alives()
                || boss.type != NPCID.DukeFishron
                || !boss.TryGetOverride(out DukeFishronAI ov)) {
                return (0f, 0.6f, false);
            }

            if (ov.CurrentState is FishronVortexSnareState snare) {
                float t = snare.Timer;
                if (t <= FishronVortexSnareState.SuctionStart) {
                    //蓄涡：三次方爬升
                    float p = t / FishronVortexSnareState.SuctionStart;
                    return (p * p * p * 0.55f, 0.6f + p * 0.5f, true);
                }
                if (t < FishronVortexSnareState.CommitTick) {
                    float p = (t - FishronVortexSnareState.SuctionStart)
                        / (float)(FishronVortexSnareState.CommitTick - FishronVortexSnareState.SuctionStart);
                    return (0.55f + p * 0.45f, 1.1f + p * 0.9f, true);
                }
                //空振坍缩段：交给渐隐
                return (0.6f, 1.2f, false);
            }
            if (ov.CurrentState is FishronVortexGrabState grab) {
                float t = grab.Timer;
                if (t < FishronVortexGrabState.DiveStart) {
                    return (1f, 1.6f, true);
                }
                if (t < FishronVortexGrabState.LaunchTick) {
                    //深潜死寂：涡面渐平，给破水让出静默
                    float p = (t - FishronVortexGrabState.DiveStart)
                        / (float)(FishronVortexGrabState.LaunchTick - FishronVortexGrabState.DiveStart);
                    return (1f - p * 0.6f, 1.6f - p * 1f, true);
                }
                return (0.4f, 0.5f, false);
            }
            return (0f, 0.6f, false);
        }

        public override void AI() {
            LiveTime++;
            (float intensity, float spin, bool alive) = ReadStagePhase();

            if (!alive) {
                FadeOut++;
                //渐隐尽头服务端收尸；客户端 eff 归零自然隐形，等 Kill 包对齐
                if (FadeOut >= FadeTime && !VaultUtils.isClient) {
                    Projectile.Kill();
                }
            }
            else {
                FadeOut = 0f;
            }

            float fade = 1f - MathHelper.Clamp(FadeOut / FadeTime, 0f, 1f);
            float eff = intensity * fade;
            //旋转相位记录在 rotation，本地推进
            Projectile.rotation += 0.045f * spin;

            Lighting.AddLight(Projectile.Center, FishronMotionFX.SeaGreen.ToVector3() * 0.8f * eff);

            if (VaultUtils.isServer || eff <= 0.03f) {
                return;
            }

            //涡口泡沫环
            if (LiveTime % 5 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = Projectile.Center + new Vector2((float)Math.Cos(ang) * 170f, (float)Math.Sin(ang) * 34f - 6f);
                PRTLoader.NewParticle<PRT_FishronFoam>(rim,
                    new Vector2(-(float)Math.Cos(ang) * 1.4f * spin, -0.5f),
                    FishronMotionFX.FoamWhite * (0.3f * eff), Main.rand.NextFloat(0.6f, 1f))
                    ?.Configure(Main.rand.Next(24, 40), 0.05f);
            }
            //抽吸期：外场向心收束的水线（吸力可视化）
            if (spin > 1.05f && LiveTime % 2 == 0) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2CircularEdge(
                    FishronGrabFacts.SuctionRadius * 0.85f, FishronGrabFacts.SuctionRadius * 0.5f);
                Vector2 vel = (Projectile.Center - from) * 0.055f;
                PRTLoader.NewParticle<PRT_FishronSpray>(from, vel,
                    Color.Lerp(FishronMotionFX.SeaGreen, FishronMotionFX.FoamWhite, Main.rand.NextFloat(0.5f)),
                    Main.rand.NextFloat(0.6f, 1.1f))?.Configure(Main.rand.Next(16, 26), 0f);
            }
            //连段稳态：涡心上浮的气泡（有人被按在水下）
            if (spin >= 1.5f && LiveTime % 4 == 0) {
                Vector2 heart = FishronGrabFacts.Heart(Projectile.Center);
                PRTLoader.NewParticle<PRT_FishronSpray>(heart + Main.rand.NextVector2Circular(40f, 26f),
                    -Vector2.UnitY * Main.rand.NextFloat(1.5f, 3.5f),
                    FishronMotionFX.FoamWhite * 0.8f, Main.rand.NextFloat(0.4f, 0.8f))
                    ?.Configure(Main.rand.Next(18, 30), -0.02f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            //收场溅落
            FishronMotionFX.SpawnSplashBurst(Projectile.Center, 1.1f, playSound: false);
        }

        public override bool PreDraw(ref Color lightColor) {
            (float intensity, float spin, bool alive) = ReadStagePhase();
            float fade = 1f - MathHelper.Clamp(FadeOut / FadeTime, 0f, 1f);
            float eff = (alive ? intensity : intensity * 0.6f) * fade;
            if (eff <= 0.02f) {
                return false;
            }

            Texture2D cyclone = CWRAsset.Cyclone?.Value;
            Texture2D ring = CWRAsset.DiffusionCircle?.Value;
            if (cyclone == null || ring == null) {
                return false;
            }

            SpriteBatch sb = Main.spriteBatch;
            Vector2 mouth = Projectile.Center - Main.screenPosition;
            //加色语义：颜色 A=0，强度写进乘数（默认 AlphaBlend 批下 A=0 即加算）
            Color seaAdd = new(FishronMotionFX.SeaGreen.R, FishronMotionFX.SeaGreen.G, FishronMotionFX.SeaGreen.B, 0);
            Color foamAdd = new(FishronMotionFX.FoamWhite.R, FishronMotionFX.FoamWhite.G, FishronMotionFX.FoamWhite.B, 0);
            Color boltAdd = new(FishronMotionFX.StormBolt.R, FishronMotionFX.StormBolt.G, FishronMotionFX.StormBolt.B, 0);

            //水面涨落线：横压扁的扩散环
            float breath = 1f + 0.05f * (float)Math.Sin(LiveTime * 0.09f);
            sb.Draw(ring, mouth, null, seaAdd * (0.5f * eff), 0f,
                ring.Size() * 0.5f, new Vector2(1.15f, 0.22f) * breath * eff, SpriteEffects.None, 0f);
            sb.Draw(ring, mouth, null, foamAdd * (0.3f * eff), 0f,
                ring.Size() * 0.5f, new Vector2(0.9f, 0.17f) * breath * eff, SpriteEffects.None, 0f);

            //涡口双层对转旋盘
            float mouthScale = 2.9f * eff;
            sb.Draw(cyclone, mouth, null, seaAdd * (0.85f * eff), Projectile.rotation,
                cyclone.Size() * 0.5f, new Vector2(mouthScale, mouthScale * 0.42f), SpriteEffects.None, 0f);
            sb.Draw(cyclone, mouth, null, foamAdd * (0.4f * eff), -Projectile.rotation * 1.35f,
                cyclone.Size() * 0.5f, new Vector2(mouthScale * 0.72f, mouthScale * 0.31f), SpriteEffects.None, 0f);

            //涡底漏斗：向下逐层收窄的旋环，相位随深度扭进（螺旋错觉）
            const int FunnelLayers = 5;
            for (int i = 1; i <= FunnelLayers; i++) {
                float d = i / (float)FunnelLayers;
                Vector2 pos = mouth + new Vector2(0f, i * 34f * eff);
                float layerScale = mouthScale * MathHelper.Lerp(0.82f, 0.28f, d);
                float layerRot = Projectile.rotation * (1f + d * 1.6f) + i * 0.9f;
                Color c = Color.Lerp(seaAdd, boltAdd, d * 0.5f);
                c.A = 0;
                sb.Draw(cyclone, pos, null, c * (0.55f * eff * (1f - d * 0.45f)), layerRot,
                    cyclone.Size() * 0.5f, new Vector2(layerScale, layerScale * 0.4f), SpriteEffects.None, 0f);
            }

            return false;
        }

        public override void DrawBehind(int index, System.Collections.Generic.List<int> behindNPCsAndTiles,
            System.Collections.Generic.List<int> behindNPCs, System.Collections.Generic.List<int> behindProjectiles,
            System.Collections.Generic.List<int> overPlayers, System.Collections.Generic.List<int> overWiresUI)
            => behindNPCsAndTiles.Add(index);
    }
}
