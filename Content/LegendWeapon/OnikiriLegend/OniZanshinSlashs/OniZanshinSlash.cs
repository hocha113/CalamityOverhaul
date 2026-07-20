using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OAR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates.OniAnnihilateRenderer;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniZanshinSlashs
{
    /// <summary>
    /// 残心斩：操控交还帧(疾走刹停/樱流落地)后的追斩窗内，按下沿把普攻转换成的巨型反拔刀弧月。<br/>
    /// 定位卡在连段单拍与灭世一闪之间的"惊人一刻"：锵前按下由 <see cref="OnikiriPlayer"/> 缓冲到
    /// 纳刀结算同帧释放，出刀与墨痕齐裂压成一拍；实体刀经 <see cref="OniBladeHandoff"/> 继承
    /// 纳刀刀角反手甩出，弧刃复用灭世一闪的水墨管线并缩规格。<br/>
    /// 表世界为樱衣：粉白弧月 + 沿弧一次性绽放的花瓣爆(TechPetal)，命中溅散瓣；其余为墨衣。<br/>
    /// ai[0]=刀线角(弧度) ai[1]=变体(0墨/1樱) ai[2]=与锵同帧(1=震屏减半)
    /// </summary>
    internal class OniZanshinSlash : ModProjectile, IPrimitiveDrawable, IAdditiveDrawable, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==== 时间轴常量 ====
        /// <summary>反拔甩刀帧数(itemTime 锁仅覆盖此窗)</summary>
        private const int PoseFrames = 4;
        /// <summary>踏步前压帧数</summary>
        private const int StepFrames = 3;
        /// <summary>每帧前压距离(px),合计 ~72px</summary>
        private const float StepPerFrame = 24f;
        /// <summary>伤害窗末帧</summary>
        private const int DamageEnd = 6;
        /// <summary>演出总时长</summary>
        private const int Lifetime = 36;
        /// <summary>甩刀后的残心余韵帧数</summary>
        private const int ZanshinHoldFrames = 12;
        //==== 几何常量(层级纪律:明确小于灭世一闪的 760/690/3.60) ====
        private const float ArcHalfX = 430f;
        private const float ArcHalfY = 385f;
        private const float ArcSpan = 2.42f;
        /// <summary>贴身补判半径(px)</summary>
        private const float NearRadius = 120f;
        /// <summary>弧带判定走廊宽(px)</summary>
        private const float ArcCorridor = 150f;
        /// <summary>扇形补心辐条宽(px)</summary>
        private const float SpokeWidth = 120f;
        /// <summary>樱衣沿弧绽放的花瓣数</summary>
        private const int PetalCount = 36;

        /// <summary>樱衣调色:同一套水墨管线换粉白基底,白热核仍偏暖</summary>
        private static readonly OFR.BladePalette SakuraPalette = new() {
            Hot = new Vector3(1.75f, 1.58f, 1.50f),
            Bright = new Vector3(1.42f, 0.62f, 0.78f),
            Deep = new Vector3(0.78f, 0.22f, 0.40f),
            Dark = new Vector3(0.24f, 0.07f, 0.14f),
        };

        private sealed class Petal
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Rotation;
            public float RotSpeed;
            public float Scale;
            public float Depth;
            public float Spin;
            public float Seed;
            public float Alpha;
            public int Age;
            public int MaxLife;
            public bool DeepColor;
        }

        private OFR.BladeDef arcDef;
        private bool initialized;
        private int timer;
        private int facing = 1;
        /// <summary>反拔起手刀角:优先继承交接黑板(纳刀位),无交接退回反手预备位</summary>
        private float drawStartRot;
        private readonly OniBladePose bladePose = new();
        private readonly List<Petal> petals = new(PetalCount + 16);

        private float CutAngle => Projectile.ai[0];
        private bool IsSakura => Projectile.ai[1] > 0.5f;
        /// <summary>与锵同帧释放:纳刀结算已带震屏/白闪,本体反馈减半防同帧过载</summary>
        private bool SyncedJudge => Projectile.ai[2] > 0.5f;
        private Player Owner => Main.player[Projectile.owner];

        /// <summary>甩刀+落定头段硬占刀权:疾走残心与连段就地让位</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= PoseFrames + 4;

        /// <summary>落定后的签名拍软保留:连段续接时刀从收势位划出</summary>
        bool IOniBladeOccupant.ReservesBlade => timer <= PoseFrames + 6;

        /// <summary>
        /// 触发接口：在持有者客户端调用（<c>player.whoAmI == Main.myPlayer</c> 时），
        /// tML 自动完成多人同步；按下即斩，同一玩家已有残心斩进行中时忽略并返回 null
        /// </summary>
        public static Projectile Fire(Player player, Vector2 aim, int damage, float knockback,
            bool sakura, bool syncedWithJudge, IEntitySource source = null) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<OniZanshinSlash>()] > 0) {
                return null;
            }
            source ??= player.GetSource_Misc("CWR_OniZanshinSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX * player.direction).ToRotation();
            return Projectile.NewProjectileDirect(source, player.Center, Vector2.Zero
                , ModContent.ProjectileType<OniZanshinSlash>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(aimAngle), ai1: sakura ? 1f : 0f, ai2: syncedWithJudge ? 1f : 0f);
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Lifetime + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 60;   //伤害窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float seed = Projectile.identity * 0.6180339887f % 1f;
            float cos = MathF.Cos(CutAngle);
            facing = MathF.Abs(cos) < 0.05f ? Owner.direction : MathF.Sign(cos);
            Owner.ChangeDir(facing);

            //反拔起手:纳刀位有新鲜交接就从那里划出,否则反手预备位
            if (!OniBladeHandoff.TryPeek(Owner, out drawStartRot, out _)) {
                drawStartRot = CutAngle - facing * 2.0f;
            }

            arcDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                ErodeStart = 8, ErodeFrames = 22,
                ColorShiftDelay = 10, ColorShiftFrames = 20,
                Mode = 0f, Rot = CutAngle, Span = ArcSpan,
                Thick = 0.42f,
                HalfX = ArcHalfX, HalfY = ArcHalfY, Flip = facing,
                Opacity = 1f, FrontGlow = IsSakura ? 2.1f : 1.9f, Seed = seed + 0.29f,
                TailErode = 0.35f, FlashPower = 0.9f,
                RazorTailWiden = 0.70f,
                Palette = IsSakura ? SakuraPalette : OFR.BladePalette.Escalate(0.25f),
            };
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                DetonateFx();
            }
            timer++;

            bladePose.Update();
            if (timer <= PoseFrames + ZanshinHoldFrames && Owner.active && !Owner.dead) {
                ApplyCastPose();
            }

            StepIn();

            //弧完全揭开的那一帧绽放瓣爆(樱衣,客户端本地)
            if (IsSakura && timer == 2 && !Main.dedServ) {
                BloomPetals();
            }
            UpdatePetals();

            float seam = MathF.Exp(-timer * 0.11f);
            Vector3 glow = IsSakura ? new Vector3(1.15f, 0.55f, 0.66f) : new Vector3(1.20f, 0.42f, 0.28f);
            Lighting.AddLight(Projectile.Center, glow * seam * 1.2f);
        }

        /// <summary>踏步前压:出刀头 3 帧沿刀线小步压进(子步碰撞),旧动量顺势衰减不清零</summary>
        private void StepIn() {
            if (!Projectile.IsOwnedByLocalPlayer() || timer > StepFrames
                || Owner.mount?.Active == true || !Owner.active || Owner.dead) {
                return;
            }
            Vector2 dir = CutAngle.ToRotationVector2();
            float moved = 0f;
            while (moved < StepPerFrame - 0.01f) {
                float sub = MathF.Min(12f, StepPerFrame - moved);
                Vector2 next = Owner.position + dir * sub;
                if (Collision.SolidCollision(next, Owner.width, Owner.height)) {
                    break;
                }
                Owner.position = next;
                moved += sub;
            }
            Owner.velocity *= 0.6f;
            Owner.fallStart = (int)(Owner.position.Y / 16f);
        }

        /// <summary>
        /// 反拔甩刀：实体刀 4 帧内从纳刀交接位反手甩到收势位(挥动帧甩残影)，
        /// 随后残心停刀、末段淡出；只摆姿态不锁位移，itemTime 锁仅覆盖甩刀窗
        /// </summary>
        private void ApplyCastPose() {
            float sw = OFR.EaseOutCubic(MathHelper.Clamp(timer / (float)PoseFrames, 0f, 1f));
            bladePose.Rotation = OniBladePose.LerpAngle(drawStartRot, CutAngle + facing * 0.55f, sw);

            if (timer <= PoseFrames) {
                Owner.itemTime = Owner.itemAnimation = 2;
                Owner.itemRotation = MathHelper.WrapAngle(CutAngle
                    + Owner.direction * MathHelper.Lerp(-0.8f, 0.45f, sw));
                bladePose.Opacity = 1f;
                if (timer >= 2) {
                    bladePose.PushSmear(1f);
                }
            }
            else {
                //残心:停在收势位,后段淡出;玩家续连段或另起技能时立刻放手
                if (OniBladeOccupancy.ComboClaims(Owner) || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                    bladePose.Opacity = 0f;
                    return;
                }
                bladePose.Opacity = 1f - MathHelper.Clamp((timer - PoseFrames - 4f) / (ZanshinHoldFrames - 4f), 0f, 1f);
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层:反拔实体刀与其残影,稳定盖在弧刃之上</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        /// <summary>出生帧声画:与锵同帧时震屏/Bloom 减半(纳刀结算已经砸过一拍)</summary>
        private void DetonateFx() {
            SoundEngine.PlaySound(CWRSound.KatanaSwing with { Volume = 0.85f, Pitch = 0.05f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.42f, Volume = 0.55f }, Projectile.Center);
            if (IsSakura) {
                //樱衣:风铃 + 草叶簌响,花瓣的材质声
                SoundEngine.PlaySound(SoundID.Item35 with { Pitch = 0.30f, Volume = 0.52f, MaxInstances = 2 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Grass with { Pitch = -0.15f, Volume = 0.60f }, Projectile.Center);
            }

            Owner.CWR().GetScreenShake(SyncedJudge ? 2.5f : 5f);

            if (Main.dedServ) {
                return;
            }
            CrimsonImpactFX.PushImpact(Projectile.Center, SyncedJudge ? 0.05f : 0.10f);
            CrimsonImpactFX.PushAmbience(Projectile.Center, 0.22f);
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , IsSakura ? new Color(255, 226, 233) : new Color(255, 232, 212), 1.1f);

            Vector2 dir = CutAngle.ToRotationVector2();
            if (IsSakura) {
                //粉雾两缕垫底(花瓣主体在瓣爆里)
                for (int i = 0; i < 5; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.7) * Main.rand.NextFloat(2f, 5f);
                    PRTLoader.NewParticle<PRT_CrimsonSmoke>(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f)
                        , vel, Color.White, Main.rand.NextFloat(0.07f, 0.12f))
                        ?.Configure(Main.rand.Next(18, 28), new Color(232, 142, 164), new Color(96, 38, 58));
                }
            }
            else {
                //墨衣:墨滴甩出 + 绯红火花
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.85) * Main.rand.NextFloat(4f, 10f);
                    vel.Y -= Main.rand.NextFloat(0f, 1.6f);
                    PRTLoader.NewParticle<PRT_OniInkDrop>(Projectile.Center, vel, new Color(60, 14, 20)
                        , Main.rand.NextFloat(0.26f, 0.46f))
                        ?.Configure(Main.rand.Next(18, 30));
                }
                for (int i = 0; i < 6; i++) {
                    Vector2 vel = dir.RotatedByRandom(0.6) * Main.rand.NextFloat(4f, 9f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center, vel, new Color(255, 116, 66)
                        , Main.rand.NextFloat(0.35f, 0.6f))
                        ?.Configure(Main.rand.Next(14, 24), affectedByGravity: true);
                }
            }
        }

        //==================== 判定 ====================

        public override bool? CanHitNPC(NPC target) {
            if (timer > DamageEnd) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        /// <summary>巨物减伤(与灭世一闪同表,伤害基数低故略缓):蠕虫节体 0.25,阿瑞斯节段 0.45</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.25f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.45f;
            }
        }

        /// <summary>
        /// 三层判定(缩规格移植灭世一闪):贴身小圈 + 弧带折线(16 段) + 扇形补心辐条;
        /// localNPCHitCooldown 大于伤害窗,多层命中仍是单次结算
        /// </summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized) {
                return false;
            }

            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(Projectile.Center.Y, targetHitbox.Top, targetHitbox.Bottom));
            if (Vector2.DistanceSquared(Projectile.Center, nearest) <= NearRadius * NearRadius) {
                return true;
            }

            const int Segments = 16;
            OFR.BladeState state = OFR.ComputeState(in arcDef, Math.Max(timer, 1));
            float cp = 0f;
            Vector2 prev = OFR.PointAt(in arcDef, in state, Projectile.Center, 0f);
            for (int i = 1; i <= Segments; i++) {
                Vector2 next = OFR.PointAt(in arcDef, in state, Projectile.Center, i / (float)Segments);
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , prev, next, ArcCorridor, ref cp)) {
                    return true;
                }
                if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                    , Projectile.Center, (prev + next) * 0.5f, SpokeWidth, ref cp)) {
                    return true;
                }
                prev = next;
            }
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = 0.25f, Volume = 0.85f, MaxInstances = 3 }, target.Center);
            target.CWR().TimeFrozenTick = 3;   //穿身微滞:惊人一刻的咬合感
            if (Projectile.IsOwnedByLocalPlayer()) {
                Owner.GetModPlayer<OnikiriPlayer>().OnZanshinHit(target);
            }

            if (Main.dedServ) {
                return;
            }
            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(target.Center, Vector2.Zero
                , IsSakura ? new Color(255, 214, 224) : new Color(255, 222, 198), 0.9f);
            Vector2 cutDir = CutAngle.ToRotationVector2();
            if (IsSakura) {
                //命中溅散瓣:花瓣沿刀线掠出
                for (int i = 0; i < 6 && petals.Count < PetalCount + 14; i++) {
                    SpawnPetal(target.Center + Main.rand.NextVector2Circular(12f, 12f)
                        , cutDir.RotatedByRandom(0.55) * Main.rand.NextFloat(3.5f, 8f)
                            - Vector2.UnitY * Main.rand.NextFloat(0f, 1.2f)
                        , Main.rand.Next(30, 48), Main.rand.NextFloat(0.5f, 0.8f));
                }
            }
            else {
                for (int i = 0; i < 7; i++) {
                    Vector2 vel = cutDir.RotatedByRandom(0.5) * Main.rand.NextFloat(4f, 11f);
                    PRTLoader.NewParticle<PRT_OniShard>(target.Center, vel, new Color(255, 132, 76)
                        , Main.rand.NextFloat(0.35f, 0.65f))
                        ?.Configure(Main.rand.Next(16, 28), Main.rand.NextFloat(-0.22f, 0.22f)
                            , Main.rand.NextFloat(1.3f, 2.2f), affectedByGravity: true);
                }
            }
        }

        //==================== 樱瓣(客户端本地) ====================

        /// <summary>弧完全揭开帧沿弧带一次性绽放花瓣:外法线向散出,随后飘落</summary>
        private void BloomPetals() {
            OFR.BladeState state = OFR.ComputeState(in arcDef, timer);
            for (int i = 0; i < PetalCount; i++) {
                float uc = (i + 0.5f) / PetalCount;
                Vector2 onArc = OFR.PointAt(in arcDef, in state, Projectile.Center, uc);
                Vector2 outward = (onArc - Projectile.Center).SafeNormalize(CutAngle.ToRotationVector2());
                Vector2 pos = onArc + Main.rand.NextVector2Circular(18f, 18f);
                Vector2 vel = outward * Main.rand.NextFloat(2.2f, 6.5f)
                    + outward.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-1.4f, 1.4f)
                    - Vector2.UnitY * Main.rand.NextFloat(0f, 0.8f);
                SpawnPetal(pos, vel, Main.rand.Next(34, 58), Main.rand.NextFloat(0.55f, 0.9f));
            }
        }

        private void SpawnPetal(Vector2 position, Vector2 velocity, int life, float alpha) {
            petals.Add(new Petal {
                Position = position,
                Velocity = velocity,
                Rotation = Main.rand.NextFloat(MathHelper.TwoPi),
                RotSpeed = Main.rand.NextFloat(-0.13f, 0.13f),
                Scale = Main.rand.NextFloat(0.45f, 0.95f),
                Spin = Main.rand.NextFloat(0.055f, 0.14f),
                Seed = Main.rand.NextFloat(MathHelper.TwoPi),
                Alpha = alpha,
                MaxLife = life,
                DeepColor = Main.rand.NextBool(16),
            });
        }

        private void UpdatePetals() {
            for (int i = petals.Count - 1; i >= 0; i--) {
                Petal petal = petals[i];
                petal.Age++;
                if (petal.Age >= petal.MaxLife) {
                    petals.RemoveAt(i);
                    continue;
                }
                petal.Velocity *= 0.965f;
                petal.Velocity.Y += 0.014f;
                petal.Velocity.X += MathF.Sin(petal.Age * 0.11f + petal.Seed) * 0.03f;
                petal.Position += petal.Velocity;
                petal.Rotation += petal.RotSpeed;
                petal.Depth = MathF.Sin(petal.Age * petal.Spin + petal.Seed);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) => false;

        /// <summary>弧刃(水墨管线) + 樱瓣批次:实体扩展图元层</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OAR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                OFR.BladeState arcState = OFR.ComputeState(in arcDef, timer);
                if (arcState.Opacity > 0.012f) {
                    OAR.InkParams ink = ComposeInk();
                    OAR.DrawBladeLayers(device, fx, in arcDef, in arcState, Projectile.Center, in ink);
                }
                OAR.EndDraw(device, pb, pr, pd);
            }

            DrawPetals();
        }

        /// <summary>水墨旋钮:墨衣全套笔法;樱衣压低墨阶/飞白(花瓣不是墨,弧只做能量载体)</summary>
        private OAR.InkParams ComposeInk() {
            float erodeT = MathHelper.Clamp((timer - 7) / 22f, 0f, 1f);
            if (IsSakura) {
                return new OAR.InkParams {
                    InkStep = 0.35f,
                    FeiBai = 0.15f + 0.30f * erodeT,
                    Bleed = MathHelper.Clamp((timer - 7) / 20f, 0f, 1f) * 0.8f,
                    SplitTail = 0.55f,
                };
            }
            return new OAR.InkParams {
                InkStep = 0.80f,
                FeiBai = 0.25f + 0.50f * erodeT,
                Bleed = MathHelper.Clamp((timer - 7) / 20f, 0f, 1f),
                SplitTail = 0.85f,
            };
        }

        /// <summary>樱瓣批次:TechPetal 形体,PSPetal 自行输出预乘色,这里只写透明度</summary>
        private void DrawPetals() {
            if (petals.Count == 0
                || VaultAsset.placeholder2?.Value is not Texture2D white
                || EffectLoader.OniDomainDeco?.Value is not Effect effect) {
                return;
            }

            petals.Sort(static (a, b) => a.Depth.CompareTo(b.Depth));

            SpriteBatch spriteBatch = Main.spriteBatch;
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);

            effect.Parameters["uTime"]?.SetValue((float)Main.timeForVisualEffects * 0.05f);
            effect.CurrentTechnique = effect.Techniques["TechPetal"];
            effect.CurrentTechnique.Passes[0].Apply();

            Vector2 origin = white.Size() * 0.5f;
            foreach (Petal petal in petals) {
                float life = petal.Age / (float)petal.MaxLife;
                float envelope = MathF.Pow(MathF.Sin(life * MathHelper.Pi), 0.5f);
                float opacity = MathHelper.Clamp(petal.Alpha * envelope, 0f, 1f);
                if (opacity <= 0.01f) {
                    continue;
                }

                float front = (petal.Depth + 1f) * 0.5f;
                Color back = petal.DeepColor ? new Color(178, 48, 79) : new Color(244, 157, 183);
                Color middle = petal.DeepColor ? new Color(229, 90, 119) : new Color(255, 196, 213);
                Color face = petal.DeepColor ? new Color(255, 174, 191) : new Color(255, 243, 247);
                Color color = front < 0.5f
                    ? Color.Lerp(back, middle, front * 2f)
                    : Color.Lerp(middle, face, front * 2f - 1f);
                color.A = (byte)(opacity * byte.MaxValue);

                float flip = MathHelper.Lerp(0.18f, 1f, MathF.Abs(petal.Depth));
                float stretch = 1f + MathHelper.Clamp(petal.Velocity.Length() / 10f, 0f, 0.3f);
                float width = 19f * petal.Scale * flip;
                float height = 25f * petal.Scale * stretch;
                spriteBatch.Draw(white, petal.Position - Main.screenPosition, null, color,
                    petal.Rotation, origin,
                    new Vector2(width / white.Width, height / white.Height),
                    SpriteEffects.None, 0f);
            }

            spriteBatch.End();
        }

        /// <summary>加色层:出生星芒过曝一拍钉住出刀点(与锵同帧时纳刀结算自带白闪,免去)</summary>
        void IAdditiveDrawable.DrawAdditiveAfterNon(SpriteBatch spriteBatch) {
            if (Main.dedServ || SyncedJudge || timer >= 5
                || CWRAsset.StarFlare02?.Value is not Texture2D flare) {
                return;
            }
            float t = timer / 5f;
            float alpha = MathF.Pow(1f - t, 1.5f) * 0.8f;
            Color color = IsSakura ? new Color(255, 216, 228) : new Color(255, 238, 224);
            Vector2 pos = Projectile.Center + CutAngle.ToRotationVector2() * 46f - Main.screenPosition;
            spriteBatch.Draw(flare, pos, null, color * alpha, arcDef.Seed * 5f
                , flare.Size() * 0.5f, (0.9f + t * 0.4f), SpriteEffects.None, 0);
        }
    }
}
