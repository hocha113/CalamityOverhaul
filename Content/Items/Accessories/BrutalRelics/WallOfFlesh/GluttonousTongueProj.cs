using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.WallOfFlesh
{
    /// <summary>
    /// 饕餮之喉的血肉巨舌。ai[0]=目标whoAmI ai[1]=目标type(槽位复用校验)。
    /// 时间轴各端本地推进(固定拍长)：出舌14t → 咬合10t → 三口回卷32t → 收舌14t；
    /// 舌尖各端都贴住目标同步位置绘制，NPC 位移只在服务端书写并按节奏 netUpdate。
    /// Boss/多段体不可拖动：咬合与腐锯照常，回卷阶段读作"咬住拖不动"。
    /// 无伤害无判定，纯机制+演出体
    /// </summary>
    internal class GluttonousTongueProj : ModProjectile, IPrimitiveDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder2;

        #region 时间轴与常量
        /// <summary>出舌结束</summary>
        internal const int StrikeEnd = 14;
        /// <summary>咬合结束</summary>
        internal const int LatchEnd = 24;
        /// <summary>回卷结束(释放拍)</summary>
        internal const int ReelEnd = 56;
        /// <summary>收舌结束(消亡)</summary>
        internal const int RetractEnd = 70;
        /// <summary>喉口锚距玩家中心 px</summary>
        private const float MawRadius = 26f;
        /// <summary>释放点水平前距 px</summary>
        private const float FrontOffsetX = 100f;
        /// <summary>释放点垂直偏移 px</summary>
        private const float FrontOffsetY = -12f;
        /// <summary>顶点带画布半宽 px(着色器内容 ≤75%)</summary>
        private const float CanvasHalfWidth = 30f;
        /// <summary>顶点带采样数</summary>
        private const int Samples = 26;
        #endregion

        private ref float TargetIndex => ref Projectile.ai[0];
        private ref float TargetType => ref Projectile.ai[1];

        /// <summary>本端演出时钟</summary>
        private int timer;
        /// <summary>本端咬合一次性闩</summary>
        private bool latched;
        /// <summary>目标失效提前收舌(不再有释放拍)</summary>
        private bool whiffed;
        /// <summary>收舌起点(舌尖定格处)</summary>
        private Vector2 lastTipPos;
        /// <summary>本帧舌尖(AI 写、绘制读)</summary>
        private Vector2 drawTip;
        /// <summary>蠕动相位(累计值，方向随阶段反转：出舌向尖、回卷向根)</summary>
        private float peristalsis;
        /// <summary>服务端：拖拽路径起点(咬合处)</summary>
        private Vector2 grabAnchor;
        /// <summary>该目标可被拖动(纯函数，各端一致)</summary>
        private bool displaceable;
        /// <summary>回卷吞咽拍去重</summary>
        private int lastGulp = -1;

        private Player Owner => Main.player[Projectile.owner];

        /// <summary>校验槽位复用后的目标：索引+类型双验</summary>
        private NPC Target {
            get {
                int idx = (int)TargetIndex;
                if (idx < 0 || idx >= Main.maxNPCs) {
                    return null;
                }
                NPC npc = Main.npc[idx];
                if (!npc.active || npc.type != (int)TargetType || npc.friendly) {
                    return null;
                }
                return npc;
            }
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = RetractEnd + 20;
        }

        //方向数据不入位移积分
        public override bool ShouldUpdatePosition() => false;

        #region AI
        public override void AI() {
            Player owner = Owner;
            if (!owner.Alives()) {
                Projectile.Kill();
                return;
            }
            Projectile.Center = owner.Center;
            Projectile.timeLeft = 2;
            timer++;

            //首帧初始化舌尖方向，避免 RootPos 以默认(0,0)推算朝向
            if (timer == 1) {
                NPC first = Target;
                Vector2 dir0 = first != null
                    ? (first.Center - owner.Center).SafeNormalize(Vector2.UnitX * owner.direction)
                    : Vector2.UnitX * owner.direction;
                drawTip = owner.Center + dir0 * MawRadius;
            }

            if (timer > RetractEnd) {
                Projectile.Kill();
                return;
            }

            NPC target = Target;
            //目标失效(死亡/槽位复用/离场)且尚未进入收舌 → 断舌
            if (!whiffed && target == null && timer <= ReelEnd) {
                BeginRetract();
                target = null;
            }

            if (timer <= StrikeEnd) {
                UpdateStrike(owner, target);
            }
            else if (!whiffed && timer <= ReelEnd) {
                UpdateLatchAndReel(owner, target);
            }
            else {
                UpdateRetract(owner);
            }

            //血舌照明
            if (!VaultUtils.isServer) {
                Vector2 root = RootPos(owner);
                for (int i = 0; i < 5; i++) {
                    Lighting.AddLight(Vector2.Lerp(root, drawTip, i / 4f),
                        WofMotionFX.BloodHot.ToVector3() * 0.3f);
                }
            }
        }

        /// <summary>出舌：贝塞尔尖端以过冲缓动扑向目标，蠕动波涌向舌尖</summary>
        private void UpdateStrike(Player owner, NPC target) {
            peristalsis += 0.22f;
            if (timer == 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.6f, Volume = 1.1f }, owner.Center);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.2f, Volume = 0.8f }, owner.Center);
                WofMotionFX.CameraPunch(owner.Center, 2.2f, 8, "GluttonousLash",
                    target != null ? target.Center - owner.Center : Vector2.UnitX);
            }
            if (target == null) {
                return;
            }
            float t = timer / (float)StrikeEnd;
            drawTip = Vector2.Lerp(RootPos(owner), target.Center, EaseOutBack(t));
            //舌尖甩涎
            if (!VaultUtils.isServer && Main.rand.NextBool(3)) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(drawTip,
                    Main.rand.NextVector2Circular(3f, 3f), WofMotionFX.BloodMid,
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(12, 22), 0.32f);
            }
        }

        /// <summary>咬合与回卷：舌尖贴住目标；服务端权威书写拖拽位移，三口吞咽节奏</summary>
        private void UpdateLatchAndReel(Player owner, NPC target) {
            drawTip = target.Center;

            //咬合一次性拍：腐锯上身 + 可拖性判定 + 全端咬合演出
            if (!latched) {
                latched = true;
                displaceable = CanDisplace(target);
                grabAnchor = target.Center;
                if (!VaultUtils.isClient) {
                    target.AddBuff(ModContent.BuffType<RotsawRendDebuff>(), RotsawRendDebuff.Duration);
                }
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.45f, Volume = 1.1f }, target.Center);
                    SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = -0.6f, Volume = 1f }, target.Center);
                    WofMotionFX.SpawnBloodBurst(target.Center, 0.9f,
                        (owner.Center - target.Center).SafeNormalize(Vector2.UnitX));
                    WofMotionFX.CameraPunch(target.Center, 3.2f, 10, "GluttonousLatch");
                }
            }

            //各端冻结目标自驱 AI(时间戳自然过期，无需清理)
            target.GetGlobalNPC<GluttonousThroatGlobalNPC>().DragHoldUntil = Main.GameUpdateCount + 2;

            if (timer <= LatchEnd) {
                peristalsis += 0.06f;
                UpdateLatchHold(target);
            }
            else {
                peristalsis -= 0.16f;
                UpdateReelDrag(owner, target);
            }

            //释放拍：落点定格 + 处刑标记 + 视网膜锁定入场
            if (timer == ReelEnd) {
                ReleaseBeat(owner, target);
            }
        }

        /// <summary>咬合定身：服务端把目标钉在锚点上微颤(挣扎读感)</summary>
        private void UpdateLatchHold(NPC target) {
            if (VaultUtils.isClient || !displaceable) {
                return;
            }
            float judder = MathF.Sin(timer * 2.3f + Projectile.identity * 1.7f) * 3f;
            Vector2 want = grabAnchor + new Vector2(judder, MathF.Sin(timer * 3.1f) * 2f);
            target.velocity = want - target.Center;
            target.Center = want;
            if (timer % 4 == 0) {
                target.netUpdate = true;
            }
        }

        /// <summary>回卷拖拽：三口吞咽的非匀速路径，弧线摆送+垂直蠕颤，幅度随进度收敛</summary>
        private void UpdateReelDrag(Player owner, NPC target) {
            float u = (timer - LatchEnd) / (float)(ReelEnd - LatchEnd);
            //吞咽拍演出(全端)：跨入每一口时的湿响与血沫
            int gulp = GulpIndex(u);
            if (gulp != lastGulp) {
                lastGulp = gulp;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit13 with { Pitch = -0.3f + gulp * 0.18f, Volume = 0.95f }, target.Center);
                    SoundEngine.PlaySound(SoundID.Zombie10 with { Pitch = -0.4f, Volume = 0.45f }, owner.Center);
                    WofMotionFX.SpawnBloodBurst(target.Center, 0.45f,
                        (owner.Center - target.Center).SafeNormalize(Vector2.UnitX));
                }
            }
            //沿舌回流的血珠(吞咽语法：向喉汇聚)
            if (!VaultUtils.isServer && timer % 3 == 0) {
                float along = Main.rand.NextFloat(0.2f, 0.9f);
                Vector2 pos = Vector2.Lerp(RootPos(owner), target.Center, along);
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(pos,
                    (RootPos(owner) - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 4.5f),
                    WofMotionFX.BloodMid, Main.rand.NextFloat(0.5f, 0.9f))?.Configure(Main.rand.Next(10, 18), 0.24f);
            }

            //服务端权威位移
            if (VaultUtils.isClient || !displaceable) {
                return;
            }
            Vector2 front = FrontPoint(owner);
            float p = GulpEase(u);
            Vector2 pullDir = (front - grabAnchor).SafeNormalize(Vector2.UnitX);
            Vector2 perp = pullDir.RotatedBy(MathHelper.PiOver2);
            float pathLen = Vector2.Distance(grabAnchor, front);
            //弧线摆送：整段呈半月弧，方向由弹幕身份决定(各端一致的确定性)
            float arcSign = Projectile.identity % 2 == 0 ? 1f : -1f;
            float arc = MathF.Sin(p * MathHelper.Pi) * MathF.Min(pathLen * 0.16f, 130f) * arcSign;
            //垂直蠕颤：确定性正弦叠加，靠近面前收敛归零
            float judder = (MathF.Sin(timer * 1.9f + Projectile.identity) * 0.6f
                + MathF.Sin(timer * 3.7f + Projectile.identity * 2.3f) * 0.4f) * 14f * (1f - p);
            Vector2 want = Vector2.Lerp(grabAnchor, front, p) + perp * (arc + judder);
            target.velocity = want - target.Center;
            target.Center = want;
            if (timer % 4 == 0) {
                target.netUpdate = true;
            }
        }

        /// <summary>释放拍：目标定格在面前，处刑窗口开启(各端本地推得，拥有者结算翻倍)</summary>
        private void ReleaseBeat(Player owner, NPC target) {
            lastTipPos = target.Center;

            GluttonousThroatGlobalNPC mark = target.GetGlobalNPC<GluttonousThroatGlobalNPC>();
            mark.MarkOwner = Projectile.owner;
            mark.MarkUntil = Main.GameUpdateCount + GluttonousThroatPlayer.MarkWindow;
            mark.MarkConsumed = false;

            if (!VaultUtils.isClient && displaceable) {
                Vector2 front = FrontPoint(owner);
                target.Center = front;
                target.velocity = (front - owner.Center).SafeNormalize(Vector2.UnitX) * 2f;
                target.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                //视网膜锁定开环：标记准星由 GluttonousRetinaRender 逐帧接管
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.35f, Volume = 0.8f }, target.Center);
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.1f, Volume = 0.9f }, target.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(target.Center, Vector2.Zero,
                    WofMotionFX.BloodHot, 0.1f)?.Configure(0.1f, 0.9f, 18);
                WofMotionFX.CameraPunch(target.Center, 2.6f, 9, "GluttonousRelease");
            }
        }

        /// <summary>收舌：舌尖回吞进喉，蠕动急促回流，尾段噪声撕散。
        /// 断舌路径下 timer 已被跳到 ReelEnd+1，同样按剩余时长回收</summary>
        private void UpdateRetract(Player owner) {
            peristalsis -= 0.28f;
            float rt = MathHelper.Clamp((timer - ReelEnd) / (float)(RetractEnd - ReelEnd), 0f, 1f);
            drawTip = Vector2.Lerp(lastTipPos, RootPos(owner), EaseInCubic(rt));

            if (timer == ReelEnd + 1 && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit18 with { Pitch = 0.3f, Volume = 0.8f }, owner.Center);
            }
        }

        /// <summary>断舌：跳到收舌段，释放拍作废</summary>
        private void BeginRetract() {
            whiffed = true;
            lastTipPos = drawTip;
            if (timer <= ReelEnd) {
                timer = ReelEnd + 1;
            }
        }
        #endregion

        #region 几何与工具
        /// <summary>喉口锚点：玩家中心朝舌尖方向前伸</summary>
        private Vector2 RootPos(Player owner) {
            Vector2 dir = (drawTip - owner.Center).SafeNormalize(Vector2.UnitX * owner.direction);
            return owner.Center + dir * MawRadius;
        }

        /// <summary>释放点：玩家面前(朝向侧)</summary>
        private static Vector2 FrontPoint(Player owner) {
            return owner.Center + new Vector2(owner.direction * FrontOffsetX, FrontOffsetY);
        }

        /// <summary>
        /// 可拖性(纯函数，各端一致)：Boss、计名Boss、多段共血体、蠕虫类不位移，
        /// 咬合与腐锯照常("锚咬不拖")
        /// </summary>
        private static bool CanDisplace(NPC npc) {
            return !npc.boss && !NPCID.Sets.ShouldBeCountedAsBoss[npc.type]
                && npc.realLife < 0 && npc.aiStyle != NPCAIStyleID.Worm
                && npc.type != NPCID.TargetDummy;
        }

        /// <summary>三口吞咽缓动：每口前 30% 顿住、后 70% 平滑拉近，禁匀速</summary>
        private static float GulpEase(float u) {
            u = MathHelper.Clamp(u, 0f, 1f);
            float scaled = u * 3f;
            int seg = Math.Min((int)scaled, 2);
            float frac = scaled - seg;
            float pull = MathHelper.SmoothStep(0f, 1f, MathHelper.Clamp((frac - 0.3f) / 0.7f, 0f, 1f));
            return (seg + pull) / 3f;
        }

        /// <summary>当前吞咽口序(0..2)</summary>
        private static int GulpIndex(float u) {
            return Math.Min((int)(MathHelper.Clamp(u, 0f, 0.999f) * 3f), 2);
        }

        private static float EaseOutBack(float t) {
            const float C1 = 1.70158f;
            const float C3 = C1 + 1f;
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f + C3 * MathF.Pow(t - 1f, 3f) + C1 * MathF.Pow(t - 1f, 2f);
        }

        private static float EaseInCubic(float t) => t * t * t;
        #endregion

        #region 绘制
        public override bool PreDraw(ref Color lightColor) => false;

        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.gameMenu) {
                return;
            }
            Player owner = Owner;
            if (!owner.active || timer < 1) {
                return;
            }
            Vector2 root = RootPos(owner);
            if (!WofMotionFX.OnScreen(root, 260f) && !WofMotionFX.OnScreen(drawTip, 260f)) {
                return;
            }

            DrawMaw(owner, root);
            DrawTongueStrip(owner, root);
            DrawTipHead();
        }

        /// <summary>喉口肉涡：复用 WofMawVortex，开阖由时间轴驱动，吞咽拍抽吸增速</summary>
        private void DrawMaw(Player owner, Vector2 root) {
            Effect effect = EffectLoader.WofMawVortex?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }

            //开阖包络：前 8t 张开、末 8t 闭合
            float open = MathHelper.Clamp(timer / 8f, 0f, 1f)
                * MathHelper.Clamp((RetractEnd - timer) / 8f, 0f, 1f);
            if (open <= 0.02f) {
                return;
            }
            //回卷期抽吸增强，吞咽口内脉冲
            float suck = 0.35f;
            if (timer > LatchEnd && timer <= ReelEnd) {
                float u = (timer - LatchEnd) / (float)(ReelEnd - LatchEnd);
                suck = 0.6f + 0.4f * MathF.Sin(u * 3f * MathHelper.Pi);
            }

            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly + Projectile.identity * 0.37f);
            effect.Parameters["uProgress"]?.SetValue(open);
            effect.Parameters["uIntensity"]?.SetValue(0.95f);
            effect.Parameters["uSuck"]?.SetValue(MathHelper.Clamp(suck, 0f, 1f));

            float size = 150f * (0.6f + 0.4f * open);
            Vector2 anchor = root + (root - owner.Center).SafeNormalize(Vector2.UnitX) * 6f;

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;
            Texture2D quad = VaultAsset.placeholder2.Value;
            sb.Draw(quad, anchor - Main.screenPosition, null, Color.White, 0f,
                quad.Size() / 2f, size / quad.Width, SpriteEffects.None, 0f);
            sb.End();
        }

        /// <summary>舌体：多节贝塞尔顶点带 + BRelicThroatTongue 蠕动着色</summary>
        private void DrawTongueStrip(Player owner, Vector2 root) {
            Effect effect = EffectLoader.BRelicThroatTongue?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (effect == null || noise == null) {
                return;
            }
            float len = Vector2.Distance(root, drawTip);
            if (len < 12f) {
                return;
            }

            //阶段参数：松紧/蠕胀/尖端撕散
            float taut, engorge, erode;
            if (timer <= StrikeEnd) {
                taut = 0.5f; engorge = 0.3f; erode = 0.15f;
            }
            else if (timer <= LatchEnd) {
                taut = 0.92f; engorge = 0.4f; erode = 0f;
            }
            else if (timer <= ReelEnd && !whiffed) {
                float u = (timer - LatchEnd) / (float)(ReelEnd - LatchEnd);
                float gulpPulse = MathF.Sin(MathHelper.Clamp(u * 3f % 1f, 0f, 1f) * MathHelper.Pi);
                taut = 0.95f; engorge = 0.5f + 0.45f * gulpPulse; erode = 0f;
            }
            else {
                float rt = MathHelper.Clamp((timer - ReelEnd) / (float)(RetractEnd - ReelEnd), 0f, 1f);
                taut = 0.15f; engorge = 0.3f; erode = 0.3f + 0.6f * rt;
            }

            //贝塞尔控制点：垂线摆动幅度随松弛度走
            Vector2 dir = (drawTip - root) / len;
            Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
            float slack = 1f - taut;
            float amp = MathF.Min(len * 0.13f, 90f) * (0.25f + slack);
            float seedF = Projectile.identity * 0.61f % 1f;
            float w1 = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.1f + seedF * 9f) * amp;
            float w2 = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.7f + seedF * 14f + 2.1f) * amp * 0.8f;
            Vector2 c1 = root + dir * (len * 0.34f) + perp * w1;
            Vector2 c2 = root + dir * (len * 0.68f) + perp * w2;

            //出生前 3t 全局淡入，防首帧硬闪
            float fade = MathHelper.Clamp(timer / 3f, 0f, 1f);

            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[Samples * 2];
            Vector2 prev = root;
            for (int i = 0; i < Samples; i++) {
                float t = i / (float)(Samples - 1);
                Vector2 pos = CubicBezier(root, c1, c2, drawTip, t);
                Vector2 tangent = i == 0
                    ? CubicBezier(root, c1, c2, drawTip, t + 0.04f) - pos
                    : pos - prev;
                Vector2 n = tangent.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
                prev = pos;
                //画布几何轻收尖，细节轮廓交给着色器
                float halfW = CanvasHalfWidth * MathHelper.Lerp(1f, 0.82f, t);
                Color col = Color.White * fade;
                verts[i * 2] = new VertexPositionColorTexture((pos + n * halfW).ToVector3(), col, new Vector2(t, 0f));
                verts[i * 2 + 1] = new VertexPositionColorTexture((pos - n * halfW).ToVector3(), col, new Vector2(t, 1f));
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            BlendState origBlend = device.BlendState;
            RasterizerState origRaster = device.RasterizerState;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullNone;

            effect.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["seed"]?.SetValue(seedF);
            effect.Parameters["fadeAlpha"]?.SetValue(1f);
            effect.Parameters["uQuadLen"]?.SetValue(len);
            effect.Parameters["uFlow"]?.SetValue(peristalsis);
            effect.Parameters["uEngorge"]?.SetValue(engorge);
            effect.Parameters["uTaut"]?.SetValue(taut);
            effect.Parameters["uTipErode"]?.SetValue(erode);
            device.Textures[1] = noise;
            device.SamplerStates[1] = SamplerState.LinearWrap;
            foreach (EffectPass pass in effect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, Samples * 2 - 2);
            }

            device.BlendState = origBlend;
            device.RasterizerState = origRaster;
        }

        /// <summary>舌尖肉锤与缠体环：暗核+湿高光；咬合期在受害者身上绕缠两圈舌肉</summary>
        private void DrawTipHead() {
            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Texture2D drop = CWRAsset.Extra_98.Value;
            Vector2 tipScreen = drawTip - Main.screenPosition;
            float tipRot = (drawTip - Projectile.Center).SafeNormalize(Vector2.UnitX).ToRotation() + MathHelper.PiOver2;
            //咬合口收张节律
            float biteFlex = 1f + 0.1f * MathF.Sin(timer * 0.9f);
            sb.Draw(drop, tipScreen, null, WofMotionFX.BloodDark, tipRot,
                drop.Size() / 2f, new Vector2(0.78f, 0.92f) * biteFlex, SpriteEffects.None, 0f);
            sb.Draw(drop, tipScreen - new Vector2(2f, 3f), null, WofMotionFX.BloodHot * 0.7f, tipRot,
                drop.Size() / 2f, new Vector2(0.46f, 0.6f) * biteFlex, SpriteEffects.None, 0f);

            //缠体环(咬合到释放)：舌肉在目标身上绕两圈，读作缠住而非贴着
            NPC target = Target;
            if (!whiffed && latched && timer <= ReelEnd && target != null) {
                Texture2D chain = TextureAssets.Chain12.Value;
                for (int ring = 0; ring < 2; ring++) {
                    float radius = 20f + ring * 10f;
                    float spin = Main.GlobalTimeWrappedHourly * (ring == 0 ? 1.7f : -1.3f);
                    const int RingSegs = 9;
                    for (int i = 0; i < RingSegs; i++) {
                        float ang = MathHelper.TwoPi * i / RingSegs + spin;
                        Vector2 pos = target.Center + ang.ToRotationVector2() * new Vector2(radius, radius * 0.62f);
                        Color light = Lighting.GetColor((int)pos.X / 16, (int)(pos.Y / 16f));
                        sb.Draw(chain, pos - Main.screenPosition, null,
                            Color.Lerp(light, WofMotionFX.BloodDark, 0.5f), ang + MathHelper.PiOver2,
                            chain.Size() / 2f, 0.68f, SpriteEffects.None, 0f);
                    }
                }
                //勒痕血光(A=0 加色技法，AlphaBlend 批内合法)
                Texture2D glow = CWRAsset.SoftGlow.Value;
                sb.Draw(glow, target.Center - Main.screenPosition, null,
                    new Color(255, 55, 40, 0) * 0.35f, 0f, glow.Size() / 2f, 0.72f, SpriteEffects.None, 0f);
            }

            sb.End();
        }

        /// <summary>三次贝塞尔</summary>
        private static Vector2 CubicBezier(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float it = 1f - t;
            return it * it * it * p0 + 3f * it * it * t * p1 + 3f * it * t * t * p2 + t * t * t * p3;
        }
        #endregion
    }
}
