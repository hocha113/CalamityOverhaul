using CalamityOverhaul.Content.NPCs.BloomsandSerpents.Core;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.NPCs.BloomsandSerpents
{
    /// <summary>
    /// 体节：跟链 + 统一血池 + 抖动/脉冲表现。
    /// 款式按链序确定（每 FlowerStep 节开一朵红花，红花节是钉刺/花瓣发射器），各端一致。
    /// </summary>
    internal class BssBody : BssModNPC
    {
        public override string Texture => CWRConstant.NPC + "BSS/Body";

        /// <summary>链序（生成时写入 npc.ai[0]）</summary>
        protected int Ordinal => (int)NPC.ai[0];
        /// <summary>前邻（生成时写入 npc.ai[1]）</summary>
        protected NPC FrontNPC => Main.npc[(int)NPC.ai[1]];
        /// <summary>头索引（生成时写入 npc.ai[3]）</summary>
        protected int HeadIndex => (int)NPC.ai[3];

        /// <summary>死亡演出冻结姿态</summary>
        private bool deathFreezeCaptured;
        private Vector2 deathFrozenOffset;
        private float deathFrozenRotation;
        /// <summary>死亡溃爆已触发标记（本地防重播）</summary>
        private bool ruptureFired;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 2;
            NPCID.Sets.NPCBestiaryDrawModifiers hide = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, hide);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
        }

        public override void SetDefaults() {
            NPC.width = 38;
            NPC.height = 38;
            NPC.damage = BssDirector.BodyContact;
            NPC.defense = BssDirector.BodyDefense;
            NPC.lifeMax = BssDirector.BodyLife;
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
            int headType = ModContent.NPCType<BssHead>();
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
            => head != null && (int)head.ai[3] == (int)BssStateIndex.Death;
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
            UpdateAlphaFade(head);

            BssStateContext ctx = (head.ModNPC as BssHead)?.Context;

            //死亡演出：冻结姿态保活 + 本地溃爆表现
            if (HeadInDeathPerformance(head)) {
                HandleDeathPerformanceSegment(head);
                UpdateDeathRuptureFX(ctx);
                return;
            }
            deathFreezeCaptured = false;
            ruptureFired = false;

            //前邻失效（非演出期）：链条已断，跟随溃散
            NPC front = FrontNPC;
            bool frontValid = front.Alives()
                && (front.type == ModContent.NPCType<BssHead>() || front.type == ModContent.NPCType<BssBody>());
            if (!frontValid) {
                SelfDestruct();
                return;
            }

            float compression = ctx != null && ctx.Compression > 0.01f ? ctx.Compression : 1f;
            FollowChain(front, compression);

            //高速沙沫（客户端表现）
            if (!VaultUtils.isServer) {
                float segSpeed = (NPC.position - NPC.oldPosition).Length();
                if (segSpeed > 18f && Main.rand.NextBool(12)) {
                    BssVfx.SandTrickle(NPC.Center, 1.2f);
                }
            }

            BssHead.ForcedNetUpdating(NPC);
        }

        /// <summary>无头/断链溃散</summary>
        private void SelfDestruct() {
            NPC.life = 0;
            NPC.HitEffect();
            NPC.checkDead();
            NPC.active = false;
            NPC.netUpdate = true;
        }

        /// <summary>标准跟链；压缩系数由头声明（盘拢呼吸）</summary>
        private void FollowChain(NPC front, float compression) {
            float gap = BssDirector.SegmentGap * NPC.scale * compression;

            Vector2 toFront = front.Center - NPC.Center;

            //前邻转角带动（链条弯曲传递）
            if (front.rotation != NPC.rotation) {
                float angleDelta = MathHelper.WrapAngle(front.rotation - NPC.rotation);
                toFront = toFront.RotatedBy(angleDelta * 0.12f);
            }

            NPC.velocity = Vector2.Zero;
            NPC.rotation = toFront.ToRotation() + BssHead.FacingRot;
            NPC.Center = front.Center - toFront.SafeNormalize(Vector2.Zero) * gap;

            //接触伤害按位移速度门控（伤害窗=可见冲势）
            float moved = (NPC.position - NPC.oldPosition).Length();
            const float minContact = 7f;
            if (moved <= minContact) {
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

            BssHead.ForcedNetUpdating(NPC);
        }

        /// <summary>死亡溃爆波扫过本节的一次性演出（各端本地，波前尾→头）</summary>
        private void UpdateDeathRuptureFX(BssStateContext ctx) {
            if (VaultUtils.isServer || ctx == null || ctx.TotalSegments <= 0 || ctx.PulseKind != 3) {
                return;
            }
            float fraction = Ordinal / (float)ctx.TotalSegments;
            if (!ruptureFired && ctx.PulsePhase <= fraction) {
                ruptureFired = true;
                BssVfx.SandBurst(NPC.Center, 1.1f);
                for (int i = 0; i < (IsFlower ? 6 : 2); i++) {
                    BssVfx.PetalDrift(NPC.Center + Main.rand.NextVector2Circular(14f, 14f),
                        Main.rand.NextVector2Circular(2.2f, 1.6f));
                }
                BssVfx.Shake(NPC.Center, 3f, 900f);
            }
        }

        /// <summary>出生 alpha=255：入场按前邻涟漪淡入（破土渐显），其余状态直接淡入</summary>
        private void UpdateAlphaFade(NPC head) {
            if (NPC.alpha <= 0) {
                return;
            }
            if ((int)head.ai[3] == (int)BssStateIndex.Intro) {
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
        /// <summary>红花节判定（款式2；尾节恒否）</summary>
        protected virtual bool IsFlower => BssStateContext.IsFlowerOrdinal(Ordinal);

        public override void FindFrame(int frameHeight) {
            NPC.frame = new Rectangle(0, IsFlower ? frameHeight : 0,
                TextureAssets.Npc[Type].Width(), frameHeight);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(16f, 16f),
                    DustID.Sand, new Vector2(hit.HitDirection * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(0.8f, 2.4f)),
                    110, default, Main.rand.NextFloat(0.8f, 1.2f));
                d.noGravity = false;
            }
            //红花节受击偶落花瓣
            if (IsFlower && Main.rand.NextBool(3)) {
                BssVfx.PetalDrift(NPC.Center + Main.rand.NextVector2Circular(10f, 10f),
                    new Vector2(hit.HitDirection * 1f, -0.8f));
            }
            if (NPC.life <= 0) {
                BssVfx.SandBurst(NPC.Center, 0.9f);
                if (IsFlower) {
                    for (int i = 0; i < 4; i++) {
                        BssVfx.PetalDrift(NPC.Center, Main.rand.NextVector2Circular(2f, 1.5f));
                    }
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            NPC head = ResolveHead();
            BssStateContext ctx = (head?.ModNPC as BssHead)?.Context;

            Main.instance.LoadNPC(Type);
            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = NPC.frame;
            if (frame.Width <= 0 || frame.Height <= 0) {
                frame = new Rectangle(0, 0, texture.Width, texture.Height / Main.npcFrameCount[Type]);
            }
            Vector2 origin = frame.Size() / 2f;
            float fade = 1f - NPC.alpha / 255f;

            //全身抖动：足端踩定、身体在腿上晃（位置不动，纯绘制偏移）
            Vector2 shakeOffset = Vector2.Zero;
            float perpAng = NPC.rotation; //体轴垂线（rotation = 链向 - PiOver2，本身即垂线向）
            if (ctx != null && ctx.ShakeStrength > 0.02f) {
                shakeOffset = perpAng.ToRotationVector2()
                    * MathF.Sin(Main.GlobalTimeWrappedHourly * 46f + Ordinal * 0.9f)
                    * (5f * ctx.ShakeStrength);
            }
            //鞭链行波：一记冲量沿链序延迟传播、指数衰减（冲刺/破土/急转的余韵）
            if (ctx != null && ctx.WhipStrength > 0.1f) {
                float local = ctx.WhipAge - Ordinal * 2.1f;
                if (local > 0f && local < 34f) {
                    float wave = MathF.Sin(local * 0.42f) * MathF.Exp(-local * 0.085f);
                    shakeOffset += perpAng.ToRotationVector2() * wave * ctx.WhipStrength;
                }
            }
            //落足下沉：前段体节跟头一起被腿撑住下沉回弹
            if (ctx != null && ctx.StepBob > 0.02f && Ordinal < 11) {
                shakeOffset.Y += ctx.StepBob * 3f * (1f - Ordinal / 11f);
            }
            Vector2 drawPos = NPC.Center + shakeOffset - screenPos;

            //高速残影：冲刺期体节也甩影（速度门控，读出整条虫在飞）
            float moved = (NPC.position - NPC.oldPosition).Length();
            float ghostIntensity = MathHelper.Clamp((moved - 15f) / 22f, 0f, 1f);
            if (ghostIntensity > 0.05f) {
                Vector2 back = NPC.Center - (NPC.position - NPC.oldPosition) * 1.5f - screenPos;
                spriteBatch.Draw(texture, back, frame,
                    BssVfx.SandWarm with { A = 0 } * (0.22f * ghostIntensity * fade),
                    NPC.rotation, origin, NPC.scale * 0.95f, SpriteEffects.None, 0f);
                Vector2 back2 = NPC.Center - (NPC.position - NPC.oldPosition) * 2.8f - screenPos;
                spriteBatch.Draw(texture, back2, frame,
                    BssVfx.SandWarm with { A = 0 } * (0.12f * ghostIntensity * fade),
                    NPC.rotation, origin, NPC.scale * 0.9f, SpriteEffects.None, 0f);
            }

            //死亡溃爆：波前扫过后的体节熄灭发暗
            Color bodyColor = drawColor;
            bool ruptured = false;
            if (ctx != null && ctx.PulseKind == 3 && ctx.TotalSegments > 0) {
                float fraction = Ordinal / (float)ctx.TotalSegments;
                ruptured = ctx.PulsePhase <= fraction;
                if (ruptured) {
                    bodyColor = Color.Lerp(drawColor, BssVfx.SandDark, 0.66f);
                }
            }

            spriteBatch.Draw(texture, drawPos, frame, bodyColor * fade, NPC.rotation,
                origin, NPC.scale, SpriteEffects.None, 0f);

            //脉冲辉光：预告/发射波扫过红花节时泛红
            if (ctx != null && !ruptured && IsFlower && ctx.TotalSegments > 0) {
                float glow = 0f;
                float fraction = Ordinal / (float)ctx.TotalSegments;
                if (ctx.PulseKind is 1 or 2) {
                    float dist = Math.Abs(fraction - ctx.PulsePhase);
                    glow = MathHelper.Clamp(1f - dist / 0.16f, 0f, 1f);
                }
                else if (ctx.PulseKind == 4) {
                    glow = 0.65f + 0.35f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f + Ordinal * 0.8f);
                }
                glow = Math.Max(glow, ctx.BloomGlow * 0.6f);
                if (glow > 0.03f) {
                    Color bloom = BssVfx.BloomRed with { A = 0 } * (0.6f * glow * fade);
                    spriteBatch.Draw(texture, drawPos, frame, bloom, NPC.rotation,
                        origin, NPC.scale * 1.05f, SpriteEffects.None, 0f);
                    Lighting.AddLight(NPC.Center, BssVfx.BloomRed.ToVector3() * 0.3f * glow);
                }
            }

            return false;
        }
        #endregion
    }
}
