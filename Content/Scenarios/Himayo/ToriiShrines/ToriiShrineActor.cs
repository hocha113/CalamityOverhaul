using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.Actors;
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
    /// 本地玩家拔刀后走退场演出（颤抖→沉入地下→溶解成樱瓣散去），
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

        //刀的中心离地高度：贴图对角半长约65px，刀尖入土约18px
        internal const float SwordCenterHeight = 47f;
        //刀身旋转：原贴图刀尖朝右上(-45°)，转到刀尖朝下再往回带一点倾角
        private const float SwordRotation = MathHelper.PiOver4 * 3f - 0.26f;

        private float glowTimer;
        private int motePrtTimer;
        private int sparklePrtTimer;

        /// <summary>刀的中心点（世界坐标）</summary>
        public Vector2 SwordAnchor => Position + new Vector2(0f, -SwordCenterHeight);

        #region 退场状态
        private enum DeparturePhase
        {
            None,
            Trembling,
            Sinking,
            Gone
        }

        private const int TrembleFrames = 50;
        private const int SinkFrames = 165;
        //余响之后再留约0.83秒静默拍，真夜才开口（见 DepartureHoldingStage）
        private const int PostGoneQuietFrames = 50;
        //下沉总深度：略超模型可视高度(~260px)保证完全没入
        private const float SinkDepth = 300f;
        private const float TrembleMaxAmp = 3.2f;
        private const float ModelTopHeight = 260f;
        //柱脚离锚点的横向距离，冒土/落点音效用
        private const float PillarOffsetX = 96f;
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
        private bool burialThudDone;
        private int departTimer;
        private int departFrames;
        private int postGoneTimer;
        private float trembleAmp;
        private float jitterX;
        private float rotationJitter;
        private float sinkOffset;
        private float modelOpacity = 1f;
        private float dissolveTint;
        private float petalSpawnCarry;
        private List<Vector2> silhouettePoints;
        private readonly List<DeparturePetal> departPetals = [];
        private readonly List<DeparturePetal> petalDrawBuffer = [];
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
        /// 拔刀瞬间的本地演出：绯红光点环状迸发 + 白色碎晶，由 <see cref="ToriiShrine.PullSword"/> 调用
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
                    if (actor.departPhase == DeparturePhase.Trembling
                        || actor.departPhase == DeparturePhase.Sinking) {
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

        /// <summary>
        /// 开始退场演出（纯本地视觉）：颤抖→沉入地下→溶解成樱瓣散去。
        /// 拔刀瞬间由 <see cref="ToriiShrine.PullSword"/> 调用；重复调用无效
        /// </summary>
        public void BeginDeparture() {
            if (Main.dedServ || departPhase != DeparturePhase.None) {
                return;
            }

            departPhase = DeparturePhase.Trembling;
            departTimer = 0;
            departFrames = 0;
            //接管本层合成并趁模型仍完整时申请一次剪影读回
            ToriiShrineDissolve.Begin(Position);
            SoundEngine.PlaySound(SoundID.WormDigQuiet with { Pitch = -0.7f, Volume = 0.85f }, Position);
        }

        private static bool LocalPlayerTookSword() {
            Player player = Main.LocalPlayer;
            return player != null && player.active && HimayoStorySync.ToriiSwordTaken;
        }

        /// <summary>把退场状态复位到"从未开始"，供调试回归（拔刀标记被清除）时鸟居原样回归</summary>
        private void ResetDepartureState() {
            departPhase = DeparturePhase.None;
            departTimer = 0;
            departFrames = 0;
            postGoneTimer = 0;
            trembleAmp = 0f;
            jitterX = 0f;
            rotationJitter = 0f;
            sinkOffset = 0f;
            modelOpacity = 1f;
            dissolveTint = 0f;
            petalSpawnCarry = 0f;
            burialThudDone = false;
            silhouettePoints = null;
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

            departFrames++;
            departTimer++;

            if (departPhase == DeparturePhase.Trembling) {
                UpdateTremblePhase();
            }
            else if (departPhase == DeparturePhase.Sinking) {
                UpdateSinkPhase();
            }

            //共用抖动：颤抖期渐强，下沉期渐弱，掩盖切相瞬间
            jitterX = MathF.Sin(departFrames * 1.9f) * trembleAmp;
            rotationJitter = MathF.Sin(departFrames * 2.3f) * 0.012f * (trembleAmp / TrembleMaxAmp);
        }

        private void UpdateTremblePhase() {
            trembleAmp = TrembleMaxAmp * (departTimer / (float)TrembleFrames);

            if (departTimer % 9 == 0) {
                NearShake(1.5f);
            }
            if (departTimer % 6 == 0) {
                SpawnSoilBurst(1);
            }
            SpawnDeparturePetals(0.22f);

            if (departTimer >= TrembleFrames) {
                departPhase = DeparturePhase.Sinking;
                departTimer = 0;
                SoundEngine.PlaySound(SoundID.WormDig with { Pitch = -0.5f, Volume = 0.8f }, Position);
            }
        }

        private void UpdateSinkPhase() {
            float t = departTimer / (float)SinkFrames;
            //ease-in：起沉迟缓，越沉越快
            sinkOffset = t * t * SinkDepth;
            trembleAmp = MathHelper.Lerp(TrembleMaxAmp, 0.9f, t);

            ToriiShrineDissolve.Progress = Smooth01((t - 0.28f) / 0.62f);
            ToriiShrineDissolve.GroundY = Position.Y + 2f;
            modelOpacity = 1f - Smooth01((t - 0.66f) / 0.30f);
            dissolveTint = Smooth01((t - 0.30f) / 0.55f);

            if (departTimer % 3 == 0) {
                SpawnSoilBurst(1 + (int)(t * 3f));
            }
            if (departTimer % 16 == 0) {
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = Main.rand.NextFloat(-0.45f, -0.15f), Volume = 0.55f }, Position);
            }
            if (departTimer % 42 == 0) {
                SoundEngine.PlaySound(SoundID.WormDigQuiet with { Pitch = -0.6f, Volume = 0.6f }, Position);
            }
            if (departTimer % 12 == 0) {
                NearShake(0.9f);
            }
            SpawnDeparturePetals(MathHelper.Lerp(0.5f, 3f, MathF.Sin(t * MathHelper.Pi)));

            //顶梁没入土面的顿挫
            if (!burialThudDone && sinkOffset > ModelTopHeight) {
                burialThudDone = true;
                NearShake(5f);
                SoundEngine.PlaySound(SoundID.Dig with { Pitch = -0.6f, Volume = 0.85f }, Position);
            }

            //沉没中的余光渐弱
            float lightFade = (1f - t) * 0.55f;
            if (lightFade > 0.02f) {
                Lighting.AddLight(Position + new Vector2(0f, -110f + sinkOffset * 0.5f),
                    0.5f * lightFade, 0.12f * lightFade, 0.16f * lightFade);
            }

            if (departTimer >= SinkFrames) {
                departPhase = DeparturePhase.Gone;
                //静默拍从余响这一刻起算，走完才放行初见对话
                postGoneTimer = 0;
                ToriiShrineDissolve.End();
                //与神社现世时的清响首尾呼应的一声余响
                SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.45f, Pitch = -0.5f }, Position);
            }
        }

        private void NearShake(float strength) {
            Player player = Main.LocalPlayer;
            if (player.Alives() && player.DistanceSQ(Position) < 2200f * 2200f) {
                player.CWR().GetScreenShake(strength);
            }
        }

        /// <summary>柱脚冒土 + 偶发绯红光点，颤抖与下沉期共用</summary>
        private void SpawnSoilBurst(int count) {
            for (int i = 0; i < count * 2; i++) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                Vector2 pos = new(Position.X + side * PillarOffsetX + Main.rand.NextFloat(-16f, 16f),
                    Position.Y + Main.rand.NextFloat(-4f, 2f));
                Dust dust = Dust.NewDustPerfect(pos, DustID.Dirt,
                    new Vector2(Main.rand.NextFloat(-0.9f, 0.9f), Main.rand.NextFloat(-2.8f, -0.9f)),
                    120, default, Main.rand.NextFloat(1.1f, 1.7f));
                dust.noGravity = false;
            }
            if (Main.rand.NextBool(3)) {
                Vector2 motePos = new(Position.X + Main.rand.NextFloat(-70f, 70f), Position.Y - Main.rand.NextFloat(0f, 30f));
                PRTLoader.NewParticle<PRT_Light>(motePos, new Vector2(0f, -0.5f), new Color(255, 70, 92), 0.2f)
                    .Configure(40, opacity: 0.8f);
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
            else if (departPhase == DeparturePhase.Sinking && departTimer > 10) {
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
                for (int attempt = 0; attempt < 6; attempt++) {
                    Vector2 offset = silhouettePoints[Main.rand.Next(silhouettePoints.Count)];
                    Vector2 world = Position + offset + new Vector2(jitterX, sinkOffset);
                    if (world.Y > Position.Y - 6f) {
                        //已沉入土面的部位不再剥离
                        continue;
                    }

                    float outward = offset.X >= 0f ? 1f : -1f;
                    departPetals.Add(new DeparturePetal {
                        Position = world,
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
                    break;
                }
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
        /// 退场期间叠加抖动位移、下沉偏移与渐隐
        /// </summary>
        private void SubmitToriiModel() {
            Vault3DModel model = ToriiShrine.ToriiModel;
            if (model is null || !model.IsValid) {
                return;
            }

            //取鸟居中段的环境光做整体着色，混一点白保证夜里仍有轮廓；下沉时采样点跟着走，入土自然渐暗
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)((Position.Y - 130f + sinkOffset * 0.6f) / 16f));
            if (dissolveTint > 0f) {
                //溶解时向樱粉褪色，与剥离花瓣的色彩交接
                light = Color.Lerp(light, new Color(255, 205, 216), dissolveTint * 0.32f);
            }

            Model3DRenderer.Submit(new Model3DInstance(model) {
                Position = Position + new Vector2(jitterX, -ModelBottomOffset * ModelScale + 2 + sinkOffset),
                Rotation = new Vector3(0f, ModelYaw, rotationJitter),
                Scale = new Vector3(ModelScale),
                Layer = Model3DLayer.AfterTiles,
                LightingEnabled = true,
                Tint = light,
                Opacity = modelOpacity,
            });
        }

        /// <summary>
        /// 插在鸟居下的鬼切：软辉光衬底 + 脉动的绯红发光层 + 受环境光的刀身本体
        /// </summary>
        private void DrawSword(SpriteBatch spriteBatch) {
            Texture2D sword = ToriiShrine.OnikiriTexture?.Value;
            if (sword == null) {
                return;
            }

            Vector2 drawPos = SwordAnchor - Main.screenPosition;
            Vector2 origin = sword.Size() / 2f;
            float pulse = MathF.Sin(glowTimer * 2f) * 0.5f + 0.5f;

            //软辉光衬底
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Color backing = new Color(255, 60, 84) with { A = 0 } * (0.22f + pulse * 0.14f);
            spriteBatch.Draw(glow, drawPos, null, backing, 0f, glow.Size() / 2f
                , new Vector2(150f / glow.Width, 130f / glow.Height), SpriteEffects.None, 0f);

            //刀形发光层
            Color bladeGlow = new Color(255, 82, 100) with { A = 0 };
            for (int i = 0; i < 3; i++) {
                float glowScale = 1.06f + i * 0.06f;
                float glowAlpha = (0.3f + pulse * 0.3f) * (1f - i * 0.3f);
                spriteBatch.Draw(sword, drawPos, null, bladeGlow * glowAlpha, SwordRotation
                    , origin, glowScale, SpriteEffects.None, 0f);
            }

            //本体
            Color bodyColor = Lighting.GetColor((SwordAnchor / 16f).ToPoint());
            //刀身自带一点微光，避免夜晚完全看不见
            bodyColor = Color.Lerp(bodyColor, Color.White, 0.25f);
            spriteBatch.Draw(sword, drawPos, null, bodyColor, SwordRotation, origin, 1f, SpriteEffects.None, 0f);
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (ToriiShrine.SwordPresentForLocalPlayer()) {
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
