using CalamityOverhaul.Common;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Core;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering
{
    /// <summary>黑闪四臂动作阶段</summary>
    internal enum MLordUltArmPhase
    {
        /// <summary>未激活</summary>
        Hidden = 0,
        /// <summary>物化登场：自披风后展开</summary>
        Manifest,
        /// <summary>合拢环抱：四掌收向黑球</summary>
        Embrace,
        /// <summary>揉搓蓄力：错拍搓动压缩黑球</summary>
        Knead,
        /// <summary>寂静一拍：全部收干定格</summary>
        Silence,
        /// <summary>掷出：四掌向掷向甩出+回撤反冲</summary>
        Throw,
        /// <summary>余波：外张消散</summary>
        Aftermath,
        /// <summary>失手（蓄力被打断）：黑球在掌中崩散</summary>
        Fumble,
    }

    /// <summary>黑闪四臂驱动数据，状态每帧（非服务端）计算并下发，全量由 Timer 确定性推导</summary>
    internal struct MLordUltArmDrive
    {
        public MLordUltArmPhase Phase;
        /// <summary>阶段内进度 0~1</summary>
        public float PhaseT;
        /// <summary>黑球世界坐标（掷出后为惯性外推位）</summary>
        public Vector2 BallCenter;
        /// <summary>黑球当前半径 px</summary>
        public float BallRadius;
        /// <summary>掷出方向（Silence 拍锁定）</summary>
        public Vector2 ThrowDir;
        /// <summary>黑球可见度 0~1（掷出交棒给弹体后渐隐）</summary>
        public float BallVisible;
        /// <summary>黑球压缩度 0~1（喂给 shader：越压越暗、电弧越躁）</summary>
        public float Collapse;
        /// <summary>打断进度 0~1（揉搓窗失血占阈值比例）：红纹/电弧随之加剧，打断博弈可见化</summary>
        public float BreakCharge;
        /// <summary>演出种子（服务端骰点同步）</summary>
        public int Seed;
    }

    /// <summary>
    /// 黑闪大招的四条幻影手臂：纯客户端渲染模块，不占 NPC 实体。
    /// 状态每帧下发 <see cref="MLordUltArmDrive"/>，掌位由临界阻尼弹簧追踪确定性目标
    /// （弹簧滞后=重量感，确定性伪噪声=搓动的不完美跟踪）；肘位双骨解析 IK，
    /// 极性带迟滞投票，历史教训（2026-08-13）：任何从残留状态进入的路径都必须自愈，
    /// 断帧/换主即硬重建（snap），弹簧永不从来历不明的状态里积分。
    /// 视觉双层构造：暗体真 alpha 遮挡 + 红黑电弧加色缘（黑闪专属材质）
    /// </summary>
    internal static class MLordUltArms
    {
        //―――― 几何常数 ――――
        /// <summary>幻影臂单骨长 px（略短于本体骨臂 340，读作"次生幻影"但不失体量——
        /// 175/0.58 的旧值在引力昏暗背景上小到近乎不可见，2026-08 审计根因）</summary>
        private const float Bone = 250f;
        /// <summary>骨段最大拉伸</summary>
        private const float StretchCap = 1.3f;
        /// <summary>幻影臂贴图整体缩放</summary>
        private const float ArmScale = 0.82f;
        /// <summary>断帧硬重建阈值：超过此 tick 未驱动即视作残留状态</summary>
        private const uint SnapGapTicks = 8;
        /// <summary>极性侧向死区：|外向投影| 低于此值不计翻转票（纯抖动锁死当前侧）</summary>
        private const float SideDeadZone = 0.15f;
        /// <summary>极性翻转投票：连续帧数</summary>
        private const int PolarityVotes = 6;

        /// <summary>肩锚相对核心（随 core.rotation 旋转），X 形四点</summary>
        private static readonly Vector2[] ShoulderAnchors = [
            new(-92f, -148f), new(92f, -148f), new(-146f, 26f), new(146f, 26f),
        ];
        /// <summary>各掌环抱黑球的驻留角（弧度，X 形对位）</summary>
        private static readonly float[] HomeAngles = [-2.36f, -0.79f, 2.50f, 0.64f];

        private struct ArmSim
        {
            /// <summary>掌心世界坐标（弹簧积分量）</summary>
            public Vector2 Palm;
            public Vector2 PalmVel;
            /// <summary>肘极性 +1/-1（迟滞投票锁定）</summary>
            public float Polarity;
            /// <summary>对侧优势连续帧计数</summary>
            public int FlipVotes;
            /// <summary>物化登场时本臂已弹出（逐条星爆只放一次）</summary>
            public bool Popped;
            public bool Init;
        }

        private static readonly ArmSim[] arms = new ArmSim[4];
        private static int ownerWhoAmI = -1;
        private static uint lastDriveTick;
        private static MLordUltArmDrive drive;
        /// <summary>整体可见度（激活渐入，断驱动渐出）</summary>
        private static float visibility;

        /// <summary>正在展示（Draw 消费）</summary>
        public static bool Active => visibility > 0.01f && ownerWhoAmI >= 0;

        /// <summary>卸载/离开世界清空</summary>
        public static void Reset() {
            Array.Clear(arms);
            ownerWhoAmI = -1;
            visibility = 0f;
            drive = default;
        }

        #region 驱动

        /// <summary>状态每帧下发驱动（仅非服务端调用）。断帧/换主自动硬重建</summary>
        public static void Drive(NPC core, in MLordUltArmDrive newDrive) {
            uint tick = Main.GameUpdateCount + 1u;

            //换主或断帧：残留状态一律推倒重建，弹簧不从旧状态续积分
            bool rebuild = ownerWhoAmI != core.whoAmI || tick - lastDriveTick > SnapGapTicks;
            ownerWhoAmI = core.whoAmI;
            lastDriveTick = tick;
            drive = newDrive;

            visibility = MathHelper.Clamp(visibility + 0.09f, 0f, 1f);

            for (int i = 0; i < 4; i++) {
                Vector2 shoulder = ShoulderWorld(core, i);
                Vector2 target = PalmTarget(core, i, shoulder);
                ref ArmSim sim = ref arms[i];

                if (rebuild || !sim.Init) {
                    sim.Palm = target;
                    sim.PalmVel = Vector2.Zero;
                    sim.Polarity = 0f;
                    sim.FlipVotes = 0;
                    sim.Popped = false;
                    sim.Init = true;
                    continue;
                }

                //物化逐条弹出：本臂错拍苏醒的一记星爆+轻震屏（召唤要一条条被看见）
                if (drive.Phase == MLordUltArmPhase.Manifest && !sim.Popped
                    && ManifestSlotT(drive.PhaseT, i) > 0.03f) {
                    sim.Popped = true;
                    MLordScreenFX.StarBurst(shoulder, 0.85f, 9);
                    MLordScreenFX.Punch(shoulder, 2.4f, 6);
                    SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.6f, Pitch = -0.7f + i * 0.12f, MaxInstances = 4 }, shoulder);
                }

                SpringVector(ref sim.Palm, ref sim.PalmVel, target, PhaseOmega(drive.Phase));
                //NaN 防线：任何数值事故直接硬贴目标，绝不让污染扩散到下一帧
                if (float.IsNaN(sim.Palm.X) || float.IsNaN(sim.Palm.Y)) {
                    sim.Palm = target;
                    sim.PalmVel = Vector2.Zero;
                }
            }

            EmitParticles(core);
        }

        /// <summary>各阶段弹簧角频率：慢=飘（物化/余波），快=狠（掷出）</summary>
        private static float PhaseOmega(MLordUltArmPhase phase) => phase switch {
            MLordUltArmPhase.Manifest => 7f,
            MLordUltArmPhase.Embrace => 9.5f,
            MLordUltArmPhase.Knead => 13f,
            MLordUltArmPhase.Silence => 18f,
            MLordUltArmPhase.Throw => 26f,
            MLordUltArmPhase.Fumble => 22f,
            _ => 5.5f,
        };

        private static Vector2 ShoulderWorld(NPC core, int slot) {
            return core.Center + ShoulderAnchors[slot].RotatedBy(core.rotation);
        }

        /// <summary>掌心目标：阶段的确定性函数（时间+种子），各端一致</summary>
        private static Vector2 PalmTarget(NPC core, int slot, Vector2 shoulder) {
            Vector2 outDir = ShoulderAnchors[slot].SafeNormalize(Vector2.UnitX);
            float t = (float)(Main.GameUpdateCount % 216000u);
            float phaseT = drive.PhaseT;

            switch (drive.Phase) {
                case MLordUltArmPhase.Manifest: {
                    //自披风后向外舒展：四臂按槽序错拍逐条弹出，末段微颤（苏醒）
                    float slotT = ManifestSlotT(phaseT, slot);
                    float reach = 60f + 300f * VaultUtils.EaseOutCubic(slotT);
                    Vector2 tremor = slotT > 0.7f
                        ? new Vector2(Noise(t * 0.9f, slot), Noise(t * 0.9f, slot + 7)) * 4f
                        : Vector2.Zero;
                    return shoulder + outDir.RotatedBy(core.rotation) * reach + tremor;
                }
                case MLordUltArmPhase.Embrace: {
                    //EaseOutBack：掌先冲过球面再回弹，环抱有"合拢撞击"的一拍
                    float back = EaseOutBack(phaseT, 1.4f);
                    float reach = MathHelper.Lerp(420f, drive.BallRadius + 30f, back);
                    return drive.BallCenter + HomeAngles[slot].ToRotationVector2() * Math.Max(reach, 8f);
                }
                case MLordUltArmPhase.Knead: {
                    //错拍搓动：角向对位摆动 + 径向вдав压 + 伪噪声不完美跟踪
                    float w = 0.055f + 0.075f * phaseT;   //越搓越快
                    float sway = 0.55f * (float)Math.Sin(w * t + slot * MathHelper.PiOver2)
                        + Noise(t * 0.35f, slot * 3 + 1) * 0.16f;
                    float dent = 16f * Math.Max(0f, (float)Math.Sin(w * 1.7f * t + slot * 1.7f));
                    float r = drive.BallRadius + 24f - dent + Noise(t * 0.5f, slot * 5 + 2) * 7f;
                    return drive.BallCenter + (HomeAngles[slot] + sway).ToRotationVector2() * Math.Max(r, 6f);
                }
                case MLordUltArmPhase.Silence: {
                    //定格在搓动的最后一相 + 整体内压 6px + 高频微颤（憋住的力）
                    Vector2 hold = drive.BallCenter
                        + HomeAngles[slot].ToRotationVector2() * (drive.BallRadius + 18f);
                    float shiver = 1.4f * (float)Math.Sin(t * 2.6f + slot * 2.1f);
                    return hold + new Vector2(shiver, -shiver * 0.6f);
                }
                case MLordUltArmPhase.Throw: {
                    if (phaseT < 0.45f) {
                        //甩出：四掌顺掷向猛推
                        float push = VaultUtils.EaseOutCubic(phaseT / 0.45f);
                        return drive.BallCenter + drive.ThrowDir * (40f + 340f * push)
                            + HomeAngles[slot].ToRotationVector2() * 34f;
                    }
                    //反冲：质量守恒的回撤（掷出重量感来源）
                    return shoulder + outDir.RotatedBy(core.rotation) * 210f - drive.ThrowDir * 80f;
                }
                case MLordUltArmPhase.Fumble: {
                    //失手：黑球崩散的冲击把四掌炸开，外上方甩脱 + 剧烈抖动衰减
                    float kick = 1f - VaultUtils.EaseOutCubic(phaseT);
                    Vector2 jolt = new Vector2(Noise(t * 1.7f, slot + 11), Noise(t * 1.7f, slot + 17)) * 30f * kick;
                    return shoulder + outDir.RotatedBy(core.rotation) * 360f + new Vector2(0f, -80f) + jolt;
                }
                default: {
                    //Aftermath/Hidden：归位外张，缓慢漂散
                    Vector2 driftOff = new(Noise(t * 0.2f, slot + 23) * 18f, Noise(t * 0.17f, slot + 29) * 14f);
                    return shoulder + outDir.RotatedBy(core.rotation) * 250f + driftOff;
                }
            }
        }

        /// <summary>物化错拍进度：槽 i 延迟 0.16·i 后在 0.52 相位窗内完成弹出</summary>
        private static float ManifestSlotT(float phaseT, int slot) {
            return MathHelper.Clamp((phaseT - slot * 0.16f) / 0.52f, 0f, 1f);
        }

        /// <summary>搓动/寂静期的红黑星尘（向球心汇聚 + 掌间迸溅）</summary>
        private static void EmitParticles(NPC core) {
            _ = core;
            if (drive.Phase == MLordUltArmPhase.Knead && drive.BallVisible > 0.5f) {
                //汇聚：红黑星流被吸进球
                if (Main.rand.NextBool(3)) {
                    Vector2 pos = drive.BallCenter + Main.rand.NextVector2Unit()
                        * Main.rand.NextFloat(150f, 320f);
                    Vector2 pull = (drive.BallCenter - pos) * 0.09f;
                    Color c = Color.Lerp(MLordDirector.BlackFlashRed, MLordDirector.VoidBlack, Main.rand.NextFloat(0.55f));
                    PRTLoader.NewParticle<PRT_HeavenfallStar>(pos, pull.RotatedBy(0.5f), c,
                        Main.rand.NextFloat(0.35f, 0.7f))?.Configure(false, Main.rand.Next(12, 20));
                }
                //掌根迸红火花（搓出的静电；打断进度越高迸得越密——球在被打碎的边缘）
                int sparkDenom = Math.Max(2, 6 - (int)(drive.BreakCharge * 4f));
                if (Main.rand.NextBool(sparkDenom)) {
                    int slot = Main.rand.Next(4);
                    PRTLoader.NewParticle<PRT_Spark>(arms[slot].Palm,
                        Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                        MLordDirector.BlackFlashRed, Main.rand.NextFloat(0.7f, 1.1f))
                        ?.Configure(false, Main.rand.Next(10, 18));
                }
            }
        }

        #endregion

        #region 绘制

        /// <summary>
        /// 核心 Draw 末尾调用：下对臂→黑球→上对臂三层。
        /// 未被本帧驱动即快速渐出（状态离场自然收干）
        /// </summary>
        public static void Draw(SpriteBatch spriteBatch, NPC core, Vector2 screenPos) {
            if (core.whoAmI != ownerWhoAmI || !Active) {
                return;
            }
            //本帧未驱动：渐出而非硬消失
            if (Main.GameUpdateCount + 1u - lastDriveTick > 2u) {
                visibility = Math.Max(0f, visibility - 0.12f);
                if (visibility <= 0.01f) {
                    return;
                }
            }

            float armAlpha = visibility * PhaseArmAlpha();

            //下对垫底
            DrawArm(spriteBatch, core, 2, screenPos, armAlpha);
            DrawArm(spriteBatch, core, 3, screenPos, armAlpha);

            if (drive.BallVisible > 0.02f) {
                DrawHeldBall(screenPos);
            }
            //寂静拍：掷向已锁，亮出瞄准线（预告即承诺）
            if (drive.Phase == MLordUltArmPhase.Silence) {
                DrawAimLine(screenPos);
            }

            //上对压前
            DrawArm(spriteBatch, core, 0, screenPos, armAlpha);
            DrawArm(spriteBatch, core, 1, screenPos, armAlpha);
        }

        /// <summary>阶段透明度包络：物化渐入，余波/失手渐出</summary>
        private static float PhaseArmAlpha() => drive.Phase switch {
            MLordUltArmPhase.Manifest => VaultUtils.EaseOutCubic(drive.PhaseT),
            MLordUltArmPhase.Aftermath => 1f - drive.PhaseT,
            MLordUltArmPhase.Fumble => 1f - drive.PhaseT * drive.PhaseT,
            MLordUltArmPhase.Hidden => 0f,
            _ => 1f,
        };

        /// <summary>单臂：解析双骨 IK + 三段绘制（红缘投影→暗体）</summary>
        private static void DrawArm(SpriteBatch spriteBatch, NPC core, int slot, Vector2 screenPos, float alpha) {
            if (alpha <= 0.01f) {
                return;
            }
            ref ArmSim sim = ref arms[slot];
            if (!sim.Init) {
                return;
            }
            Vector2 shoulder = ShoulderWorld(core, slot);
            float bodyBias = ShoulderAnchors[slot].X < 0f ? -1f : 1f;
            Vector2 elbow = SolveElbow(ref sim, shoulder, sim.Palm, bodyBias);

            Texture2D upperTex = TextureAssets.Extra[14].Value;
            Texture2D foreTex = TextureAssets.Extra[15].Value;
            Texture2D handTex = TextureAssets.Npc[NPCID.MoonLordHand].Value;

            bool leftSide = ShoulderAnchors[slot].X < 0f;
            SpriteEffects fx = leftSide ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            //骨段（原贴图关节距 340px，幻影骨 175px→基准纵向缩放）
            DrawBone(spriteBatch, upperTex, shoulder, elbow, new Vector2(76f, 66f), leftSide, alpha, screenPos, fx);
            DrawBone(spriteBatch, foreTex, elbow, sim.Palm, new Vector2(60f, 30f), leftSide, alpha, screenPos, fx);

            //手壳：抓握帧随阶段
            int grip = drive.Phase switch {
                MLordUltArmPhase.Knead => 2,
                MLordUltArmPhase.Silence => 3,
                MLordUltArmPhase.Embrace => 1,
                _ => 0,
            };
            Rectangle frame = handTex.Frame(1, 4, 0, grip);
            Vector2 handOrigin = new(120f, 180f);
            if (!leftSide) {
                handOrigin.X = handTex.Width - handOrigin.X;
            }
            //掌朝球心：掌根方向对准球（掷出/余波时对准运动方向反向）
            float palmRot = (drive.BallCenter - sim.Palm).SafeNormalize(-Vector2.UnitY).ToRotation() - MathHelper.PiOver2;
            Vector2 palmPos = sim.Palm - screenPos;
            //红黑电弧缘（加色，放大投影）——引力昏暗背景上就靠这圈亮缘立住剪影
            spriteBatch.Draw(handTex, palmPos, frame, MLordDirector.BlackFlashRed with { A = 0 } * (0.65f * alpha),
                palmRot, handOrigin, ArmScale * 1.07f, fx, 0f);
            //揉搓期白热边脉冲（憋住的力；打断进度越高越躁）
            float hot = KneadHotPulse();
            if (hot > 0.02f) {
                spriteBatch.Draw(handTex, palmPos, frame, MLordDirector.MoonWhite with { A = 0 } * (0.3f * hot * alpha),
                    palmRot, handOrigin, ArmScale * 1.1f, fx, 0f);
            }
            //暗体（真 alpha 遮挡，提高明度与背景暗化拉开档位）
            spriteBatch.Draw(handTex, palmPos, frame, new Color(46, 24, 66) * alpha,
                palmRot, handOrigin, ArmScale, fx, 0f);
        }

        /// <summary>揉搓白热脉冲强度：~2Hz 呼吸，打断进度抬升底数（红纹加剧的可见语言）</summary>
        private static float KneadHotPulse() {
            if (drive.Phase != MLordUltArmPhase.Knead && drive.Phase != MLordUltArmPhase.Silence) {
                return 0f;
            }
            float breathe = 0.5f + 0.5f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 12.6f);
            return breathe * (0.45f + 0.55f * drive.BreakCharge);
        }

        /// <summary>骨段绘制：红缘投影 + 白热脉冲 + 暗体，沿骨轴缩放到实际距离</summary>
        private static void DrawBone(SpriteBatch spriteBatch, Texture2D tex, Vector2 from, Vector2 to,
            Vector2 origin, bool leftSide, float alpha, Vector2 screenPos, SpriteEffects fx) {
            float dist = Vector2.Distance(from, to);
            if (dist < 2f) {
                return;
            }
            if (!leftSide) {
                origin.X = tex.Width - origin.X;
            }
            float rot = (to - from).ToRotation() - MathHelper.PiOver2;
            //340 = 原贴图关节距；纵向贴合骨距，横向统一幻影缩放
            Vector2 scale = new(ArmScale, dist / 340f);
            Vector2 pos = from - screenPos;
            spriteBatch.Draw(tex, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.6f * alpha),
                rot, origin, scale * 1.06f, fx, 0f);
            float hot = KneadHotPulse();
            if (hot > 0.02f) {
                spriteBatch.Draw(tex, pos, null, MLordDirector.MoonWhite with { A = 0 } * (0.26f * hot * alpha),
                    rot, origin, scale * 1.09f, fx, 0f);
            }
            spriteBatch.Draw(tex, pos, null, new Color(46, 24, 66) * alpha, rot, origin, scale, fx, 0f);
        }

        /// <summary>
        /// 解析双骨 IK：肘 = 弦中点 ± 垂距。极性判据 = 垂向对（球外向 + 体侧解剖偏好）的
        /// 带符号投影：球外向让肘环抱朝外，体侧常量偏好在"掌位于球正下/正上"的退化位形里
        /// 兜底（纯球外向曾在该位形陷入死区永不自愈，2026-08-13 案的形态）。
        /// |投影| 落入 <see cref="SideDeadZone"/> 不计票（边界抖动锁死当前侧），
        /// 对侧连续 <see cref="PolarityVotes"/> 帧足签才翻转。
        /// 仿真回归：错侧残留在全部揉搓驻留位 ≤7 tick 自愈
        /// </summary>
        private static Vector2 SolveElbow(ref ArmSim sim, Vector2 shoulder, Vector2 palm, float bodyBias) {
            Vector2 chord = palm - shoulder;
            float d = chord.Length();
            if (d < 1f) {
                return shoulder + new Vector2(0f, Bone);
            }
            Vector2 dir = chord / d;
            //骨可达：过远则拉伸（h→0 手臂拉直），过近则受压保持弯折
            float boneEff = Math.Clamp(d * 0.52f, Bone * 0.6f, Bone * StretchCap);
            float half = Math.Min(d * 0.5f, boneEff);
            float h = (float)Math.Sqrt(Math.Max(boneEff * boneEff - half * half, 0f));
            Vector2 mid = shoulder + dir * half;
            Vector2 perp = new(-dir.Y, dir.X);

            //外向 = 球心指向掌 + 体侧解剖偏好（左臂肘向左、右臂肘向右的常量兜底）
            Vector2 outward = (palm - drive.BallCenter).SafeNormalize(Vector2.UnitY)
                + new Vector2(bodyBias * 0.6f, 0f);
            outward = outward.SafeNormalize(Vector2.UnitY);
            float side = Vector2.Dot(perp, outward);
            float wantSide = side >= 0f ? 1f : -1f;

            if (sim.Polarity == 0f) {
                //重建首帧：直接采当前位形，不带残留
                sim.Polarity = wantSide;
                sim.FlipVotes = 0;
            }
            else if (wantSide != sim.Polarity) {
                sim.FlipVotes = Math.Abs(side) > SideDeadZone ? sim.FlipVotes + 1 : 0;
                if (sim.FlipVotes >= PolarityVotes) {
                    sim.Polarity = wantSide;
                    sim.FlipVotes = 0;
                }
            }
            else {
                sim.FlipVotes = 0;
            }
            return mid + perp * (h * sim.Polarity);
        }

        /// <summary>
        /// 掌中黑球：shader 出体（暗核吞光+吸积盘+红黑电弧），缺 shader 走 CPU 双层回退。
        /// 暗体永远真 alpha（遮挡背景），电弧永远加色，黑闪材质铁律
        /// </summary>
        private static void DrawHeldBall(Vector2 screenPos) {
            float r = Math.Max(drive.BallRadius, 4f);
            float vis = drive.BallVisible * visibility;
            Vector2 pos = drive.BallCenter - screenPos;

            Effect shader = EffectLoader.MLordBlackFlash?.Value;
            if (shader != null) {
                Texture2D canvas = CWRUtils.GetT2DAsset(CWRConstant.VaultPlaceholder2).Value;
                //画布边长 = 半径×5：给电弧和吸积盘留出画幅
                float scale = r * 5f / canvas.Width;
                shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                shader.Parameters["uCollapse"]?.SetValue(drive.Collapse);
                //打断进度抬升电弧强度：球体红纹加剧 = 打断博弈的可见语言
                shader.Parameters["uArc"]?.SetValue(0.5f + drive.Collapse * 0.5f + drive.BreakCharge * 0.4f);
                shader.Parameters["uAlpha"]?.SetValue(vis);
                shader.Parameters["uSeed"]?.SetValue(drive.Seed % 89 * 0.211f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                //噪声显式绑 s1（SpriteBatch.Draw 会覆写 s0，参数式贴图绑定实机失效）
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                gd.Textures[1] = CWRAsset.PerlinNoise.Value;
                gd.SamplerStates[1] = SamplerState.LinearWrap;
                shader.CurrentTechnique.Passes[0].Apply();
                Main.spriteBatch.Draw(canvas, pos, null, Color.White, 0f,
                    canvas.Size() * 0.5f, scale, SpriteEffects.None, 0f);
                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                return;
            }

            //CPU 回退：暗核（真 alpha）+ 红缘（打断进度提亮）+ 吸积斜盘
            Texture2D glow = CWRAsset.DiffusionCircle?.Value;
            if (glow == null) {
                return;
            }
            float texScale = r * 2.6f / glow.Width;
            Main.EntitySpriteDraw(glow, pos, null,
                MLordDirector.BlackFlashRed with { A = 0 } * ((0.5f + 0.35f * drive.BreakCharge) * vis),
                Main.GlobalTimeWrappedHourly * 1.3f, glow.Size() / 2f, texScale * 1.25f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.VoidBlack * (0.96f * vis),
                -Main.GlobalTimeWrappedHourly * 0.8f, glow.Size() / 2f, texScale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, MLordDirector.BlackFlashRed with { A = 0 } * (0.55f * vis),
                Main.GlobalTimeWrappedHourly * 2.1f, glow.Size() / 2f,
                new Vector2(texScale * 1.7f, texScale * 0.4f), SpriteEffects.None, 0);
        }

        /// <summary>寂静拍瞄准线：黑球沿锁定掷向的细红线，脉动提示但不遮场</summary>
        private static void DrawAimLine(Vector2 screenPos) {
            Texture2D streak = CWRAsset.LightShot?.Value;
            if (streak == null) {
                return;
            }
            float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 18f);
            float rot = drive.ThrowDir.ToRotation();
            Vector2 anchor = new(0f, streak.Height * 0.5f);
            Vector2 start = drive.BallCenter + drive.ThrowDir * drive.BallRadius - screenPos;
            Main.EntitySpriteDraw(streak, start, null,
                MLordDirector.BlackFlashRed with { A = 0 } * (0.3f * pulse * visibility), rot, anchor,
                new Vector2(1400f / streak.Width, 10f / streak.Height), SpriteEffects.None, 0);
        }

        #endregion

        #region 数学小件

        /// <summary>临界阻尼向量弹簧（与 MLordArmIK 同式，作用于二维位置）</summary>
        private static void SpringVector(ref Vector2 pos, ref Vector2 vel, Vector2 target, float omega) {
            const float Dt = 1f / 60f;
            Vector2 x = pos - target;
            Vector2 temp = (vel + x * omega) * Dt;
            float decay = (float)Math.Exp(-omega * Dt);
            vel = (vel - temp * omega) * decay;
            pos = target + (x + temp) * decay;
        }

        /// <summary>确定性伪噪声 -1~1：双相位正弦叠加，无 rand 依赖，各端一致</summary>
        private static float Noise(float t, int salt) {
            return 0.6f * (float)Math.Sin(t * 0.31f + salt * 2.17f)
                 + 0.4f * (float)Math.Sin(t * 0.113f + salt * 5.9f);
        }

        /// <summary>回弹缓动：冲过头再弹回（合拢撞击感）</summary>
        private static float EaseOutBack(float t, float overshoot) {
            t = MathHelper.Clamp(t, 0f, 1f);
            float inv = t - 1f;
            return 1f + inv * inv * ((overshoot + 1f) * inv + overshoot);
        }

        #endregion
    }
}
