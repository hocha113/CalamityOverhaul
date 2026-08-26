using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 蓑翁（P4 点子7，R2-A）：滩涂水线边背对而坐的蓑衣钓者，全场至多 1，会话限 2 现。
    /// 规矩是方位禁忌而非注视禁忌：从身后（东侧）绕过安全；走进他面前
    /// （正面锥 × 140px，看见正脸的方位角，纯几何）即触怒。受击同触怒，不被杀不掉落。<br/>
    /// 触怒序列：转身拍（StateEdge，一声闷吼）→ 18t 后对触怒者一次 55 伤定向拍击
    /// （拍程内退出=落空）→ 化雾退场。<br/>
    /// 联机合同：ai[0]=状态 ai[1]=计时 ai[3]=触怒者 whoAmI（与转移同包过线）；
    /// 裁决全服务器，各端由 ai 重放；拍击走服务器 Hurt + SendPlayerHurt
    /// （二审正字：发包分支仅 netMode==1，服务器直调只写本端镜像），无 debuff 故无 55 号包。<br/>
    /// 视觉：Scarecrow1 帧 0 坐姿源矩形裁剪（帽 + 蓑肩的干草剪影）+ 陈草色 GetAlpha
    /// （守田人同手法）+ 魔法像素钓竿；无 shader，他的恐怖在于像个还在钓鱼的人
    /// </summary>
    internal class MinoFisherYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Scarecrow1;

        private const int StateSit = 0;
        private const int StateTurn = 1;
        private const int StateDissolve = 2;

        //──── 纯演出常量（机制量在 KiyumeYokaiMetrics.Mino*）────

        /// <summary>坐姿裁剪：取帧 0 顶部这一比例（帽 + 头 + 蓑肩，裁掉杆腿）</summary>
        private const float SitCropFrac = 0.62f;
        /// <summary>坐姿前倾（rad，绕座底轴向水面伛偻）</summary>
        private const float HunchRad = 0.12f;
        /// <summary>钓竿长（px）/ 垂线长（px）</summary>
        private const float RodLenPx = 52f;
        private const float LineLenPx = 26f;
        /// <summary>现形语法（人形诱饵极性）：基础全显，远且雾浓才沉入雾影</summary>
        private const float FogHideNearPx = 300f;
        private const float FogHideFarPx = 800f;
        private const float FogHideMax = 0.75f;
        /// <summary>陈草色（蓑衣比守田人的干草更旧更沉）</summary>
        private static readonly Color StrawMul = new(176, 150, 102);
        /// <summary>竿身老木色 / 垂线灰</summary>
        private static readonly Color RodWood = new(58, 44, 32);
        private static readonly Color LineGray = new(140, 140, 150);
        /// <summary>化雾雾色（干草腐白）</summary>
        private static readonly Color MistTint = new(168, 158, 138);

        //──── 各端本地表现 ────

        private float presentAlpha;
        private int facing = -1;   //朝向恒向西（背对东侧来路），触怒后转向触怒者

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = 6;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        protected override void SetYokaiDefaults() {
            //坐姿判定盒：比站立稻草人矮一截
            NPC.width = 24;
            NPC.height = 30;
            NPC.damage = 0;    //无接触伤：唯一伤害是转身拍击（服务器点名结算）
            NPC.defense = 0;
            NPC.lifeMax = KiyumeYokaiMetrics.MinoLife;
            NPC.knockBackResist = 0f;   //坐定的人挪不动
            NPC.aiStyle = -1;           //静坐体：原版重力/碰撞落地
            NPC.npcSlots = 0f;
            NPC.alpha = 0;   //出生透明度显式（VFX 高复发缺陷②）
            NPC.HitSound = SoundID.Grass;
            NPC.DeathSound = null;
        }

        //接触判定永不伤人：贴着他的背站一夜也没事
        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => false;

        /// <summary>锁帧 0：他坐着，永远不走</summary>
        public override void FindFrame(int frameHeight) {
            NPC.frame.Y = 0;
        }

        public override Color? GetAlpha(Color drawColor)
            => drawColor.MultiplyRGB(StrawMul) * NPC.Opacity;

        //==================== 行为 ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateCue();
            }
            ServerSyncPacer(60);   //静坐体低频锚
            NPC.velocity.X = 0f;
            //化雾中不吃补刀（坐着与转身时可打：打他就是触怒的另一个入口）
            NPC.dontTakeDamage = (int)State == StateDissolve;

            switch ((int)State) {
                case StateTurn:
                    UpdateTurn();
                    break;
                case StateDissolve:
                    UpdateDissolve();
                    break;
                default:
                    UpdateSit();
                    break;
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        private void UpdateSit() {
            StateTimer++;
            facing = -1;   //背对东侧来路，脸朝湖
            if (VaultUtils.isClient) {
                return;
            }
            JudgeProvoke();
        }

        private void UpdateTurn() {
            StateTimer++;
            //各端从 ai[3] 重放转身朝向（与状态同包过线）
            Player provoker = ProvokerOrNull();
            if (provoker != null) {
                float dx = provoker.Center.X - NPC.Center.X;
                if (Math.Abs(dx) > 8f) {
                    facing = dx > 0f ? 1 : -1;
                }
            }
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.MinoTurnTicks) {
                SettleSlap();
                ChangeState(StateDissolve);
            }
        }

        private void UpdateDissolve() {
            StateTimer++;
            if (StateTimer >= KiyumeYokaiMetrics.MinoDissolveTicks) {
                //各端同式确定退场（雾散尽的那一帧人已不在）；服务器 SyncNPC 兜底迟到端
                NPC.active = false;
            }
        }

        //==================== 服务器裁决 ====================

        /// <summary>触怒判定：受击即怒（血线就是证词）；正面锥 × 140px 内看见正脸即怒</summary>
        private void JudgeProvoke() {
            if (NPC.life < NPC.lifeMax) {
                Enrage(NearestLiveWho());
                return;
            }
            Player best = null;
            float bestDist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                Vector2 to = player.Center - NPC.Center;
                float d = to.Length();
                if (d > KiyumeYokaiMetrics.MinoWakeRadius || d < 0.01f) {
                    continue;
                }
                //方位角判定（纯几何）：朝向恒向西，正面方向 (-1,0) 与来向的点积过阈=看见正脸；
                //正上方/正后方点积不过阈，从身后绕、从头顶跳都安全
                if (-to.X / d < KiyumeYokaiMetrics.MinoFrontDot) {
                    continue;
                }
                if (d < bestDist) {
                    bestDist = d;
                    best = player;
                }
            }
            if (best != null) {
                Enrage(best.whoAmI);
            }
        }

        private void Enrage(int who) {
            StackCount = who;   //ai[3]=触怒者，与转移同包过线
            ChangeState(StateTurn);
        }

        /// <summary>
        /// 定向拍击（服务器，只结算触怒者）：转身拍里退出拍程=落空。
        /// 服务器 Hurt 只写本端镜像（对源：发包分支仅 netMode==1 且 myPlayer）——
        /// 显式广播 HurtInfo（二审正字样板照抄）；无附加 debuff，故无 AddPlayerBuff 包
        /// </summary>
        private void SettleSlap() {
            Player victim = ProvokerOrNull();
            if (victim == null
                || victim.Distance(NPC.Center) > KiyumeYokaiMetrics.MinoSlapRange) {
                return;
            }
            int dir = victim.Center.X < NPC.Center.X ? -1 : 1;
            double dealt = victim.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI),
                KiyumeYokaiMetrics.MinoSlapDamage, dir, out Player.HurtInfo info);
            if (dealt > 0.0 && VaultUtils.isServer) {
                NetMessage.SendPlayerHurt(victim.whoAmI, info);
            }
        }

        /// <summary>不可真死：血线归零只是触怒的另一个入口，无掉落无播报</summary>
        public override bool CheckDead() {
            NPC.life = 1;
            if (!VaultUtils.isClient && (int)State == StateSit) {
                Enrage(NearestLiveWho());
            }
            return false;
        }

        private Player ProvokerOrNull() {
            int who = (int)MathHelper.Clamp(StackCount, 0f, Main.maxPlayers - 1);
            Player player = Main.player[who];
            return player?.active == true && !player.dead ? player : null;
        }

        private int NearestLiveWho() {
            int who = 0;
            float best = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d < best) {
                    best = d;
                    who = player.whoAmI;
                }
            }
            return who;
        }

        //==================== 导演泵出口（服务器；泵挂点区只留薄调用） ====================

        /// <summary>
        /// 落座：水线列窗内探地抽点，距全员足距（防落座上屏）；探样全败返回 false 由泵短臂重试
        /// </summary>
        internal static bool TrySeatSpawn() {
            for (int i = 0; i < 8; i++) {
                int col = Main.rand.Next(KiyumeYokaiMetrics.MinoSpawnColMin,
                    KiyumeYokaiMetrics.MinoSpawnColMax + 1);
                int row = KiyumePlans.ProbeGround(col, KiyumePlans.FloorTopAt(col) - 6);
                var pos = new Vector2(col * 16f + 8f, row * 16f);
                bool tooClose = false;
                foreach (Player player in Main.ActivePlayers) {
                    tooClose |= !player.dead
                        && player.Distance(pos) < KiyumeYokaiMetrics.MinoSpawnMinDist;
                }
                if (tooClose) {
                    continue;
                }
                KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<MinoFisherYokai>(), pos);
                return true;
            }
            return false;
        }

        //==================== 表现（各端本地，由 ai 重放） ====================

        /// <summary>状态变迁沿音画（各端本地重放；迟入端首帧也吃到一次沿）</summary>
        private void PlayStateCue() {
            if (Main.dedServ) {
                return;
            }
            switch ((int)State) {
                case StateTurn:
                    //转身拍：一声闷吼（很沉，像从蓑衣底下滚出来）+ 惊起的干草
                    SoundEngine.PlaySound(SoundID.DD2_OgreRoar with {
                        Volume = 0.6f, Pitch = -0.4f, MaxInstances = 2
                    }, NPC.Center);
                    EmitHay(8, 1.6f);
                    break;
                case StateDissolve:
                    //拍击挥出的一声风响（命中与落空同声），人随即散进雾里
                    SoundEngine.PlaySound(SoundID.Item1 with {
                        Volume = 0.85f, Pitch = -0.5f, MaxInstances = 2
                    }, NPC.Center);
                    EmitHay(12, 2.4f);
                    for (int i = 0; i < 10; i++) {
                        PRTLoader.NewParticle<PRT_GhostRainMist>(
                            NPC.Center + Main.rand.NextVector2Circular(12f, 14f),
                            new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(0.2f, 0.7f)),
                            MistTint * 0.7f, Main.rand.NextFloat(0.26f, 0.42f))
                            ?.Configure(Main.rand.Next(45, 75));
                    }
                    break;
            }
        }

        private void EmitHay(int count, float speed) {
            for (int i = 0; i < count; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Hay,
                    Main.rand.NextFloat(-speed, speed), -Main.rand.NextFloat(0.5f, speed));
                dust.noGravity = Main.rand.NextBool(3);
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //蓑衣受击：干草窸窣，无血
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Hay,
                    hit.HitDirection * Main.rand.NextFloat(0.5f, 1.6f), -Main.rand.NextFloat(0.3f, 1.2f));
                dust.velocity *= 0.6f;
            }
        }

        private void UpdatePresentation() {
            //现形语法（人形诱饵极性）：基础全显，远且雾浓沉入雾影；转身起不许再藏
            float fog = FogRevealTerm(NPC.Center);
            float dist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead) {
                    dist = Math.Min(dist, player.Distance(NPC.Center));
                }
            }
            float hide = fog * DistanceRevealTerm(dist, FogHideNearPx, FogHideFarPx);
            float target = 1f - FogHideMax * hide;
            if ((int)State >= StateTurn) {
                target = Math.Max(target, 0.95f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);
        }

        /// <summary>化雾进度（散尽那一帧实体已退场）</summary>
        private float Dissolve01() {
            if ((int)State == StateDissolve) {
                return MathHelper.Clamp(
                    StateTimer / (float)KiyumeYokaiMetrics.MinoDissolveTicks, 0f, 1f);
            }
            return 0f;
        }

        //==================== 绘制：坐姿裁剪 + 钓竿，无 shader ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游实体批状态泄漏自愈
            BeginDefault(spriteBatch);
            float alpha = presentAlpha * (1f - Dissolve01());
            if (alpha < 0.02f) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Scarecrow1);
            Texture2D tex = TextureAssets.Npc[NPCID.Scarecrow1]?.Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[NPCID.Scarecrow1];
            //坐姿裁剪：帧 0 顶部（帽 + 头 + 蓑肩），顶内缩 1px 防帧表渗色
            int cropH = (int)(frameH * SitCropFrac);
            var source = new Rectangle(0, 1, tex.Width, cropH - 1);

            //座底中心为轴：坐姿前倾伛偻，转身拍里直起 + 怒颤
            float hunch = facing * HunchRad;
            float shiver = 0f;
            if ((int)State == StateTurn) {
                float t = MathHelper.Clamp(
                    StateTimer / (float)KiyumeYokaiMetrics.MinoTurnTicks, 0f, 1f);
                hunch = MathHelper.Lerp(hunch, facing * 0.02f, t);
                shiver = MathF.Sin(StateTimer * 1.7f) * 1.2f;
            }
            else if ((int)State == StateSit) {
                //坐着的呼吸：极小的摆，像睡着又像没睡
                hunch += MathF.Sin(AmbientClock * 0.02f + Seed) * 0.015f;
            }
            var seat = new Vector2(NPC.Center.X + shiver, NPC.Bottom.Y + 2f) - screenPos;
            var origin = new Vector2(source.Width * 0.5f, source.Height);
            SpriteEffects flip = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color col = (GetAlpha(drawColor) ?? drawColor) * alpha;

            if ((int)State == StateSit) {
                DrawRod(spriteBatch, screenPos, drawColor, alpha);
            }
            spriteBatch.Draw(tex, seat, source, col, hunch, origin, 1f, flip, 0f);
#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"状态 {(int)State}  触怒者 {(int)StackCount}",
                NPC.Top - screenPos + new Vector2(-28f, -34f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        /// <summary>钓竿 + 垂线（魔法像素细条）：竿梢缓摆，是他还「活着在钓」的唯一动静</summary>
        private void DrawRod(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor, float alpha) {
            Texture2D px = VaultAsset.placeholder2?.Value;
            if (px == null || px.IsDisposed) {
                return;
            }
            var src = new Rectangle(0, 0, 1, 1);
            float bob = MathF.Sin(AmbientClock * 0.05f + Seed) * 0.04f;
            //朝西：竿指西上方（屏幕系 Y 向下，π+δ 即西且上）
            float rodAngle = facing > 0 ? -0.55f + bob : MathHelper.Pi + 0.55f - bob;
            Vector2 grip = NPC.Center + new Vector2(facing * 10f, -4f);
            Color rodCol = drawColor.MultiplyRGB(RodWood) * alpha;
            spriteBatch.Draw(px, grip - screenPos, src, rodCol, rodAngle,
                new Vector2(0f, 0.5f), new Vector2(RodLenPx, 2f), SpriteEffects.None, 0f);
            //垂线：竿梢向下，摆相略滞后
            Vector2 tip = grip + rodAngle.ToRotationVector2() * RodLenPx;
            float lineSway = MathHelper.PiOver2 + MathF.Sin(AmbientClock * 0.05f + Seed - 0.9f) * 0.07f;
            spriteBatch.Draw(px, tip - screenPos, src, LineGray * (alpha * 0.45f), lineSway,
                new Vector2(0f, 0.5f), new Vector2(LineLenPx, 1f), SpriteEffects.None, 0f);
        }
    }
}
