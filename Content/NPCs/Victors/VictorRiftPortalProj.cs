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

namespace CalamityOverhaul.Content.NPCs.Victors
{
    /// <summary>
    /// Victor 出场门；<see cref="VictorPortalSpawner"/> 主端生成，StableEnd 主端 NewNPC
    /// <br/>ai0=facing±1 ai1=Victor whoAmI(主写客读) ai2=尺寸缩放
    /// <br/>演出七拍：前兆→撕开→稳定→浮现→停驻→收口→余韵，全部是 timeLeft 的确定函数，各端一致
    /// </summary>
    internal class VictorRiftPortalProj : ModProjectile, IWarpDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //---- 时间轴（帧） ----
        /// <summary>总生命，与阶段帧同步</summary>
        public const int TotalLife = 330;
        /// <summary>0..HeraldEnd 前兆：微粒汇聚 + 发丝竖缝明灭，末 3 帧静默空拍</summary>
        public const int HeraldEnd = 72;
        /// <summary>HeraldEnd..SnapSettleEnd 撕开：6 帧冲到 1.14 过冲，其余回稳 1.0</summary>
        public const int SnapSettleEnd = 92;
        /// <summary>SnapSettleEnd..StableEnd 稳定呼吸；StableEnd 主端生成 NPC</summary>
        public const int StableEnd = 150;
        /// <summary>StableEnd..EmergeEnd 剪影浮现踏出</summary>
        public const int EmergeEnd = 214;
        /// <summary>EmergeEnd..HoldEnd 停驻拍，尾段断电闪烁；此后收口</summary>
        public const int HoldEnd = 252;
        /// <summary>HoldEnd..CloseEnd 门体合拢回竖缝；其后余晖+烬屑到 TotalLife</summary>
        public const int CloseEnd = 266;

        //---- 尺寸 ----
        /// <summary>椭圆宽轴半径 px</summary>
        public const float BaseHalfWidth = 80f;
        /// <summary>高宽比，&gt;1 偏竖</summary>
        public const float AspectRatio = 1.55f;
        /// <summary>高轴半径，Spawner 用下沿贴地</summary>
        public const float BaseHalfHeight = BaseHalfWidth * AspectRatio;
        /// <summary>视觉横向收窄，造型偏"撕开的口"</summary>
        private const float VisualWidthMul = 0.85f;
        /// <summary>quad 半轴 = 门半轴×最大过冲 + 辉光余量，保证辉光在画布内解析归零</summary>
        private const float MaxOvershoot = 1.15f;
        /// <summary>门缘外的辉光余量 px</summary>
        private const float GlowMarginPx = 64f;

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

            PlayBeatSounds(age);

            //主端 StableEnd 生成并同步 whoAmI
            if (age == StableEnd && Main.netMode != NetmodeID.MultiplayerClient && BoundVictorWhoAmI < 0) {
                SpawnVictorOnServer();
            }

            UpdateBoundVictor(age);
            SpawnAmbientParticles(age);
            EmitLight(age);
        }

        /// <summary>节拍音效，全部低音量单发</summary>
        private void PlayBeatSounds(int age) {
            if (VaultUtils.isServer) {
                return;
            }
            if (age == 2) {
                //前兆：扫描式低鸣
                SoundEngine.PlaySound(CWRSound.Scanning with { Volume = 0.30f, Pitch = -0.15f }, Projectile.Center);
            }
            else if (age == HeraldEnd) {
                //撕开：门开 + 轻电弧
                SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen with { Volume = 0.45f, Pitch = 0.10f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.28f, Pitch = -0.20f }, Projectile.Center);
            }
            else if (age == StableEnd) {
                //浮现增辉
                SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.45f, Pitch = -0.05f }, Projectile.Center);
            }
            else if (age == HoldEnd) {
                //收口
                SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.50f, Pitch = -0.40f }, Projectile.Center);
                SoundEngine.PlaySound(CWRSound.Faultrelease with { Volume = 0.45f, Pitch = 0.10f }, Projectile.Center);
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

        /// <summary>浮现期锚定位姿；停驻结束(HoldEnd)后交还原版 AI</summary>
        private void UpdateBoundVictor(int age) {
            int who = BoundVictorWhoAmI;
            if (who < 0 || who >= Main.maxNPCs) {
                return;
            }
            NPC v = Main.npc[who];
            if (!v.active || v.type != ModContent.NPCType<Victor>()) {
                return;
            }

            if (age < StableEnd || age > HoldEnd) {
                return;
            }

            //StableEnd→EmergeEnd alpha 255→0，剪影表现见 Victor.PreDraw
            float emergeT = MathHelper.Clamp((age - StableEnd) / (float)(EmergeEnd - StableEnd), 0f, 1f);
            float emergeEase = 1f - (1f - emergeT) * (1f - emergeT);
            int targetAlpha = (int)MathHelper.Lerp(255f, 0f, emergeEase);
            //头几帧硬设：客户端 NPC alpha 不随生成包同步，从 0 起 lerp 会闪现一瞬
            if (age - StableEnd <= 8) {
                v.alpha = targetAlpha;
            }
            else {
                v.alpha = (int)MathHelper.Lerp(v.alpha, targetAlpha, 0.35f);
            }

            //两步踏出：两段 smoothstep 接续，替代匀速平移
            float stride = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp(emergeT / 0.52f, 0f, 1f)) * 0.55f
                + MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((emergeT - 0.48f) / 0.52f, 0f, 1f)) * 0.45f;
            float walkOff = 24f * stride * Facing;

            float halfH = BaseHalfHeight * Scale;
            float groundY = Projectile.Center.Y + halfH;
            v.Bottom = new Vector2(Projectile.Center.X + walkOff, groundY);
            v.velocity = Vector2.Zero;
            v.direction = v.spriteDirection = Facing >= 0f ? 1 : -1;
            if (emergeT >= 1f && v.alpha <= 4) {
                v.alpha = 0;
            }
        }

        private void SpawnAmbientParticles(int age) {
            if (VaultUtils.isServer) {
                return;
            }

            float halfW = BaseHalfWidth * VisualWidthMul * Scale;
            float halfH = BaseHalfHeight * Scale;

            //——前兆：微粒向成缝点汇聚；末 3 帧静默，给撕开留空拍——
            if (age < HeraldEnd) {
                if (age >= HeraldEnd - 3 || age % 3 != 0) {
                    return;
                }
                int n = age > HeraldEnd / 2 ? 2 : 1;
                for (int i = 0; i < n; i++) {
                    float slitHalf = ComputeSlitLen(age) * halfH;
                    Vector2 target = Projectile.Center + new Vector2(0f, Main.rand.NextFloat(-slitHalf, slitHalf));
                    Vector2 spawn = target + Main.rand.NextVector2Unit() * Main.rand.NextFloat(110f, 190f) * Scale;
                    float speed = Main.rand.NextFloat(2.4f, 4.4f);
                    Vector2 vel = (target - spawn).SafeNormalize(Vector2.UnitX) * speed;
                    int life = (int)(Vector2.Distance(spawn, target) / speed) + Main.rand.Next(-4, 3);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, vel, Color.White,
                        Main.rand.NextFloat(0.35f, 0.8f)).Configure(Math.Clamp(life, 14, 50));
                }
                return;
            }

            //——撕开：环状爆散 + 地面暗尘——
            if (age == HeraldEnd) {
                for (int i = 0; i < 16; i++) {
                    float ang = MathHelper.TwoPi * i / 16f + Main.rand.NextFloat(-0.14f, 0.14f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2f, 6f);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(Projectile.Center, vel, Color.White,
                        Main.rand.NextFloat(0.7f, 1.5f)).Configure(Main.rand.Next(18, 36));
                }
                float dustY = Projectile.Center.Y + halfH;
                for (int i = 0; i < 6; i++) {
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 pos = new(Projectile.Center.X + side * Main.rand.NextFloat(10f, halfW), dustY - Main.rand.NextFloat(2f, 8f));
                    Vector2 vel = new(side * Main.rand.NextFloat(0.6f, 1.8f), -Main.rand.NextFloat(0.1f, 0.5f));
                    Dust d = Dust.NewDustPerfect(pos, DustID.Smoke, vel, 170, new Color(66, 34, 30), Main.rand.NextFloat(0.9f, 1.5f));
                    d.noGravity = false;
                }
                return;
            }

            //——收口：贴边吸入——
            if (age >= HoldEnd && age < CloseEnd) {
                if (age % 2 != 0) {
                    return;
                }
                int n = Main.rand.Next(3, 6);
                for (int i = 0; i < n; i++) {
                    float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                    float radial = Main.rand.NextFloat(0.9f, 1.3f);
                    Vector2 ringPos = new Vector2(MathF.Cos(ang) * halfW, MathF.Sin(ang) * halfH) * radial;
                    Vector2 vel = -ringPos.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(3f, 6.5f);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(Projectile.Center + ringPos, vel, Color.White,
                        Main.rand.NextFloat(0.5f, 1.1f)).Configure(Main.rand.Next(14, 26));
                }
                return;
            }

            //——余韵：竖缝残点带重力坠落，痕迹活得比门久——
            if (age >= CloseEnd) {
                if (age % 6 == 0 && age < TotalLife - 30) {
                    float slitHalf = ComputeSlitLen(age) * halfH;
                    Vector2 spawn = Projectile.Center + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-slitHalf, slitHalf));
                    Vector2 vel = new(Facing * Main.rand.NextFloat(-0.3f, 0.6f), Main.rand.NextFloat(0.2f, 0.9f));
                    PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, vel, Color.White,
                        Main.rand.NextFloat(0.3f, 0.65f)).Configure(Main.rand.Next(26, 44), 0.11f);
                }
                return;
            }

            //——稳定/浮现/停驻：裂缘剥落烬屑（带重力）+ 偶发短弧——
            if (age % 8 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = new(MathF.Cos(ang) * halfW, MathF.Sin(ang) * halfH);
                Vector2 spawn = Projectile.Center + rim * Main.rand.NextFloat(0.96f, 1.06f);
                Vector2 vel = rim.SafeNormalize(Vector2.UnitX) * Main.rand.NextFloat(0.4f, 1.2f);
                PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, vel, Color.White,
                    Main.rand.NextFloat(0.35f, 0.75f)).Configure(Main.rand.Next(30, 52), 0.10f);
            }
            if (age % 26 == 0 && Main.rand.NextBool()) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 rim = new(MathF.Cos(ang) * halfW, MathF.Sin(ang) * halfH);
                Vector2 spawn = Projectile.Center + rim;
                for (int i = 0; i < 3; i++) {
                    Vector2 vel = rim.SafeNormalize(Vector2.UnitX).RotatedByRandom(0.7f) * Main.rand.NextFloat(1.5f, 3f);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(spawn, vel, Color.White,
                        Main.rand.NextFloat(0.4f, 0.9f)).Configure(Main.rand.Next(12, 22));
                }
            }
            //——浮现爆点（克制）——
            if (age == StableEnd) {
                for (int i = 0; i < 14; i++) {
                    float ang = MathHelper.TwoPi * i / 14f + Main.rand.NextFloat(-0.12f, 0.12f);
                    Vector2 vel = ang.ToRotationVector2() * Main.rand.NextFloat(2.5f, 5.5f);
                    PRTLoader.NewParticle<PRT_BanishGlitch>(Projectile.Center, vel, Color.White,
                        Main.rand.NextFloat(0.7f, 1.4f)).Configure(Main.rand.Next(18, 34));
                }
            }
        }

        /// <summary>照亮周围地形，随供能/闪烁/余晖起伏</summary>
        private void EmitLight(int age) {
            float open = MathHelper.Clamp(ComputeOpen(age), 0f, 1f);
            float flick = 0.88f + 0.12f * MathF.Sin(age * 0.53f + visualSeed * 9f);
            float strength = open * ComputePower(age) * flick + ComputeSlit(age) * 0.35f + ComputeFlare(age) * 0.6f;
            Lighting.AddLight(Projectile.Center, new Vector3(0.85f, 0.18f, 0.10f) * MathHelper.Clamp(strength, 0f, 1.3f));
        }

        #region 演出曲线
        /// <summary>门张开度：6 帧冲到 1.14 过冲 → 回稳 1.0 → 呼吸 → 收口快速归零</summary>
        private float ComputeOpen(int age) {
            if (age < HeraldEnd) {
                return 0f;
            }
            if (age < HeraldEnd + 6) {
                float t = (age - HeraldEnd) / 6f;
                return MathF.Pow(t, 0.55f) * 1.14f;
            }
            if (age < SnapSettleEnd) {
                float t = (age - HeraldEnd - 6) / (float)(SnapSettleEnd - HeraldEnd - 6);
                float ease = t * t * (3f - 2f * t);
                return MathHelper.Lerp(1.14f, 1f, ease);
            }
            if (age < HoldEnd) {
                return 1f + 0.022f * MathF.Sin((age - SnapSettleEnd) * 0.075f + visualSeed * MathHelper.TwoPi);
            }
            if (age < CloseEnd) {
                float t = (age - HoldEnd) / (float)(CloseEnd - HoldEnd);
                return MathHelper.Lerp(1f, 0f, MathF.Sqrt(t));
            }
            return 0f;
        }

        /// <summary>竖缝亮度：前兆渐强（快闪由 shader 负责）→ 门开即熄 → 收口余晖指数衰减</summary>
        private float ComputeSlit(int age) {
            if (age < HeraldEnd) {
                float t = age / (float)HeraldEnd;
                return 0.25f + 0.75f * t * t;
            }
            if (age < HeraldEnd + 8) {
                return 1f - (age - HeraldEnd) / 8f;
            }
            if (age >= CloseEnd - 2) {
                float glow = 0.9f * MathF.Exp(-(age - CloseEnd + 2) / 22f);
                float endFade = 1f - MathHelper.Clamp((age - (TotalLife - 14)) / 12f, 0f, 1f);
                return glow * endFade;
            }
            return 0f;
        }

        /// <summary>竖缝半长占门半高比例</summary>
        private float ComputeSlitLen(int age) {
            if (age < HeraldEnd + 8) {
                float t = MathHelper.Clamp(age / (float)HeraldEnd, 0f, 1f);
                return MathHelper.Lerp(0.18f, 0.85f, 1f - (1f - t) * (1f - t));
            }
            float ct = MathHelper.Clamp((age - CloseEnd) / (float)(TotalLife - CloseEnd), 0f, 1f);
            return MathHelper.Lerp(0.80f, 0.30f, ct);
        }

        /// <summary>浮现增辉：预热 10 帧 → StableEnd 尖峰指数衰减 → 浮现期余辉</summary>
        private float ComputeFlare(int age) {
            float pre = 0f;
            if (age >= StableEnd - 10 && age < StableEnd) {
                pre = (age - (StableEnd - 10)) / 10f * 0.30f;
            }
            float spike = 0f;
            int diff = age - StableEnd;
            if (diff >= 0 && diff < 60) {
                spike = 0.85f * MathF.Exp(-diff / 14f);
            }
            float hold = age >= StableEnd && age < EmergeEnd ? 0.12f : 0f;
            return MathF.Max(pre, MathF.Max(spike, hold));
        }

        /// <summary>断电式供能骤降，停驻末尾三次，预告收口</summary>
        private float ComputePower(int age) {
            int d = age - HoldEnd;
            if (d >= -16 && d <= -13) return 0.30f;
            if (d >= -9 && d <= -8) return 0.55f;
            if (d >= -5 && d <= -1) return 0.18f;
            return 1f;
        }

        private float ComputeCollapseT(int age) {
            if (age < HoldEnd) {
                return 0f;
            }
            return MathHelper.Clamp((age - HoldEnd) / (float)(CloseEnd - HoldEnd), 0f, 1f);
        }

        /// <summary>白闪：撕开 2 帧 + 收口 1 帧</summary>
        private float ComputeFlash(int age) {
            if (age == HeraldEnd) return 0.85f;
            if (age == HeraldEnd + 1) return 0.45f;
            if (age == CloseEnd - 1) return 0.35f;
            return 0f;
        }
        #endregion

        #region 屏幕扭曲
        public bool CanDrawCustom() => false;
        //红黑门体避免蓝移色边
        public bool DontUseBlueshiftEffect() => true;
        public void DrawCustom(SpriteBatch spriteBatch) { }

        /// <summary>扭曲包络：前兆缓升→撕开顶峰→稳定低值→收口再峰→余晖归零</summary>
        private float WarpEnvelope(int age) {
            if (age < HeraldEnd) {
                return age / (float)HeraldEnd * 0.45f;
            }
            if (age < SnapSettleEnd) {
                return MathHelper.Lerp(1f, 0.55f, (age - HeraldEnd) / (float)(SnapSettleEnd - HeraldEnd));
            }
            if (age < HoldEnd) {
                return 0.5f + ComputeFlare(age) * 0.3f;
            }
            if (age < CloseEnd) {
                return MathHelper.Lerp(0.9f, 0.4f, ComputeCollapseT(age));
            }
            float t = MathHelper.Clamp((age - CloseEnd) / 30f, 0f, 1f);
            return 0.35f * (1f - t);
        }

        public void Warp() {
            float env = WarpEnvelope(AgeFrame);
            if (env <= 0.03f) {
                return;
            }
            float size = BaseHalfHeight * Scale * 2.6f;
            NeutronWarpHelper.DrawWarp(Projectile.Center, size, size, 0.30f, env, 0f, "GravitationalLens", 0.42f);
        }
        #endregion

        public override bool PreDraw(ref Color lightColor) {
            int age = AgeFrame;

            //着色器缺失走兜底
            Effect shader = EffectLoader.VictorCyberPortal?.Value;
            if (shader == null || VaultAsset.placeholder2?.Value == null
                || CWRAsset.PerlinNoise?.Value == null) {
                DrawFallback(age);
                return false;
            }

            DrawShaderPortal(shader, age);
            return false;
        }

        private void DrawShaderPortal(Effect shader, int age) {
            Texture2D canvas = VaultAsset.placeholder2.Value;
            Texture2D noise = CWRAsset.PerlinNoise.Value;

            float halfW = BaseHalfWidth * VisualWidthMul * Scale;
            float halfH = BaseHalfHeight * Scale;
            float quadX = halfW * MaxOvershoot + GlowMarginPx * Scale;
            float quadY = halfH * MaxOvershoot + GlowMarginPx * Scale;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + visualSeed * 12f);
            shader.Parameters["seed"]?.SetValue(visualSeed);
            shader.Parameters["openProgress"]?.SetValue(ComputeOpen(age));
            shader.Parameters["slit"]?.SetValue(ComputeSlit(age));
            shader.Parameters["slitLen"]?.SetValue(ComputeSlitLen(age));
            shader.Parameters["flare"]?.SetValue(ComputeFlare(age));
            shader.Parameters["collapse"]?.SetValue(ComputeCollapseT(age));
            shader.Parameters["uPower"]?.SetValue(ComputePower(age));
            shader.Parameters["uFlash"]?.SetValue(ComputeFlash(age));
            shader.Parameters["portalSize"]?.SetValue(new Vector2(halfW, halfH));
            shader.Parameters["quadSize"]?.SetValue(new Vector2(quadX, quadY));
            shader.Parameters["facing"]?.SetValue(Facing);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            //预乘输出走 AlphaBlend：暗体真正遮挡地形，发光成分低 alpha 近似加色
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.graphics.GraphicsDevice.Textures[1] = noise;
            Main.graphics.GraphicsDevice.SamplerStates[1] = SamplerState.LinearWrap;
            shader.CurrentTechnique.Passes[0].Apply();

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 scale = new(quadX * 2f / canvas.Width, quadY * 2f / canvas.Height);
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>着色器缺失兜底：竖缝线 + 暗椭圆近似 + 边光</summary>
        private void DrawFallback(int age) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null) {
                return;
            }
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Rectangle onePx = new(0, 0, 1, 1);
            float open = MathHelper.Clamp(ComputeOpen(age), 0f, 1f);
            float power = ComputePower(age);

            if (open > 0.02f) {
                float halfW = BaseHalfWidth * VisualWidthMul * Scale * open;
                float halfH = BaseHalfHeight * Scale * open;
                Color rim = new Color(255, 60, 35) * (0.65f * power);
                Main.spriteBatch.Draw(px, drawPos, onePx, rim, 0f, new Vector2(0.5f),
                    new Vector2(halfW * 2.15f, halfH * 2.15f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(px, drawPos, onePx, Color.Black * 0.92f, 0f, new Vector2(0.5f),
                    new Vector2(halfW * 1.9f, halfH * 1.9f), SpriteEffects.None, 0f);
                float pulse = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f + visualSeed * 6f);
                Main.spriteBatch.Draw(px, drawPos, onePx,
                    new Color(255, 110, 70) * (0.35f * pulse * power + ComputeFlare(age) * 0.5f), 0f,
                    new Vector2(0.5f), new Vector2(halfW * 0.5f, halfH * 0.5f), SpriteEffects.None, 0f);
            }

            float slitGlow = ComputeSlit(age);
            if (slitGlow > 0.02f) {
                float slitHalf = ComputeSlitLen(age) * BaseHalfHeight * Scale;
                Main.spriteBatch.Draw(px, drawPos, onePx, new Color(255, 200, 160) * (0.8f * slitGlow), 0f,
                    new Vector2(0.5f), new Vector2(2.5f, slitHalf * 2f), SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(px, drawPos, onePx, new Color(255, 90, 45) * (0.35f * slitGlow), 0f,
                    new Vector2(0.5f), new Vector2(14f, slitHalf * 2.1f), SpriteEffects.None, 0f);
            }
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs,
            List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI) {
            //Victor 走出压在门上
            behindNPCs.Add(index);
        }
    }
}
