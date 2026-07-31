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
    /// 鸟居Actor，锚点=<see cref="Actor.Position"/>地表中心<br/>
    /// 右键仪式→退场(<see cref="ToriiPullCutscene"/> / <see cref="ToriiDusk"/>)，纯本地视觉，未拔刀玩家侧鸟居不动
    /// </summary>
    internal class ToriiShrineActor : Actor
    {
        //半高64.7，pivot抬半高*缩放使柱脚落锚点
        private const float ModelBottomOffset = 64.7f;
        /// <summary>整体缩放，原约142x129，2倍约18x16格</summary>
        private const float ModelScale = 2f;
        //Y偏转露出侧面进深
        private const float ModelYaw = 0.32f;

        //刀缩放，贴图作静物偏大
        private const float SwordScale = 0.75f;
        //刀心离地，对角半长×缩放≈49，刀尖入土约14
        internal const float SwordCenterHeight = 35f;
        //刀尖朝下再带回一点倾角(原贴图-45°)
        private const float SwordRotation = MathHelper.PiOver4 * 3f - 0.26f;
        //刀尖世界朝向，拔离取其反向
        private const float SwordTipAngle = SwordRotation - MathHelper.PiOver4;

        private float glowTimer;
        private int motePrtTimer;
        private int sparklePrtTimer;

        /// <summary>刀心世界坐标</summary>
        public Vector2 SwordAnchor => Position + new Vector2(0f, -SwordCenterHeight);

        #region 退场状态
        private enum DeparturePhase
        {
            None,
            /// <summary>蓄势→拔离→归弧到手，运镜见 <see cref="ToriiPullCutscene"/></summary>
            PullRite,
            /// <summary>模型原样，天色变，见 <see cref="ToriiDusk"/></summary>
            DuskIn,
            /// <summary>噪声溶解+樱瓣剥离</summary>
            Dissolving,
            Gone
        }

        //蓄势0-40 → 拔离40-70(46闪光) → 归弧70-110
        internal const int RiteChargeFrames = 40;
        internal const int RiteDrawFrames = 30;
        internal const int RiteFrames = 110;
        //仪式后再驻留看黄昏
        internal const int RiteCutsceneFrames = 170;

        private const int DuskInFrames = 50;
        private const int DissolveFrames = 165;
        //余响后约0.83s静默拍再放行初见
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
        //外部已有刀或进场前已拔刀时静默隐藏，区别于正常溶解后的Gone
        private bool hiddenByEligibility;
        private int departTimer;
        private int postGoneTimer;
        private float modelOpacity = 1f;
        private float dissolveTint;
        private float petalSpawnCarry;
        private List<Vector2> silhouettePoints;
        private readonly List<DeparturePetal> departPetals = [];
        private readonly List<DeparturePetal> petalDrawBuffer = [];

        //仪式绘制态，UpdatePullRite写 DrawSword读
        private Vector2 riteSwordOffset;
        private float riteSwordRotation;
        private float riteSwordScale = 1f;
        private float riteCharge;
        private float riteGlint;
        #endregion

        public override void OnSpawn(params object[] args) {
            Width = 64;
            Height = 128;
            //剔除扩张防半入屏弹出
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

        //刀周绯红光点，梁上偶尔白微光
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
                //横梁附近
                Vector2 beamPos = Position + new Vector2(Main.rand.NextFloat(-120f, 120f) * ModelScale * 0.5f
                    , -Main.rand.NextFloat(150f, 240f));
                PRTLoader.NewParticle<PRT_Sparkle>(beamPos, Vector2.Zero, new Color(255, 220, 225), Main.rand.NextFloat(0.4f, 0.7f));
            }
        }

        /// <summary>拔刀迸发，仪式拔离节拍或 <see cref="ToriiShrine.PullSword"/> 交付时调</summary>
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
        /// 初见排期闸门，本地退场(含静默拍)中为true<br/>
        /// 服务端与进场即缺席恒false，不卡住赠刀等无退场玩家
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

        /// <summary>本地仪式在演，交互让路</summary>
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

        /// <summary>开始仪式，收尾同帧交付并进退场，仅None可入，见 <see cref="ToriiShrine.TryBeginPullRite"/></summary>
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

        /// <summary>开始退场，仪式收尾或 <see cref="ToriiShrine.PullSword"/> 调，重复无效</summary>
        public void BeginDeparture() {
            if (Main.dedServ || departPhase != DeparturePhase.None) {
                return;
            }
            EnterDuskIn();
        }

        /// <summary>进黄昏相，接管合成并趁模型完整申请剪影读回</summary>
        private void EnterDuskIn() {
            departPhase = DeparturePhase.DuskIn;
            departTimer = 0;
            ToriiShrineDissolve.Begin(Position);
        }

        private static bool LocalPlayerTookSword() {
            Player player = Main.LocalPlayer;
            return player != null && player.active && HimayoStorySync.ToriiSwordTaken;
        }

        /// <summary>退场复位到从未开始，调试回归与仪式中止用</summary>
        private void ResetDepartureState() {
            departPhase = DeparturePhase.None;
            hiddenByEligibility = false;
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

        /// <summary>
        /// 本地可见性闸门：玩家就绪后立刻判定，已拔刀或随身已有鬼切则静默隐藏。<br/>
        /// PreDraw与AI共用，避免「先画一帧再隐藏」闪现。
        /// </summary>
        private void EnsureDepartureInit() {
            if (departInitChecked || Main.dedServ) {
                return;
            }
            Player player = Main.LocalPlayer;
            //本地玩家未就绪前不判定；期间不提交模型。
            if (player == null || !player.active) {
                return;
            }

            departInitChecked = true;
            if (!ToriiShrine.ShouldShowForLocalPlayer()) {
                HideByEligibility();
            }
        }

        private void HideByEligibility() {
            hiddenByEligibility = true;
            departPhase = DeparturePhase.Gone;
            postGoneTimer = PostGoneQuietFrames;
            modelOpacity = 1f;
            ToriiShrineDissolve.End();
        }

        private void UpdateDeparture() {
            EnsureDepartureInit();
            if (!departInitChecked) {
                return;
            }

            bool shouldShow = ToriiShrine.ShouldShowForLocalPlayer();
            if (hiddenByEligibility) {
                if (shouldShow && departPetals.Count == 0) {
                    ResetDepartureState();
                }
                else {
                    return;
                }
            }

            //外部获得鬼切时静默隐藏；正常拔刀仪式已经进入退场相，不在这里截断。
            if (departPhase == DeparturePhase.None && !shouldShow) {
                HideByEligibility();
                return;
            }

            if (departPhase == DeparturePhase.Gone && !LocalPlayerTookSword() && departPetals.Count == 0) {
                //拔刀标记被重置(调试)，鸟居原样回归；若仍持刀则转入资格隐藏。
                ResetDepartureState();
                if (!ToriiShrine.ShouldShowForLocalPlayer()) {
                    HideByEligibility();
                    return;
                }
            }

            if (departPhase == DeparturePhase.None) {
                //拔刀瞬间Actor不在场时靠剧情标记补触发。
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

        /// <summary>仪式逐帧，到手帧交付并同帧进黄昏</summary>
        private void UpdatePullRite() {
            Player player = Main.LocalPlayer;
            //玩家死亡/失效则中止，刀未交付可重拔
            if (player == null || !player.Alives()) {
                ResetDepartureState();
                if (CutsceneDirector.CurrentClip is ToriiPullCutscene) {
                    CutsceneDirector.Stop();
                }
                return;
            }

            //短无敌+面向神社
            player.GivePlayerImmuneState(4);
            if (MathF.Abs(player.Center.X - Position.X) > 8f) {
                player.ChangeDir(player.Center.X < Position.X ? 1 : -1);
            }

            Vector2 pullDirection = (SwordTipAngle - MathHelper.Pi).ToRotationVector2();

            if (departTimer <= RiteChargeFrames) {
                //蓄势
                riteCharge = departTimer / (float)RiteChargeFrames;
                if (departTimer % 5 == 0) {
                    Vector2 spawnPos = SwordAnchor + Main.rand.NextVector2CircularEdge(60f, 60f);
                    PRTLoader.NewParticle<PRT_Light>(spawnPos, (SwordAnchor - spawnPos) * 0.055f,
                        new Color(255, 70, 92), Main.rand.NextFloat(0.16f, 0.26f))
                        .Configure(20, opacity: 0.85f);
                }
            }
            else if (departTimer <= RiteChargeFrames + RiteDrawFrames) {
                //拔离
                float draw = (departTimer - RiteChargeFrames) / (float)RiteDrawFrames;
                float eased = Smooth01(draw);
                riteSwordOffset = pullDirection * eased * 34f;
                riteGlint = MathHelper.Clamp(riteGlint - 0.09f, 0f, 1f);

                if (departTimer == RiteChargeFrames + 6) {
                    //离土，闪光+迸发+震屏
                    riteGlint = 1f;
                    SwordPulledBurst();
                    player.CWR().GetScreenShake(7f);
                    SoundEngine.PlaySound(SoundID.Dig with { Pitch = 0.35f, Volume = 0.9f }, SwordAnchor);
                }
            }
            else if (departTimer < RiteFrames) {
                //归弧
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
                //到手，交付+进黄昏
                ToriiShrine.GrantSwordFromRite(player);
                for (int i = 0; i < 10; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(player.Center,
                        Main.rand.NextVector2Circular(3f, 3f) - new Vector2(0f, 1.2f),
                        new Color(255, 235, 238), Main.rand.NextFloat(0.45f, 0.8f));
                }
                EnterDuskIn();
            }
        }

        /// <summary>黄昏渐入，等 <see cref="ToriiDusk"/> 就位</summary>
        private void UpdateDuskInPhase() {
            if (departTimer >= DuskInFrames) {
                departPhase = DeparturePhase.Dissolving;
                departTimer = 0;
                //化樱起点叶簌
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.3f, Volume = 0.6f }, Position);
            }
        }

        /// <summary>原地化樱，褪色→溶解→收干透明度</summary>
        private void UpdateDissolvingPhase() {
            float t = departTimer / (float)DissolveFrames;

            dissolveTint = Smooth01(t / 0.30f);
            ToriiShrineDissolve.Progress = Smooth01((t - 0.10f) / 0.80f);
            modelOpacity = 1f - Smooth01((t - 0.70f) / 0.28f);

            SpawnDeparturePetals(MathHelper.Lerp(0.5f, 3f, MathF.Sin(t * MathHelper.Pi)));

            //余光渐弱
            float lightFade = (1f - t) * 0.55f;
            if (lightFade > 0.02f) {
                Lighting.AddLight(Position + new Vector2(0f, -110f),
                    0.5f * lightFade, 0.12f * lightFade, 0.16f * lightFade);
            }

            if (departTimer >= DissolveFrames) {
                departPhase = DeparturePhase.Gone;
                //静默拍起算，走完放行初见
                postGoneTimer = 0;
                ToriiShrineDissolve.End();
            }
        }

        /// <summary>花瓣发射点，优先RT剪影，失败则程序化</summary>
        private void EnsureSilhouettePoints() {
            if (silhouettePoints != null) {
                return;
            }
            if (ToriiShrineDissolve.TryTakeSilhouette(out List<Vector2> captured)) {
                silhouettePoints = captured;
            }
            else if (departPhase == DeparturePhase.Dissolving && departTimer > 10) {
                //黄昏50帧够读回，仍无则程序化兜底
                silhouettePoints = BuildFallbackSilhouette();
            }
        }

        /// <summary>程序化剪影兜底，双柱+笠木+贯</summary>
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

        /// <summary>樱瓣绘制，TechPetal SDF Immediate批，画完恢复ActorRender批次</summary>
        private void DrawDeparturePetals(SpriteBatch spriteBatch) {
            if (departPetals.Count == 0) {
                return;
            }
            Texture2D white = VaultAsset.placeholder2?.Value;
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
                    //PSPetal预乘，只写A
                    color.A = (byte)(alpha * byte.MaxValue);

                    float width = 19f * petal.Scale * flip;
                    float height = 25f * petal.Scale * stretch;
                    spriteBatch.Draw(white, petal.Position - Main.screenPosition, null, color,
                        petal.Rotation, origin,
                        new Vector2(width / white.Width, height / white.Height), SpriteEffects.None, 0f);
                }
            } finally {
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
            //绘制前先落本地闸门，已拔刀玩家进世界不会闪一帧鸟居
            EnsureDepartureInit();
            if (!departInitChecked || departPhase == DeparturePhase.Gone) {
                DrawDeparturePetals(spriteBatch);
                return false;
            }

            if (!SubmitToriiModel()) {
                DrawFallbackTorii(spriteBatch);
            }

            if (ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawSword(spriteBatch);
            }

            DrawDeparturePetals(spriteBatch);
            return false;
        }

        /// <summary>每帧提交鸟居实例；返回false时由调用方绘制程序化兜底</summary>
        private bool SubmitToriiModel() {
            Vault3DModel model = ToriiShrine.ToriiModel;
            if (model is null || !model.IsValid) {
                return false;
            }

            //中段环境光，混白保夜里轮廓
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)((Position.Y - 130f) / 16f));
            if (dissolveTint > 0f) {
                //化樱向樱粉褪色
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
            return true;
        }

        /// <summary>模型资源异常时仍提供清晰可辨的双柱鸟居剪影</summary>
        private void DrawFallbackTorii(SpriteBatch spriteBatch) {
            Texture2D pixel = VaultAsset.placeholder2?.Value;
            if (pixel == null || modelOpacity <= 0.01f) {
                return;
            }

            Vector2 center = Position - Main.screenPosition;
            Color light = Lighting.GetColor((int)(Position.X / 16f), (int)((Position.Y - 130f) / 16f));
            Color vermilion = Color.Lerp(light, new Color(176, 43, 52), 0.72f) * modelOpacity;
            Color shadow = Color.Lerp(light, new Color(76, 20, 26), 0.78f) * modelOpacity;
            Vector2 origin = pixel.Size() * 0.5f;

            void DrawBeam(Vector2 offset, Vector2 size, Color color) {
                spriteBatch.Draw(pixel, center + offset, null, color, 0f, origin,
                    new Vector2(size.X / pixel.Width, size.Y / pixel.Height), SpriteEffects.None, 0f);
            }

            DrawBeam(new Vector2(-96f, -116f), new Vector2(26f, 232f), shadow);
            DrawBeam(new Vector2(96f, -116f), new Vector2(26f, 232f), shadow);
            DrawBeam(new Vector2(-96f, -126f), new Vector2(18f, 212f), vermilion);
            DrawBeam(new Vector2(96f, -126f), new Vector2(18f, 212f), vermilion);
            DrawBeam(new Vector2(0f, -190f), new Vector2(270f, 24f), shadow);
            DrawBeam(new Vector2(0f, -198f), new Vector2(252f, 16f), vermilion);
            DrawBeam(new Vector2(0f, -244f), new Vector2(346f, 30f), shadow);
            DrawBeam(new Vector2(0f, -253f), new Vector2(326f, 20f), vermilion);
        }

        /// <summary>插地鬼切，仪式期叠拔离/归弧/刃光</summary>
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
            //蓄势脉动增强
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
            //微光，防夜里看不见
            bodyColor = Color.Lerp(bodyColor, Color.White, 0.25f);
            spriteBatch.Draw(sword, drawPos, null, bodyColor, rotation, origin, scale, SpriteEffects.None, 0f);

            //离土刃光闪帧
            if (riteGlint > 0.01f) {
                Color glint = Color.White with { A = 0 } * riteGlint;
                spriteBatch.Draw(sword, drawPos, null, glint, rotation, origin, scale * 1.02f, SpriteEffects.None, 0f);
            }
        }

        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            //仪式期不叠交互提示
            if (departPhase != DeparturePhase.PullRite && ToriiShrine.SwordPresentForLocalPlayer()) {
                DrawInteractPrompt(spriteBatch);
            }
        }

        /// <summary>交互提示，柔光衬底+描边文字</summary>
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
