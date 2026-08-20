using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Rendering;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalKingSlime.Projectiles
{
    /// <summary>
    /// 凝胶潮波。ai[0]=宿主whoAmI(-1独立) ai[1]=模式 ai[2]=行进帧数<br/>
    /// 模式0：潮汐冲刷头(粘连本体，TideRush期间本体即波体)<br/>
    /// 模式1：海啸波(立塔倾倒后独立行进，渐衰)<br/>
    /// 模式2：皇权涨潮墙(慢速高墙)<br/>
    /// 模式3：质心回流(矮波，抛掷质量沿地爬回本体)<br/>
    /// 服务端生成
    /// </summary>
    internal class BKSTideWaveProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private const int CollapseTime = 18;

        //---- 波高公平阀(契约3)：一个数字同时驱动视觉与碰撞(Colliding/PreDraw共用WaveHeight) ----
        /// <summary>模式0冲刷头波高：低于玩家单跳(约6.4格)，原地起跳即可越过</summary>
        internal const float RushWaveHeightPx = 96f;
        /// <summary>模式1海啸波高：需借势跳跃或绕到倒塌方向背面</summary>
        internal const float TsunamiWaveHeightPx = 150f;
        /// <summary>模式2皇权潮墙波高：不可跳越，中央净空区是唯一解(由生成参数保证)</summary>
        internal const float DecreeWallHeightPx = 180f;
        /// <summary>模式3质心回流波高：矮波，单跳轻松越过(公平阀)</summary>
        internal const float ReturnFlowHeightPx = 64f;

        private int HostIndex => (int)Projectile.ai[0];
        private int Mode => (int)Projectile.ai[1];
        private int TravelFrames => (int)Projectile.ai[2] <= 0 ? 90 : (int)Projectile.ai[2];

        private ref float Timer => ref Projectile.localAI[0];
        /// <summary>锁定的行进方向。横速归零(转向/卡停)时不能回退成 +X，否则左向潮体视觉与击退都会翻成朝右</summary>
        private ref float StoredDir => ref Projectile.localAI[1];

        private float WaveLength => Mode switch { 1 => 560f, 2 => 320f, 3 => 300f, _ => 400f };
        private float WaveHeight => Mode switch {
            1 => TsunamiWaveHeightPx,
            2 => DecreeWallHeightPx,
            3 => ReturnFlowHeightPx,
            _ => RushWaveHeightPx,
        };

        /// <summary>行进方向符号：有横速时刷新，否则沿用锁定值</summary>
        private int Dir {
            get {
                if (Math.Abs(Projectile.velocity.X) > 0.4f) {
                    StoredDir = Math.Sign(Projectile.velocity.X);
                }
                return StoredDir < 0f ? -1 : 1;
            }
        }

        /// <summary>寿命包络：起势→全高→崩解</summary>
        private float HeightEnvelope {
            get {
                float grow = MathHelper.Clamp(Timer / 14f, 0f, 1f);
                float collapse = MathHelper.Clamp((TravelFrames - Timer) / (float)CollapseTime, 0f, 1f);
                return VaultUtils.EaseOutQuad(grow) * VaultUtils.EaseInQuad(collapse) * heightMul;
            }
        }

        private float heightMul = 1f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2600;

        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 700;
            CooldownSlot = ImmunityCooldownID.Bosses;
        }

        public override void AI() {
            Timer++;
            //生成速度在首帧仍是 NewProjectile 写入的锁定方向，先记下再被宿主速度覆盖
            if (StoredDir == 0f && Math.Abs(Projectile.velocity.X) > 0.01f) {
                StoredDir = Math.Sign(Projectile.velocity.X);
            }

            //模式0：粘连宿主，宿主不在潮汐态则崩解
            if (Mode == 0) {
                NPC host = HostIndex >= 0 && HostIndex < Main.maxNPCs ? Main.npc[HostIndex] : null;
                bool hostValid = host != null && host.active && host.type == NPCID.KingSlime
                    && (int)host.ai[2] == (int)KingSlimeStateIndex.TideRush;
                if (hostValid) {
                    if (Math.Abs(host.velocity.X) > 0.5f) {
                        StoredDir = Math.Sign(host.velocity.X);
                    }
                    else if (host.direction != 0) {
                        StoredDir = host.direction;
                    }
                    Projectile.Center = host.Center;
                    Projectile.velocity = host.velocity;
                    //宿主速度极低时(转向/重组拍)波头也塌一点
                    heightMul = MathHelper.Lerp(heightMul, MathHelper.Clamp(Math.Abs(host.velocity.X) / 14f, 0.4f, 1f), 0.2f);
                    if (Timer > TravelFrames - CollapseTime) {
                        Timer = TravelFrames - CollapseTime;//宿主在位则不老死
                    }
                }
                else if (Timer < TravelFrames - CollapseTime) {
                    Timer = TravelFrames - CollapseTime;//宿主离开，进入崩解
                }
            }
            else {
                //独立行进：贴地形起伏
                Vector2 probe = Projectile.Center + new Vector2(Dir * WaveLength * 0.18f, -40f);
                Vector2 ground = KingSlimeGelFX.FindGroundBelow(probe, 24);
                float targetY = ground.Y - 8f;
                //上坡快贴、下坡缓落
                float lerpRate = targetY < Projectile.Center.Y ? 0.35f : 0.16f;
                Projectile.Center = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Center.Y, targetY, lerpRate));

                //迎面撞上陡壁(前方地面高出太多)提前崩解
                if (ground.Y < Projectile.Center.Y - 130f && Timer < TravelFrames - CollapseTime) {
                    Timer = TravelFrames - CollapseTime;
                }
            }

            if (Timer >= TravelFrames) {
                Projectile.Kill();
                return;
            }

            float env = HeightEnvelope;

            //客户端表现：波峰喷洒+贴地尘
            if (!VaultUtils.isServer && env > 0.3f) {
                Vector2 crest = Projectile.Center + new Vector2(Dir * WaveLength * 0.32f, -WaveHeight * env * 0.75f);
                if (Main.rand.NextBool(2)) {
                    Vector2 vel = new Vector2(Dir * Main.rand.NextFloat(1.5f, 5f), -Main.rand.NextFloat(2f, 6.5f));
                    PRTLoader.NewParticle<PRT_BKSGelBead>(crest + Main.rand.NextVector2Circular(26f, 14f), vel,
                        Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelFoam, Main.rand.NextFloat(0.5f)) * 0.8f,
                        Main.rand.NextFloat(0.6f, 1.2f))?.Configure(Main.rand.Next(18, 32));
                }
                if (Main.rand.NextBool(3)) {
                    Dust d = Dust.NewDustDirect(Projectile.Center + new Vector2(-WaveLength * 0.4f, -12f),
                        (int)(WaveLength * 0.8f), 10, DustID.TintableDust, 0, 0, 150, KingSlimeGelFX.DustBlue, Main.rand.NextFloat(1.2f, 2f));
                    d.noGravity = true;
                    d.velocity = new Vector2(Dir * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.5f, 2f));
                }
                if (Main.rand.NextBool(6)) {
                    KingSlimeGelFX.BubbleFizz(Projectile.Center + new Vector2(Main.rand.NextFloat(-0.3f, 0.3f) * WaveLength, -WaveHeight * env * 0.4f), 20f, 1);
                }
            }

            for (int i = 0; i < 4; i++) {
                Lighting.AddLight(Projectile.Center + new Vector2((i - 1.5f) * WaveLength * 0.25f, -30f),
                    KingSlimeGelFX.GelMid.ToVector3() * 0.4f * env);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(BuffID.Slimed, 180);
            //波体把玩家往行进方向卷带
            target.velocity.X += Dir * 3.5f;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            float env = HeightEnvelope;
            if (env < 0.25f) {
                return false;
            }
            //贴地扁宽盒，略窄于视觉
            float halfW = WaveLength * 0.42f;
            float h = WaveHeight * env * 0.85f;
            Rectangle waveRect = new Rectangle(
                (int)(Projectile.Center.X - halfW), (int)(Projectile.Center.Y - h),
                (int)(halfW * 2f), (int)(h + 16f));
            return waveRect.Intersects(targetHitbox);
        }

        public override bool PreDraw(ref Color lightColor) {
            Effect shader = EffectLoader.BKSGelSurge?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            float env = HeightEnvelope;
            if (env < 0.03f) {
                return false;
            }
            if (shader == null || noise == null) {
                //着色器不可用：CPU回退，绝不许无形判定
                DrawFallback(env);
                return false;
            }

            KingSlimeGelFX.SetSurgeParams(shader,
                flow: Mode == 2 ? 0.35f : 0.85f,
                foam: Mode == 1 ? 0.3f : 0.22f,
                alpha: 0.95f,
                edgeGlow: 0.9f + env * 0.4f,
                churn: Mode == 2 ? 0.9f : 0.6f,
                seed: Projectile.whoAmI * 0.211f % 1f);

            Vector2 head = new Vector2(Projectile.Center.X + Dir * WaveLength * 0.42f, Projectile.Center.Y + 12f);
            KingSlimeGelFX.DrawSurgeStrip(shader, noise, head, new Vector2(Dir, 0f),
                WaveLength, WaveHeight * env, crestEnergy: Mode == 1 ? 1.2f : 0.9f, alphaEnvelope: 1f, segments: 16);
            return false;
        }

        /// <summary>无着色器回退：拉伸凝胶团铺出波形轮廓</summary>
        private void DrawFallback(float env) {
            Texture2D blob = CWRAsset.Extra_98?.Value;
            if (blob == null) {
                return;
            }
            Color gel = Color.Lerp(KingSlimeGelFX.GelMid, KingSlimeGelFX.GelDeep, 0.35f) * 0.7f;
            int segs = 7;
            for (int i = 0; i < segs; i++) {
                float t = (i + 0.5f) / segs;
                float h = WaveHeight * env * (0.35f + 0.65f * t);
                Vector2 pos = Projectile.Center + new Vector2(Dir * (t - 0.5f) * WaveLength, -h * 0.4f) - Main.screenPosition;
                Main.EntitySpriteDraw(blob, pos, null, gel, 0f, blob.Size() * 0.5f,
                    new Vector2(WaveLength / segs / blob.Width * 1.5f, h / blob.Height * 2.2f), SpriteEffects.None, 0);
            }
        }
    }
}
