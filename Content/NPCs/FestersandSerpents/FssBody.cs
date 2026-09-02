using CalamityOverhaul.Content.NPCs.FestersandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.FestersandSerpents
{
    /// <summary>
    /// 体节：跟链 + 统一血池。款式按链序确定（每 CystStep 节一个囊肿节 = 灵液发射器）。
    /// 绘制集中在头部整链层（本类 PreDraw 返回 false）。
    /// ai[2]=1 标记蜕变生长节：出生时从小胀大（当场长出来的读数），生命不并入血池。
    /// </summary>
    internal class FssBody : FssModNPC
    {
        public override string Texture => CWRConstant.NPC + "BSS/Body";

        /// <summary>链序（生成时写入 npc.ai[0]）</summary>
        protected int Ordinal => (int)NPC.ai[0];
        /// <summary>前邻（生成时写入 npc.ai[1]）</summary>
        protected NPC FrontNPC => Main.npc[(int)NPC.ai[1]];
        /// <summary>头索引（生成时写入 npc.ai[3]）</summary>
        protected int HeadIndex => (int)NPC.ai[3];
        /// <summary>蜕变生长节标记（ai[2]=1）</summary>
        protected bool IsGrowthSegment => (int)NPC.ai[2] == 1;

        /// <summary>死亡演出冻结姿态</summary>
        private bool deathFreezeCaptured;
        private Vector2 deathFrozenOffset;
        private float deathFrozenRotation;
        /// <summary>死亡溃爆已触发标记（本地防重播）</summary>
        private bool ruptureFired;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = SerpentChainMath.BodyStyleCount;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Ichor] = true;
        }

        public override void SetDefaults() {
            NPC.width = 44;
            NPC.height = 44;
            NPC.scale = FssDirector.BodyScale;
            NPC.damage = FssDirector.BodyContact;
            NPC.defense = FssDirector.BodyDefense;
            NPC.lifeMax = FssDirector.BodyLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true;
            NPC.lavaImmune = true;
            NPC.dontCountMe = true;
            NPC.alpha = 255;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
        }

        public override bool CheckActive() => false;

        //统一血池：体节不可独立摧毁，悬浮小血条只会误导玩家
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;

        #region 头部解析
        /// <summary>找到有效的头 NPC，找不到返回 null</summary>
        protected NPC ResolveHead() {
            int headType = ModContent.NPCType<FssHead>();
            int idx = HeadIndex;
            if (idx >= 0 && idx < Main.maxNPCs) {
                NPC head = Main.npc[idx];
                if (head.active && head.type == headType) {
                    return head;
                }
            }
            foreach (var n in Main.ActiveNPCs) {
                if (n.type == headType) {
                    NPC.ai[3] = n.whoAmI;
                    return n;
                }
            }
            return null;
        }

        /// <summary>头是否处于死亡演出（头的状态机槽在 head.ai[3]）</summary>
        private static bool HeadInDeathPerformance(NPC head)
            => head != null && (int)head.ai[3] == (int)FssStateIndex.Death;
        #endregion

        #region 主 AI
        public override void AI() {
            NPC head = ResolveHead();
            if (head == null) {
                SelfDestruct();
                return;
            }

            NPC.realLife = head.whoAmI;
            NPC.timeLeft = 1800;

            FssStateContext ctx = (head.ModNPC as FssHead)?.Context;
            UpdateAlphaFade(head, ctx);
            UpdateGrowthBirth();

            //死亡演出：冻结姿态保活 + 本地溃爆表现
            if (HeadInDeathPerformance(head)) {
                HandleDeathPerformanceSegment(head);
                UpdateDeathRuptureFX(ctx);
                return;
            }
            deathFreezeCaptured = false;
            ruptureFired = false;

            //裂躯领节：跳过跟链，运动由头部状态直驱（各端同跑，位置靠周期同步纠偏）
            if (ctx != null && ctx.SplitLeaderOrdinal == Ordinal) {
                UpdateSplitLeader(ctx);
                return;
            }

            //前邻失效（非演出期）：链条已断，跟随溃散
            NPC front = FrontNPC;
            bool frontValid = front.Alives()
                && (front.type == ModContent.NPCType<FssHead>() || front.type == ModContent.NPCType<FssBody>());
            if (!frontValid) {
                SelfDestruct();
                return;
            }

            FollowChain(front, ctx);

            //高速渗漏（客户端表现）
            if (!VaultUtils.isServer) {
                float segSpeed = (NPC.position - NPC.oldPosition).Length();
                if (segSpeed > 18f && Main.rand.NextBool(12)) {
                    FssVfx.FesterTrickle(NPC.Center, 1.2f);
                }
            }

            FssHead.ForcedNetUpdating(NPC);
        }

        /// <summary>蜕变生长节的出生胀大（各端本地同放；scale 参与跟链间距 = 链条当场变长）</summary>
        private void UpdateGrowthBirth() {
            if (!IsGrowthSegment) {
                return;
            }
            if (NPC.localAI[0] == 0f) {
                NPC.localAI[0] = 1f;
                NPC.scale = FssDirector.BodyScale * 0.3f;
            }
            if (NPC.scale < FssDirector.BodyScale) {
                NPC.scale = Math.Min(FssDirector.BodyScale, NPC.scale + FssDirector.BodyScale / 36f);
                if (!Main.dedServ && Main.rand.NextBool(3)) {
                    FssVfx.IchorBurst(NPC.Center, 0.4f, Main.rand.NextVector2Unit());
                }
            }
        }

        /// <summary>
        /// 裂躯领节帧：保留状态写入的速度（位置由引擎积分），旋转跟速度或状态覆盖，
        /// 接触伤害按速度门控（伤害窗=可见冲势），断口持续渗漏。
        /// </summary>
        private void UpdateSplitLeader(FssStateContext ctx) {
            if (!float.IsNaN(ctx.SplitLeaderAim)) {
                NPC.rotation = NPC.rotation.AngleLerp(ctx.SplitLeaderAim + FssHead.FacingRot, 0.35f);
            }
            else if (NPC.velocity.LengthSquared() > 0.2f) {
                NPC.rotation = NPC.velocity.ToRotation() + FssHead.FacingRot;
            }

            float speed = NPC.velocity.Length();
            const float minContact = 7f;
            NPC.damage = speed <= minContact
                ? 0
                : (int)MathHelper.Lerp(0f, NPC.defDamage, MathHelper.Clamp((speed - minContact) / 13f, 0f, 1f));

            //断口渗漏（伪头的伤口底噪）
            if (!VaultUtils.isServer && Main.rand.NextBool(4)) {
                Vector2 wound = NPC.Center + (NPC.rotation - FssHead.FacingRot).ToRotationVector2() * 18f;
                FssVfx.FesterTrickle(wound, 1.5f);
                if (Main.rand.NextBool(3)) {
                    Dust gold = Dust.NewDustPerfect(wound, DustID.Ichor,
                        Main.rand.NextVector2Circular(1.2f, 1.2f) + new Vector2(0f, 1f),
                        40, default, Main.rand.NextFloat(0.7f, 1.1f));
                    gold.noGravity = false;
                }
            }

            FssHead.ForcedNetUpdating(NPC);
        }

        /// <summary>无头/断链溃散</summary>
        private void SelfDestruct() {
            NPC.life = 0;
            NPC.HitEffect();
            NPC.checkDead();
            NPC.active = false;
            NPC.netUpdate = true;
        }

        /// <summary>
        /// 标准跟链 + 链体力学：均匀压缩（盘拢呼吸）之上叠三层真实节距语言
        /// （蓄力聚拢/肌肉行波/高速拉伸），颈段带刚度梯度与弯角钳制（永不锐角）。
        /// 裂躯期后半身以领节为临时首领，刚度与波形按相对链序计。
        /// </summary>
        private void FollowChain(NPC front, FssStateContext ctx) {
            float compression = ctx != null && ctx.Compression > 0.01f ? ctx.Compression : 1f;
            float gap = FssDirector.SegmentGap * NPC.scale * compression;
            int total = ctx != null && ctx.TotalSegments > 0
                ? ctx.TotalSegments : FssDirector.BodyCount + 1;

            float relOrdinal = Ordinal;
            if (ctx != null && ctx.SplitLeaderOrdinal >= 0 && Ordinal > ctx.SplitLeaderOrdinal) {
                relOrdinal = Ordinal - ctx.SplitLeaderOrdinal;
            }

            if (ctx != null) {
                gap *= SerpentChainMath.GatherFactor(relOrdinal, ctx.GatherLevel);
                gap *= SerpentChainMath.GapWaveFactor(relOrdinal, ctx.GapWaveKind, ctx.GapWaveAge, ctx.GapWaveAmp);
                gap *= SerpentChainMath.SpeedStretchFactor(ctx.HeadSpeed);
            }

            Vector2 toFront = front.Center - NPC.Center;

            //堆叠態（门冲/出生）不做刚度与钳制：保持原地等前邻拉出的鱼贯行为
            if (toFront.LengthSquared() > 1f) {
                //前邻转角带动（链条弯曲传递）：颈紧尾松的刚度梯度
                if (front.rotation != NPC.rotation) {
                    float angleDelta = MathHelper.WrapAngle(front.rotation - NPC.rotation);
                    toFront = toFront.RotatedBy(angleDelta * SerpentChainMath.StiffnessFactor(relOrdinal, total));
                }

                //颈段弯角硬钳制：相对前邻体轴的折角超限即圆化（出手帧不许发卡折颈）
                float maxBend = SerpentChainMath.MaxBendAngle(relOrdinal);
                if (maxBend < MathHelper.Pi) {
                    float frontAxis = front.rotation - FssHead.FacingRot;
                    float bend = MathHelper.WrapAngle(toFront.ToRotation() - frontAxis);
                    if (Math.Abs(bend) > maxBend) {
                        float clamped = frontAxis + Math.Sign(bend) * maxBend;
                        toFront = clamped.ToRotationVector2() * toFront.Length();
                    }
                }
            }

            NPC.velocity = Vector2.Zero;
            NPC.rotation = toFront.ToRotation() + FssHead.FacingRot;
            NPC.Center = front.Center - toFront.SafeNormalize(Vector2.Zero) * gap;

            //接触伤害按位移速度门控（伤害窗=可见冲势）；
            //隐身段（门冲吞没/传送帧的巨额位移）无判定——看不见的东西不许咬人
            float moved = (NPC.position - NPC.oldPosition).Length();
            const float minContact = 7f;
            if (moved <= minContact || NPC.alpha > 200) {
                NPC.damage = 0;
            }
            else {
                float scalar = MathHelper.Clamp((moved - minContact) / 13f, 0f, 1f);
                NPC.damage = (int)MathHelper.Lerp(0f, NPC.defDamage, scalar);
            }
        }
        #endregion

        #region 演出辅助
        /// <summary>死亡演出体节：锁血冻结相对前邻姿态</summary>
        private void HandleDeathPerformanceSegment(NPC head) {
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            if (NPC.life < 1) {
                NPC.life = 1;
            }
            NPC.timeLeft = 60;

            NPC front = FrontNPC;
            if (front.Alives()) {
                if (!deathFreezeCaptured) {
                    deathFrozenOffset = NPC.Center - front.Center;
                    deathFrozenRotation = NPC.rotation;
                    deathFreezeCaptured = true;
                }
                NPC.velocity = Vector2.Zero;
                NPC.Center = front.Center + deathFrozenOffset;
                NPC.rotation = deathFrozenRotation;
            }
            else {
                NPC.velocity = Vector2.Zero;
            }

            FssHead.ForcedNetUpdating(NPC);
        }

        /// <summary>死亡溃爆波扫过本节的一次性演出（各端本地，波前尾→头）</summary>
        private void UpdateDeathRuptureFX(FssStateContext ctx) {
            if (VaultUtils.isServer || ctx == null || ctx.TotalSegments <= 0 || ctx.PulseKind != 3) {
                return;
            }
            float fraction = Ordinal / (float)ctx.TotalSegments;
            if (!ruptureFired && ctx.PulsePhase <= fraction) {
                ruptureFired = true;
                FssVfx.CorruptSandBurst(NPC.Center, 1f);
                FssVfx.IchorBurst(NPC.Center, IsCyst ? 1.4f : 0.6f);
                FssVfx.Shake(NPC.Center, 3f, 900f);
            }
        }

        /// <summary>
        /// 出生 alpha=255：入场/门冲按前邻涟漪淡入（破土渐显/鱼贯出门），其余状态直接淡入；
        /// 门冲吞没段全链快速渐隐（门口吞没的读数）。
        /// </summary>
        private void UpdateAlphaFade(NPC head, FssStateContext ctx) {
            if (ctx != null && ctx.PortalHiding) {
                NPC.alpha = Math.Min(NPC.alpha + 40, 255);
                return;
            }
            if (NPC.alpha <= 0) {
                return;
            }
            bool ripple = (int)head.ai[3] == (int)FssStateIndex.Intro
                || (ctx != null && ctx.PortalPhase);
            if (ripple) {
                NPC front = FrontNPC;
                if (front.Alives() && front.alpha >= 128) {
                    return; //涟漪未到本节
                }
            }
            NPC.alpha = Math.Max(NPC.alpha - 42, 0);
        }
        #endregion

        #region 伤害修正
        /// <summary>体节减伤：统一血池下抑制穿透武器的多节盛宴</summary>
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.FinalDamage *= 0.6f;
        }
        #endregion

        #region 帧与绘制
        /// <summary>囊肿节判定（款式2 帧；尾节恒否）</summary>
        protected virtual bool IsCyst => FssStateContext.IsCystOrdinal(Ordinal);

        public override void FindFrame(int frameHeight) {
            int style = SerpentChainMath.BodyStyleIndex(Ordinal, IsCyst);
            NPC.frame = new Rectangle(0, style * frameHeight,
                TextureAssets.Npc[Type].Width(), frameHeight);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(18f, 18f),
                    DustID.Sand, new Vector2(hit.HitDirection * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.8f, 2.4f)),
                    110, FssVfx.TaintedSand, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
            //囊肿节受击渗金
            if (IsCyst && Main.rand.NextBool(3)) {
                FssVfx.IchorBurst(NPC.Center, 0.4f, new Vector2(hit.HitDirection, -0.5f));
            }
            if (NPC.life <= 0) {
                FssVfx.CorruptSandBurst(NPC.Center, 0.9f);
                if (IsCyst) {
                    FssVfx.IchorBurst(NPC.Center, 1f);
                }
            }
        }

        //绘制集中在头部整链层（偏移/辉光/鼓包/瘪缩全在 FssSkinFX）
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) => false;
        #endregion
    }
}
