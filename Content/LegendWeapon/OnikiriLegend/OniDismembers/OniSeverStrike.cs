using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using OFR = CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs.OniFinaleRenderer;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers
{
    /// <summary>肢解结算斩. 直斩/锚模式</summary>
    internal class OniSeverStrike : ModProjectile, IOverlayDrawable, IOniBladeOccupant
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        /// <summary>蓄势帧数、刀在鞘位反向压势</summary>
        public const int WindupFrames = 6;
        /// <summary>拔刀闪帧数、快到只剩残影</summary>
        public const int DrawFlashFrames = 2;
        /// <summary>落刀帧、目标入冻 + 终斩刀线生成</summary>
        public const int StrikeFrame = WindupFrames + DrawFlashFrames;
        /// <summary>纳刀帧 = 刀线引爆帧 = 碎片分离帧</summary>
        public const int SheatheFrame = StrikeFrame + OniFinaleCut.HoldFrames;
        /// <summary>纳刀一挑时长</summary>
        private const int NotoFlickFrames = 6;
        /// <summary>反噬帧、刀入鞘的下一瞬，同等的肢解落回自己（两种模式，落刀成功即必反噬）</summary>
        public const int SelfCutFrame = SheatheFrame + NotoFlickFrames;
        /// <summary>纳刀后持刀淡出</summary>
        private const int NotoFadeFrames = 12;
        /// <summary>演出总时长</summary>
        public const int TotalDuration = SheatheFrame + NotoFlickFrames + NotoFadeFrames + 4;
        /// <summary>空挥（目标失效）后的快速收鞘时长</summary>
        private const int WhiffSheatheFrames = 8;
        /// <summary>点锚模式的 ai[0] 标记</summary>
        private const int PointModeMarker = -2;

        private int timer;
        /// <summary>首帧捕获的目标类型，槽位复用校验</summary>
        private int targetType = -1;
        private int omokageEntryId;
        private int tutorialTargetOwner = -1;
        private int tutorialTargetSession;
        private uint tutorialTargetToken;
        /// <summary>落刀已执行（冻结+刀线已触发）</summary>
        private bool struck;
        /// <summary>落刀帧目标已失效、转空挥收势</summary>
        private bool whiffed;
        /// <summary>反噬已落下（防帧等值判断被计时抖动漏过）</summary>
        private bool selfCutDone;
        /// <summary>起手继承的连段刀角、从普攻移交时蓄势段顺势收拢入鞘，刀不跳变</summary>
        private float inheritRot;
        private bool inheritPose;
        private readonly OniBladePose bladePose = new();

        private int TargetIndex => (int)Projectile.ai[0];
        /// <summary>点锚模式、斩媒介（纸面），锚点=生成位置</summary>
        private bool PointMode => (int)Projectile.ai[0] == PointModeMarker;
        private float CutAngle => Projectile.ai[1];
        private float SizeMul => Projectile.ai[2] > 0.05f ? Projectile.ai[2] : 1f;
        private Player Owner => Main.player[Projectile.owner];

        /// <summary>蓄+闪硬占刀权、人已进入拔刀的呼吸；残心起软姿态，玩家输入随时接管</summary>
        bool IOniBladeOccupant.HardOccupiesBlade => timer <= StrikeFrame + 2 && !whiffed;

        /// <summary>触发接口（直接模式）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="target">肢解目标（落刀帧仍需存活，否则空挥收势）</param>
        /// <param name="cutAngle">切线角度（世界空间弧度，同时决定拔刀挥向）</param>
        /// <param name="damage">伤害（终斩刀线引爆窗单次巨额结算）</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率（传给终斩刀线）</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        public static Projectile Fire(Player player, NPC target, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null) {
            source ??= player.GetSource_Misc("CWR_OniSeverStrike");
            int tutorialOwner = -1;
            int tutorialSession = 0;
            uint tutorialToken = 0;
            bool tutorialTarget = target != null
                && Tutorial.OnikiriTutorialTargetGlobal.TryGetTutorialIdentity(target,
                    out tutorialOwner, out tutorialSession, out tutorialToken);
            if (tutorialTarget) {
                damage = 0;
            }
            Projectile projectile = Projectile.NewProjectileDirect(source,
                target?.Center ?? player.Center, Vector2.Zero
                , ModContent.ProjectileType<OniSeverStrike>(), damage, knockback, player.whoAmI
                , ai0: target?.whoAmI ?? -1, ai1: MathHelper.WrapAngle(cutAngle), ai2: scale);
            if (tutorialTarget && projectile.ModProjectile is OniSeverStrike strike) {
                strike.tutorialTargetOwner = tutorialOwner;
                strike.tutorialTargetSession = tutorialSession;
                strike.tutorialTargetToken = tutorialToken;
                projectile.netUpdate = true;
            }
            return projectile;
        }

        /// <summary>触发接口（点锚模式）</summary>
        /// <param name="player">攻击发起者</param>
        /// <param name="point">落刀点（世界坐标，应落在纸面内）</param>
        /// <param name="cutAngle">切线角度（弧度）</param>
        /// <param name="damage">脉冲到达帧对真身结算的伤害</param>
        /// <param name="knockback">击退</param>
        /// <param name="scale">尺寸倍率</param>
        /// <param name="source">生成源，null 则回退 Misc 源</param>
        /// <param name="omokageEntryId">点击时绑定的面影 ID</param>
        public static Projectile FireAtPoint(Player player, Vector2 point, float cutAngle, int damage, float knockback,
            float scale = 1f, IEntitySource source = null, int omokageEntryId = 0) {
            source ??= player.GetSource_Misc("CWR_OniSeverStrike");
            Projectile projectile = Projectile.NewProjectileDirect(source, point, Vector2.Zero
                , ModContent.ProjectileType<OniSeverStrike>(), damage, knockback, player.whoAmI
                , ai0: PointModeMarker, ai1: MathHelper.WrapAngle(cutAngle), ai2: scale);
            if (projectile.ModProjectile is OniSeverStrike strike) {
                strike.omokageEntryId = omokageEntryId;
            }
            return projectile;
        }

        public override void SetStaticDefaults() {
            CWRLoad.ProjValue.ImmuneFrozen[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;   //主控无判定，伤害全在终斩刀线

            Projectile.penetrate = -1;
            Projectile.timeLeft = TotalDuration + 2;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool ShouldUpdatePosition() => false;

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(tutorialTargetToken != 0);
            if (tutorialTargetToken == 0) {
                return;
            }
            writer.Write((byte)tutorialTargetOwner);
            writer.Write(tutorialTargetSession);
            writer.Write(tutorialTargetToken);
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            if (!reader.ReadBoolean()) {
                tutorialTargetOwner = -1;
                tutorialTargetSession = 0;
                tutorialTargetToken = 0;
                return;
            }
            tutorialTargetOwner = reader.ReadByte();
            tutorialTargetSession = reader.ReadInt32();
            tutorialTargetToken = reader.ReadUInt32();
        }

        internal static void CancelPendingTutorialStrikes(Player player, int session) {
            if (player == null || session <= 0) {
                return;
            }
            for (int i = Main.maxProjectiles - 1; i >= 0; i--) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || projectile.owner != player.whoAmI
                    || projectile.ModProjectile is not OniSeverStrike strike
                    || strike.struck || strike.tutorialTargetOwner != player.whoAmI
                    || strike.tutorialTargetSession != session || strike.tutorialTargetToken == 0) {
                    continue;
                }
                projectile.Kill();
            }
        }

        /// <summary>绑定目标的存活实例，死亡/槽位复用返回 null</summary>
        private NPC ValidTarget() {
            if (TargetIndex < 0 || TargetIndex >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[TargetIndex];
            if (!npc.active || npc.type != targetType) {
                return null;
            }
            bool currentTutorial = Tutorial.OnikiriTutorialTargetGlobal.TryGetTutorialIdentity(npc,
                out int actualOwner, out int actualSession, out uint actualToken);
            if (tutorialTargetToken == 0) {
                return currentTutorial ? null : npc;
            }
            if (!currentTutorial || actualOwner != Projectile.owner
                || actualOwner != tutorialTargetOwner || actualSession != tutorialTargetSession
                || actualToken != tutorialTargetToken) {
                return null;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Tutorial.OnikiriTutorialNetPlayer state = Main.player[actualOwner]
                    .GetModPlayer<Tutorial.OnikiriTutorialNetPlayer>();
                if (state.ServerSession != actualSession || state.ServerTargetIndex != npc.whoAmI) {
                    return null;
                }
            }
            return npc;
        }

        public override void AI() {
            if (timer == 0) {
                if (!PointMode) {
                    NPC first = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[TargetIndex] : null;
                    targetType = first?.active == true ? first.type : -1;
                }
                //从连段移交时继承实体刀当前角度

                //连段不在场则回退交接黑板（疾走纳刀后立刻点纸等场景，刀角同样连续）

                CrimsonRendSlash combo = CrimsonRendSlash.FindController(Owner);
                inheritPose = combo != null && combo.TryGetBladePose(out inheritRot, out _)
                    || OniBladeHandoff.TryPeek(Owner, out inheritRot, out _);
                //起手屏息的低鸣、拔刀的呼吸从这里开始

                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.60f, Volume = 0.40f }, Owner.Center);
            }
            timer++;

            //直接模式落刀前锚点跟随目标

            NPC target = PointMode ? null : ValidTarget();
            if (!struck && target != null) {
                Projectile.Center = target.Center;
            }

            if (timer == WindupFrames + 1) {
                //拔刀风声、斩击本身近乎无声（居合语法），只留出手的气流

                SoundEngine.PlaySound(CWRSound.KatanaSwing with { Pitch = 0.70f, Volume = 0.42f }, Owner.Center);
            }

            if (timer == StrikeFrame) {
                if (PointMode) {
                    //斩媒介、owner 端按位置解析纸面

                    struck = true;
                    if (Projectile.owner == Main.myPlayer
                        && !(omokageEntryId != 0
                            ? OniOmokage.SeverEntry(Owner, omokageEntryId, Projectile.Center, CutAngle,
                                Projectile.damage, Projectile.knockBack)
                            : OniOmokage.SeverAt(Owner, Projectile.Center, CutAngle,
                                Projectile.damage, Projectile.knockBack))) {
                        BeginWhiff();   //纸已烧散、空挥（仅 owner 端可知，远端照常收势）

                    }
                }
                else if (target != null) {
                    struck = true;
                    if (Tutorial.OnikiriTutorialTargetGlobal.IsTutorialTarget(target, out _, out _)) {
                        Projectile.damage = 0;
                    }
                    if (Projectile.owner == Main.myPlayer)
                        Tutorial.OnikiriTutorialEvents.FireDismemberLanded(Owner, target);
                    //全组停止；只有终斩视觉刃线实际经过的体节捕获快照并裂开
                    DismemberStroke stroke = new(target.Center, CutAngle,
                        OniFinaleCut.VisualHalfLength * SizeMul, OniFinaleCut.VisualPathWidth * SizeMul);
                    OniDismember.TriggerGroup(target, in stroke, holdFrames: OniFinaleCut.HoldFrames);
                    if (Projectile.owner == Main.myPlayer) {
                        OniFinaleCut.Fire(Owner, target.Center, CutAngle, Projectile.damage
                            , Projectile.knockBack, SizeMul, Projectile.GetSource_FromAI());
                    }
                }
                else {
                    BeginWhiff();
                }
            }

            //纳刀反噬、肢解的代价，刀入鞘的下一瞬，同等的肢解与伤害落回持刀人自己；

            //媒介只替真身受刀不替人挡代价，两种模式落刀成功均反噬

            //

            if (!selfCutDone && timer >= SelfCutFrame && struck && !whiffed) {
                selfCutDone = true;
                OniPlayerDismember.Trigger(Owner, CutAngle);
            }

            UpdatePose();
        }

        /// <summary>落刀落空、转空挥快速收鞘退场</summary>
        private void BeginWhiff() {
            struck = false;
            whiffed = true;
            Projectile.timeLeft = WhiffSheatheFrames + NotoFadeFrames + 2;
            SoundEngine.PlaySound(SoundID.Unlock with { Pitch = 0.30f, Volume = 0.35f }, Owner.Center);
        }

        /// <summary>居合四段（纯视觉，不锁操控）、蓄=鞘位反压、闪=两帧甩出只剩残影、 残心=过冲回稳后屏息微晃</summary>
        private void UpdatePose() {
            bladePose.Update();
            if (!Owner.active || Owner.dead) {
                return;
            }

            //反噬落下后本体交给玩家肢解管线（刀已入鞘，随身体一并定格）

            if (struck && timer > SelfCutFrame) {
                bladePose.Opacity = 0f;
                return;
            }

            //残心起让位给玩家输入；演出播完亦收

            if (timer > StrikeFrame + 2
                && (OniBladeOccupancy.ComboClaims(Owner) || OniBladeOccupancy.AnyHardOccupant(Owner, Projectile))
                || timer > SheatheFrame + NotoFlickFrames + NotoFadeFrames) {
                bladePose.Opacity = 0f;
                return;
            }

            //面向落刀锚点；水平几乎重合时退回切线方向

            int facing = MathF.Cos(CutAngle) >= 0f ? 1 : -1;
            float toAnchorX = Projectile.Center.X - Owner.Center.X;
            if (MathF.Abs(toAnchorX) > 8f) {
                facing = toAnchorX > 0f ? 1 : -1;
            }
            //拔刀完成位顺切线，按朝向取不背手的那一端

            Vector2 cutDir = CutAngle.ToRotationVector2();
            float strikeRot = cutDir.X * facing >= 0f ? CutAngle : MathHelper.WrapAngle(CutAngle + MathHelper.Pi);
            float sheathRot = strikeRot - facing * 1.05f;
            var stretch = Player.CompositeArmStretchAmount.Full;

            if (whiffed) {
                UpdateWhiffPose(facing, strikeRot, sheathRot);
                return;
            }

            if (timer <= WindupFrames) {
                //蓄、鞘位再反向压一分、出刀前的那口气；

                //继承连段刀角时从当前角度顺势收拢（刀已在手，不淡入，划弧带轻残影）

                float wind = OFR.EaseOutCubic(timer / (float)WindupFrames);
                if (inheritPose) {
                    bladePose.Rotation = OniBladePose.LerpAngle(inheritRot, sheathRot - facing * 0.30f, wind);
                    bladePose.Opacity = 1f;
                    bladePose.PushSmear(0.4f);
                }
                else {
                    bladePose.Rotation = sheathRot - facing * 0.30f * wind;
                    bladePose.Opacity = MathHelper.Clamp(timer / 3f, 0f, 1f);
                }
                stretch = Player.CompositeArmStretchAmount.Quarter;
            }
            else if (timer <= StrikeFrame) {
                //闪、两帧从鞘底甩到切线带过冲，逐帧压残影

                float t = (timer - WindupFrames) / (float)DrawFlashFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(sheathRot - facing * 0.30f
                    , strikeRot + facing * 0.16f, ease);
                bladePose.Opacity = 1f;
                bladePose.PushSmear(1f);
            }
            else if (timer <= SheatheFrame) {
                //残心、过冲回稳后屏息，微晃是唯一的动静

                float settle = MathHelper.Clamp((timer - StrikeFrame) / 6f, 0f, 1f);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot + facing * 0.16f, strikeRot, settle)
                    + MathF.Sin(timer * 0.045f) * 0.03f * settle;
                bladePose.Opacity = 1f;
            }
            else if (timer <= SheatheFrame + NotoFlickFrames) {
                //纳刀、与引爆同帧起手，一挑入鞘、刀入鞘，目标才裂

                float t = (timer - SheatheFrame) / (float)NotoFlickFrames;
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot, sheathRot, ease);
                bladePose.Opacity = 1f;
                if (timer - SheatheFrame <= 3) {
                    bladePose.PushSmear(0.8f);
                }
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }
            else {
                //收、持刀淡出

                bladePose.Rotation = sheathRot;
                bladePose.Opacity = 1f - (timer - SheatheFrame - NotoFlickFrames) / (float)NotoFadeFrames;
                stretch = Player.CompositeArmStretchAmount.ThreeQuarters;
            }

            bladePose.ApplyPose(Owner, Projectile, stretch);
        }

        /// <summary>空挥收势、没有残心可言，顺势快速收鞘淡出</summary>
        private void UpdateWhiffPose(int facing, float strikeRot, float sheathRot) {
            int wt = timer - StrikeFrame;
            if (wt <= WhiffSheatheFrames) {
                float ease = OFR.EaseOutCubic(wt / (float)WhiffSheatheFrames);
                bladePose.Rotation = OniBladePose.LerpAngle(strikeRot + facing * 0.16f, sheathRot, ease);
                bladePose.Opacity = 1f;
            }
            else {
                bladePose.Rotation = sheathRot;
                bladePose.Opacity = 1f - (wt - WhiffSheatheFrames) / (float)NotoFadeFrames;
            }
            bladePose.ApplyPose(Owner, Projectile);
        }

        /// <summary>遮挡层、居合持刀的实体刀与拔刀残影</summary>
        void IOverlayDrawable.DrawOverlay(SpriteBatch spriteBatch) {
            if (Main.dedServ) {
                return;
            }
            bladePose.Draw(spriteBatch, Owner);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
