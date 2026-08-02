using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using OAR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates.OniAnnihilateRenderer;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates
{
    /// <summary>鬼哭·灭世一闪. ai[0]=刀线角(弧度) ai[1]=尺寸倍率. 50%架势技能</summary>
    internal class OniAnnihilate : ModProjectile, IPrimitiveDrawable, ICrimsonFarDrawable, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>伤害窗末帧</summary>
        private const int DamageEnd = 8;
        /// <summary>演出总时长</summary>
        private const int Lifetime = 46;
        /// <summary>摆臂帧</summary>
        private const int PoseFrames = 6;
        /// <summary>残心余韵帧</summary>
        private const int ZanshinFrames = 12;
        /// <summary>主弧半长轴(px)</summary>
        private const float ArcHalfX = 760f;
        /// <summary>主弧半短轴(px)</summary>
        private const float ArcHalfY = 690f;
        /// <summary>近身罡气半径(px)</summary>
        private const float NearBurstRadius = 320f;
        /// <summary>擦边外扩(px)</summary>
        private const int GrazePad = 18;
        /// <summary>弧带厚度贪婪倍率</summary>
        private const float ArcThickMul = 1.12f;
        /// <summary>扇形辐条宽(px)</summary>
        private const float SpokeWidth = 180f;
        /// <summary>罡气舌数</summary>
        private const int TongueCount = 10;

        private OFR.BladeDef arcDef; //主弧

        private bool initialized;
        private bool hitVfxBurst;
        private int timer;
        /// <summary>髭切断首:本闪的击杀返势已结算(每次招式至多一次)</summary>
        private bool executeRefunded;

        //罡气舌,出生帧定死不追人
        private readonly float[] tongueAngle = new float[TongueCount];
        private readonly float[] tongueLen = new float[TongueCount];
        private readonly float[] tongueHalfWidth = new float[TongueCount];
        private readonly float[] tongueSeed = new float[TongueCount];

        private float CutAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];
        private OniMeiActionContext ActionContext => OniMeiActionContext.Get(Projectile);

        private readonly OniBladePose bladePose = new();

        /// <summary>硬占刀权,Pose+4 帧</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= PoseFrames + 4;

        /// <summary>软保留收势,Pose+6 帧</summary>
        bool IOniBladeOccupant.ReservesBlade => timer <= PoseFrames + 6;

        /// <summary>owner 端触发,已有进行中则返 null</summary>
        /// <param name="focus">刀线中心,一般玩家中心</param>
        /// <param name="aim">瞄准方向,可未归一化</param>
        /// <param name="source">null 回退 Misc</param>
        public static Projectile Fire(Player player, Vector2 focus, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null, int baseWeaponDamage = 0) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0) {
                return null;
            }
            source ??= player.GetSource_Misc("CWR_OniAnnihilate");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX * player.direction).ToRotation();
            Projectile projectile = Projectile.NewProjectileDirect(source, focus, Vector2.Zero
                , ModContent.ProjectileType<OniAnnihilate>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(aimAngle), ai1: scale);
            OniMeiActionContext.Capture(projectile, player, source,
                baseWeaponDamage > 0 ? baseWeaponDamage : Math.Max(1, damage / 5), OniMeiActionKind.Annihilate);
            OniMeiActionContext.ArmConditions(projectile, player,
                allowSilent: false, allowPlanted: true);
            return projectile;
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
            Projectile.localNPCHitCooldown = 60; //伤害窗单次结算
        }

        public override bool ShouldUpdatePosition() => false;

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;

            float cos = MathF.Cos(CutAngle);
            int facingDir = MathF.Abs(cos) < 0.05f ? Owner.direction : MathF.Sign(cos);
            Owner.ChangeDir(facingDir);
            float flip = facingDir;

            //主弧中心=玩家,Rot=瞄准角,两帧揭开
            arcDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                ErodeStart = 10, ErodeFrames = 30,
                ColorShiftDelay = 12, ColorShiftFrames = 26,
                Mode = 0f, Rot = CutAngle, Span = 3.60f,
                Thick = 0.40f,
                HalfX = ArcHalfX * s, HalfY = ArcHalfY * s, Flip = flip,
                Opacity = 1f, FrontGlow = 2.2f, Seed = seed + 0.37f,
                TailErode = 0.35f, FlashPower = 1f,
                RazorTailWiden = 0.85f,
                Palette = OFR.BladePalette.Escalate(0.55f),
            };

            //黄金角均布+抖动
            const float GoldenAngle = 2.39996323f;
            for (int i = 0; i < TongueCount; i++) {
                tongueAngle[i] = MathHelper.WrapAngle(seed * MathHelper.TwoPi + i * GoldenAngle
                    + Main.rand.NextFloat(-0.15f, 0.15f));
                tongueLen[i] = Main.rand.NextFloat(190f, 280f) * s;
                tongueHalfWidth[i] = Main.rand.NextFloat(27f, 45f) * s;
                tongueSeed[i] = seed + i * 0.173f;
            }
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                DetonateFx();
            }
            timer++;

            bladePose.Update();
            if (timer <= PoseFrames + ZanshinFrames && Owner.active && !Owner.dead) {
                ApplyCastPose();
            }

            float seam = MathF.Exp(-timer * 0.10f);
            Lighting.AddLight(Projectile.Center, new Vector3(1.35f, 0.55f, 0.32f) * seam * 1.5f);
        }

        /// <summary>摆臂姿态,不锁位移,残心期放手</summary>
        private void ApplyCastPose() {
            int dir = MathF.Cos(CutAngle) >= 0f ? 1 : -1;
            float sw = OFR.EaseOutCubic(MathHelper.Clamp(timer / (float)PoseFrames, 0f, 1f));
            bladePose.Rotation = CutAngle + dir * MathHelper.Lerp(-2.0f, 0.55f, sw);

            if (timer <= PoseFrames) {
                Owner.itemTime = Owner.itemAnimation = 2;
                Owner.itemRotation = MathHelper.WrapAngle(CutAngle
                    + Owner.direction * MathHelper.Lerp(-0.9f, 0.45f, sw));
                bladePose.Opacity = 1f;
                if (timer >= 2) {
                    bladePose.PushSmear(1f);
                }
            }
            else {
                //残心停刀,连段/硬占则放手
                if (OniBladeOccupancy.ComboClaims(Owner) || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                    bladePose.Opacity = 0f;
                    return;
                }
                bladePose.Opacity = 1f - MathHelper.Clamp((timer - PoseFrames - 4f) / (ZanshinFrames - 4f), 0f, 1f);
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>实体刀遮挡层</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        /// <summary>出生帧声画</summary>
        private void DetonateFx() {
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.30f, Volume = 1f }, Projectile.Center);
            //SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.50f, Volume = 0.85f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.50f, Volume = 0.90f }, Projectile.Center);
            //SoundEngine.PlaySound(CWRSound.KatanaA, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.KatanaSwing, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            //大招节点,准入一次白闪
            CrimsonImpactFX.PushImpact(Projectile.Center, 0.20f);
            CrimsonImpactFX.PushAmbience(Projectile.Center, 0.35f);

            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                , perp, 15f, 9f, 24, -1f, FullName));

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , new Color(255, 236, 216), 1.7f * SizeMul);

            SpawnBurstParticles();
        }

        /// <summary>罡气粒子敷层</summary>
        private void SpawnBurstParticles() {
            float s = SizeMul;
            Vector2 feet = Owner.active ? Owner.Bottom : Projectile.Center;

            //墨浪烟横推
            for (int i = 0; i < 16; i++) {
                float dir = Main.rand.NextBool() ? 1f : -1f;
                Vector2 pos = feet + new Vector2(dir * Main.rand.NextFloat(6f, 30f) * s, -Main.rand.NextFloat(0f, 14f));
                Vector2 vel = new(dir * Main.rand.NextFloat(4f, 9f), -Main.rand.NextFloat(0.2f, 1.1f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.10f, 0.17f) * s)
                    ?.Configure(Main.rand.Next(26, 40), new Color(70, 18, 26), new Color(18, 8, 14));
            }
            //竖直上涌
            for (int i = 0; i < 6; i++) {
                Vector2 pos = Projectile.Center + new Vector2(Main.rand.NextFloat(-26f, 26f) * s, Main.rand.NextFloat(-10f, 16f));
                Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(1.4f, 2.6f));
                PRTLoader.NewParticle<PRT_CrimsonSmoke>(pos, vel, Color.White
                    , Main.rand.NextFloat(0.09f, 0.14f) * s)
                    ?.Configure(Main.rand.Next(30, 44), new Color(60, 16, 24), new Color(16, 8, 14));
            }
            //墨滴,AlphaBlend(加色画不了黑)
            for (int i = 0; i < 14; i++) {
                Vector2 vel = (Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2()
                    * Main.rand.NextFloat(5f, 13f);
                vel.Y -= Main.rand.NextFloat(0f, 2.5f);
                PRTLoader.NewParticle<PRT_OniInkDrop>(Projectile.Center, vel, new Color(60, 14, 20)
                    , Main.rand.NextFloat(0.30f, 0.55f) * s)
                    ?.Configure(Main.rand.Next(22, 36));
            }
            //绯红火花点缀
            for (int i = 0; i < 10; i++) {
                Vector2 vel = (Main.rand.NextFloat(MathHelper.TwoPi)).ToRotationVector2()
                    * Main.rand.NextFloat(4f, 11f);
                PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center, vel, new Color(255, 120, 70)
                    , Main.rand.NextFloat(0.4f, 0.7f) * s)
                    ?.Configure(Main.rand.Next(18, 30), affectedByGravity: true);
            }
        }

        public override bool? CanHitNPC(NPC target) {
            if (timer > DamageEnd) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        /// <summary>蠕虫0.25 阿瑞斯节段0.5</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                ? ActionContext.Profile
                : OniMeiCombatProfile.Identity;
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.25f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
            //对双子魔眼造成1.25倍伤害
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            //对塔纳托斯体节仅造成50%伤害
            if (target.type == CWRID.NPC_ThanatosBody1 || target.type == CWRID.NPC_ThanatosBody2) {
                modifiers.FinalDamage *= 2f;
            }
            //对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
            //髭切「断首」/旧首「取首」:斩杀线内随已损生命递增的终结倍率(owner 端结算,随命中包同步)
            if (Projectile.IsOwnedByLocalPlayer()) {
                float meiMul = Owner.GetModPlayer<OnikiriPlayer>().BuildMeiHitMultiplier(
                    target, in profile, ActionContext?.ActionSerial ?? 0,
                    allowPlanted: true, allowIron: false, zanshin: false,
                    armedConditionMul: ActionContext?.ArmedConditionMul ?? 1f,
                    tideOnBeatSnapshot: ActionContext?.TideOnBeat == true);
                if (OniMeiCombat.TryGetExecuteBonus(in profile, target, out float executeMul)) {
                    meiMul *= executeMul;
                }
                modifiers.FinalDamage *= OniMeiCombat.ClampConditionalDamage(
                    meiMul, in profile, target);
            }
            float offsetX = Projectile.To(target.Center).X;
            modifiers.HitDirectionOverride = MathF.Abs(offsetX) > 0.01f
                ? Math.Sign(offsetX)
                : (MathF.Cos(CutAngle) >= 0f ? 1 : -1);
            OnikiriItem.ApplySlashPenetration(target, ref modifiers);
        }

        /// <summary>擦边外扩+弧折线+辐条+贴身圈,ScaleMul 下限防出生漏打</summary>
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (!initialized) {
                return false;
            }

            Rectangle greedyBox = targetHitbox;
            greedyBox.Inflate(GrazePad, GrazePad);

            float nearR = NearBurstRadius * SizeMul;
            Vector2 nearest = new(
                MathHelper.Clamp(Projectile.Center.X, greedyBox.Left, greedyBox.Right),
                MathHelper.Clamp(Projectile.Center.Y, greedyBox.Top, greedyBox.Bottom));
            if (Vector2.DistanceSquared(Projectile.Center, nearest) <= nearR * nearR) {
                return true;
            }

            const int Segments = 24;
            OFR.BladeState state = OFR.ComputeState(in arcDef, Math.Max(timer, 1));
            float hitScale = MathF.Max(state.ScaleMul, 0.92f);
            //厚度对齐视觉 Thick×HalfX
            float thickWorld = MathF.Max(56f, arcDef.Thick * arcDef.HalfX * hitScale * ArcThickMul);
            float spokeW = SpokeWidth * SizeMul;
            float cp = 0f;
            Vector2 prev = HitPointAt(in state, hitScale, 0f);
            for (int i = 1; i <= Segments; i++) {
                float uc = i / (float)Segments;
                Vector2 next = HitPointAt(in state, hitScale, uc);
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , prev, next, thickWorld, ref cp)) {
                    return true;
                }
                if (Collision.CheckAABBvLineCollision(greedyBox.TopLeft(), greedyBox.Size()
                    , Projectile.Center, (prev + next) * 0.5f, spokeW, ref cp)) {
                    return true;
                }
                prev = next;
            }
            return false;
        }

        /// <summary>碰撞用弧上点,强制 hitScale</summary>
        private Vector2 HitPointAt(in OFR.BladeState state, float hitScale, float uc) {
            OFR.BladeState hitState = state;
            hitState.ScaleMul = hitScale;
            return OFR.PointAt(in arcDef, in hitState, Projectile.Center, uc);
        }

        /// <summary>弧带+辐条割草</summary>
        public override void CutTiles() {
            if (!initialized || timer > DamageEnd) {
                return;
            }
            DelegateMethods.tilecut_0 = Terraria.Enums.TileCuttingContext.AttackProjectile;

            const int Samples = 14;
            OFR.BladeState state = OFR.ComputeState(in arcDef, Math.Max(timer, 1));
            float hitScale = MathF.Max(state.ScaleMul, 0.92f);
            float width = MathF.Max(40f, arcDef.Thick * arcDef.HalfX * hitScale * 0.95f);
            float spokeW = SpokeWidth * SizeMul;
            Vector2 prev = Vector2.Zero;
            bool hasPrev = false;
            for (int k = 0; k < Samples; k++) {
                float uc = k / (float)(Samples - 1);
                Vector2 mid = HitPointAt(in state, hitScale, uc);
                if (hasPrev) {
                    Utils.PlotTileLine(prev, mid, width, DelegateMethods.CutTiles);
                }
                if (k % 2 == 0) {
                    Utils.PlotTileLine(Projectile.Center, mid, spokeW, DelegateMethods.CutTiles);
                }
                prev = mid;
                hasPrev = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(CWRSound.KatanaHit with { Pitch = 0.15f, Volume = 1.2f }, target.Center);

            //髭切断首:入线命中画断线,了结返势(每闪一次)
            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                OnikiriPlayer onikiri = Owner.GetModPlayer<OnikiriPlayer>();
                onikiri.OnPrimaryBladeHit(target, in profile);
                OniMeiCombat.OnExecuteStrikeHit(Owner, target, CutAngle, ref executeRefunded,
                    in profile, ActionContext?.ActionSerial ?? 0);
                if (!target.active || target.life <= 0) {
                    onikiri.TryPetalPruneOnKill(target,
                        ActionContext?.BaseWeaponDamage ?? Math.Max(1, Projectile.damage / 5),
                        Projectile.knockBack, Projectile, in profile);
                }
            }

            if (Main.dedServ) {
                return;
            }
            bool steel = CWRLoad.NPCValue.ISTheofSteel(target);
            Vector2 cutDir = CutAngle.ToRotationVector2();
            if (!hitVfxBurst) {
                hitVfxBurst = true;
                CrimsonRendHitVFX.SpawnImpactBurst(target.Center, cutDir, 1f, SizeMul, steel);
            }
            else {
                CrimsonRendHitVFX.SpawnHitTick(target.Center, cutDir, SizeMul, steel);
            }
        }

        /// <summary>主弧水墨旋钮时间轴</summary>
        private OAR.InkParams ComposeInk() {
            float erodeT = MathHelper.Clamp((timer - 8) / 26f, 0f, 1f);
            return new OAR.InkParams {
                InkStep = 0.85f,
                FeiBai = 0.30f + 0.55f * erodeT,
                Bleed = MathHelper.Clamp((timer - 8) / 22f, 0f, 1f),
                SplitTail = 0.90f,
            };
        }

        /// <summary>主弧,实体扩展图元层</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }

            OFR.BladeState arcState = OFR.ComputeState(in arcDef, timer);
            if (arcState.Opacity > 0.012f) {
                OAR.InkParams ink = ComposeInk();
                OAR.DrawBladeLayers(device, fx, in arcDef, in arcState, Projectile.Center, in ink);
            }
            OAR.EndDraw(device, pb, pr, pd);
        }

        /// <summary>身后层罡气,<see cref="CrimsonFarLayerRender"/></summary>
        void ICrimsonFarDrawable.DrawFarSlashes() {
            if (Main.dedServ || !initialized || timer > 20) {
                return;
            }

            DrawBurstRings();

            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (!OAR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                return;
            }
            //舌根锚触发点
            float extend = OFR.EaseOutCubic(MathHelper.Clamp(timer / 7f, 0f, 1f));
            float dissolve = MathHelper.Clamp((timer - 6) / 10f, 0f, 1f);
            float intensity = 1f - 0.30f * dissolve;
            for (int i = 0; i < TongueCount; i++) {
                OAR.DrawTongue(device, fx, Projectile.Center, tongueAngle[i]
                    , tongueLen[i] * extend, tongueHalfWidth[i]
                    , tongueSeed[i], dissolve, intensity, 1f);
            }
            OAR.EndDraw(device, pb, pr, pd);
        }

        /// <summary>冲击环双层,暗墨+绯红缘</summary>
        private void DrawBurstRings() {
            if (CWRAsset.Ring01?.Value is not Texture2D ring) {
                return;
            }
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            SpriteBatch sb = Main.spriteBatch;

            //暗墨环
            float darkT = MathHelper.Clamp(timer / 12f, 0f, 1f);
            if (darkT < 1f) {
                float ease = 1f - MathF.Pow(1f - darkT, 3f);
                float scale = MathHelper.Lerp(0.4f, 2.6f, ease) * SizeMul;
                float alpha = 0.5f * MathF.Pow(1f - darkT, 1.2f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(ring, screenPos, null, new Color(28, 12, 18) * alpha, 0f
                    , ring.Size() * 0.5f, scale, SpriteEffects.None, 0);
                sb.End();
            }

            //绯红缘环,略超前
            float rimT = MathHelper.Clamp(timer / 10f, 0f, 1f);
            if (rimT < 1f) {
                float ease = 1f - MathF.Pow(1f - rimT, 3f);
                float scale = MathHelper.Lerp(0.55f, 2.9f, ease) * SizeMul;
                float alpha = 0.45f * MathF.Pow(1f - rimT, 2f);
                sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                sb.Draw(ring, screenPos, null, new Color(255, 90, 50) * alpha, 0f
                    , ring.Size() * 0.5f, scale, SpriteEffects.None, 0);
                sb.End();
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
