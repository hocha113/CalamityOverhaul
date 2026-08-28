using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using CalamityOverhaul.Content.Scenarios.Kiyume.Stealth;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 无面者（P4 §2.5）：村里唯一人形，零伤害纯恐惧，全场唯一，导演调度。
    /// 规矩：远看是背对而泣的素衣姑娘；贴身对视她转过脸来——脸是平的。
    /// 状态机 ai[0]：0 背身而立 → 1 察觉（动停）→ 2 尖啸 30t → 3 化雾 40t。
    /// 触发 = 贴身 ≤96px 且反向观测（她的判定框被任一玩家看见）累计 60t；
    /// 远程受击直接走同序列但无 debuff 无击退。不可真死（CheckDead 拦截），无掉落。
    /// 联机合同：ai[0]=状态 ai[1]=计时 ai[2]=变体（1=对视触发）ai[3]=触发者 whoAmI；
    /// 裁决全服务器，各端由 ai 重放；黑暗只上触发者（服务器 AddBuff 不自发包，
    /// 对源已核 Player.cs L5698 只有 netMode==1 分支——须显式补 55 号 AddPlayerBuff）；
    /// 背向击退由触发者本端在 StateEdge 重放（纯演出量）。会话现身计数在同文件 ModPlayer。
    /// 帧表事实（沙盒标定实证）：LostGirl 7 帧是纯调色渐变（她化形前的原版变身帧），
    /// 几何零位移——循环播放只会颜色脉动，故 Stand 锁 f0 以绘制层呼吸微幅代「哭动」，
    /// 7 帧渐变留给尖啸拍当「现出真身」演出（桃肤 → 尸绿红斑），面区 pass 全程压平五官
    /// </summary>
    internal class FacelessOneYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.LostGirl;

        private const int StateStand = 0;
        private const int StateAware = 1;
        private const int StateShriek = 2;
        private const int StateDissolve = 3;

        //──── 纯演出常量（机制量在 KiyumeYokaiMetrics.Faceless*，本组只读不添）────

        /// <summary>尖啸拍内变身帧步进（tick/帧）：0→6 于 24t 走满，末 6t 定格尸相</summary>
        private const int MorphFrameTicks = 4;
        /// <summary>察觉退出滞回（px）：离开 240+40 才回 Stand，贴阈值不抖动</summary>
        private const float AwareExitPadPx = 40f;
        /// <summary>背身死区（px）：玩家横跨此带内不翻面，免得原地鬼畜</summary>
        private const float FaceAwayDeadZonePx = 24f;
        /// <summary>现形语法（她的极性与犬影相反：人形诱饵必须远处可见）——
        /// 基础全显，仅「远且雾浓」才沉入雾影，遮蔽上限 0.75</summary>
        private const float FogHideNearPx = 300f;
        private const float FogHideFarPx = 800f;
        private const float FogHideMax = 0.75f;
        /// <summary>素衣冷灰（§2.5：GetAlpha 乘此色 + 血暮光压暗兜底首见误认）</summary>
        private static readonly Color ColdGray = new(185, 175, 190);
        /// <summary>蛋壳肤基调（P4-B 沙盒预验值）/ 轮廓缘光（冷紫灰，不入血暮红族）</summary>
        private static readonly Vector3 SkinTint = new(0.93f, 0.80f, 0.72f);
        private static readonly Vector3 EdgeTint = new(0.35f, 0.30f, 0.40f);

        /// <summary>
        /// 7 帧 uFaceRect 常量表（内缩帧局部 uv：source=(0,frameTop+1,20,44)，未翻转原生朝向）。
        /// 逐帧标定结论（2026-08-27 沙盒 kaidan_faceless_f0..f6 + f0flip + f6d55 全 PASS）：
        /// 7 帧逐像素 alpha diff = 0（纯调色渐变、几何零位移），故 7 锚同值；
        /// 保留逐帧表结构，换纹理或改内缩量时按帧重标
        /// </summary>
        private static readonly Vector4[] FaceRects = [
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f0 素面
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f1
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f2
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f3
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f4
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f5
            new(0.120f, 0.207f, 0.620f, 0.209f),   //f6 尸相
        ];

        //──── 服务器侧字段（裁决量不入同步）────

        private int gazeTicks;

        //──── 各端本地表现 ────

        private float presentAlpha;
        private float bobLevel;
        private int facing = -1;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.LostGirl];
        }

        protected override void SetYokaiDefaults() {
            NPC.width = 18;
            NPC.height = 40;
            NPC.damage = 0;          //零伤害：她只吓人
            NPC.defense = 0;
            NPC.lifeMax = KiyumeYokaiMetrics.FacelessLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;        //站桩体：原版重力/碰撞落地，不走探针
            NPC.HitSound = SoundID.NPCHit36 with { Volume = 0.5f, Pitch = -0.2f };
            NPC.DeathSound = null;
        }

        //==================== AI ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateCue();
            }
            ServerSyncPacer(60);   //站桩低频锚
            //现身序列免打扰：各端从 ai[0] 同式推导，不入同步包
            NPC.dontTakeDamage = (int)State >= StateShriek;

            switch ((int)State) {
                case StateStand:
                case StateAware:
                    UpdateIdlePhase();
                    break;
                case StateShriek:
                    UpdateShriek();
                    break;
                default:
                    UpdateDissolve();
                    break;
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        private void UpdateIdlePhase() {
            StateTimer++;
            NPC.velocity.X = 0f;
            Player nearest = NearestLivePlayer(out float dist);

            //背身：脸永远朝远离侧（带死区防原地鬼畜）；两端同式（位置原版同步）
            if (nearest != null) {
                float dx = nearest.Center.X - NPC.Center.X;
                if (Math.Abs(dx) > FaceAwayDeadZonePx) {
                    facing = dx > 0f ? -1 : 1;
                }
            }

            if (VaultUtils.isClient) {
                return;
            }
            //察觉档转移：≤240px 动停，滞回退出
            if ((int)State == StateStand && dist <= KiyumeYokaiMetrics.FacelessAwareRange) {
                ChangeState(StateAware);
            }
            else if ((int)State == StateAware
                && dist > KiyumeYokaiMetrics.FacelessAwareRange + AwareExitPadPx) {
                ChangeState(StateStand);
            }
            JudgeReveal(nearest, dist);
        }

        private void UpdateShriek() {
            StateTimer++;
            NPC.velocity.X = 0f;
            if (!VaultUtils.isClient && StateTimer >= KiyumeYokaiMetrics.FacelessShriekBeat) {
                ChangeState(StateDissolve);
            }
        }

        private void UpdateDissolve() {
            StateTimer++;
            NPC.velocity.X = 0f;
            if (StateTimer >= KiyumeYokaiMetrics.FacelessDissolveTicks) {
                //uDissolve 走满同帧退场，残斑不上屏（KiyumeKaidan.fx 约定）；服务器 SyncNPC 兜底迟到端
                NPC.active = false;
            }
        }

        //==================== 服务器裁决 ====================

        /// <summary>触发判定：受击即现（无 debuff 变体）；贴身对视累计 60t 即现（完整变体）</summary>
        private void JudgeReveal(Player nearest, float dist) {
            int who = nearest?.whoAmI ?? 0;
            //血线就是证词（镜像提灯翁）：远程狙她只换来一次回头，无演出收益
            if (NPC.life < NPC.lifeMax) {
                TriggerReveal(who, byGaze: false);
                return;
            }
            //对视 = 贴身 ≤96px 且她的判定框被任一玩家看见（反向观测通道，裁决10；
            //雾盲阈与守田人共用同一物理口径 ScareFogBlind）
            bool gazed = nearest != null && dist <= KiyumeYokaiMetrics.FacelessTriggerRange
                && KiyumeStealthSense.ObservedByAnyPlayer(NPC.Hitbox, KiyumeYokaiMetrics.ScareFogBlind);
            if (gazed) {
                if (++gazeTicks >= KiyumeYokaiMetrics.FacelessGazeTicks) {
                    TriggerReveal(who, byGaze: true);
                }
            }
            else if (gazeTicks > 0) {
                //断视缓降：退开再凑不清零，但也不白攒
                gazeTicks = Math.Max(0, gazeTicks - 2);
            }
        }

        private void TriggerReveal(int who, bool byGaze) {
            StackCount = who;   //ai[3]=触发者，与状态同包过线（StateEdge 重放读它）
            ChangeState(StateShriek, byGaze ? 1f : 0f);
            //尖啸撕破村巷，挂上听觉地图（裁决11 天然噪声源，量级对齐开火脉冲同阶）
            KiyumeStealthSense.ReportNoise(NPC.Center, KiyumeHoundMetrics.WeaponImpulse);
            if (byGaze && who >= 0 && who < Main.maxPlayers) {
                //黑暗只上触发者：服务器镜像先记，再显式广播 55 号包
                //（对源已核：AddBuff 只在 netMode==1 自发包，服务器直调只写镜像）
                Main.player[who].AddBuff(BuffID.Darkness, KiyumeYokaiMetrics.FacelessDarkTicks);
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null, who,
                        BuffID.Darkness, KiyumeYokaiMetrics.FacelessDarkTicks);
                }
            }
            MarkWitnesses();
        }

        /// <summary>会话现身计数：观测窗内的活人都算「见过她」（per-player，服务器侧 ModPlayer）</summary>
        private void MarkWitnesses() {
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                if (Math.Abs(player.Center.X - NPC.Center.X) <= KiyumeHoundMetrics.ObserveHalfWidthPx
                    && Math.Abs(player.Center.Y - NPC.Center.Y) <= KiyumeHoundMetrics.ObserveHalfHeightPx) {
                    player.GetModPlayer<FacelessMemoryPlayer>().MarkSeen();
                }
            }
        }

        /// <summary>不可真死：血线归零只是受击触发的另一个入口，无掉落无播报</summary>
        public override bool CheckDead() {
            NPC.life = 1;
            if (!VaultUtils.isClient && (int)State < StateShriek) {
                Player nearest = NearestLivePlayer(out _);
                TriggerReveal(nearest?.whoAmI ?? 0, byGaze: false);
            }
            return false;
        }

        //==================== 导演泵出口（服务器；泵挂点区只留薄调用） ====================

        /// <summary>会话闸：任一活人还没看满三次她才肯再来（恐惧要留白，第 4 次起不再出现）</summary>
        internal static bool AnyPlayerBelowSessionCap() {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.GetModPlayer<FacelessMemoryPlayer>().SeenThisSession
                    < KiyumeYokaiMetrics.FacelessSessionCap) {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 重现落位：门洞锚优先（KiyumeStructures.DoorwayPoints，服务器列表合法），
        /// 取距锚玩家最近的合法点（她要能被遇见）；W2 村落纵深注册前回退村落带随机平地点。
        /// 合法 = 距全员 ≥600px 且出现拍不被任何人看见（反向观测口径，雾盲同阈）
        /// </summary>
        internal static bool TryHauntSpawn() {
            Player anchor = KiyumeHauntDirector.AnyLivePlayer();
            if (anchor == null) {
                return false;
            }
            Vector2 best = default;
            float bestDist = float.MaxValue;
            bool found = false;
            foreach (Point door in KiyumeStructures.DoorwayPoints) {
                int row = KiyumePlans.ProbeGround(door.X, door.Y);
                Vector2 pos = new(door.X * 16f + 8f, row * 16f);
                float d = anchor.Distance(pos);
                if (d < bestDist && SpawnSpotOk(pos)) {
                    bestDist = d;
                    best = pos;
                    found = true;
                }
            }
            for (int i = 0; i < 6 && !found; i++) {
                int col = Main.rand.Next(KiyumeMetrics.VillageLeft + 8, KiyumeMetrics.GroveLeft - 8);
                int row = KiyumePlans.ProbeGround(col, KiyumePlans.FloorTopAt(col) - 6);
                Vector2 pos = new(col * 16f + 8f, row * 16f);
                if (SpawnSpotOk(pos)) {
                    best = pos;
                    found = true;
                }
            }
            if (!found) {
                return false;
            }
            KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<FacelessOneYokai>(), best);
            return true;
        }

        private static bool SpawnSpotOk(Vector2 pos) {
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && player.Distance(pos) < KiyumeYokaiMetrics.FacelessSpawnMinDist) {
                    return false;
                }
            }
            //出现拍不上屏：站位框被人看见（且雾未盲）就换点
            var rect = new Rectangle((int)pos.X - 9, (int)pos.Y - 40, 18, 40);
            return !KiyumeStealthSense.ObservedByAnyPlayer(rect, KiyumeYokaiMetrics.ScareFogBlind);
        }

        //==================== 表现（各端本地，由 ai 重放） ====================

        /// <summary>状态变迁沿音画；触发者本端在此重放背向击退（纯演出量，误差可容）</summary>
        private void PlayStateCue() {
            switch ((int)State) {
                case StateShriek:
                    //翻面拍：转向触发者（ai[3] 与状态同包过线，各端同帧重放）
                    int who = (int)MathHelper.Clamp(StackCount, 0f, Main.maxPlayers - 1);
                    if (Main.player[who]?.active == true) {
                        facing = Main.player[who].Center.X > NPC.Center.X ? 1 : -1;
                    }
                    //尖啸：NPCDeath6 +0.8 试听基准（§2.5）
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                        Volume = 0.75f,
                        Pitch = 0.8f,
                        MaxInstances = 2
                    }, NPC.Center);
                    EmitRevealBurst();
                    if (!Main.dedServ && (int)StateParam == 1
                        && (int)StackCount == Main.myPlayer) {
                        Player me = Main.LocalPlayer;
                        int dir = me.Center.X < NPC.Center.X ? -1 : 1;
                        me.velocity.X = dir * KiyumeYokaiMetrics.FacelessKnockback;
                        //小抬升让横推真吃到（贴地摩擦两帧就吞干横速）
                        me.velocity.Y = Math.Min(me.velocity.Y, -2.2f);
                    }
                    break;
                case StateDissolve:
                    //散尽的一声低息（尖啸的余韵）
                    SoundEngine.PlaySound(SoundID.NPCDeath6 with {
                        Volume = 0.35f,
                        Pitch = -0.35f,
                        MaxInstances = 2
                    }, NPC.Center);
                    EmitDissolveMist();
                    break;
            }
        }

        /// <summary>Reveal 拍环爆：PRT_GhostRainMist ×12 圆环外扩（§2.5）</summary>
        private void EmitRevealBurst() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 ray = (MathHelper.TwoPi * i / 12f).ToRotationVector2();
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + ray * 6f, ray * Main.rand.NextFloat(1.6f, 2.4f),
                    new Color(205, 195, 210), Main.rand.NextFloat(0.26f, 0.4f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
        }

        private void EmitDissolveMist() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(10f, 18f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), Main.rand.NextFloat(-0.5f, -0.1f)),
                    new Color(190, 182, 198), Main.rand.NextFloat(0.22f, 0.36f))
                    ?.Configure(Main.rand.Next(45, 75));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //灵体受击：几缕冷灰，无血
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Smoke, hit.HitDirection * 1.0f, -0.5f, 130, new Color(188, 180, 196), 0.85f);
                dust.noGravity = true;
            }
        }

        private void UpdatePresentation() {
            //现形语法（她的极性）：基础全显（人形诱饵要远处可见），远且雾浓才沉入雾影
            float fog = FogRevealTerm(NPC.Center);
            NearestLivePlayer(out float dist);
            float hide = fog * DistanceRevealTerm(dist, FogHideNearPx, FogHideFarPx);
            float target = 1f - FogHideMax * hide;
            if ((int)State >= StateShriek) {
                target = Math.Max(target, 0.95f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);

            //哭动感：Stand 呼吸微幅，Aware 起收干（「肩膀停住」的可读拍）
            bobLevel = MathHelper.Lerp(bobLevel, (int)State == StateStand ? 1f : 0f, 0.1f);
        }

        /// <summary>变身帧序：尖啸拍 0→6 播原版化形渐变（桃肤 → 尸绿红斑），化雾定格尸相</summary>
        private int MorphFrameIndex() {
            if ((int)State == StateShriek) {
                return Math.Min(6, (int)StateTimer / MorphFrameTicks);
            }
            return (int)State == StateDissolve ? 6 : 0;
        }

        /// <summary>化雾进度：走满的那一帧实体已退场（残斑同帧退场兜底）</summary>
        private float Dissolve01() {
            if ((int)State == StateDissolve) {
                return MathHelper.Clamp(StateTimer / (float)KiyumeYokaiMetrics.FacelessDissolveTicks, 0f, 1f);
            }
            return 0f;
        }

        /// <summary>素衣色相（§2.5）：冷灰乘光照色；血暮压暗由场景光自带</summary>
        public override Color? GetAlpha(Color drawColor) => drawColor.MultiplyRGB(ColdGray);

        public override void FindFrame(int frameHeight) {
            //帧由状态确定（Stand/Aware 锁 f0——原版 7 帧是调色渐变不是姿势 idle，循环会色彩脉动）
            NPC.frame.Y = frameHeight * MorphFrameIndex();
        }

        //==================== 绘制（全接管） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            //上游批状态自愈：先归位默认批（镜像提灯翁）
            BeginDefault(spriteBatch);
            if (presentAlpha >= 0.02f) {
                if ((int)State >= StateShriek) {
                    DrawRevealBody(spriteBatch, screenPos, drawColor);
                }
                else {
                    DrawStandingBody(spriteBatch, screenPos, drawColor);
                }
            }
#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"状态 {(int)State}  对视 {gazeTicks}",
                NPC.Top - screenPos + new Vector2(-28f, -34f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
        }

        //帧源矩形：上下各内缩 1px 防帧表渗色（FaceRects 与此内缩坐标系绑定，改一处必改两处）
        private static Rectangle InsetSource(Texture2D tex, int frameIdx) {
            int frameH = tex.Height / Main.npcFrameCount[NPCID.LostGirl];
            return new Rectangle(0, frameIdx * frameH + 1, tex.Width, frameH - 2);
        }

        private Vector2 BodyTopLeft(Rectangle source) {
            return new Vector2(NPC.Center.X - source.Width * 0.5f,
                NPC.Bottom.Y + 2f - source.Height);
        }

        /// <summary>背身相：原帧 + 冷灰乘光照（她看上去必须像个普通人）+ 呼吸微幅</summary>
        private void DrawStandingBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.LostGirl);
            Texture2D tex = TextureAssets.Npc[NPCID.LostGirl].Value;
            if (tex == null) {
                return;
            }
            Rectangle source = InsetSource(tex, 0);
            //呼吸微幅：双正弦错拍 ≤1.3px，像压着哭；Aware 起 bobLevel 收干 = 「肩膀停住」
            float bob = bobLevel * (MathF.Sin(AmbientClock * 0.052f + Seed) * 0.9f
                + MathF.Sin(AmbientClock * 0.021f + Seed * 2f) * 0.45f);
            Vector2 drawPos = BodyTopLeft(source) + new Vector2(0f, bob) - screenPos;
            SpriteEffects flip = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color col = (GetAlpha(drawColor) ?? drawColor) * presentAlpha;
            spriteBatch.Draw(tex, drawPos, source, col, 0f, Vector2.Zero, 1f, flip, 0f);
        }

        /// <summary>现身相：TechFacelessSkin 压平面区 + 变身帧渐变 + 化雾蚀散；着色器缺编回退素面平涂</summary>
        private void DrawRevealBody(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Main.instance.LoadNPC(NPCID.LostGirl);
            Texture2D tex = TextureAssets.Npc[NPCID.LostGirl].Value;
            if (tex == null) {
                return;
            }
            int frameIdx = MorphFrameIndex();
            Rectangle source = InsetSource(tex, frameIdx);
            Vector2 drawPos = BodyTopLeft(source) - screenPos;
            //镜头时刻小幅自提亮（演出量）：深夜里那张平脸必须读得出来
            Color lit = Color.Lerp((GetAlpha(drawColor) ?? drawColor), Color.White, 0.3f);

            Effect fx = EffectLoader.KiyumeKaidan?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (fx == null || noise == null) {
                //着色器缺编：素面平涂随化雾淡出（HoundShade 回退同款语义，面不平但序列完整）
                SpriteEffects flip = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, drawPos, source,
                    lit * (presentAlpha * (1f - Dissolve01())),
                    0f, Vector2.Zero, 1f, flip, 0f);
                return;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend,
                SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone,
                null, Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            fx.Parameters["uSeed"]?.SetValue(Seed);
            fx.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            fx.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            fx.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            fx.Parameters["uFlipH"]?.SetValue(facing > 0 ? 1f : 0f);
            fx.Parameters["uFlipV"]?.SetValue(0f);
            fx.Parameters["uEyeGlow"]?.SetValue(0f);   //她没有眼睛，这正是重点
            fx.Parameters["uEyeAnchor"]?.SetValue(new Vector2(0.44f, 0.30f));
            fx.Parameters["uDissolve"]?.SetValue(Dissolve01());
            fx.Parameters["uEdgeTint"]?.SetValue(EdgeTint);
            fx.Parameters["uPaperTint"]?.SetValue(SkinTint);   //面妖 pass 借作肤色基调
            fx.Parameters["uFaceRect"]?.SetValue(FaceRects[frameIdx]);
            fx.CurrentTechnique = fx.Techniques["TechFacelessSkin"];
            fx.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, drawPos, source,
                lit * MathHelper.Clamp(presentAlpha * 1.15f, 0f, 1f),
                0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;
        }

        //==================== 工具 ====================

        private Player NearestLivePlayer(out float dist) {
            Player best = null;
            dist = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d < dist) {
                    dist = d;
                    best = player;
                }
            }
            return best;
        }
    }

    /// <summary>
    /// 会话戳（每次世界加载 +1）：ModPlayer 计数以戳懒复位，跨梦次不残留
    /// （ShouldSave=false 语义：每次入梦全新）。实例字段经 GetInstance 读，零 static 状态
    /// </summary>
    internal class FacelessSessionSystem : ModSystem
    {
        public override bool IsLoadingEnabled(Mod mod) => KiyumeYokaiGate.Enabled;

        private int stamp;

        internal static int Stamp => ModContent.GetInstance<FacelessSessionSystem>()?.stamp ?? 0;

        public override void OnWorldLoad() => stamp++;
    }

    /// <summary>
    /// per-player 现身计数（§2.5 会话上限 3；挂 ModPlayer 禁 static，P2 的 KiyumeStealthPlayer 不碰）。
    /// 只有服务器消费（导演泵闸门 + 见证计数都在服务器侧实例上），客户端不读——零同步面，
    /// 故不需要 SyncPlayer 面；玩家重连丢会话计数可容（梦境会话本就即弃）
    /// </summary>
    internal class FacelessMemoryPlayer : ModPlayer
    {
        public override bool IsLoadingEnabled(Mod mod) => KiyumeYokaiGate.Enabled;

        private int seenStamp;
        private int seenCount;

        /// <summary>本会话已见次数（戳不匹配 = 新会话，视作 0）</summary>
        internal int SeenThisSession => seenStamp == FacelessSessionSystem.Stamp ? seenCount : 0;

        internal void MarkSeen() {
            int stamp = FacelessSessionSystem.Stamp;
            if (seenStamp != stamp) {
                seenStamp = stamp;
                seenCount = 0;
            }
            seenCount++;
        }
    }
}
