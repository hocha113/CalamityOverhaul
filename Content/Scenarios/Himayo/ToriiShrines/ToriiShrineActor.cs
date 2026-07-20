using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
using InnoVault.Cinematics;
using InnoVault.Models3D.Runtime;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Scenarios.Himayo.ToriiShrines
{
    /// <summary>
    /// 鸟居Actor：负责3D鸟居模型的每帧提交、鸟居下插地鬼切的绘制与拔刀交互提示。<br/>
    /// 逻辑锚点 <see cref="Actor.Position"/> 约定为鸟居正下方的地表中心（非左上角），
    /// 所有绘制/粒子/光照都相对该锚点展开。<br/>
    /// 本地玩家右键先走拔刀仪式（蓄势→拔离→归弧到手，运镜见 <see cref="ToriiPullCutscene"/>），
    /// 到手后进入退场演出（黄昏渐入→原地化樱消散，见 <see cref="ToriiDusk"/>），
    /// 纯客户端视觉：Actor 世界侧仍存活，未拔刀的玩家看到的鸟居原样不动
    /// </summary>
    internal class ToriiShrineActor : Actor
    {
        //模型包围盒半高约64.7单位(FlipY后上下对称)，pivot在包围盒中心：
        //把pivot抬到地面上方 半高*缩放 处，鸟居柱脚正好落在锚点地表
        private const float ModelBottomOffset = 64.7f;
        /// <summary>鸟居整体缩放；模型原始尺寸约142x129单位，2倍后约18x16格</summary>
        private const float ModelScale = 2f;
        //轻微的Y轴偏转让鸟居露出一点侧面进深，避免看起来像一张平面贴纸
        private const float ModelYaw = 0.32f;

        //插地鬼切的整体缩放：物品贴图原尺寸作静物偏大，压过鸟居基座的视觉层级
        private const float SwordScale = 0.75f;
        //刀的中心离地高度：贴图对角半长约65px×缩放≈49px，刀尖入土约14px
        internal const float SwordCenterHeight = 35f;
        //刀身旋转：原贴图刀尖朝右上(-45°)，转到刀尖朝下再往回带一点倾角
        private const float SwordRotation = MathHelper.PiOver4 * 3f - 0.26f;
        //刀尖在贴图空间的朝向为-45°，叠加插地旋转后的世界朝向；拔离方向取其反向
        private const float SwordTipAngle = SwordRotation - MathHelper.PiOver4;

        private float glowTimer;
        private int motePrtTimer;
        private int sparklePrtTimer;

        /// <summary>刀的中心点（世界坐标）</summary>
        public Vector2 SwordAnchor => Position + new Vector2(0f, -SwordCenterHeight);

        #region 退场状态
        private enum DeparturePhase
        {
            None,
            /// <summary>拔刀仪式：蓄势→拔离→归弧到手（运镜由 <see cref="ToriiPullCutscene"/> 承担）</summary>
            PullRite,
            /// <summary>黄昏渐入：模型原样，只有天色在变（<see cref="ToriiDusk"/> 包络就位）</summary>
            DuskIn,
            /// <summary>原地化樱：噪声溶解 + 樱瓣从轮廓剥离</summary>
            Dissolving,
            Gone
        }

        //拔刀仪式节拍：蓄势(0-40) → 拔离滑出(40-70，46帧闪光/迸发) → 归弧到手(70-110)
        internal const int RiteChargeFrames = 40;
        internal const int RiteDrawFrames = 30;
        internal const int RiteFrames = 110;
        //运镜总时长：仪式后镜头再驻留一段，看着黄昏渐入再交还控制权
        internal const int RiteCutsceneFrames = 170;

        private const int DuskInFrames = 50;
        private const int DissolveFrames = 165;
        //余响之后再留约0.83秒静默拍，真夜才开口（见 DepartureHoldingStage）
        private const int PostGoneQuietFrames = 50;
        private const int MaxDeparturePetals = 260;

        private sealed class DeparturePetal
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float RotSpeed;
            public float Scale;
            public float Seed;
            public float BaseAlpha;
            public float Depth;
            public int Age;
            public int MaxLife;
            public bool Deep;
        }

        private DeparturePhase departPhase;
        private bool departInitChecked;
        private int departTimer;
        private int postGoneTimer;
        private float modelOpacity = 1f;
        private float dissolveTint;
        private float petalSpawnCarry;
        private List<Vector2> silhouettePoints;
        private readonly List<DeparturePetal> departPetals = [];
        private readonly List<DeparturePetal> petalDrawBuffer = [];

        //====== 拔刀仪式的绘制状态（由 UpdatePullRite 逐帧写入，DrawSword 消费） ======
        private Vector2 riteSwordOffset;
        private float riteSwordRotation;
        private float riteSwordScale = 1f;
        private float riteCharge;
        private float riteGlint;
        #endregion

        public override void OnSpawn(params object[] args) {
            Width = 64;
            Height = 128;
            //鸟居在2倍缩放下横向延展约±142px、纵向约260px，剔除扩张给足余量防止半入屏时弹出
            DrawExtendMode = 700;
            DrawLayer = ActorDrawLayer.AfterTiles;

            glowTimer = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void AI() {
            glowTimer += 0.03f;
            if (glowTimer > MathHelper.TwoPi) {
                glowTimer -= MathHelper.TwoPi;
            }

            if (Main.dedServ) {
                return;
            }

            UpdateDeparture();

            if (departPhase == DeparturePhase.None && ToriiShrine.SwordPresentForLocalPlayer()) {
                Lighting.AddLight(SwordAnchor, 0.5f, 0.12f, 0.16f);
                UpdateAmbience();
            }
        }

        /// <summary>
        /// 氛围粒子：刀周缓慢升腾的绯红光点，鸟居梁上偶尔一粒白色微光
        /// </summary>
        private void UpdateAmbience() {
            motePrtTimer++;
            if (motePrtTimer >= 26) {
                motePrtTimer = 0;
                Vector2 spawnPos = SwordAnchor + new Vector2(Main.rand.NextFloat(-26f, 26f), Main.rand.NextFloat(-10f, 24f));
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-0.15f, 0.15f), Main.rand.NextFloat(-0.7f, -0.35f));
                PRTLoader.NewParticle<PRT_Light>(spawnPos, velocity, new Color(255, 70, 92), Main.rand.NextFloat(0.14f, 0.24f))
                    .Configure(Main.rand.Next(40, 70), opacity: 0.8f);
            }

            sparklePrtTimer++;
            if (sparklePrtTimer >= 110) {
                sparklePrtTimer = 0;
                //在鸟居横梁高度附近取一点
                Vector2 beamPos = Position + new Vector2(Main.rand.NextFloat(-120f, 120f) * ModelScale * 0.5f
                    , -Main.rand.NextFloat(150f, 240f));
                PRTLoader.NewParticle<PRT_Sparkle>(beamPos, Vector2.Zero, new Color(255, 220, 225), Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        /// <summary>
        /// 拔刀瞬间的本地演出：绯红光点环状迸发 + 白色碎晶；
        /// 仪式在拔离节拍调用，瞬发兜底路径（<see cref="ToriiShrine.PullSword"/>）在交付时调用
        /// </summary>
        public void SwordPulledBurst() {
            if (Main.dedServ) {
                return;
            }

            for (int i = 0; i < 26; i++) {
                float angle = MathHelper.TwoPi * i / 26f + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(2f, 7f);
                PRTLoader.NewParticle<PRT_Light>(SwordAnchor, velocity, new Color(255, 70, 92), Main.rand.NextFloat(0.2f, 0.42f))
                    .Configure(Main.rand.Next(30, 55), opacity: 0.9f);
            }
            for (int i = 0; i < 12; i++) {
                Vector2 velocity = Main.rand.NextVector2Circular(4f, 4f) - new Vector2(0, 2f);
                PRTLoader.NewParticle<PRT_Sparkle>(SwordAnchor, velocity, new Color(255, 235, 238), Main.rand.NextFloat(0.5f, 0.9f));
            }
        }

        #region 退场演出
        /// <summary>
        /// 初见对话的排期闸门：本地退场演出进行中（含余响后的静默拍）时为 true，
        /// <see cref="FirstMetHimayo"/> 借此把真夜的开场白排到鸟居完全消散之后。
        /// 服务端与"进场即缺席"的场合恒为 false，不会卡住被赠刀等无退场可看的玩家
        /// </summary>
        internal static bool DepartureHoldingStage {
            get {
                foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                    if (actor.departPhase == DeparturePhase.DuskIn
                        || actor.departPhase == DeparturePhase.Dissolving) {
                        return true;
                    }
                    if (actor.departPhase == DeparturePhase.Gone
                        && actor.postGoneTimer < PostGoneQuietFrames) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>本地是否有拔刀仪式在演，交互提示与右键受理都要在仪式期让路</summary>
        internal static bool PullRiteHolding {
            get {
                foreach (ToriiShrineActor actor in ActorLoader.GetActiveActors<ToriiShrineActor>()) {
                    if (actor.departPhase == DeparturePhase.PullRite) {
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// 开始拔刀仪式（纯本地视觉）：蓄势→拔离→归弧到手，收尾同帧交付鬼切并进入退场。
        /// 由 <see cref="ToriiShrine.TryBeginPullRite"/> 调用；仅 None 相位可入
        /// </summary>
        public bool BeginPullRite() {
            if (Main.dedServ || departPhase != DeparturePhase.None) {
                return false;
            }

            departPhase = DeparturePhase.PullRite;
            departTimer = 0;
            riteSwordOffset = Vector2.Zero;
            riteSwordRotation = SwordRotation;
            riteSwordScale = 1f;
            riteCharge = 0f;
            riteGlint = 0f;
            return true;
        }

        /// <summary>
        /// 开始退场演出（纯本地视觉）：黄昏渐入→原地化樱消散。
        /// 仪式收尾或无仪式的瞬发拔刀（<see cref="ToriiShrine.PullSword"/>）调用；重复调用无效
        /// </summary>
        public void BeginDeparture() {
            if (Main.dedServ || departPhase != DeparturePhase.None) {
                return;
            }
            EnterDuskIn();
        }

        /// <summary>进入黄昏渐入相：接管本层合成并趁模型仍完整时申请一次剪影读回；
        /// 黄昏渐入本身无声，天色变化就是全部预告</summary>
        private void EnterDuskIn() {
            departPhase = DeparturePhase.DuskIn;
            departTimer = 0;
            ToriiShrineDissolve.Begin(Position);
        }

        private static bool LocalPlayerTookSword() {
            Player player = Main.LocalPlayer;
            return player != null && player.active && HimayoStorySync.ToriiSwordTaken;
        }

        /// <summary>把退场状态复位到"从未开始"，供调试回归（拔刀标记被清除）与仪式中止时鸟居原样回归</summary>
        private void ResetDepartureState() {
            departPhase = DeparturePhase.None;
            departTimer = 0;
            postGoneTimer = 0;
            modelOpacity = 1f;
            dissolveTint = 0f;
            petalSpawnCarry = 0f;
            silhouettePoints = null;
            riteSwordOffset = Vector2.Zero;
            riteSwordRotation = SwordRotation;
            riteSwordScale = 1f;
            riteCharge = 0f;
            riteGlint = 0f;
            ToriiShrineDissolve.End();
        }

        private void UpdateDeparture() {
            if (!departInitChecked) {
                //本地玩家未就绪前不判定，防止把"早已拔过刀"误判为进行中拔刀而重播退场
                Player player = Main.LocalPlayer;
                if (player == null || !player.active) {
                    return;
                }
                departInitChecked = true;
                //进场时就已拔过刀的玩家：鸟居直接缺席，不重播退场；
                //顺带解掉前任Actor（调试重建等）可能遗留的合成钩子。
                //静默拍计时置为已过期，重进世界补触发的初见对话不被压住
                if (HimayoStorySync.ToriiSwordTaken) {
                    departPhase = DeparturePhase.Gone;
                    postGoneTimer = PostGoneQuietFrames;
                    ToriiShrineDissolve.End();
                    return;
                }
            }

            if (departPhase == DeparturePhase.Gone && !LocalPlayerTookSword() && departPetals.Count == 0) {
                //拔刀标记被重置（调试回归流）：鸟居原样回归，允许重看退场
                ResetDepartureState();
            }

            if (departPhase == DeparturePhase.None) {
                //兜底：拔刀瞬间 Actor 不在场（如恰逢补种）时靠剧情标记补触发
                if (LocalPlayerTookSword()) {
                    BeginDeparture();
                }
                else {
                    return;
                }
            }

            UpdateDeparturePetals();

            if (departPhase == DeparturePhase.Gone) {
                if (postGoneTimer < PostGoneQuietFrames) {
                    postGoneTimer++;
                }
                return;
            }

            departTimer++;

            if (departPhase == DeparturePhase.PullRite) {
                UpdatePullRite();
            }
            else if (departPhase == DeparturePhase.DuskIn) {
                UpdateDuskInPhase();
            }
            else if (departPhase == DeparturePhase.Dissolving) {
                UpdateDissolvingPhase();
            }
        }

        /// <summary>
        /// 拔刀仪式逐帧推进：蓄势聚光→刀沿自身轴线拔离（闪光+迸发+一次有动机的震屏）
        /// →归弧飞向玩家，到手帧交付鬼切并同帧进入黄昏渐入（闸门/黄昏零空隙接管）
        /// </summary>
        private void UpdatePullRite() {
            Player player = Main.LocalPlayer;
            //仪式期间玩家死亡/失效：中止并恢复原状，刀未交付可随时重拔
            if (player == null || !player.Alives()) {
                ResetDepartureState();
                if (CutsceneDirector.CurrentClip is ToriiPullCutscene) {
                    CutsceneDirector.Stop();
                }
                return;
            }

            //短无敌 + 面向神社，仪式很短，不给"锁着输入被打死"留机会
            player.GivePlayerImmuneState(4);
            if (MathF.Abs(player.Center.X - Position.X) > 8f) {
                player.ChangeDir(player.Center.X < Position.X ? 1 : -1);
            }

            Vector2 pullDirection = (SwordTipAngle - MathHelper.Pi).ToRotationVector2();

            if (departTimer <= RiteChargeFrames) {
                //蓄势：辉光渐强，绯红光点向刀身收拢
                riteCharge = departTimer / (float)RiteChargeFrames;
                if (departTimer % 5 == 0) {
                    Vector2 spawnPos = SwordAnchor + Main.rand.NextVector2CircularEdge(60f, 60f);
                    PRTLoader.NewParticle<PRT_Light>(spawnPos, (SwordAnchor - spawnPos) * 0.055f,
                        new Color(255, 70, 92), Main.rand.NextFloat(0.16f, 0.26f))
                        .Configure(20, opacity: 0.85f);
                }
            }
            else if (departTimer <= RiteChargeFrames + RiteDrawFrames) {
                //拔离：沿刀轴滑出土面
                float draw = (departTimer - RiteChargeFrames) / (float)RiteDrawFrames;
                float eased = Smooth01(draw);
                riteSwordOffset = pullDirection * eased * 34f;
                riteGlint = MathHelper.Clamp(riteGlint - 0.09f, 0f, 1f);

                if (departTimer == RiteChargeFrames + 6) {
                    //离土瞬间：刃光闪帧 + 环状迸发 + 一次有动机的震屏与拔刀声
                    riteGlint = 1f;
                    SwordPulledBurst();
                    player.CWR().GetScreenShake(7f);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.35f, Volume = 0.9f }, SwordAnchor);
                }
            }
            else if (departTimer < RiteFrames) {
                //归弧：从拔离终点沿弧线飞向玩家，樱瓣拖尾
                float t = (departTimer - RiteChargeFrames - RiteDrawFrames)
                    / (float)(RiteFrames - RiteChargeFrames - RiteDrawFrames);
                float eased = Smooth01(t);

                Vector2 start = SwordAnchor + pullDirection * 34f;
                Vector2 end = player.Center;
                Vector2 control = Vector2.Lerp(start, end, 0.5f) + new Vector2(0f, -110f);
                Vector2 arcPos = Vector2.Lerp(Vector2.Lerp(start, control, eased),
                    Vector2.Lerp(control, end, eased), eased);

                riteSwordOffset = arcPos - SwordAnchor;
                riteSwordRotation = SwordRotation + eased * MathHelper.TwoPi * 0.75f;
                riteSwordScale = MathHelper.Lerp(1f, 0.55f, eased);
                riteGlint = MathHelper.Clamp(riteGlint - 0.09f, 0f, 1f);

                if (departTimer % 3 == 0 && departPetals.Count < MaxDeparturePetals) {
                    departPetals.Add(new DeparturePetal {
                        Position = arcPos + Main.rand.NextVector2Circular(10f, 10f),
                        Velocity = Main.rand.NextVector2Circular(0.8f, 0.8f) - new Vector2(0f, 0.4f),
                        Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                        RotSpeed = Main.rand.NextFloat(-0.12f, 0.12f),
                        Scale = Main.rand.NextFloat(0.4f, 0.8f),
                        Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                        BaseAlpha = Main.rand.NextFloat(0.6f, 0.85f),
                        MaxLife = Main.rand.Next(40, 75),
                        Deep = Main.rand.NextBool(16),
                    });
                }
            }
            else {
                //到手帧：交付鬼切 + 同帧进入黄昏渐入，闸门与黄昏无空隙接管；
                //到手闪光落在玩家身上
                ToriiShrine.GrantSwordFromRite(player);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(player.Center,
                        Main.rand.NextVector2Circular(3f, 3f) - new Vector2(0f, 1.2f),
                        new Color(255, 235, 238), Main.rand.NextFloat(0.45f, 0.8f));
                }
                EnterDuskIn();
            }
        }

        /// <summary>黄昏渐入：模型原样站着，等 <see cref="ToriiDusk"/> 的天色就位</summary>
        private void UpdateDuskInPhase() {
            if (departTimer >= DuskInFrames) {
                departPhase = DeparturePhase.Dissolving;
                departTimer = 0;
                //化樱起点的一声轻响：与樱流化身同款的叶簌语言
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.6f }, Position);
            }
        }

        /// <summary>原地化樱：先向樱粉褪色，随后噪声溶解推进、樱瓣从轮廓剥离，尾段收干透明度</summary>
        private void UpdateDissolvingPhase() {
            float t = departTimer / (float)DissolveFrames;

            dissolveTint = Smooth01(t / 0.30f);
            ToriiShrineDissolve.Progress = Smooth01((t - 0.10f) / 0.80f);
            modelOpacity = 1f - Smooth01((t - 0.70f) / 0.28f);

            SpawnDeparturePetals(MathHelper.Lerp(0.5f, 3f, MathF.Sin(t * MathHelper.Pi)));

            //化樱中的余光渐弱
            float lightFade = (1f - t) * 0.55f;
            if (lightFade > 0.02f) {
                Lighting.AddLight(Position + new Vector2(0f, -110f),
                    0.5f * lightFade, 0.12f * lightFade, 0.16f * lightFade);
            }

            if (departTimer >= DissolveFrames) {
                departPhase = DeparturePhase.Gone;
                //静默拍从余响这一刻起算，走完才放行初见对话
                postGoneTimer = 0;
                ToriiShrineDissolve.End();
                //与神社现世时的清响首尾呼应的一声余响
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = -0.5f }, Position);
            }
        }

        /// <summary>
        /// 花瓣发射点就绪检查：优先用层 RT 读回的真实剪影，读回失败/超时则退回程序化几何
        /// </summary>
        private void EnsureSilhouettePoints() {
            if (silhouettePoints != null) {
                return;
            }
            if (ToriiShrineDissolve.TryTakeSilhouette(out List<Vector2> captured)) {
                silhouettePoints = captured;
            }
            else if (departPhase == DeparturePhase.Dissolving && departTimer > 10) {
                //黄昏渐入的50帧足够剪影读回完成，进入化樱后仍没拿到就走程序化兜底
                silhouettePoints = BuildFallbackSilhouette();
            }
        }

        /// <summary>程序化剪影兜底：双柱 + 笠木 + 贯，对应 2 倍缩放模型的大致几何</summary>
        private static List<Vector2> BuildFallbackSilhouette() {
            List<Vector2> points = new(250);
            void Fill(float x0, float y0, float x1, float y1, int count) {
                for (int i = 0; i < count; i++) {
                    points.Add(new Vector2(Main.rand.NextFloat(x0, x1), Main.rand.NextFloat(y0, y1)));
                }
            }
            Fill(-104f, -235f, -84f, -6f, 70);
            Fill(84f, -235f, 104f, -6f, 70);
            Fill(-168f, -266f, 168f, -234f, 70);
            Fill(-130f, -192f, 130f, -166f, 40);
            return points;
        }

        private void SpawnDeparturePetals(float rate) {
            if (departPetals.Count >= MaxDeparturePetals) {
                return;
            }
            EnsureSilhouettePoints();
            if (silhouettePoints == null || silhouettePoints.Count == 0) {
                return;
            }

            petalSpawnCarry += rate;
            while (petalSpawnCarry >= 1f && departPetals.Count < MaxDeparturePetals) {
                petalSpawnCarry -= 1f;
                Vector2 offset = silhouettePoints[Main.rand.Next(silhouettePoints.Count)];
                float outward = offset.X >= 0f ? 1f : -1f;
                departPetals.Add(new DeparturePetal {
                    Position = Position + offset,
                    Velocity = new Vector2(
                        outward * Main.rand.NextFloat(0.2f, 1.1f) + Main.rand.NextFloat(-0.6f, 0.6f),
                        Main.rand.NextFloat(-1.6f, -0.35f)),
                    Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                    RotSpeed = Main.rand.NextFloat(-0.12f, 0.12f),
                    Scale = Main.rand.NextFloat(0.45f, 0.95f),
                    Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                    BaseAlpha = Main.rand.NextFloat(0.72f, 0.95f),
                    MaxLife = Main.rand.Next(55, 105),
                    Deep = Main.rand.NextBool(16),
                });
            }
        }

        private void UpdateDeparturePetals() {
            for (int i = departPetals.Count - 1; i >= 0; i--) {
                DeparturePetal petal = departPetals[i];
                petal.Age++;
                if (petal.Age >= petal.MaxLife) {
                    departPetals.RemoveAt(i);
                    continue;
                }

                petal.Velocity *= 0.977f;
                petal.Velocity.Y += 0.006f;
                petal.Velocity.X += MathF.Sin(petal.Age * 0.11f + petal.Seed) * 0.03f;
                petal.Velocity.Y += MathF.Cos(petal.Age * 0.08f + petal.Seed) * 0.012f;
                petal.Position += petal.Velocity;
                petal.Rotation += petal.RotSpeed;
                petal.Depth = MathF.Sin(petal.Age * 0.09f + petal.Seed);
            }
        }

        /// <summary>
        /// 樱瓣绘制：借 <see cref="EffectLoader.OniDomainDeco"/> 的 TechPetal SDF，
        /// Immediate 批内逐瓣摆 quad，画完恢复 ActorRender 的批次约定
        /// </summary>
        private void DrawDeparturePetals(SpriteBatch spriteBatch) {
            if (departPetals.Count == 0) {
                return;
            }
            Texture2D white = CWRAsset.Placeholder_White?.Value;
            Effect petalEffect = EffectLoader.OniDomainDeco?.Value;
            if (white == null || petalEffect == null) {
                return;
            }

            petalDrawBuffer.Clear();
            petalDrawBuffer.AddRange(departPetals);
            petalDrawBuffer.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            try {
                petalEffect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                petalEffect.CurrentTechnique = petalEffect.Techniques["TechPetal"];
                petalEffect.CurrentTechnique.Passes[0].Apply();

                Vector2 origin = white.Size() * 0.5f;
                foreach (DeparturePetal petal in petalDrawBuffer) {
                    float life = petal.Age / (float)petal.MaxLife;
                    float envelope = MathF.Pow(MathF.Sin(life * MathHelper.Pi), 0.45f);
                    float alpha = petal.BaseAlpha * envelope;
                    if (alpha <= 0.01f) {
                        continue;
                    }

                    float front = (petal.Depth + 1f) * 0.5f;
                    float flip = MathHelper.Lerp(0.2f, 1f, MathF.Abs(petal.Depth));
                    float stretch = 1f + MathHelper.Clamp(petal.Velocity.Length() / 9f, 0f, 0.3f);

                    Color back = petal.Deep ? new Color(178, 48, 79) : new Color(244, 157, 183);
                    Color middle = petal.Deep ? new Color(229, 90, 119) : new Color(255, 196, 213);
                    Color face = petal.Deep ? new Color(255, 174, 191) : new Color(255, 243, 247);
                    Color color = front < 0.5f
                        ? Color.Lerp(back, middle, front * 2f)
                        : Color.Lerp(middle, face, front * 2f - 1f);
                    //PSPetal 自行输出预乘色：只写透明度，不再压暗 RGB
                    color.A = (byte)(alpha * byte.MaxValue);

                    float width = 19f * petal.Scale * flip;
                    float height = 25f * petal.Scale * stretch;
                    spriteBatch.Draw(white, petal.Position - Main.screenPosition, null, color,
                        petal.Rotation, origin,
                        new Vector2(width / white.Width, height / white.Height), SpriteEffects.None, 0f);
                }
            }
            finally {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }
        }

        private static float Smooth01(float value) {
            value = MathHelper.Clamp(value, 0f, 1f);
            return value * value * (3f - 2f * value);
        }
        #endregion

        public override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (departPhase != DeparturePhase.Gone) {
                SubmitToriiModel();
            }

            if (ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawSword(spriteBatch);
            }

            DrawDeparturePetals(spriteBatch);
            return false;
        }

        /// <summary>
        /// 每个渲染帧向Models3D管线提交一次鸟居实例：生命周期跟随Actor绘制，无需常驻注册/注销。
        /// 化樱期间叠加樱粉褪色与渐隐，模型位置全程不动
        /// </summary>
        private void SubmitToriiModel() {
            Vault3DModel model = ToriiShrine.ToriiModel;
            if (model is null || !model.IsValid) {
                return;
            }

            //取鸟居中段的环境光做整体着色，混一点白保证夜里仍有轮廓
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)((Position.Y - 130f) / 16f));
            if (dissolveTint > 0f) {
                //化樱时向樱粉褪色，与剥离花瓣的色彩交接
                light = Color.Lerp(light, new Color(255, 205, 216), dissolveTint * 0.32f);
            }

            Model3DRenderer.Submit(new Model3DInstance(model) {
                Position = Position + new Vector2(0f, -ModelBottomOffset * ModelScale + 2),
                Rotation = new Vector3(0f, ModelYaw, 0f),
                Scale = new Vector3(ModelScale),
                Layer = Model3DLayer.AfterTiles,
                LightingEnabled = true,
                Tint = light,
                Opacity = modelOpacity,
            });
        }

        /// <summary>
        /// 插在鸟居下的鬼切：软辉光衬底 + 脉动的绯红发光层 + 受环境光的刀身本体。
        /// 拔刀仪式期间叠加拔离位移/归弧旋转/刃光闪帧（riteSword* 状态由 UpdatePullRite 写入）
        /// </summary>
        private void DrawSword(SpriteBatch spriteBatch) {
            Texture2D sword = ToriiShrine.OnikiriTexture?.Value;
            if (sword == null) {
                return;
            }

            bool inRite = departPhase == DeparturePhase.PullRite;
            Vector2 drawPos = SwordAnchor + (inRite ? riteSwordOffset : Vector2.Zero) - Main.screenPosition;
            float rotation = inRite ? riteSwordRotation : SwordRotation;
            float scale = SwordScale * (inRite ? riteSwordScale : 1f);
            Vector2 origin = sword.Size() / 2f;
            //蓄势期脉动增强
            float pulse = (MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f) * (1f + riteCharge * 0.8f);

            //软辉光衬底
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color backing = new Color(255, 60, 84) with { A = 0 } * (0.22f + pulse * 0.14f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f
                , new Vector2(150f * scale / glow.Width, 130f * scale / glow.Height), SpriteEffects.None, 0f);

            //刀形发光层
            Color bladeGlow = new Color(255, 82, 100) with { A = 0 };
            for (int i = 0; i < 3; i++) {
                float glowScale = (1.06f + i * 0.06f) * scale;
                float glowAlpha = (0.3f + pulse * 0.3f) * (1f - i * 0.3f);
                spriteBatch.Draw(sword, drawPos, null, bladeGlow * glowAlpha, rotation
                    , origin, glowScale, SpriteEffects.None, 0f);
            }

            //本体
            Color bodyColor = Lighting.GetColor((SwordAnchor / 16f).ToPoint());
            //刀身自带一点微光，避免夜晚完全看不见
            bodyColor = Color.Lerp(bodyColor, Color.White, 0.25f);
            spriteBatch.Draw(sword, drawPos, null, bodyColor, rotation, origin, scale, SpriteEffects.None, 0f);

            //离土瞬间的刃光闪帧
            if (riteGlint > 0.01f) {
                Color glint = Color.White with { A = 0 } * riteGlint;
                spriteBatch.Draw(sword, drawPos, null, glint, rotation, origin, scale * 1.02f, SpriteEffects.None, 0f);
            }
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            //仪式运镜期间不叠交互提示
            if (departPhase != DeparturePhase.PullRite && ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawInteractPrompt(spriteBatch);
            }
        }

        /// <summary>
        /// 交互提示：柔光衬底+描边文字，绯红配色呼应鬼切主题（拒绝方框UI）
        /// </summary>
        private void DrawInteractPrompt(SpriteBatch sb) {
            float alpha = ToriiShrine.GetInteractPromptAlpha();
            if (alpha <= 0.01f) {
                return;
            }

            Vector2 textPos = SwordAnchor - Main.screenPosition + new Vector2(0, -96f);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            string hintText = ToriiShrine.GetPromptText();
            Vector2 textSize = font.MeasureString(hintText) * 0.9f;

            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 0.5f + 0.5f;

            //柔光椭圆衬底
            Vector2 backingScale = new Vector2((textSize.X + 50f) / glow.Width, (textSize.Y + 30f) / glow.Height);
            Color backingColor = new Color(190, 55, 80) with { A = 0 } * (alpha * (0.3f + pulse * 0.12f));
            sb.Draw(glow, textPos, null, backingColor, 0f, glow.Size() / 2f, backingScale, SpriteEffects.None, 0f);

            //文字
            Color textColor = new Color(255, 228, 232) * alpha;
            Utils.DrawBorderString(sb, hintText, textPos - textSize / 2, textColor, 0.9f);

            //脉动光带
            float lineWidth = textSize.X * (0.7f + pulse * 0.25f);
            Vector2 linePos = textPos + new Vector2(0, textSize.Y / 2f + 6f);
            Color lineColor = new Color(235, 95, 118) with { A = 0 } * (alpha * 0.6f);
            sb.Draw(glow, linePos, null, lineColor, 0f, glow.Size() / 2f
                , new Vector2(lineWidth / glow.Width, 4f / glow.Height), SpriteEffects.None, 0f);
        }
    }
}
