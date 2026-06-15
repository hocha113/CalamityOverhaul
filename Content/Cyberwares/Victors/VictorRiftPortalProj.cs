using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Cyberwares.Victors
{
    /// <summary>
    /// Victor 出场传送门：赛博乱流裂口，撕开→稳定→Victor 浮现→坍塌
    /// <br/>由 <see cref="VictorPortalSpawner"/> 服务端/单机生成，AI 在 NPC 浮现帧主端 NewNPC
    /// <br/>ai[0]=facing(±1) ai[1]=绑定 Victor whoAmI (主端写客户端读) ai[2]=尺寸缩放
    /// </summary>
    internal class VictorRiftPortalProj : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        /// <summary>总生命，与下方各阶段帧数同步</summary>
        public const int TotalLife = 220;
        /// <summary>0..TearEnd 撕开</summary>
        public const int TearEnd = 28;
        /// <summary>TearEnd..StableEnd 稳定开放；StableEnd 时主端生成 NPC</summary>
        public const int StableEnd = 96;
        /// <summary>StableEnd..EmergeEnd Victor 淡入 + 浮出</summary>
        public const int EmergeEnd = 160;
        /// <summary>CollapseStart..TotalLife 坍塌关闭</summary>
        public const int CollapseStart = 165;

        /// <summary>视觉半径（像素），椭圆门口的宽轴</summary>
        public const float BaseHalfWidth = 80f;
        /// <summary>椭圆高宽比，>1 偏竖立"撕开的口"形状</summary>
        public const float AspectRatio = 1.55f;
        /// <summary>椭圆高度半径（像素），用于 Spawner 锚定 portal 下沿到地面</summary>
        public const float BaseHalfHeight = BaseHalfWidth * AspectRatio;

        private float Facing => Projectile.ai[0] >= 0f ? 1f : -1f;
        /// <summary>主端写入：被绑定的 Victor NPC whoAmI，负值表示尚未生成</summary>
        public int BoundVictorWhoAmI {
            get => (int)Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }
        private float Scale => Projectile.ai[2] <= 0.01f ? 1f : Projectile.ai[2];

        /// <summary>当前已经历的帧数（从 0 开始）</summary>
        private int AgeFrame => TotalLife - Projectile.timeLeft;

        /// <summary>每实例随机种子（不参与同步，每端各自渲染）</summary>
        private float visualSeed;

        public override void SetStaticDefaults() {
            //保证弹幕"半离屏"也能继续绘制传送门发光带
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
            //城镇 NPC 生成事件，需保证多人客户端能稳定接收弹幕
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;
        public override bool? CanDamage() => false;

        public override void AI() {
            int age = AgeFrame;

            //初始化：随机化每端独立的渲染种子；主端首帧确保 BoundVictorWhoAmI=-1
            if (Projectile.localAI[0] == 0f) {
                Projectile.localAI[0] = 1f;
                visualSeed = Main.rand.NextFloat();
                if (Main.netMode != NetmodeID.MultiplayerClient && BoundVictorWhoAmI == 0) {
                    //ai[1] 默认 0 会和 whoAmI=0 冲突，初始化为 -1 表示"未生成"
                    BoundVictorWhoAmI = -1;
                    Projectile.netUpdate = true;
                }
                PlaySfxTear();
            }

            //稳定开放期间的环境音/震屏
            if (age == TearEnd && !VaultUtils.isServer) {
                PlaySfxStable();
            }

            //主端：稳定结束帧创建 Victor，并同步 whoAmI
            if (age == StableEnd && Main.netMode != NetmodeID.MultiplayerClient && BoundVictorWhoAmI < 0) {
                SpawnVictorOnServer();
            }

            //出场期间：锁定/淡入 Victor，并向门内吐故障粒子（任意端可独立做视觉）
            UpdateBoundVictor(age);
            SpawnAmbientParticles(age);

            //坍塌瞬间播放关闭音
            if (age == CollapseStart && !VaultUtils.isServer) {
                PlaySfxCollapse();
            }
        }

        /// <summary>主端创建 Victor NPC，写入 ai[1] 同步给客户端</summary>
        private void SpawnVictorOnServer() {
            int npcType = ModContent.NPCType<Victor>();
            //已经存在 Victor 就不再重复（兜底防异常二次生成）
            if (NPC.AnyNPCs(npcType)) {
                BoundVictorWhoAmI = -2;//-2 = 放弃生成，但允许动画继续走完
                Projectile.netUpdate = true;
                return;
            }

            //Spawner 已把 portal 下沿对齐到地面，groundY = Center.Y + halfH
            float halfH = BaseHalfHeight * Scale;
            int groundY = (int)(Projectile.Center.Y + halfH);
            //NPC.NewNPC 的 x/y 是 NPC 顶部参考点，y 给 groundY，让 NPC 脚部贴地
            int x = (int)Projectile.Center.X;
            int y = groundY;

            int index = NPC.NewNPC(new EntitySource_WorldEvent(), x, y, npcType);
            if (index < 0 || index >= Main.maxNPCs) {
                BoundVictorWhoAmI = -2;
                Projectile.netUpdate = true;
                return;
            }

            NPC v = Main.npc[index];
            //初始状态：完全透明、面向玩家方向（即 Facing 指向）、零速度
            v.alpha = 255;
            v.direction = v.spriteDirection = Facing >= 0f ? 1 : -1;
            v.velocity = Vector2.Zero;
            //立即把脚部锚定到地面
            v.Bottom = new Vector2(Projectile.Center.X, groundY);
            v.netUpdate = true;

            BoundVictorWhoAmI = index;
            Projectile.netUpdate = true;
        }

        /// <summary>每帧维护绑定 Victor 的可见性/朝向/位置锚定</summary>
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
                //生成前：保险起见若 NPC 已存在则保持隐藏（生成由 SpawnVictorOnServer 触发）
                return;
            }

            //出场曲线：StableEnd→EmergeEnd 内 alpha 255→0
            int emergeAge = age - StableEnd;
            int emergeSpan = EmergeEnd - StableEnd;
            float emergeT = MathHelper.Clamp(emergeAge / (float)emergeSpan, 0f, 1f);
            //缓出，前段更暗后段快显
            float emergeEase = 1f - (1f - emergeT) * (1f - emergeT);
            int targetAlpha = (int)MathHelper.Lerp(255f, 0f, emergeEase);
            //逐帧逼近，避免突跳
            v.alpha = (int)MathHelper.Lerp(v.alpha, targetAlpha, 0.35f);

            //出场锚定：Victor 从门口"踏出"半步，脚部紧贴 portal 下沿（=地面）
            float halfH = BaseHalfHeight * Scale;
            float groundY = Projectile.Center.Y + halfH;
            float walkOff = MathHelper.Lerp(0f, 22f, emergeEase) * Facing;

            v.Bottom = new Vector2(Projectile.Center.X + walkOff, groundY);
            v.velocity = Vector2.Zero;
            v.direction = v.spriteDirection = Facing >= 0f ? 1 : -1;
            //完全出场后释放，让原版 Passive AI 自然接管
            if (emergeT >= 1f && v.alpha <= 4) {
                v.alpha = 0;
            }
        }

        /// <summary>客户端/单机：每隔几帧吐一些故障粒子，加强乱码氛围</summary>
        private void SpawnAmbientParticles(int age) {
            if (VaultUtils.isServer) return;

            //频率：撕开期最密，稳定期适中，坍塌期一次性爆发
            int interval;
            if (age < TearEnd) interval = 1;
            else if (age < StableEnd) interval = 3;
            else if (age < EmergeEnd) interval = 4;
            else if (age < CollapseStart) interval = 6;
            else interval = 2;

            if (Projectile.timeLeft % interval != 0) {
                return;
            }

            //椭圆边缘上随机点吐粒子（向外侧抛出）
            float halfW = BaseHalfWidth * Scale;
            float halfH = halfW * AspectRatio;

            int count = age < TearEnd ? Main.rand.Next(2, 4)
                : age >= CollapseStart ? Main.rand.Next(3, 6)
                : Main.rand.Next(1, 3);

            for (int i = 0; i < count; i++) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                //撕开/坍塌瞬间扩散到门外，稳定期沿门口贴边
                float radial = age < TearEnd || age >= CollapseStart
                    ? Main.rand.NextFloat(0.9f, 1.3f)
                    : Main.rand.NextFloat(0.75f, 1.05f);
                Vector2 ringPos = new Vector2(MathF.Cos(ang) * halfW, MathF.Sin(ang) * halfH) * radial;
                Vector2 spawn = Projectile.Center + ringPos;
                Vector2 outward = (ringPos.SafeNormalize(Vector2.UnitX)) * Main.rand.NextFloat(1.5f, 4.5f);

                //撕开期向外飞，坍塌期反向被吸进去
                if (age >= CollapseStart) {
                    outward = -outward * 1.4f;
                }

                float scl = Main.rand.NextFloat(0.6f, 1.6f);
                int life = Main.rand.Next(20, 40);
                PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, outward, Color.White, scl).Configure(life);
            }

            //出场闪光帧：从中心 burst 一次密集粒子
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

        private void PlaySfxTear() {
            if (VaultUtils.isServer) return;
            //撕开瞬间，玻璃碎+电流
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.35f, Pitch = -0.25f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.ShortCircuit with { Volume = 0.55f, Pitch = 0.15f }, Projectile.Center);
        }
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

            //着色器缺失时回退到简易绘制，保证不出空场
            Effect shader = EffectLoader.VictorCyberPortal?.Value;
            if (shader == null || CWRAsset.Placeholder_White?.Value == null
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
                //撕开使用 ease-out 弹起，让"门"先快速张开再微微回弹
                return 1f - MathF.Pow(1f - t, 2.4f);
            }
            if (age < CollapseStart) {
                //稳定期 1，呼吸 ±0.04
                float t = (age - TearEnd) * 0.08f;
                return 1f + 0.04f * MathF.Sin(t);
            }
            //坍塌期：先线性回到 1，由 collapse 接管收缩
            return 1f - (age - CollapseStart) / (float)(TotalLife - CollapseStart) * 0.15f;
        }

        private float ComputeEmergePulse(int age) {
            //在 StableEnd 出现一次强闪，然后衰减
            int diff = age - StableEnd;
            if (diff < 0 || diff > 40) return 0f;
            //尖峰在 0 帧，衰减约 25 帧
            return MathF.Exp(-diff / 12f);
        }

        private float ComputeCollapseT(int age) {
            if (age < CollapseStart) return 0f;
            return MathHelper.Clamp((age - CollapseStart) / (float)(TotalLife - CollapseStart), 0f, 1f);
        }

        private void DrawShaderPortal(Effect shader, float openProg, float emergePulse, float collapseT) {
            Texture2D canvas = CWRAsset.Placeholder_White.Value;
            Texture2D noise = CWRAsset.Extra_193.Value;

            float halfW = BaseHalfWidth * Scale;
            float halfH = halfW * AspectRatio;
            //画布往外多 20% 给撕裂边缘留余量
            float quadW = halfW * 2.45f;
            float quadH = halfH * 2.45f;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + visualSeed * 12f);
            shader.Parameters["openProgress"]?.SetValue(MathHelper.Clamp(openProg, 0f, 1.1f));
            shader.Parameters["emergePulse"]?.SetValue(MathHelper.Clamp(emergePulse, 0f, 1f));
            shader.Parameters["collapse"]?.SetValue(MathHelper.Clamp(collapseT, 0f, 1f));
            shader.Parameters["seed"]?.SetValue(visualSeed);
            shader.Parameters["portalSize"]?.SetValue(new Vector2(halfW, halfH));
            shader.Parameters["facing"]?.SetValue(Facing);

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

        /// <summary>着色器缺失兜底：以纯色椭圆 + 多层光斑表示传送门</summary>
        private void DrawFallback(float openProg, float emergePulse, float collapseT) {
            Texture2D px = TextureAssets.MagicPixel.Value;
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
            //传送门在 NPC 之前绘制（让 Victor 走出时压在传送门上）
            behindNPCs.Add(index);
        }
    }
}
