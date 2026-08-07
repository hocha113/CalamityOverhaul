using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// Victor 出场门；<see cref="VictorPortalSpawner"/> 主端生成，StableEnd 主端 NewNPC
    /// <br/>ai0=facing±1 ai1=Victor whoAmI(主写客读) ai2=尺寸缩放
    /// </summary>
    internal class VictorRiftPortalProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>总生命，与阶段帧同步</summary>
        public const int TotalLife = 220;
        /// <summary>0..TearEnd 撕开</summary>
        public const int TearEnd = 28;
        /// <summary>TearEnd..StableEnd 稳定；StableEnd 主端生成 NPC</summary>
        public const int StableEnd = 96;
        /// <summary>StableEnd..EmergeEnd 淡入浮出</summary>
        public const int EmergeEnd = 160;
        /// <summary>CollapseStart..TotalLife 坍塌</summary>
        public const int CollapseStart = 165;

        /// <summary>椭圆宽轴半径 px</summary>
        public const float BaseHalfWidth = 80f;
        /// <summary>高宽比，&gt;1 偏竖</summary>
        public const float AspectRatio = 1.55f;
        /// <summary>高轴半径，Spawner 用下沿贴地</summary>
        public const float BaseHalfHeight = BaseHalfWidth * AspectRatio;

        private float Facing => Projectile.ai[0] >= 0f ? 1f : -1f;
        /// <summary>主端写 whoAmI，负值未生成</summary>
        public int BoundVictorWhoAmI {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        private float Scale => Projectile.ai[2] <= 0.01f ? 1f : Projectile.ai[2];

        private int AgeFrame => TotalLife - Projectile.timeLeft;

        /// <summary>每端独立渲染种子，不同步</summary>
        private float visualSeed;

        public override void SetStaticDefaults() {
            //半离屏仍绘发光带
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 800;
            ProjectileID.Sets.TrailingMode[Type] = -1;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalLife;
            //城镇出场，需客户端稳收弹幕
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        public override void AI() {
            int age = AgeFrame;

            //首帧种子；主端 ai1 默认 0 与 whoAmI=0 冲突，置 -1
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                visualSeed = Main.rand.NextFloat();
                if (Main.netMode != NetmodeID.MultiplayerClient && BoundVictorWhoAmI == 0) {
                    BoundVictorWhoAmI = -1;
                    Projectile.netUpdate = true;
                }
            }

            if (age == TearEnd && !VaultUtils.isServer) {
                PlaySfxStable();
            }

            //主端 StableEnd 生成并同步 whoAmI
            if (age == StableEnd && Main.netMode != NetmodeID.MultiplayerClient && BoundVictorWhoAmI < 0) {
                SpawnVictorOnServer();
            }

            UpdateBoundVictor(age);
            SpawnAmbientParticles(age);

            if (age == CollapseStart && !VaultUtils.isServer) {
                PlaySfxCollapse();
            }
        }

        /// <summary>主端 NewNPC，写 ai1</summary>
        private void SpawnVictorOnServer() {
            int npcType = ModContent.NPCType<Victor>();
            if (NPC.AnyNPCs(npcType)) {
                BoundVictorWhoAmI = -2;//放弃生成，动画继续
                Projectile.netUpdate = true;
                return;
            }

            //下沿已贴地；NewNPC y=地面顶，脚贴地
            float halfH = BaseHalfHeight * Scale;
            int groundY = (int)(Projectile.Center.Y + halfH);
            int x = (int)Projectile.Center.X;
            int y = groundY;

            int index = NPC.NewNPC(new EntitySource_WorldEvent(), x, y, npcType);
            if (index < 0 || index >= Main.maxNPCs) {
                BoundVictorWhoAmI = -2;
                Projectile.netUpdate = true;
                return;
            }

            NPC v = Main.npc[index];
            v.alpha = 255;
            v.direction = v.spriteDirection = Facing >= 0f ? 1 : -1;
            v.velocity = Vector2.Zero;
            v.Bottom = new Vector2(Projectile.Center.X, groundY);
            //NewNPC 默认 homeless=false + homeTile=-1，会把家钉死在落点；
            //置 true 交给原版分房流程搬进玩家房屋
            v.homeless = true;
            v.netUpdate = true;

            //首次登场后转正常城镇 NPC，死后由原版住房系统重生
            if (!VictorWorldState.HasArrived) {
                VictorWorldState.HasArrived = true;
                if (Main.netMode == NetmodeID.Server) {
                    NetMessage.SendData(MessageID.WorldData);
                }
            }

            BoundVictorWhoAmI = index;
            Projectile.netUpdate = true;
        }

        private void UpdateBoundVictor(int age) {
            int who = BoundVictorWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return;
            }
            NPC v = Main.npc[who];
            if (!v.active || v.type != ModContent.NPCType<Victor>()) {
                return;
            }

            if (age < StableEnd) {
                return;
            }

            //StableEnd→EmergeEnd alpha 255→0
            int emergeAge = age - StableEnd;
            int emergeSpan = EmergeEnd - StableEnd;
            float emergeT = MathHelper.Clamp(emergeAge / (float)emergeSpan, 0f, 1f);
            float emergeEase = 1f - (1f - emergeT) * (1f - emergeT);
            int targetAlpha = (int)MathHelper.Lerp(255f, 0f, emergeEase);
            v.alpha = (int)MathHelper.Lerp(v.alpha, targetAlpha, 0.35f);

            //门口踏出半步，脚下沿=地
            float halfH = BaseHalfHeight * Scale;
            float groundY = Projectile.Center.Y + halfH;
            float walkOff = MathHelper.Lerp(0f, 22f, emergeEase) * Facing;

            v.Bottom = new Vector2(Projectile.Center.X + walkOff, groundY);
            v.velocity = Vector2.Zero;
            v.direction = v.spriteDirection = Facing >= 0f ? 1 : -1;
            if (emergeT >= 1f && v.alpha <= 4) {
                v.alpha = 0;
            }
        }

        private void SpawnAmbientParticles(int age) {
            if (VaultUtils.isServer) return;

            //撕开密、稳定中、坍塌爆发
            int interval;
            if (age < TearEnd) interval = 1;
            else if (age < StableEnd) interval = 3;
            else if (age < EmergeEnd) interval = 4;
            else if (age < CollapseStart) interval = 6;
            else interval = 2;

            if (Projectile.timeLeft % interval != 0) {
                return;
            }

            float halfW = BaseHalfWidth * Scale;
            float halfH = halfW * AspectRatio;

            int count = age < TearEnd ? Main.rand.Next(2, 4)
                : age >= CollapseStart ? Main.rand.Next(3, 6)
                : Main.rand.Next(1, 3);

            for (int i = 0; i < count; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                //撕开/坍塌外扩，稳定贴边
                float radial = age < TearEnd || age >= CollapseStart
                    ? Main.rand.NextFloat(0.9f, 1.3f)
                    : Main.rand.NextFloat(0.75f, 1.05f);
                Vector2 ringPos = new Vector2(MathF.Cos(ang) * halfW, MathF.Sin(ang) * halfH) * radial;
                Vector2 spawn = Projectile.Center + ringPos;
                Vector2 outward = (ringPos.SafeNormalize(Vector2.UnitX)) * Main.rand.NextFloat(1.5f, 4.5f);

                //坍塌反向吸入
                if (age >= CollapseStart) {
                    outward = -outward * 1.4f;
                }

                float scl = Main.rand.NextFloat(0.6f, 1.6f);
                int life = Main.rand.Next(20, 40);
                PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, outward, Color.White, scl).Configure(life);
            }

            //StableEnd 中心 burst
            if (age == StableEnd) {
                for (int i = 0; i < 28; i++) {
                    float ang = MathHelper.TwoPi * i / 28f + Main.rand.NextFloat(-0.1f, 0.1f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(3f, 8f);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(Projectile.Center, vel, Color.White,
                        Main.rand.NextFloat(0.9f, 1.8f)).Configure(Main.rand.Next(22, 42));
                }
            }
        }

        #region 音效
        private void PlaySfxStable() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.45f, Pitch = -0.05f }, Projectile.Center);
        }
        private void PlaySfxCollapse() {
            if (VaultUtils.isServer) return;
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.6f, Pitch = -0.4f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.5f, Pitch = 0.1f }, Projectile.Center);
        }

        #endregion

        public override bool PreDraw(ref Color lightColor) {
            int age = AgeFrame;
            float openProgress = ComputeOpenProgress(age);
            float emergePulse = ComputeEmergePulse(age);
            float collapseT = ComputeCollapseT(age);

            //着色器缺失走兜底
            Effect shader = EffectLoader.VictorCyberPortal?.Value;
            if (shader == null || VaultAsset.placeholder2?.Value == null
                || CWRAsset.Extra_193?.Value == null) {
                DrawFallback(openProgress, emergePulse, collapseT);
                return false;
            }

            DrawShaderPortal(shader, openProgress, emergePulse, collapseT);
            return false;
        }

        private float ComputeOpenProgress(int age) {
            if (age < TearEnd) {
                float t = age / (float)TearEnd;
                //ease-out 撕开
                return 1f - MathF.Pow(1f - t, 2.4f);
            }
            if (age < CollapseStart) {
                //稳定 1±0.04 呼吸
                float t = (age - TearEnd) * 0.08f;
                return 1f + 0.04f * MathF.Sin(t);
            }
            //坍塌先回 1，collapse 接管
            return 1f - (age - CollapseStart) / (float)(TotalLife - CollapseStart) * 0.15f;
        }

        private float ComputeEmergePulse(int age) {
            //StableEnd 尖峰，约 25 帧衰减
            int diff = age - StableEnd;
            if (diff < 0 || diff > 40) return 0f;
            return MathF.Exp(-diff / 12f);
        }

        private float ComputeCollapseT(int age) {
            if (age < CollapseStart) return 0f;
            return MathHelper.Clamp((age - CollapseStart) / (float)(TotalLife - CollapseStart), 0f, 1f);
        }

        /// <summary>quad 半径 = portal×此值；须&gt;1，防辉光被切边；shader 收 1/此值作内圈</summary>
        private const float QuadOverPortal = 1.6f;

        private void DrawShaderPortal(Effect shader, float openProg, float emergePulse, float collapseT) {
            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float halfW = BaseHalfWidth * Scale;
            float halfH = halfW * AspectRatio;
            //外发光余量
            float quadW = halfW * QuadOverPortal * 2f;
            float quadH = halfH * QuadOverPortal * 2f;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + visualSeed * 12f);
            shader.Parameters["openProgress"]?.SetValue(MathHelper.Clamp(openProg, 0f, 1.1f));
            shader.Parameters["emergePulse"]?.SetValue(MathHelper.Clamp(emergePulse, 0f, 1f));
            shader.Parameters["collapse"]?.SetValue(MathHelper.Clamp(collapseT, 0f, 1f));
            shader.Parameters["seed"]?.SetValue(visualSeed);
            shader.Parameters["portalSize"]?.SetValue(new Vector2(halfW, halfH));
            shader.Parameters["facing"]?.SetValue(Facing);
            shader.Parameters["quadInnerRadius"]?.SetValue(1f / QuadOverPortal);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 scale = new(quadW / canvas.Width, quadH / canvas.Height);
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            //外圈光源点亮周围地形
            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.18f, 0.10f) * MathHelper.Clamp(openProg, 0f, 1f));
        }

        /// <summary>着色器缺失兜底</summary>
        private void DrawFallback(float openProg, float emergePulse, float collapseT) {
            Texture2D px = VaultAsset.placeholder2.Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float halfW = BaseHalfWidth * Scale * MathHelper.Clamp(openProg, 0f, 1f) * (1f - collapseT);
            float halfH = halfW * AspectRatio;

            float pulse = 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 15f + visualSeed * 6f);
            Color rim = new Color(255, 60, 35) * (0.7f + emergePulse * 0.5f);
            Main.spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), rim * 0.8f, 0f,
                new Vector2(0.5f), new Vector2(halfW * 2f, halfH * 2f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1), Color.Black * 0.9f, 0f,
                new Vector2(0.5f), new Vector2(halfW * 1.7f, halfH * 1.7f), SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(px, drawPos, new Rectangle(0, 0, 1, 1),
                new Color(255, 110, 70) * pulse * (0.4f + emergePulse * 0.7f), 0f,
                new Vector2(0.5f), new Vector2(halfW * 0.35f, halfH * 0.35f), SpriteEffects.None, 0f);

            Lighting.AddLight(Projectile.Center, new Vector3(0.7f, 0.15f, 0.08f) * openProg);
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            //Victor 走出压在门上
            behindNPCs.Add(index);
        }
    }
}
