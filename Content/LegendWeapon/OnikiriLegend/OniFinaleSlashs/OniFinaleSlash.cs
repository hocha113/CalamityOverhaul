using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.Inscriptions;
using CalamityOverhaul.Content.TimeFreezes;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs
{
    /// <summary>终之太刀主控. 时停蓄势→裂世</summary>
    internal class OniFinaleSlash : ModProjectile, IOverlayDrawable, IOniBladeOccupant, IOniCrispDrawable
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>乱舞起点</summary>
        public const int FlurryStart = 16;
        /// <summary>死寂起点（最后一道直痕之后）</summary>
        public const int SilenceStart = 90;
        /// <summary>终斩细线出现帧</summary>
        public const int CutSpawnFrame = 94;
        /// <summary>纳刀引爆帧 = 解冻帧</summary>
        public const int DetonateFrame = CutSpawnFrame + OniFinaleCut.HoldFrames;
        /// <summary>演出总时长</summary>
        public const int TotalDuration = 165;

        /// <summary>乱舞环斩排拍、(帧, 滚转偏移, 扫掠镜像)，节奏 8→6→4 帧逐段收紧</summary>
        private static readonly (int Frame, float RollOff, int Flip)[] RingBeats = [
            (16,  0.40f,  1), (24, -0.92f, -1), (32,  1.80f,  1), (40, -2.40f, -1),
            (46,  0.15f,  1), (52,  2.90f, -1), (58, -1.30f,  1), (64,  0.70f, -1),
            (68, -2.00f,  1), (72,  1.10f, -1), (76, -0.50f,  1), (80,  2.30f, -1),
            (84, -1.70f,  1),
        ];

        /// <summary>直痕排拍、(帧, 相对瞄准角偏移)，前疏后密，越接近纳刀越急</summary>
        private static readonly (int Frame, float AngleOff)[] ScarBeats = [
            (38,  1.35f), (48, -0.52f), (50,  0.55f), (60, -1.15f), (70,  0.22f),
            (74, -0.88f), (78,  1.55f), (82, -0.30f), (86,  0.75f), (88, -1.60f),
        ];

        /// <summary>死寂期纯演出过刃线排拍、(帧, 相对瞄准角偏移, 深度)。静止中世界仍被无声切开，
        /// 节奏渐急、深浅混排撑纵深；近平面(深度&lt;0.28)的线兑入碎屏网格</summary>
        private static readonly (int Frame, float AngleOff, float Depth)[] SilenceBeats = [
            (95, -0.55f, 0.30f), (99, 1.18f, 0.55f), (102, 0.30f, 0.16f),
            (105, -1.30f, 0.68f), (108, 0.82f, 0.22f),
        ];

        /// <summary>碎晶流向偏置（弧度）、直痕引爆时碎片顺终斩刀线漂移，仅演出期有效</summary>
        internal static float ShatterFlowAngle;
        /// <summary>演出进行中（主控存活），直痕据此决定碎晶是否吃流向偏置</summary>
        internal static bool ShatterFlowActive;

        private int timer;

        private float Aim => Projectile.ai[0];
        private float SizeMul => Projectile.ai[1] > 0.05f ? Projectile.ai[1] : 1f;
        private Player Owner => Main.player[Projectile.owner];

        /// <summary>纳刀一挑时长,与引爆帧同步起手</summary>
        private const int NotoFlickFrames = 6;
        /// <summary>纳刀后持刀淡出</summary>
        private const int NotoFadeFrames = 12;
        private readonly OniBladePose bladePose = new();

        /// <summary>开场短促硬占:清掉在场连段刀光,给演出一个干净的起手;之后普攻自由(不锁)</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= 10;

        /// <summary>纳刀一挑的签名拍软保留:长残心照旧可随时接管,只护刀入鞘与世界裂开压在同一拍的那一挑</summary>
        bool IOniBladeOccupant.ReservesBlade => timer > DetonateFrame && timer <= DetonateFrame + NotoFlickFrames;

        /// <summary>触发接口（调试入口）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="focus">演出焦点（世界坐标，乱舞围绕此处、终斩刀线过此点）</param>
        /// <param name="aim">瞄准方向（无需归一化，决定终斩刀线角度）</param>
        /// <param name="damage">基准伤害（环斩 45%/直痕 35%/终斩 400% 派生）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, Vector2 focus, Vector2 aim, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniFinaleSlash");
            float aimAngle = aim.SafeNormalize(Vector2.UnitX).ToRotation();
            Projectile projectile = Projectile.NewProjectileDirect(source, focus, Vector2.Zero
                , ModContent.ProjectileType<OniFinaleSlash>(), damage, knockback, player.whoAmI
                , ai0: aimAngle, ai1: scale);
            OniMeiActionContext.Capture(projectile, player, source, damage, OniMeiActionKind.Finale);
            OniMeiActionContext.ArmConditions(projectile, player,
                allowSilent: false, allowPlanted: true);
            return projectile;
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在子斩体

            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void AI() {
            int syncedTimer = TotalDuration - Projectile.timeLeft + 1;
            timer = Math.Clamp(Math.Max(timer + 1, syncedTimer), 1, TotalDuration);

            ShatterFlowAngle = Aim;
            ShatterFlowActive = true;

            //时停、纳刀帧前每帧刷新，之后停止、tick 自然衰减，世界恰在终斩落下时苏醒

            if (timer <= DetonateFrame) {
                TimeFreezeSystem.RefreshCinematic<OniFinaleSlash>(2);
            }

            PushDimEnvelope();

            //暗场里的刀光辉光、复用绯红 Bloom 管线（本效果权重 1.09 先压暗，

            //1.10 的 Bloom 提取在暗场上只剩刀光，光圈恰好圈住演出主体）

            if (timer >= FlurryStart && timer <= DetonateFrame + 10) {
                CrimsonImpactFX.PushAmbience(Projectile.Center, 0.24f);
            }

            PlayTimelineSounds();
            UpdateStandPose();
            RunLatticeTimeline();

            if (Projectile.owner == Main.myPlayer) {
                RunSpawnTimeline();
            }
        }

        /// <summary>过刃线格架的客户端时间轴：起手清上一场登记，死寂期排纯演出线并推同步呼吸</summary>
        private void RunLatticeTimeline() {
            if (Main.dedServ) {
                return;
            }
            OniFinaleLattice.Update();

            //死寂呼吸、全场细线同一口气，随逼近纳刀升压（深处相位滞后在格架内处理）；
            //同窗径向模糊向刀线中心蓄力，空间被吸向那道将落未落的斩线

            if (timer >= SilenceStart - 4 && timer <= DetonateFrame) {
                float amp = MathHelper.Lerp(0.45f, 1f
                    , (timer - SilenceStart + 4) / (float)(DetonateFrame - SilenceStart + 4));
                OniFinaleLattice.PushBreath(timer, amp);
                OniFinaleShatter.PushCharge(Projectile.Center
                    , (timer - SilenceStart + 4) / (float)(DetonateFrame - SilenceStart + 4));
            }

            foreach ((int frame, float angleOff, float depth) in SilenceBeats) {
                if (timer != frame) {
                    continue;
                }
                Vector2 center = Projectile.Center + Main.rand.NextVector2Circular(130f, 95f);
                float angle = Aim + angleOff + Main.rand.NextFloat(-0.05f, 0.05f);
                OniFinaleLattice.AddLine(center, angle, depth, SizeMul);
                if (depth < 0.28f) {
                    //近平面的线切开画面本体+落刀碎面，配一声几不可闻的高频细响；深处的保持死寂

                    OniFinaleFX.PushSlice(center, angle, 4f * SizeMul);
                    OniFinaleShatter.AddFacets(center, 2, SizeMul);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.9f, Volume = 0.14f }, center);
                }
            }
        }

        /// <summary>演出期残心静立(纯视觉,不锁操控)、世界被劈开的整场,持刀人立定屏息; 纳刀一挑与引爆帧同帧</summary>
        private void UpdateStandPose() {
            bladePose.Update();
            if (!Owner.active || Owner.dead) {
                return;
            }
            if (timer > DetonateFrame + NotoFlickFrames + NotoFadeFrames
                || OniBladeOccupancy.ComboClaims(Owner)
                || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile)) {
                bladePose.Opacity = 0f;
                return;
            }

            int facing = MathF.Cos(Aim) >= 0f ? 1 : -1;
            //残心持刀位:刀锋斜垂身前

            float standRot = Aim + facing * 0.72f;
            if (timer <= DetonateFrame) {
                bladePose.Rotation = standRot + MathF.Sin(timer * 0.045f) * 0.035f;   //屏息的呼吸

                bladePose.Opacity = MathHelper.Clamp(timer / 8f, 0f, 1f);
            }
            else if (timer <= DetonateFrame + NotoFlickFrames) {
                //纳刀:与引爆同帧,一挑入鞘

                float t = (timer - DetonateFrame) / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(standRot, Aim - facing * 1.05f, ease);
                bladePose.Opacity = 1f;
                if (timer - DetonateFrame <= 3) {
                    bladePose.PushSmear(0.8f);
                }
            }
            else {
                bladePose.Opacity = 1f - (timer - DetonateFrame - NotoFlickFrames) / (float)NotoFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层、残心静立/纳刀的实体刀</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        /// <summary>格架指定绘制者：编号最小的在场主控代画，多场演出并存也只画一遍，
        /// 且与暂停解耦（不依赖 AI 复位标志）</summary>
        private bool IsLatticeDriver() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == Type) {
                    return p.whoAmI == Projectile.whoAmI;
                }
            }
            return false;
        }

        /// <summary>过刃线格架主体 + 出生掠光，锋利层（后效之上），切线是施刀者，不被自己的斩击切碎</summary>
        void IOniCrispDrawable.DrawCrisp() {
            if (Main.dedServ || !OniFinaleLattice.HasAny || !IsLatticeDriver()) {
                return;
            }
            GraphicsDevice device = Main.instance.GraphicsDevice;
            if (OFR.BeginDraw(device, out Effect fx, out var pb, out var pr, out var pd)) {
                OniFinaleLattice.DrawLines(device, fx);
                OFR.EndDraw(device, pb, pr, pd);
            }

            SpriteBatch sb = Main.spriteBatch;
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp
                , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            OniFinaleLattice.DrawGlints(sb);
            sb.End();
        }

        /// <summary>暗场包络、起手浸入 → 乱舞恒定 → 死寂压到最深 → 纳刀后停推自然回落</summary>
        private void PushDimEnvelope() {
            float dim;
            if (timer < FlurryStart) {
                dim = 0.62f * timer / FlurryStart;
            }
            else if (timer < SilenceStart) {
                dim = 0.62f;
            }
            else if (timer <= DetonateFrame) {
                dim = MathHelper.Lerp(0.62f, 0.82f
                    , (timer - SilenceStart) / (float)(DetonateFrame - SilenceStart));
            }
            else {
                return;
            }
            OniFinaleFX.PushDim(Projectile.Center, dim);
        }

        /// <summary>主控级音效、开幕低鸣与纳刀前的吸气，其余节拍音交给各斩体自播</summary>
        private void PlayTimelineSounds() {
            if (timer == 1) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.80f, Volume = 0.60f }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.90f, Volume = 0.30f }, Projectile.Center);
            }
            //死寂中一丝极轻的高频吸气，暗示屏息

            if (timer == DetonateFrame - 6) {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.95f, Volume = 0.22f }, Projectile.Center);
            }
        }

        /// <summary>排拍生成（仅持有者客户端，tML 同步到各端）</summary>
        private void RunSpawnTimeline() {
            OniMeiActionContext context = OniMeiActionContext.Get(Projectile);
            int baseWeaponDamage = context?.HasSnapshot == true
                ? context.BaseWeaponDamage
                : Projectile.damage;
            int ringDamage = (int)(baseWeaponDamage * 0.45f);
            int scarDamage = (int)(baseWeaponDamage * 0.35f);

            //环斩/直痕是本招式编排好的主体伤害，不是铭刻附属：出生后立刻回填主伤身份，
            //否则 OnSpawn 的父源继承默认把它们记作副伤，被"同目标副伤总量 100% 预算"拦停
            //表现为乱舞只有头两刀有伤、其余斩击与撕裂拍全部空刀

            for (int i = 0; i < RingBeats.Length; i++) {
                if (timer != RingBeats[i].Frame) {
                    continue;
                }
                float escalate = i / (float)(RingBeats.Length - 1) * 0.85f;
                Vector2 center = Projectile.Center + Main.rand.NextVector2Circular(70f, 55f);
                float roll = Aim + RingBeats[i].RollOff + Main.rand.NextFloat(-0.15f, 0.15f);
                Projectile ring = OniFinaleRing.Fire(Owner, center, roll, escalate, RingBeats[i].Flip
                    , ringDamage, Projectile.knockBack, SizeMul * (1f + 0.18f * escalate) * 1.12f
                    , Projectile.GetSource_FromAI());
                OniMeiActionContext.Inherit(Projectile, ring, secondary: false, OniMeiActionKind.Finale);
            }

            for (int i = 0; i < ScarBeats.Length; i++) {
                if (timer != ScarBeats[i].Frame) {
                    continue;
                }
                Vector2 center = Projectile.Center + Main.rand.NextVector2Circular(170f, 130f);
                float angle = Aim + ScarBeats[i].AngleOff + Main.rand.NextFloat(-0.08f, 0.08f);
                //引爆延迟对齐纳刀帧，i%3 错帧让刀痕网连锁碎裂而非同帧齐爆

                int detonateDelay = DetonateFrame - ScarBeats[i].Frame + i % 3;
                Projectile scar = OniFinaleScar.Fire(Owner, center, angle, detonateDelay
                    , scarDamage, Projectile.knockBack * 0.5f, SizeMul, Projectile.GetSource_FromAI());
                OniMeiActionContext.Inherit(Projectile, scar, secondary: false, OniMeiActionKind.Finale);
            }

            if (timer == CutSpawnFrame) {
                Projectile cut = OniFinaleCut.Fire(Owner, Projectile.Center, Aim
                    , (int)(baseWeaponDamage * 4f), Projectile.knockBack * 2f, SizeMul
                    , Projectile.GetSource_FromAI());
                OniMeiActionContext.Inherit(Projectile, cut, secondary: false, OniMeiActionKind.FinaleCut);
            }
        }

        public override void OnKill(int timeLeft) {
            ShatterFlowActive = false;
            //格架随主控退场：自然走完时细线早已在纳刀帧兑现清空，
            //提前夭折（玩家阵亡等）则硬清，没有主控继续驱动淡出

            OniFinaleLattice.Clear();

            //提前夭折时碎镜面优雅闭合（自然流程由终斩在纳刀帧 Burst）

            if (timer <= DetonateFrame) {
                OniFinaleShatter.Burst(Projectile.Center);
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
