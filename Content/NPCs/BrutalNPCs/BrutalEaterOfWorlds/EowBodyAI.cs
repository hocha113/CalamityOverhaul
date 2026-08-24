using CalamityOverhaul.Content.DamageModify;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Core;
using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds.Rendering;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalEaterOfWorlds
{
    /// <summary>体节读取的头部脉冲通道快照</summary>
    internal struct EowPulseInfo
    {
        /// <summary>0无 1蓄势波 2蜕皮波 3死亡溃爆波 4分裂点闪</summary>
        public int Kind;
        /// <summary>波前相位 0头→1尾(死亡波反向消费)</summary>
        public float Phase;
        /// <summary>本节链上比例 0头→1尾</summary>
        public float Fraction;
        /// <summary>波前经过的加亮 0~1</summary>
        public float WaveGlow;
        /// <summary>撕裂点加亮 0~1</summary>
        public float BoundaryGlow;
    }

    /// <summary>体节：跟链/分组首节自驾/压缩呼吸/脉冲染色</summary>
    internal class EowBodyAI : BrutalNPCOverride
    {
        #region 数据
        public override int TargetID => NPCID.EaterofWorldsBody;

        /// <summary>基础节距(38px宽×0.9轻叠)</summary>
        internal const float BaseSegmentGap = 34f;

        /// <summary>链序(生成时写入 npc.ai[0])</summary>
        protected int Ordinal => (int)npc.ai[0];
        /// <summary>前邻(生成时写入 npc.ai[1])</summary>
        protected NPC FrontNPC => Main.npc[(int)npc.ai[1]];
        /// <summary>头索引(生成时写入 npc.ai[3])</summary>
        protected int HeadIndex => (int)npc.ai[3];

        /// <summary>分组首节形态渐变(0跟随→1昂首)，本地表现</summary>
        protected float leaderMorph;
        /// <summary>死亡演出冻结姿态</summary>
        private bool deathFreezeCaptured;
        private Vector2 deathFrozenOffset;
        private float deathFrozenRotation;
        /// <summary>蜕皮波已触发标记(本地防重播)</summary>
        private bool moltHuskFired;
        /// <summary>死亡溃爆已触发标记(本地防重播)</summary>
        private bool ruptureFired;
        #endregion

        public override bool? CanBrutalOverride() {
            return null;
        }

        public override bool CheckActive() => false;

        //统一血池：体节不可独立摧毁，悬浮小血条只会误导玩家，返回false拦掉原版绘制
        //(命中伤害数字与hover判定不走此钩子，不受影响)
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        public override void SetProperty() {
            npc.aiStyle = -1;
            npc.BossBar = ModContent.GetInstance<EowBossBar>();
        }

        #region 头部解析
        /// <summary>找到有效的头NPC，找不到返回null</summary>
        protected NPC ResolveHead() {
            int idx = HeadIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC head = Main.npc[idx];
                if (head.active && head.type == NPCID.EaterofWorldsHead) {
                    return head;
                }
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == NPCID.EaterofWorldsHead) {
                    npc.ai[3] = n.whoAmI;
                    return n;
                }
            }
            return null;
        }

        /// <summary>头是否处于死亡演出</summary>
        private static bool HeadInDeathPerformance(NPC head) {
            return head != null && (int)head.ai[2] == (int)EowStateIndex.Death;
        }
        #endregion

        #region 主AI
        public override bool AI() {
            npc.aiStyle = -1;
            NPC head = ResolveHead();

            //无头：立即溃散
            if (head == null) {
                SelfDestruct();
                return false;
            }

            npc.realLife = head.whoAmI;
            npc.timeLeft = 1800;
            UpdateAlphaFade(head);

            //死亡演出：冻结姿态保活+本地溃爆表现
            if (HeadInDeathPerformance(head)) {
                HandleDeathPerformanceSegment();
                UpdateDeathRuptureFX(head);
                return false;
            }
            deathFreezeCaptured = false;
            ruptureFired = false;

            //前邻失效(非演出期)：链条已断，跟随溃散
            NPC front = FrontNPC;
            bool frontValid = front.Alives()
                && (front.type == NPCID.EaterofWorldsHead || front.type == NPCID.EaterofWorldsBody);
            if (!frontValid) {
                SelfDestruct();
                return false;
            }

            //读取头部同步槽
            head.TryGetOverride<EowHeadAI>(out var headOverride);
            int splitGroups = headOverride != null ? (int)headOverride.ai[EowHeadAI.SlotSplitGroups] : 0;
            int totalSegs = headOverride != null ? (int)headOverride.ai[EowHeadAI.SlotSegmentCount] : 0;
            float compression = headOverride?.Context != null ? headOverride.Context.Compression : 1f;
            if (compression <= 0.01f) {
                compression = 1f;
            }

            bool isLeader = splitGroups > 1 && totalSegs > 0
                && EowSplitLayout.IsLeader(Ordinal, totalSegs, splitGroups, out _);

            //分组首节：运动由头部主控驾驶，这里只管姿态/形态/伤害门控
            if (isLeader) {
                leaderMorph = MathHelper.Clamp(leaderMorph + 0.08f, 0f, 1f);
                if (npc.velocity.LengthSquared() > 0.1f) {
                    npc.rotation = npc.velocity.ToRotation() + MathHelper.PiOver2;
                }
                float leaderSpeed = npc.velocity.Length();
                npc.damage = leaderSpeed > 9f ? npc.defDamage : 0;
            }
            else {
                leaderMorph = MathHelper.Clamp(leaderMorph - 0.06f, 0f, 1f);
                FollowChain(front, compression);
            }

            //出环境狂暴增伤：读头部同步槽，接触伤统一放大
            float enrageRamp = headOverride != null ? MathHelper.Clamp(headOverride.ai[EowHeadAI.SlotEnrageRamp], 0f, 1f) : 0f;
            if (enrageRamp > 0f && npc.damage > 0) {
                npc.damage = (int)(npc.damage * (1f + 0.8f * enrageRamp));
            }

            //投技期全链无接触伤：吞是唯一威胁，也防抓取确认前的延迟窗口里体节撞伤被吞者
            if ((int)head.ai[2] == (int)EowStateIndex.Devour) {
                npc.damage = 0;
            }

            //血池镜像：让原版世吞进度条(Σ各节life)与统一血池一致
            MirrorLifeForVanillaBar(head);

            UpdateAmbientFX(head, splitGroups, totalSegs);

            EowHeadAI.ForcedNetUpdating(npc);
            return false;
        }

        /// <summary>无头/断链溃散</summary>
        private void SelfDestruct() {
            npc.life = 0;
            npc.HitEffect();
            npc.checkDead();
            npc.active = false;
            npc.netUpdate = true;
        }
        #endregion

        #region 跟链运动
        /// <summary>标准跟链；压缩系数由头声明(手风琴呼吸)</summary>
        private void FollowChain(NPC front, float compression) {
            float gap = BaseSegmentGap * npc.scale * compression;

            Vector2 toFront = front.Center - npc.Center;

            //前邻转角带动(链条弯曲传递)
            if (front.rotation != npc.rotation) {
                float angleDelta = MathHelper.WrapAngle(front.rotation - npc.rotation);
                toFront = toFront.RotatedBy(angleDelta * 0.12f);
            }

            npc.velocity = Vector2.Zero;
            npc.rotation = toFront.ToRotation() + MathHelper.PiOver2;
            npc.Center = front.Center - toFront.SafeNormalize(Vector2.Zero) * gap;

            //接触伤害按位移速度门控
            float moved = (npc.position - npc.oldPosition).Length();
            float minContact = 7f;
            if (moved <= minContact) {
                npc.damage = 0;
            }
            else {
                float scalar = MathHelper.Clamp((moved - minContact) / 13f, 0f, 1f);
                npc.damage = (int)MathHelper.Lerp(0f, npc.defDamage, scalar);
            }
        }
        #endregion

        #region 演出辅助
        /// <summary>死亡演出体节：锁血冻结相对前邻姿态</summary>
        private void HandleDeathPerformanceSegment() {
            npc.dontTakeDamage = true;
            npc.damage = 0;
            if (npc.life < 1) {
                npc.life = 1;
            }
            npc.timeLeft = 60;

            NPC front = FrontNPC;
            if (front.Alives()) {
                if (!deathFreezeCaptured) {
                    deathFrozenOffset = npc.Center - front.Center;
                    deathFrozenRotation = npc.rotation;
                    deathFreezeCaptured = true;
                }
                npc.velocity = Vector2.Zero;
                npc.Center = front.Center + deathFrozenOffset;
                npc.rotation = deathFrozenRotation;
            }
            else {
                npc.velocity = Vector2.Zero;
            }

            EowHeadAI.ForcedNetUpdating(npc);
        }

        /// <summary>死亡溃爆波扫过本节的一次性演出(各端本地)</summary>
        private void UpdateDeathRuptureFX(NPC head) {
            if (VaultUtils.isServer) {
                return;
            }
            if (!head.TryGetOverride<EowHeadAI>(out var h) || h.Context == null) {
                return;
            }
            int totalSegs = (int)h.ai[EowHeadAI.SlotSegmentCount];
            if (totalSegs <= 0 || h.Context.PulseKind != 3) {
                return;
            }
            float fraction = Ordinal / (float)totalSegs;
            //波前从尾(1)扫向头(0)，跌破本节比例时溃爆一次
            if (!ruptureFired && h.Context.PulsePhase <= fraction) {
                ruptureFired = true;
                EowMotionFX.SpawnRipBurst(npc.Center, npc.rotation.ToRotationVector2(), 1.2f);
                EowMotionFX.SpawnMoltHusk(npc, 1.2f);
            }
        }

        /// <summary>把统一血池按比例镜像到本节，供原版世吞进度条求和</summary>
        private void MirrorLifeForVanillaBar(NPC head) {
            if (head.lifeMax <= 0) {
                return;
            }
            float ratio = MathHelper.Clamp(head.life / (float)head.lifeMax, 0f, 1f);
            int mirrored = Math.Max((int)(npc.lifeMax * ratio), 1);
            if (npc.life != mirrored) {
                npc.life = mirrored;
            }
        }

        /// <summary>
        /// 出生 alpha=255(原版 SetDefaults 给定)：入场演出期按前邻涟漪淡入(破土渐显)，<br/>
        /// 其余状态直接淡入，中途加入的客户端各节以 255 重建，没人再驱动涟漪
        /// </summary>
        private void UpdateAlphaFade(NPC head) {
            if (npc.alpha <= 0) {
                return;
            }
            if ((int)head.ai[2] == (int)EowStateIndex.Intro) {
                NPC front = FrontNPC;
                if (front.Alives() && front.alpha >= 128) {
                    return; //涟漪未到本节
                }
            }
            npc.alpha = Math.Max(npc.alpha - 42, 0);
        }

        /// <summary>体节环境表现：高速酸沫/蜕皮波蜕壳(客户端)</summary>
        private void UpdateAmbientFX(NPC head, int splitGroups, int totalSegs) {
            if (VaultUtils.isServer) {
                return;
            }

            float segSpeed = (npc.position - npc.oldPosition).Length();
            if (segSpeed > 20f && Main.rand.NextBool(11)) {
                EowMotionFX.SpawnSegmentSpeedSpray(npc, MathHelper.Clamp(segSpeed / 42f, 0.5f, 1.2f));
            }

            //读取头上下文脉冲通道(本地演出)
            var ctx = head.TryGetOverride<EowHeadAI>(out var h) ? h.Context : null;
            if (ctx == null || totalSegs <= 0) {
                return;
            }

            float fraction = Ordinal / (float)totalSegs;
            //蜕皮波经过本节：弹壳一次
            if (ctx.PulseKind == 2) {
                if (!moltHuskFired && ctx.PulsePhase >= fraction) {
                    moltHuskFired = true;
                    EowMotionFX.SpawnMoltHusk(npc);
                }
            }
            else {
                moltHuskFired = false;
            }

            //撕裂点酸光照明
            if (splitGroups > 1) {
                float boundary = EowSplitLayout.BoundaryGlow(Ordinal, totalSegs, splitGroups);
                if (boundary > 0.2f) {
                    Lighting.AddLight(npc.Center, EowMotionFX.AcidGreen.ToVector3() * 0.5f * boundary);
                }
            }
        }
        #endregion

        #region 伤害修正
        public override bool? On_ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            if (modifiers.DamageType == EndlessDamageClass.Instance) {
                return false;
            }
            //体节减伤：统一血池下抑制穿透武器的多节盛宴
            modifiers.FinalDamage *= 0.5f;
            //出环境狂暴免伤
            float enrageRamp = ReadHeadEnrageRamp();
            if (enrageRamp > 0f) {
                modifiers.FinalDamage *= 1f - 0.9f * enrageRamp;
            }
            return false;
        }

        /// <summary>读头部同步槽里的出环境狂暴强度</summary>
        protected float ReadHeadEnrageRamp() {
            NPC head = ResolveHead();
            return head != null && head.TryGetOverride<EowHeadAI>(out var h)
                ? MathHelper.Clamp(h.ai[EowHeadAI.SlotEnrageRamp], 0f, 1f) : 0f;
        }
        #endregion

        #region 绘制
        /// <summary>读取头上下文的脉冲通道(所有体节共用)</summary>
        protected EowPulseInfo ReadPulse() {
            EowPulseInfo info = default;
            NPC head = ResolveHead();
            if (head == null || !head.TryGetOverride<EowHeadAI>(out var h) || h.Context == null) {
                return info;
            }
            var ctx = h.Context;
            int totalSegs = (int)h.ai[EowHeadAI.SlotSegmentCount];
            int splitGroups = (int)h.ai[EowHeadAI.SlotSplitGroups];
            if (totalSegs <= 0) {
                return info;
            }

            info.Kind = ctx.PulseKind;
            info.Phase = ctx.PulsePhase;
            info.Fraction = Ordinal / (float)totalSegs;

            //波前经过的体节亮起
            if (info.Kind is 1 or 2 or 3) {
                float dist = Math.Abs(info.Fraction - info.Phase);
                info.WaveGlow = MathHelper.Clamp(1f - dist / 0.14f, 0f, 1f);
            }
            //分裂点闪烁(未分裂时 PulsePhase 携带目标组数)
            if (info.Kind == 4 || splitGroups > 1) {
                int g = splitGroups > 1 ? splitGroups : (int)MathHelper.Clamp(ctx.PulsePhase, 2f, 4f);
                float boundary = EowSplitLayout.BoundaryGlow(Ordinal, totalSegs, g);
                if (info.Kind == 4) {
                    boundary *= 0.55f + 0.45f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 21f + Ordinal * 0.8f);
                }
                else {
                    boundary *= 0.5f;
                }
                info.BoundaryGlow = boundary;
            }
            return info;
        }

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.EaterofWorldsBody);
            Main.instance.LoadNPC(NPCID.EaterofWorldsHead);
            Texture2D bodyTex = TextureAssets.Npc[NPCID.EaterofWorldsBody].Value;
            Texture2D headTex = TextureAssets.Npc[NPCID.EaterofWorldsHead].Value;

            DrawSegment(spriteBatch, screenPos, drawColor, bodyTex, headTex, leaderMorph);
            return false;
        }

        /// <summary>体节通用绘制：本体+角色形态+脉冲加光+死亡溃爆暗化；tail 复用</summary>
        protected void DrawSegment(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor,
            Texture2D baseTex, Texture2D morphTex, float morph) {
            Vector2 drawPos = npc.Center - screenPos;
            float fade = 1f - npc.alpha / 255f;
            EowPulseInfo pulse = ReadPulse();

            //死亡溃爆：波前(1→0)扫过后的体节熄灭发暗
            bool ruptured = pulse.Kind == 3 && pulse.Phase <= pulse.Fraction;
            Color bodyColor = drawColor;
            if (ruptured) {
                bodyColor = Color.Lerp(drawColor, EowMotionFX.FleshShadow, 0.72f);
            }
            else {
                //出环境狂暴体色：酸绿灼热
                float enrage = ReadHeadEnrageRamp();
                if (enrage > 0.01f) {
                    bodyColor = Color.Lerp(bodyColor, EowMotionFX.AcidGreen, enrage * 0.4f);
                }
            }

            //高速残影
            float segSpeed = (npc.position - npc.oldPosition).Length();
            float ghostIntensity = MathHelper.Clamp((segSpeed - 18f) / 26f, 0f, 1f);

            //本体(跟随态)
            if (morph < 0.99f) {
                Rectangle frame = baseTex.Bounds;
                Vector2 origin = frame.Size() / 2f;
                if (ghostIntensity > 0.05f && !ruptured) {
                    Vector2 back = npc.Center - (npc.position - npc.oldPosition) * 1.6f - screenPos;
                    spriteBatch.Draw(baseTex, back, frame,
                        EowMotionFX.AcidGreen with { A = 0 } * (0.22f * ghostIntensity * fade),
                        npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
                }
                spriteBatch.Draw(baseTex, drawPos, frame, bodyColor * (fade * (1f - morph)),
                    npc.rotation, origin, npc.scale, SpriteEffects.None, 0f);
            }

            //昂首形态(分组首节长出头)
            if (morph > 0.01f) {
                Rectangle frame = morphTex.Bounds;
                Vector2 origin = frame.Size() / 2f;
                spriteBatch.Draw(morphTex, drawPos, frame, bodyColor * (fade * morph),
                    npc.rotation, origin, npc.scale * (0.9f + 0.1f * morph), SpriteEffects.None, 0f);
            }

            //脉冲加光(蓄势波/撕裂点)，溃爆后的节不再发光
            float glow = Math.Max(pulse.WaveGlow, pulse.BoundaryGlow);
            if (glow > 0.03f && !ruptured) {
                Rectangle frame = morph > 0.5f ? morphTex.Bounds : baseTex.Bounds;
                Texture2D tex = morph > 0.5f ? morphTex : baseTex;
                Vector2 origin = frame.Size() / 2f;
                spriteBatch.Draw(tex, drawPos, frame,
                    EowMotionFX.AcidGreen with { A = 0 } * (0.65f * glow * fade),
                    npc.rotation, origin, npc.scale * 1.04f, SpriteEffects.None, 0f);
                Lighting.AddLight(npc.Center, EowMotionFX.AcidGreen.ToVector3() * 0.4f * glow);
            }
        }

        public override bool PostDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
        #endregion
    }
}
