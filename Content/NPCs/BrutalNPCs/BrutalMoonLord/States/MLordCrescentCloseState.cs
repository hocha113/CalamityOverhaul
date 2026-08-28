using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Projectiles;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.States
{
    /// <summary>
    /// 弦月合拢（四臂对位版）：上对双手各持一道弧光死光沿天体弧线相向合拢，
    /// 逃生楔口随缓动曲线移动（快→慢→快的呼吸）；下对双手反相开弧
    /// 自底部中央向两侧扫离（先封底后让位），与上对合拢形成"下开上合"的对位呼吸；
    /// 头部向楔口滴弹逼走位（无活头时真眼代射）。上对单手退化为单弧+对侧扫描束，
    /// 核心裸露版由锚定真眼重演全声部（全射线级——本体不发小弹幕）：
    /// 双翼相向合拢+中位天顶弧+内侧对底部交叉双开弧。旧三弧版只复刻了上对+顶弧，
    /// 全部束面朝天，眼又悬在玩家头顶 430px，整招打不到锚线以下的玩家——
    /// 2026-08-28 补回底部声部（下对双手的席位继承）并加编队冻结拍
    /// </summary>
    [InnoVault.StateMachines.VaultState((int)MLordStateIndex.CrescentClose, typeof(MLordContext))]
    internal class MLordCrescentCloseState : MLordStateBase
    {
        public override string StateName => "CrescentClose";
        public override MLordStateIndex StateIndex => MLordStateIndex.CrescentClose;

        internal const int WindupEnd = 70;
        internal const int RayLife = MLordArcRayProj.TotalLife;
        /// <summary>
        /// 真眼编队冻结拍（核心裸露版）：此帧服务端把玩家位置封存进锚点槽，
        /// 真眼阵列钉死在冻结点头顶——移动中的发射器会作废一切涌现缺口（契约3）。
        /// 弧的角度自进态即由角表锁死（瞳孔罗盘 70f+成束 20f ≥ 光束预警预算
        /// <see cref="MLordDirector.BeamTelegraphFrames"/>），引导线自此拍点亮，
        /// 落点几何 42f 读秒后出束
        /// </summary>
        internal const int EyeFreezeBeat = 28;
        /// <summary>底部声部收针角（≈14° 俯角，公平阀）：交叉双弧自冻结点脚下
        /// 向两侧扫离的终点角，终拍落点距冻结点约 ±1500px——先封底后让位，
        /// 地面各点只被扫过一次，束身掠过即安全</summary>
        internal const float EyeBottomEndAngle = 0.24f;

        private int stateLength;

        /// <summary>该手槽的弧起始角（FireArcs 与手部瞳孔预告共用一份角表）</summary>
        internal static float ArcStartAngle(int slot) => slot switch {
            0 => MathHelper.Pi + 0.55f,
            1 => -0.55f,
            2 => MathHelper.PiOver2 + 0.22f,
            _ => MathHelper.PiOver2 - 0.22f,
        };

        /// <summary>该手槽的带符号总扫角</summary>
        internal static float ArcSweep(int slot) => slot switch {
            0 => 2.15f,
            1 => -2.15f,
            2 => 1.15f,
            _ => -1.15f,
        };

        /// <summary>
        /// 该手槽当帧的弧向瞄角：蓄力期盯死起始扫向（四眼四向的罗盘预告），
        /// 出弧后随本弧缓动扫角同步推进（眼随刃转）。手部姿态消费
        /// </summary>
        internal static float ArcAimAngle(int slot, int stateTimer) {
            float start = ArcStartAngle(slot);
            if (stateTimer <= WindupEnd + MLordArcRayProj.ExpandTime) {
                return start;
            }
            float sweepT = MathHelper.Clamp(
                (stateTimer - WindupEnd - MLordArcRayProj.ExpandTime) / (float)MLordArcRayProj.SweepFrames, 0f, 1f);
            return start + ArcSweep(slot) * VaultUtils.EaseInOutCubic(sweepT);
        }

        /// <summary>
        /// 真眼重演声部表：席位→弧组（起始角/带符号扫角）写入出参，返回弧数（至多 2）。
        /// 双翼持上对相向合拢弧（角表与上对双手同源），中位眼补天顶弧，
        /// 其余席位（标准五眼阵的内侧对）持底部声部：起手角按本席锚位偏移与栖高反推、
        /// 瞄向冻结点脚下（出束即交叉封底），随后向两侧扫离至 <see cref="EyeBottomEndAngle"/>
        /// ——左内眼向右开、右内眼向左开，先封底后让位；3 眼降级阵无内侧席，
        /// 底部声部由双翼加持第二道弧。FireArcs 出束与真眼引导线共读此表（线在哪、束就在哪）
        /// </summary>
        internal static int GetEyeReplayArcs(int ordinal, int eyeCount, Span<float> starts, Span<float> sweeps) {
            if (eyeCount <= 0) {
                return 0;
            }
            int middle = eyeCount / 2;
            //兜底微阵（1~2 眼，正常裸露期不该出现）：只保留天顶弧
            if (eyeCount < 3) {
                if (ordinal == middle) {
                    starts[0] = -MathHelper.PiOver2 - 0.8f;
                    sweeps[0] = 1.6f;
                    return 1;
                }
                return 0;
            }
            int count = 0;
            bool leftWing = ordinal == 0;
            bool rightWing = ordinal == eyeCount - 1;
            if (leftWing || rightWing) {
                int slot = leftWing ? 0 : 1;
                starts[count] = ArcStartAngle(slot);
                sweeps[count] = ArcSweep(slot);
                count++;
            }
            else if (ordinal == middle) {
                starts[count] = -MathHelper.PiOver2 - 0.8f;
                sweeps[count] = 1.6f;
                count++;
            }
            //底部声部：内侧席优先，3 眼阵翼眼兼任
            if ((!leftWing && !rightWing && ordinal != middle) || (eyeCount == 3 && (leftWing || rightWing))) {
                float offsetX = (ordinal - (eyeCount - 1) * 0.5f) * MoonLordFreeEyeAI.AnchorSpreadX;
                float steep = MathF.Atan2(-MoonLordFreeEyeAI.AnchorPerchY, Math.Abs(offsetX));
                if (offsetX < 0f) {
                    starts[count] = steep;
                    sweeps[count] = EyeBottomEndAngle - steep;
                }
                else {
                    starts[count] = MathHelper.Pi - steep;
                    sweeps[count] = steep - EyeBottomEndAngle;
                }
                count++;
            }
            return count;
        }

        public override void OnEnter(MLordContext context) {
            base.OnEnter(context);
            stateLength = WindupEnd + RayLife + Frames(context, 42);
            //裸露期真眼锚定站桩，第三弧才不会被环绕运动甩成乱扫
            if (!VaultUtils.isClient && context.CoreExposed) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Anchor;
                context.Npc.netUpdate = true;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Zombie93 with { Volume = 0.85f, Pitch = -0.4f }, context.Npc.Center);
            }
        }

        public override void OnExit(MLordContext context) {
            base.OnExit(context);
            if (!VaultUtils.isClient) {
                context.Owner.ai[MLordAiSlots.OvEyeCommand] = MLordEyeCommand.Solo;
            }
        }

        public override IMLordState OnUpdate(MLordContext context) {
            NPC npc = context.Npc;
            Player target = context.Target;

            //弧光支点仪式：四爪抓桩，核心钉死做几何轴（弧原点稳定）
            RequestMove(context, target.Center + new Vector2(0f, -150f), 0.5f, MLordMovePolicy.Brace);
            UpdateLean(context);

            if (Timer < WindupEnd) {
                context.SetChargeState(Timer / (float)WindupEnd);
                if (!VaultUtils.isServer) {
                    //四手处向心星流蓄势
                    MLordPartsStatus parts = context.Parts;
                    for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                        if (parts.HandAlive(slot) && parts.HandIndex(slot) >= 0) {
                            MLordScreenFX.ConvergeStreak(Main.npc[parts.HandIndex(slot)].Center, 260f, Timer / (float)WindupEnd);
                        }
                    }
                }
            }

            //核心裸露版编队冻结拍：封存玩家位置进锚点槽（真眼阵列与底部交叉弧的
            //落点几何自此与走位解耦，预告即承诺）
            if (!VaultUtils.isClient && context.CoreExposed && Timer == EyeFreezeBeat) {
                context.Owner.ai[MLordAiSlots.OvAnchorX] = target.Center.X;
                context.Owner.ai[MLordAiSlots.OvAnchorY] = target.Center.Y;
                npc.netUpdate = true;
            }

            if (Timer == WindupEnd && !VaultUtils.isClient) {
                FireArcs(context);
            }

            //弧光存续期向楔口滴弹禁蹲桩：活头自射，头破由真眼代射（残口与心脏不当炮口）
            if (!VaultUtils.isClient && Timer > WindupEnd && Timer < WindupEnd + RayLife
                && (Timer - WindupEnd) % Frames(context, 40) == 12) {
                NPC origin = context.Parts.HeadAlive && context.Parts.Head >= 0
                    ? Main.npc[context.Parts.Head] : MLordFacts.GetFreeEye(npc, 1);
                if (origin != null) {
                    Vector2 aim = (target.Center - origin.Center).SafeNormalize(Vector2.UnitY);
                    Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center + aim * 40f, aim * 6.2f,
                        ModContent.ProjectileType<MLordBoltProj>(), ScaleDamage(context, MLordDirector.BoltDamage), 0f, Main.myPlayer);
                }
            }

            Timer++;
            if (Timer >= stateLength) {
                return NextAttack(context);
            }
            return null;
        }

        /// <summary>放出合拢弧组：上对相向合拢，下对自底开弧扫离（对位呼吸）</summary>
        private void FireArcs(MLordContext context) {
            MLordPartsStatus parts = context.Parts;
            int damage = ScaleDamage(context, MLordDirector.ArcRayDamage);
            int arcType = ModContent.ProjectileType<MLordArcRayProj>();
            bool upLeft = parts.HandAlive(0) && parts.HandIndex(0) >= 0;
            bool upRight = parts.HandAlive(1) && parts.HandIndex(1) >= 0;

            //上对相向合拢楔口，下对自底开弧扫离（角表见 ArcStartAngle/ArcSweep，与手部瞳孔预告同源）
            for (int slot = 0; slot < MLordPartsStatus.HandSlots; slot++) {
                if (!parts.HandAlive(slot) || parts.HandIndex(slot) < 0) {
                    continue;
                }
                NPC hand = Main.npc[parts.HandIndex(slot)];
                Projectile.NewProjectile(hand.GetSource_FromAI(), hand.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, hand.whoAmI, ArcStartAngle(slot), ArcSweep(slot));
            }

            //上对单手：对侧由头补一记扫描束封边
            if (upLeft ^ upRight) {
                NPC origin = context.Parts.Head >= 0 ? Main.npc[context.Parts.Head] : context.Npc;
                float sideAngle = upLeft ? MathHelper.PiOver2 - 0.9f : MathHelper.PiOver2 + 0.9f;
                Projectile.NewProjectile(origin.GetSource_FromAI(), origin.Center, Vector2.Zero,
                    ModContent.ProjectileType<MLordScanRayProj>(), ScaleDamage(context, MLordDirector.ScanRayDamage),
                    0f, Main.myPlayer, origin.whoAmI, sideAngle, 46);
            }

            //核心裸露：锚定真眼重演全声部——双翼相向合拢+中位天顶弧+内侧对底部交叉双开弧，
            //角表与真眼引导线共读 GetEyeReplayArcs（线在哪、束就在哪），编队已于冻结拍钉死。
            //全部射线级，本体在此阶段不发小弹幕
            if (context.CoreExposed) {
                Span<int> eyes = stackalloc int[MLordFacts.MaxFreeEyes];
                int eyeCount = MLordFacts.ScanFreeEyes(context.Npc, eyes);
                Span<float> starts = stackalloc float[2];
                Span<float> sweeps = stackalloc float[2];
                for (int i = 0; i < eyeCount; i++) {
                    int arcCount = GetEyeReplayArcs(i, eyeCount, starts, sweeps);
                    NPC eye = Main.npc[eyes[i]];
                    for (int k = 0; k < arcCount; k++) {
                        Projectile.NewProjectile(eye.GetSource_FromAI(), eye.Center, Vector2.Zero,
                            arcType, damage, 0f, Main.myPlayer, eye.whoAmI, starts[k], sweeps[k]);
                    }
                }
            }

            //无任何手（极端情况）：核心自射对开双弧
            if (!parts.AnyHandAlive && !context.CoreExposed) {
                NPC npc = context.Npc;
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, MathHelper.Pi + 0.55f, 2.15f);
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                    arcType, damage, 0f, Main.myPlayer, npc.whoAmI, -0.55f, -2.15f);
            }
        }
    }
}
