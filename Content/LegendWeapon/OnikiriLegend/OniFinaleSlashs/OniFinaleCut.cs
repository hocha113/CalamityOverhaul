using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions.Deeds;
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
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终之太刀·纳刀断世. ai[0]=刀线角(弧度) ai[1]=尺寸倍率</summary>
    internal class OniFinaleCut : ModProjectile, IPrimitiveDrawable, IOniCrispDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>细线滞拍帧数、出现→纳刀引爆的间隔，主控用它对齐解冻时刻</summary>
        public const int HoldFrames = 18;
        /// <summary>终斩视觉刀路半长</summary>
        public const float VisualHalfLength = 2550f;
        /// <summary>体节视觉筛选使用的中心刃线宽度</summary>
        public const float VisualPathWidth = 12f;
        private const int DamageEnd = HoldFrames + 8;
        private const int Lifetime = HoldFrames + 58;

        /// <summary>刀尖辉点跨屏行程、伤口揭开前沿追着它走。 必须在 <see</summary>
        private const int GlintFrames = 2;
        /// <summary>创面撕开（厚度弹开）时长，滞后辉点 1 帧</summary>
        private const int TearFrames = 3;
        /// <summary>裂屏推开起点、伤口先鼓起，世界随后才被推成两半</summary>
        private const int SplitStart = 2;
        /// <summary>悬停终点、世界保持裂开、伤口呼吸到此，随后开始合拢（缩短4帧，减少弹幕遮挡窗口）</summary>
        private const int HoldOpenEnd = 10;
        /// <summary>愈合终点、两半合拢、创面捏合完成</summary>
        private const int HealEnd = 32;
        /// <summary>余痕熄灭（光照/裂缝辉光走完）</summary>
        private const int AfterglowEnd = 42;
        /// <summary>飞白细创痕存活帧数</summary>
        private const int HairFrames = 6;

        private OFR.BladeDef lineDef;   //滞拍细线（斩痕本体，引爆后作残影随两半世界分开）

        private float woundHalfX;       //伤口断面 quad 半长

        private float woundHalfY;       //伤口断面 quad 半厚

        private bool initialized;
        private bool detonatedFx;
        private bool hitVfxBurst;
        private bool executeRefunded;
        private int timer;

        private float CutAngle => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private OniMeiActionContext ActionContext => OniMeiActionContext.Get(Projectile);
        /// <summary>裂屏滑移峰值（像素，两半各滑此值，视觉总豁口为其两倍；略降，错位感保留但不暴力）</summary>
        private float PeakSplitPx => 38f * SizeMul;

        /// <summary>伤口单帧动态量、由引爆后时间轴合成，直接映射 OniFinaleWound 的 uniform</summary>
        private struct WoundState
        {
            public float Open;      //创面厚度进度（含过冲/呼吸/愈合变薄）

            public float Heal;      //针尖向中心捏合进度

            public float Ember;     //断面降温 白热→余烬红

            public float Flash;     //全形白闪

            public float SweepEdge; //沿线揭开前沿（跟随刀尖辉点）

            public float Opacity;
        }

        /// <summary>触发接口、在持有者客户端调用</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="center">刀线中心（世界坐标）</param>
        /// <param name="cutAngle">刀线角度（弧度）</param>
        /// <param name="damage">伤害（引爆窗单次巨额结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 center, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleCut");
            return Projectile.NewProjectileDirect(source, center, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleCut>(), damage, knockback, player.whoAmI
                , ai0: MathHelper.WrapAngle(cutAngle), ai1: scale);
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
            Projectile.localNPCHitCooldown = 60;   //引爆窗单次结算

        }

        public override bool ShouldUpdatePosition() => false;

        //服务器代生成路径（OniSeverStrike 直接模式）里，伤害若只靠原版同步的
        //short 字段过线，2.5 倍后期武器伤害会超 32767 回绕——重携带 int 伤害
        public override void SendExtraAI(System.IO.BinaryWriter writer)
            => writer.Write(Projectile.damage);

        public override void ReceiveExtraAI(System.IO.BinaryReader reader)
            => Projectile.damage = reader.ReadInt32();

        private void Initialize() {
            initialized = true;
            float s = SizeMul;
            float seed = Projectile.identity * 0.6180339887f % 1f;
            float flip = Projectile.identity % 2 == 0 ? 1f : -1f;

            lineDef = new OFR.BladeDef {
                SweepFrames = 2, Life = Lifetime,
                DamageStart = HoldFrames, DamageEnd = DamageEnd,
                Mode = 1f, Rot = CutAngle, Span = 0f, Thick = 0.22f,
                HalfX = (VisualHalfLength + 50f) * s, HalfY = 26f * s, Flip = flip,
                Opacity = 0.85f, FrontGlow = 1.6f, Seed = seed,
                RazorTailWiden = 0f,
                Palette = OFR.BladePalette.Escalate(0.55f),
            };
            //断面 quad、长度对齐判定线（针尖恰在 2400px 判定端点附近收没），

            //厚度给创面外溢与撕裂参差留余量（shader 内最多用到 ~0.8）

            woundHalfX = VisualHalfLength * s;
            woundHalfY = 150f * s;
        }

        public override void AI() {
            if (!initialized) {
                Initialize();
                //出鞘的"斩击"本身近乎无声、世界还没意识到已经被斩开，但画面本身被切开一瞬

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 1f, Volume = 0.28f }, Projectile.Center);
                OniFinaleFX.PushSlice(Projectile.Center, CutAngle, 4f * SizeMul);
            }
            timer++;

            if (timer < HoldFrames) {
                //滞拍、细线在后效层保有一条微弱裂缝辉光，随呼吸渐强

                float breath = 0.18f + 0.16f * (timer / (float)HoldFrames)
                    + 0.05f * MathF.Sin(timer * 0.55f);
                OniFinaleFX.PushSplit(Projectile.Center, CutAngle, 0f, breath);
            }

            if (timer == HoldFrames - 1) {
                //引爆前 1 帧负片闪、刹那反白

                OniFinaleFX.PushNegative(Projectile.Center, 0.85f);
            }

            if (timer == HoldFrames) {
                DetonateFx();
            }

            if (timer >= HoldFrames) {
                int dt = timer - HoldFrames;
                DriveWorldSplit(dt);
                SpawnDetonationParticles(dt);
            }
        }

        /// <summary>裂屏包络、撕开</summary>
        private void DriveWorldSplit(int dt) {
            float offset;
            if (dt < SplitStart) {
                offset = 0f;
            }
            else if (dt <= SplitStart + 2) {
                offset = PeakSplitPx * OFR.EaseOutBack((dt - SplitStart) / 2f);
            }
            else if (dt <= HoldOpenEnd) {
                offset = PeakSplitPx * MathHelper.Lerp(1f, 0.74f
                    , OFR.SmoothStep01((dt - SplitStart - 2) / (float)(HoldOpenEnd - SplitStart - 2)));
            }
            else {
                float healT = OFR.SmoothStep01((dt - HoldOpenEnd) / (float)(HealEnd - HoldOpenEnd));
                offset = PeakSplitPx * 0.74f * (1f - healT);
            }
            if (offset < 0.5f) {
                offset = 0f;
            }

            //裂缝辉光、撕开期最亮，悬停微降，合拢完成的一瞬回光一跳，随后余痕熄灭

            float seam;
            if (dt <= HoldOpenEnd) {
                seam = MathHelper.Lerp(1f, 0.68f, dt / (float)HoldOpenEnd);
            }
            else if (dt <= HealEnd) {
                seam = MathHelper.Lerp(0.68f, 0.22f
                    , OFR.SmoothStep01((dt - HoldOpenEnd) / (float)(HealEnd - HoldOpenEnd)));
            }
            else {
                seam = 0.5f * MathF.Exp(-(dt - HealEnd) * 0.20f);
            }

            //按渲染端衰减预除，本帧渲染位移精确等于包络值、断面 quad 与虚空带物理对位

            OniFinaleFX.PushSplit(Projectile.Center, CutAngle, offset / OniFinaleFX.SplitDecay, seam);

            //引爆两帧压场至最黑、画面上只剩伤口在发光（与主控暗场取最大值，安全叠加）

            if (dt <= 2) {
                OniFinaleFX.PushDim(Projectile.Center, 0.94f);
            }

            float heat = dt <= HoldOpenEnd ? 1f
                : MathF.Max(0f, 1f - (dt - HoldOpenEnd) / (float)(AfterglowEnd - HoldOpenEnd));
            Lighting.AddLight(Projectile.Center, new Vector3(1.35f, 0.55f, 0.32f) * (0.3f + 1.2f * heat));
        }

        /// <summary>纳刀引爆帧、声画重拍</summary>
        private void DetonateFx() {
            detonatedFx = true;

            //纳刀脆响先行半拍打头，爆响垫底

            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = -0.15f, Volume = 0.95f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.35f, Volume = 1f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.50f, Volume = 0.80f }, Projectile.Center);
            SoundEngine.PlaySound(CWRSound.KatanaB, Projectile.Center);

            if (Main.dedServ) {
                return;
            }

            //全场过刃线兑现退场；积攒整场的碎镜面在此刻急速闭合，被斩碎的空间随纳刀合拢，
            //径向模糊与 RGB 色差同帧打峰（村正次元斩的爆发语言）

            OniFinaleLattice.CashIn();
            OniFinaleShatter.Burst(Projectile.Center);

            //碎镜闭合的一声玻璃脆响，垫在纳刀声浪之下

            SoundEngine.PlaySound(SoundID.Shatter with { Pitch = -0.10f, Volume = 0.55f }, Projectile.Center);

            //断世引爆,仪式节点准入白闪
            CrimsonImpactFX.PushImpact(Projectile.Center, 0.10f);

            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(Projectile.Center
                , perp, 14f, 9f, 22, -1f, FullName));

            PRTLoader.NewParticle<PRT_CrimsonHitFlash>(Projectile.Center, Vector2.Zero
                , new Color(255, 236, 216), 1.6f * SizeMul);
        }

        /// <summary>引爆后粒子排拍、碎晶帘压在世界被推开的一拍，悬停期断面持续渗余烬</summary>
        private void SpawnDetonationParticles(int dt) {
            if (Main.dedServ) {
                return;
            }
            Vector2 dir = CutAngle.ToRotationVector2();
            Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();

            if (dt == SplitStart + 1) {
                //沿刀线迸出的碎晶帘、压在裂屏位移可见的第一帧、鞘响在先、世界碎在后，撕裂有先后

                for (int i = 0; i < 18; i++) {
                    float along = Main.rand.NextFloat(-1f, 1f);
                    Vector2 pos = Projectile.Center + dir * along * 1300f * SizeMul;
                    Vector2 vel = perp * Main.rand.NextFloat(3f, 11f) * (Main.rand.NextBool() ? 1f : -1f)
                        + dir * Main.rand.NextFloat(-2f, 2f);
                    Color c = Main.rand.NextBool(3) ? new Color(255, 238, 215) : new Color(255, 115, 62);
                    PRTLoader.NewParticle<PRT_OniShard>(pos, vel, c
                        , Main.rand.NextFloat(0.5f, 0.95f) * SizeMul)
                        ?.Configure(Main.rand.Next(26, 44), Main.rand.NextFloat(-0.28f, 0.28f)
                            , Main.rand.NextFloat(1.8f, 3.2f), affectedByGravity: true);
                }
                for (int i = 0; i < 8; i++) {
                    Vector2 vel = perp.RotatedByRandom(0.5) * Main.rand.NextFloat(6f, 15f) * (Main.rand.NextBool() ? 1f : -1f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(Projectile.Center + dir * Main.rand.NextFloat(-400f, 400f) * SizeMul
                        , vel, new Color(255, 150, 95), Main.rand.NextFloat(0.5f, 0.9f) * SizeMul)
                        ?.Configure(Main.rand.Next(20, 34), affectedByGravity: true);
                }
            }

            //悬停期、余烬从两张断面之间渗出，顺各自半边世界缓慢漂离

            if (dt > TearFrames && dt <= HoldOpenEnd && timer % 2 == 0) {
                for (int i = 0; i < 2; i++) {
                    float along = Main.rand.NextFloat(-0.85f, 0.85f);
                    float side = Main.rand.NextBool() ? 1f : -1f;
                    Vector2 pos = Projectile.Center + dir * along * 2200f * SizeMul
                        + perp * side * Main.rand.NextFloat(8f, 34f) * SizeMul;
                    Vector2 vel = perp * side * Main.rand.NextFloat(0.4f, 1.6f)
                        + dir * Main.rand.NextFloat(-0.4f, 0.4f);
                    PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, vel, new Color(255, 150, 95)
                        , Main.rand.NextFloat(0.24f, 0.42f) * SizeMul)
                        ?.Configure(Main.rand.Next(18, 30), affectedByGravity: false);
                }
            }
        }

        public override bool? CanHitNPC(NPC target) {
            //零伤害生成 = 纯演出敷层（面影斩纸等），不参与判定

            if (Projectile.damage <= 0 || timer < HoldFrames || timer > DamageEnd) {
                return false;
            }
            return base.CanHitNPC(target);
        }

        /// <summary>巨物减伤（参照村正处刑斩）、蠕虫节体 0.5，阿瑞斯节段 0.75</summary>
        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) {
            if (CWRLoad.WormBodys.Contains(target.type)) {
                modifiers.FinalDamage *= 0.5f;
            }
            if (CWRLoad.ExoMechAresSegments.Contains(target.type)) {
                modifiers.FinalDamage *= 0.75f;
            }
            //对双子魔眼造成1.25倍伤害
            if (target.type == NPCID.Spazmatism || target.type == NPCID.Retinazer) {
                modifiers.FinalDamage *= 1.25f;
            }
            //对塔纳托斯头造成2.85倍伤害
            if (target.type == CWRID.NPC_ThanatosHead) {
                modifiers.FinalDamage *= 2.85f;
            }
            //对星流双子造成1.66倍伤害
            if (target.type == CWRID.NPC_Apollo || target.type == CWRID.NPC_Artemis) {
                modifiers.FinalDamage *= 1.66f;
            }
            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                OnikiriPlayer onikiri = Main.player[Projectile.owner].GetModPlayer<OnikiriPlayer>();
                float meiMul = onikiri.BuildMeiHitMultiplier(target, in profile,
                    ActionContext?.ActionSerial ?? 0, allowPlanted: true,
                    allowIron: false, zanshin: false,
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

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 dir = CutAngle.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * 2400f * SizeMul;
            Vector2 end = Projectile.Center + dir * 2400f * SizeMul;
            float cp = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size()
                , start, end, 110f * SizeMul, ref cp);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Pitch = -0.45f, Volume = 0.9f }, target.Center);

            if (Projectile.IsOwnedByLocalPlayer()) {
                OniMeiCombatProfile profile = ActionContext?.HasSnapshot == true
                    ? ActionContext.Profile
                    : OniMeiCombatProfile.Identity;
                Player owner = Main.player[Projectile.owner];
                OnikiriPlayer onikiri = owner.GetModPlayer<OnikiriPlayer>();
                onikiri.OnPrimaryBladeHit(target, in profile);
                OniMeiCombat.OnExecuteStrikeHit(owner, target, CutAngle, ref executeRefunded,
                    in profile, ActionContext?.ActionSerial ?? 0);
                onikiri.TryCallThunder(target, in profile,
                    ActionContext?.BaseWeaponDamage ?? Math.Max(1, Projectile.damage / 4),
                    Projectile.knockBack, Projectile);
                if (!target.active || target.life <= 0) {
                    onikiri.TryPetalPruneOnKill(target,
                        ActionContext?.BaseWeaponDamage ?? Math.Max(1, Projectile.damage / 4),
                        Projectile.knockBack, Projectile, in profile);
                    OniMeiDeedEvents.NotifyKill(owner, target, OniMeiDeedKillSource.Finale);
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

        /// <summary>滞拍细线、呼吸脉动，引爆后作残影被两半世界带着分开、快速淡出</summary>
        private OFR.BladeState ComposeLineState() {
            OFR.BladeState s = new() {
                Sweep = OFR.EaseOutCubic(timer / 2f),
                ScaleMul = 1f,
                FlowPhase = 0.5f * OFR.EaseOutCubic(timer / 12f),
                FrontGlow = timer <= 3 ? lineDef.FrontGlow : 0.3f,
            };
            float spawnFlash = timer <= 1 ? 0.8f : MathF.Pow(0.5f, timer - 1);
            s.Flash = spawnFlash > 0.02f ? spawnFlash : 0f;

            if (timer < HoldFrames) {
                //滞拍呼吸、亮度与厚度轻微起伏，攒住"将断未断"的势

                float breath = 0.5f + 0.5f * MathF.Sin(timer * 0.55f - MathHelper.PiOver2);
                s.Opacity = lineDef.Opacity * (0.72f + 0.22f * breath);
                s.ThickMul = 0.9f + 0.18f * breath;
                s.ColorShift = 0.15f;
            }
            else {
                //残影让位给伤口断面、比旧版更快退场

                int dt = timer - HoldFrames;
                float fade = MathHelper.Clamp(dt / 7f, 0f, 1f);
                s.Flash = MathF.Max(s.Flash, MathF.Pow(0.6f, dt));
                s.Opacity = lineDef.Opacity * (1f - fade);
                s.ThickMul = 1.2f - 0.5f * fade;
                s.Erode = fade * 0.8f;
            }
            return s;
        }

        /// <summary>刀尖辉点行进（0..1，近匀速跨屏、刻意不用重缓动，快到底才像刀）</summary>
        private static float GlintTravel(int dt)
            => MathHelper.Clamp((dt + 0.5f) / (GlintFrames + 0.5f), 0f, 1f);

        /// <summary>伤口时间轴合成、揭开前沿追着刀尖辉点，撕开带过冲，悬停呼吸，愈合捏薄降温</summary>
        private WoundState ComposeWoundState(int dt) {
            //dt0 只有辉点与细线白闪（1 帧纯线），dt1 起创面带过冲撕开

            float openT = MathHelper.Clamp(dt / (TearFrames + 1f), 0f, 1f);
            float open = OFR.EaseOutBack(openT);

            float healT = OFR.SmoothStep01((dt - HoldOpenEnd) / (float)(HealEnd - HoldOpenEnd));

            if (dt > TearFrames + 1 && dt <= HoldOpenEnd) {
                //悬停呼吸、防定格贴纸感

                open *= 1f + 0.04f * MathF.Sin(dt * 0.42f + lineDef.Seed * 9f);
            }
            open *= 1f - 0.38f * healT;   //愈合期创面变薄

            float flash = dt <= 1 ? 1f : MathF.Pow(0.55f, dt - 1);

            return new WoundState {
                Open = open,
                Heal = healT,
                Ember = OFR.SmoothStep01((dt - 8) / 20f),
                Flash = flash < 0.02f ? 0f : flash,
                SweepEdge = GlintTravel(dt) * 1.28f,
                //几何在 Heal→1 时自行收敛为零，无需透明度淡出

                Opacity = 1f,
            };
        }

        /// <summary>世界层只留伤口断面：它与裂屏位移物理对位，必须跟着两半世界一起被劈开/折射</summary>
        void IPrimitiveDrawable.DrawPrimitives() {
            if (Main.dedServ || !initialized || timer < HoldFrames) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OFR.BeginDraw(device, out Effect _, out var pb, out var pr, out var pd)) {
                DrawWoundLayers(device);
                OFR.EndDraw(device, pb, pr, pd);
            }
        }

        /// <summary>锋利层（后效之上）：滞拍细线与引爆敷层，刀线永远锋利，不被裂屏/切片/折射处理</summary>
        void IOniCrispDrawable.DrawCrisp() {
            if (Main.dedServ || !initialized) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                OFR.BladeState lineState = ComposeLineState();
                if (lineState.Opacity > 0.012f) {
                    OFR.DrawBladeLayers(device, fx, in lineDef, in lineState, Projectile.Center, 0f);
                }
                OFR.EndDraw(device, pb, pr, pd);
            }

            if (detonatedFx) {
                DrawDetonateDressing();
            }
        }

        /// <summary>伤口断面 + 飞白细创痕（复用 OFR.BeginDraw 设好的设备状态，仅换 Effect）</summary>
        private void DrawWoundLayers(GraphicsDevice device) {
            Effect fx = EffectLoader.OniFinaleWound?.Value;
            Texture2D noise = CWRAsset.NoiseSoft01?.Value;
            if (fx == null || noise == null) {
                return;
            }
            int dt = timer - HoldFrames;
            WoundState w = ComposeWoundState(dt);
            if (w.Opacity <= 0.012f || w.Heal >= 0.999f) {
                return;
            }

            fx.Parameters["transformMatrix"]?.SetValue(VaultUtils.GetTransfromMatrix());
            fx.Parameters["uNoiseTex"]?.SetValue(noise);
            OFR.BladePalette pal = OFR.BladePalette.WhiteHot;
            fx.Parameters["uColHot"]?.SetValue(pal.Hot);
            fx.Parameters["uColBright"]?.SetValue(pal.Bright);
            fx.Parameters["uColDeep"]?.SetValue(pal.Deep);
            fx.Parameters["uColDark"]?.SetValue(pal.Dark);

            //伤口本体

            DrawWoundQuad(device, fx, Projectile.Center, CutAngle
                , woundHalfX, woundHalfY, in w, lineDef.Flip, lineDef.Seed);

            //飞白、主创口两侧几条散开的细创痕，几帧就死、快到只留下毛边的速记

            if (dt <= HairFrames) {
                Vector2 dir = CutAngle.ToRotationVector2();
                Vector2 perp = (CutAngle + MathHelper.PiOver2).ToRotationVector2();
                for (int i = 0; i < 3; i++) {
                    float hs = (lineDef.Seed + i * 0.317f) % 1f;
                    float side = i % 2 == 0 ? lineDef.Flip : -lineDef.Flip;
                    Vector2 off = perp * side * (36f + 52f * hs) * SizeMul
                        + dir * (hs * 2f - 1f) * 340f * SizeMul;
                    WoundState hw = w;
                    hw.Open = 0.16f + 0.06f * hs;
                    hw.Heal = 0f;
                    hw.Ember = 0f;
                    hw.Flash = w.Flash * 0.7f;
                    hw.Opacity = (1f - dt / (float)HairFrames) * 0.8f;
                    hw.SweepEdge = w.SweepEdge * (0.9f + 0.12f * hs);
                    //微小角度偏差、飞白不与主线严格平行，像被同一刀带出的岔毫

                    DrawWoundQuad(device, fx, Projectile.Center + off, CutAngle + (hs - 0.5f) * 0.014f
                        , woundHalfX * (0.5f + 0.3f * hs), woundHalfY * 0.34f, in hw
                        , -side, lineDef.Seed + 0.53f + i * 0.29f);
                }
            }
        }

        private static void DrawWoundQuad(GraphicsDevice device, Effect fx, Vector2 center, float rot
            , float halfX, float halfY, in WoundState w, float flip, float seed) {
            fx.Parameters["uOpen"]?.SetValue(w.Open);
            fx.Parameters["uHeal"]?.SetValue(w.Heal);
            fx.Parameters["uEmber"]?.SetValue(w.Ember);
            fx.Parameters["uFlash"]?.SetValue(w.Flash);
            fx.Parameters["uSweepEdge"]?.SetValue(w.SweepEdge);
            fx.Parameters["uOpacity"]?.SetValue(w.Opacity);
            fx.Parameters["uFlip"]?.SetValue(flip);
            fx.Parameters["uSeed"]?.SetValue(seed);

            Vector2 ax = rot.ToRotationVector2();
            Vector2 ay = ax.RotatedBy(MathHelper.PiOver2);
            VertexPositionColorTexture[] verts = new VertexPositionColorTexture[4];
            verts[0] = new VertexPositionColorTexture((center - ax * halfX - ay * halfY).ToVector3(), Color.White, new Vector2(0f, 0f));
            verts[1] = new VertexPositionColorTexture((center + ax * halfX - ay * halfY).ToVector3(), Color.White, new Vector2(1f, 0f));
            verts[2] = new VertexPositionColorTexture((center - ax * halfX + ay * halfY).ToVector3(), Color.White, new Vector2(0f, 1f));
            verts[3] = new VertexPositionColorTexture((center + ax * halfX + ay * halfY).ToVector3(), Color.White, new Vector2(1f, 1f));

            foreach (EffectPass pass in fx.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, verts, 0, 2);
            }
        }

        /// <summary>引爆加色敷层、刀尖辉点跨屏 + 中心爆点 + 弱化扩散环 + 沿刀线速度线</summary>
        private void DrawDetonateDressing() {
            int dt = timer - HoldFrames;
            const int dressLife = 20;
            if (dt < 0 || dt >= dressLife) {
                return;
            }
            float bp = dt / (float)dressLife;
            float inv = 1f - bp;
            float easeOut = 1f - MathF.Pow(inv, 3f);
            Vector2 screenPos = Projectile.Center - Main.screenPosition;
            Vector2 dir = CutAngle.ToRotationVector2();

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            if (CWRAsset.StarFlare02?.Value is Texture2D flare) {
                //刀尖辉点、近匀速掠过全线，身后拖残影、伤口是它犁开的，因果先行

                if (dt <= GlintFrames + 2) {
                    float travel = GlintTravel(dt);
                    float exitFade = dt <= GlintFrames ? 1f : MathF.Pow(0.45f, dt - GlintFrames);
                    for (int g = 0; g < 4; g++) {
                        float tg = travel - g * 0.085f;
                        if (tg <= 0f) {
                            continue;
                        }
                        Vector2 gp = screenPos + dir * (tg * 2f - 1f) * woundHalfX * 0.97f;
                        float ga = (1f - g * 0.24f) * exitFade;
                        sb.Draw(flare, gp, null, new Color(255, 246, 232) * (0.95f * ga)
                            , g * 1.7f + Projectile.whoAmI, flare.Size() * 0.5f
                            , (0.62f - g * 0.11f) * SizeMul, SpriteEffects.None, 0);
                    }
                }

                //中心爆点、存在但收敛、主角是伤口与被推开的世界

                float coreA = MathF.Pow(inv, 2.2f) * 0.7f;
                float coreS = (1.0f + easeOut * 0.9f) * SizeMul;
                sb.Draw(flare, screenPos, null, new Color(255, 244, 230) * coreA, Projectile.whoAmI * 1.37f
                    , flare.Size() * 0.5f, coreS, SpriteEffects.None, 0);
            }

            //扩散环弱化为空气被排开的一圈涟漪、环读作爆炸，斩击不需要大环

            if (CWRAsset.Ring01?.Value is Texture2D ring) {
                float ringS = (0.5f + easeOut * 2.8f) * SizeMul;
                float ringA = MathF.Pow(inv, 2.6f) * 0.22f;
                sb.Draw(ring, screenPos, null, new Color(255, 98, 58) * ringA, 0f
                    , ring.Size() * 0.5f, ringS, SpriteEffects.None, 0);
            }

            if (CWRAsset.SpeedLines01?.Value is Texture2D lines) {
                float lA = MathF.Pow(inv, 1.7f) * 0.38f;
                for (int i = 0; i < 3; i++) {
                    float seed = (Projectile.whoAmI + i) * 0.6180339887f % 1f;
                    Rectangle src = new(0, (int)(seed * (1024 - 96)), 1024, 96);
                    Vector2 pos = screenPos + dir * ((seed - 0.5f) * 900f + easeOut * 220f) * SizeMul
                        + dir.RotatedBy(MathHelper.PiOver2) * (seed * 2f - 1f) * 90f * SizeMul;
                    sb.Draw(lines, pos, src, new Color(255, 176, 138) * lA, CutAngle
                        , src.Size() * 0.5f, new Vector2(0.9f + easeOut * 0.5f, 0.5f) * SizeMul
                        , SpriteEffects.None, 0);
                }
            }

            sb.End();
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
