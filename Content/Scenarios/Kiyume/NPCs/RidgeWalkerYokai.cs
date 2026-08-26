using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.Scenarios.Kiyume.Fog;
using CalamityOverhaul.Content.Scenarios.Kiyume.Gen;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.Kiyume.NPCs
{
    /// <summary>
    /// 雾脊行者（P4 §1 点子11）：涨潮只剩屋顶可走时，屋脊线上与你同路的东西。全场唯一，导演调度。<br/>
    /// 规矩：保持距离同行无事——读你的速度镜像同向同速，距离带 [260,520]px，你逼近它加速拉开，
    /// 你停它也停；抢它的屋脊（同脊 &lt;160px 持续 30t）或对它动手 → 一拍冷视（帧停+眼光亮）→
    /// 把触怒者坠雾（象征伤 12 + 黑暗 180t + 横推离脊）→ 自身化雾退场。不可击杀，无掉落。<br/>
    /// 退场：潮位跌破 0.6 或村落带内再无玩家 → 走到屏外化雾。<br/>
    /// 联机合同：ai[0]=状态 ai[1]=计时 ai[2]=状态参数（冷视=触怒者 whoAmI；化雾=触怒者+1，0=平退）
    /// ai[3]=同行锚玩家 whoAmI；裁决全服务器，各端由 ai 重放行走（玩家位置原版同步，探针 tile 同步，
    /// 两端推导确定一致，ServerSyncPacer 低频重锚）。坠雾发包正字镜像提灯翁冷握：
    /// Hurt→SendPlayerHurt、AddBuff→55 号 AddPlayerBuff 双包显式广播；横推由触怒者本端在状态沿重放。<br/>
    /// 视觉：原版 Wraith 4 帧（对源 Main.cs 帧表 index82=4）过 KikasaHound.fx uMode=1 暗影链，
    /// uEdgeTint 冷灰 (60,50,70) 与犬影/井手的血边分族；贴脊靠探针（认实心与平台踏台），
    /// 探空或脊面没入雾中即保持高度缓浮（灵体，宁飘不坠不瞬移，镜像 HoundShade）
    /// </summary>
    internal class RidgeWalkerYokai : KiyumeYokaiNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Wraith;

        private const int StateWalk = 0;
        private const int StateStare = 1;
        private const int StateLeave = 2;
        private const int StateDissolve = 3;

        //──── 纯演出/探针常量（调音数值在 KiyumeYokaiMetrics.Ridge*，此处不放机制量）────

        /// <summary>绘制缩放：屋脊剪影要在 300~500px 外读得出来</summary>
        private const float BodyScale = 1.1f;
        /// <summary>眼锚：Wraith 帧内原生 uv（面向左）；眼光只在冷视拍亮，偏差待实机校</summary>
        private static readonly Vector2 EyeAnchor = new(0.42f, 0.26f);
        /// <summary>轮廓缘光：冷灰（任务书冻结值，与犬的血边分族）</summary>
        private static readonly Vector3 EdgeTint = new Color(60, 50, 70).ToVector3();
        /// <summary>化雾怨雾色（冷灰系，同族于缘光）</summary>
        private static readonly Color MistTint = new(150, 145, 162);
        /// <summary>生成探顶窗（行）：自雾面向上开窗探屋脊</summary>
        private const int SpawnProbeUpRows = 26;
        /// <summary>贴脊探针窗（行）：自脚上 3 行向下扫这么多行</summary>
        private const int RidgeProbeRows = 12;
        /// <summary>脊面没入雾中的容差（px）：探到的面深过雾面此值即视作探空</summary>
        private const float RidgeSinkTolerancePx = 24f;

        /// <summary>坠雾死亡播报（{0}=玩家名；ToNetworkText 各端按自己语言解）</summary>
        private static LocalizedText ridgeDeathReason;

        //──── 服务器侧字段（裁决量不入同步）────

        private int stealTicks;
        private int stealWho = -1;

        //──── 各端本地表现 ────

        private float presentAlpha;
        private int facing = -1;
        private float frameClock;

        protected override void SetYokaiStaticDefaults() {
            Main.npcFrameCount[Type] = Main.npcFrameCount[NPCID.Wraith];
            ridgeDeathReason = this.GetLocalization("RidgeDeathReason", () => "{0}抢了不该抢的屋脊");
        }

        protected override void SetYokaiDefaults() {
            //原版 Wraith 同框（对源 NPC.cs type82：24×44）
            NPC.width = 24;
            NPC.height = 44;
            NPC.damage = 0;      //同行者无接触伤，惩罚只走坠雾结算
            NPC.defense = 0;
            NPC.lifeMax = KiyumeYokaiMetrics.RidgeLife;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;   //贴脊靠探针，坡脊宁飘不卡
            NPC.HitSound = SoundID.NPCHit54 with { Volume = 0.4f, Pitch = -0.4f };
            NPC.DeathSound = null;      //不可真死，无死亡声
        }

        //==================== AI ====================

        protected override void YokaiAI() {
            HealAlpha(0);
            AmbientClock++;
            if (StateEdge()) {
                PlayStateCue();
            }
            ServerSyncPacer();
            //惩罚序列免打扰：冷视/化雾不换目标（镜像无面者）；退场期仍可打，动手即认账（下方受击检查的 Leave 臂）
            NPC.dontTakeDamage = (int)State is StateStare or StateDissolve;

            //受击即触怒：血线就是证词（镜像无面者），同行与退场期都认账；
            //触怒者=最后动手的人（case 28 服务器已记 lastInteraction，对源 MessageBuffer L1817）
            if (!VaultUtils.isClient && (int)State is StateWalk or StateLeave
                && NPC.life < NPC.lifeMax) {
                TriggerStare(OffenderFromInteraction());
            }

            switch ((int)State) {
                case StateWalk:
                    UpdateWalk();
                    break;
                case StateStare:
                    UpdateStare();
                    break;
                case StateLeave:
                    UpdateLeave();
                    break;
                default:
                    UpdateDissolve();
                    break;
            }

            NPC.direction = facing;
            NPC.spriteDirection = facing;
            UpdatePresentation();
        }

        //──── 同行：贴脊走，同向同速，保持距离带 ────

        private void UpdateWalk() {
            StateTimer++;
            GlideToRidge();

            Player anchor = AnchorPlayer();
            if (anchor != null) {
                FollowVelocity(anchor);
            }
            else {
                NPC.velocity.X *= 0.9f;
            }
            //面向行进方向；驻足保持上一朝向（远远陪着，不回头盯人）
            if (MathF.Abs(NPC.velocity.X) > 0.15f) {
                facing = NPC.velocity.X > 0f ? 1 : -1;
            }

            if (VaultUtils.isClient) {
                return;
            }
            JudgeRidgeSteal();
            JudgeRetreat(anchor);
        }

        /// <summary>ai[3] 解析同行锚（各端同式；失效由服务器 JudgeRetreat 换锚或退场）</summary>
        private Player AnchorPlayer() {
            int who = (int)StackCount;
            if (who >= 0 && who < Main.maxPlayers) {
                Player player = Main.player[who];
                if (player?.active == true && !player.dead) {
                    return player;
                }
            }
            return null;
        }

        /// <summary>同向同速读锚玩家速度镜像；近于带加速拉开（无论玩家动不动）、
        /// 出带向玩家侧收拢补速；玩家停它也停</summary>
        private void FollowVelocity(Player anchor) {
            float dx = NPC.Center.X - anchor.Center.X;
            float adist = MathF.Abs(dx);
            float target;
            if (adist < KiyumeYokaiMetrics.RidgeBandNear) {
                target = (dx >= 0f ? 1f : -1f) * KiyumeYokaiMetrics.RidgePullSpeed;
            }
            else if (MathF.Abs(anchor.velocity.X) > 0.1f) {
                target = MathHelper.Clamp(anchor.velocity.X,
                    -KiyumeYokaiMetrics.RidgeMaxWalkSpeed, KiyumeYokaiMetrics.RidgeMaxWalkSpeed);
                if (adist > KiyumeYokaiMetrics.RidgeBandFar) {
                    target -= MathF.Sign(dx) * KiyumeYokaiMetrics.RidgeCatchUp;
                }
            }
            else {
                target = 0f;
            }
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, target, 0.12f);
        }

        /// <summary>贴脊滑行：探针认实心与平台顶（屋顶路线的踏台/晾台也是路）；
        /// 探到的面深过雾面容差 = 视作探空（不跟着塌口斜坡走进雾海）；
        /// 探空保持高度缓浮（灵体，宁飘不坠不瞬移；连续位移无瞬移，不涉 netOffset）</summary>
        private void GlideToRidge() {
            if (TryFindRidge(NPC.Center.X, NPC.Bottom.Y - 48f, out float ground)
                && ground <= KiyumeFogTide.SurfaceAt(NPC.Center.X) + RidgeSinkTolerancePx) {
                NPC.position.Y = MathHelper.Lerp(NPC.position.Y, ground - NPC.height, 0.25f);
            }
            NPC.velocity.Y = 0f;
        }

        //从起始高度向下探屋脊面（tile 已同步，两端结果确定一致）
        private static bool TryFindRidge(float x, float fromY, out float groundY) {
            int tileX = (int)(x / 16f);
            int tileY = (int)(fromY / 16f);
            for (int i = 0; i < RidgeProbeRows; i++) {
                int y = tileY + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    break;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile
                    && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    groundY = y * 16f;
                    return true;
                }
            }
            groundY = 0f;
            return false;
        }

        //==================== 服务器裁决 ====================

        /// <summary>抢屋脊：同脊（脚底高差 ≤RoofStepMaxDh×16）且横距 &lt;160px 持续 30t → 冷视；
        /// 断续即清零（「持续」按字面裁）</summary>
        private void JudgeRidgeSteal() {
            Player thief = null;
            float best = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || player.ghost) {
                    continue;
                }
                float dx = MathF.Abs(player.Center.X - NPC.Center.X);
                float dy = MathF.Abs(player.Bottom.Y - NPC.Bottom.Y);
                if (dx < KiyumeYokaiMetrics.RidgeStealDistPx
                    && dy <= KiyumeYokaiMetrics.RidgeStealDyPx && dx < best) {
                    best = dx;
                    thief = player;
                }
            }
            if (thief == null) {
                stealTicks = 0;
                stealWho = -1;
                return;
            }
            if (thief.whoAmI != stealWho) {
                stealWho = thief.whoAmI;
                stealTicks = 0;
            }
            if (++stealTicks >= KiyumeYokaiMetrics.RidgeStealTicks) {
                TriggerStare(stealWho);
            }
        }

        /// <summary>退场与换锚：潮位跌破 0.6（滞回）→ 退场；锚失效（死/退/离村落带）先换
        /// 最近带内玩家，全空才退场</summary>
        private void JudgeRetreat(Player anchor) {
            if (TideNorm() < KiyumeYokaiMetrics.RidgeTideGateOff) {
                ChangeState(StateLeave);
                return;
            }
            if (anchor != null && InVillageBand(anchor.Center.X)) {
                return;
            }
            Player next = null;
            float best = float.MaxValue;
            foreach (Player player in Main.ActivePlayers) {
                if (player.dead || !InVillageBand(player.Center.X)) {
                    continue;
                }
                float d = player.Distance(NPC.Center);
                if (d < best) {
                    best = d;
                    next = player;
                }
            }
            if (next == null) {
                ChangeState(StateLeave);
                return;
            }
            StackCount = next.whoAmI;
            NPC.netUpdate = true;
        }

        /// <summary>触怒者=最后动手的人；无效值（默认 255/离线）回退最近活人</summary>
        private int OffenderFromInteraction() {
            int who = NPC.lastInteraction;
            if (who >= 0 && who < Main.maxPlayers
                && Main.player[who]?.active == true && !Main.player[who].dead) {
                return who;
            }
            Player near = NearestLivePlayer(out _);
            return near?.whoAmI ?? 0;
        }

        private void TriggerStare(int who) {
            ChangeState(StateStare, who);
        }

        //──── 冷视一拍：帧停 + 眼光亮，站定 ────

        private void UpdateStare() {
            StateTimer++;
            NPC.velocity = Vector2.Zero;
            if (VaultUtils.isClient) {
                return;
            }
            if (StateTimer >= KiyumeYokaiMetrics.RidgeStareTicks) {
                int who = (int)StateParam;
                SettleDropIntoFog(who);
                //化雾变体过线：StateParam=触怒者+1（0 留给平退无罚）
                ChangeState(StateDissolve, who + 1);
            }
        }

        /// <summary>坠雾结算（发包正字镜像提灯翁 SettleGrip）：
        /// 服务器 Hurt 只写本端镜像（对源：发包分支仅 netMode==1 且 myPlayer）——须显式广播 HurtInfo；
        /// 服务器 AddBuff 同理不自发包（Player.cs L5698 仅 netMode==1），显式补 55 号。
        /// 横推离脊由触怒者本端在化雾状态沿重放（纯下压在实心脊上是零位移死写法，坠落交给重力）</summary>
        private void SettleDropIntoFog(int who) {
            if (who < 0 || who >= Main.maxPlayers) {
                return;
            }
            Player victim = Main.player[who];
            if (victim?.active != true || victim.dead) {
                return;
            }
            int dir = victim.Center.X < NPC.Center.X ? -1 : 1;
            double dealt = victim.Hurt(PlayerDeathReason.ByCustomReason(
                ridgeDeathReason.ToNetworkText(victim.name)),
                KiyumeYokaiMetrics.RidgePunishDamage, dir, out Player.HurtInfo info);
            if (dealt > 0.0 && VaultUtils.isServer) {
                NetMessage.SendPlayerHurt(victim.whoAmI, info);
            }
            victim.AddBuff(BuffID.Darkness, KiyumeYokaiMetrics.RidgePunishDarkTicks);
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.AddPlayerBuff, -1, -1, null,
                    victim.whoAmI, BuffID.Darkness, KiyumeYokaiMetrics.RidgePunishDarkTicks);
            }
        }

        //──── 退场：向远离最近玩家侧走出屏外（脊尽缓浮滑出），够远或超时化雾 ────

        private void UpdateLeave() {
            StateTimer++;
            GlideToRidge();
            Player near = NearestLivePlayer(out float dist);
            int dir = near == null ? facing : (NPC.Center.X > near.Center.X ? 1 : -1);
            NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X,
                dir * KiyumeYokaiMetrics.RidgeLeaveSpeed, 0.08f);
            if (MathF.Abs(NPC.velocity.X) > 0.15f) {
                facing = NPC.velocity.X > 0f ? 1 : -1;
            }
            if (VaultUtils.isClient) {
                return;
            }
            if (dist > KiyumeYokaiMetrics.RidgeLeaveDistPx
                || StateTimer >= KiyumeYokaiMetrics.RidgeLeaveTimeoutTicks) {
                ChangeState(StateDissolve);   //param=0：平退无罚
            }
        }

        private void UpdateDissolve() {
            StateTimer++;
            NPC.velocity.X *= 0.9f;
            NPC.velocity.Y = 0f;
            if (StateTimer >= KiyumeYokaiMetrics.RidgeDissolveTicks) {
                NPC.active = false;   //uDissolve 走满同帧退场（残点不上屏，镜像姊妹）
            }
        }

        /// <summary>不可真死：血线归零只是触怒的另一个入口，无掉落无播报</summary>
        public override bool CheckDead() {
            NPC.life = 1;
            if (!VaultUtils.isClient && (int)State is StateWalk or StateLeave) {
                TriggerStare(OffenderFromInteraction());
            }
            return false;
        }

        //==================== 导演泵出口（服务器；泵挂点区只留薄调用） ====================

        /// <summary>潮位归一（镜像导演 PumpCortege 的 LineWorldY 换算；0=退潮 1=涨满）</summary>
        internal static float TideNorm() {
            float span = (KiyumeMetrics.FogLineLowRow - KiyumeMetrics.FogLineHighRow) * 16f;
            return (KiyumeMetrics.FogLineLowRow * 16f - KiyumeFogTide.LineWorldY) / span;
        }

        private static bool InVillageBand(float worldX) {
            return worldX >= KiyumeMetrics.VillageLeft * 16f
                && worldX < KiyumeMetrics.GroveLeft * 16f;
        }

        /// <summary>出没潮窗（服务器读权威 LineWorldY）：归一 ≥0.7 ⟺ 雾线 ≤ 约 418.8 行 ×16，
        /// 村落地板（452..420 行）整段没入雾下只剩屋顶；潮汐权威未开则本怪缺席（概念潮生潮灭）</summary>
        internal static bool TideWindowOpen() {
            return KiyumeYokaiMetrics.TideGateEnabled
                && TideNorm() >= KiyumeYokaiMetrics.RidgeTideGateOn;
        }

        /// <summary>生成尝试：取村落带内、站位高于该列雾面（屋顶层活动）的玩家为锚；
        /// 同侧优先（面向侧 4 探、背侧 2 探兜底），锚 400~700px 屋脊落点生成（ai[3]=锚）。
        /// 出现无声：现形靠 presentAlpha 从 0 渐入，不放生成演出</summary>
        internal static bool TryRidgeSpawn() {
            Player anchor = null;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && InVillageBand(player.Center.X)
                    && player.Bottom.Y <= KiyumeFogTide.SurfaceAt(player.Center.X)) {
                    anchor = player;
                    break;
                }
            }
            if (anchor == null) {
                return false;
            }
            int side0 = anchor.direction != 0 ? anchor.direction : 1;
            for (int i = 0; i < 6; i++) {
                int side = i < 4 ? side0 : -side0;
                float dist = Main.rand.NextFloat(KiyumeYokaiMetrics.RidgeSpawnDistMin,
                    KiyumeYokaiMetrics.RidgeSpawnDistMax);
                float x = anchor.Center.X + side * dist;
                if (!InVillageBand(x) || !TryFindRoofTop(x, out float roofY)) {
                    continue;
                }
                KiyumeHauntDirector.SpawnYokai(ModContent.NPCType<RidgeWalkerYokai>(),
                    new Vector2(x, roofY), ai3: anchor.whoAmI);
                return true;
            }
            return false;
        }

        //自雾面向上开窗、自窗顶向下扫：首个实心/平台顶且面在雾上（≥半格）= 可走屋脊。
        //涨潮期村落地面全在雾下，此窗内探到的必是屋顶/望楼一类构造物顶
        private static bool TryFindRoofTop(float x, out float roofY) {
            float surface = KiyumeFogTide.SurfaceAt(x);
            int tileX = (int)(x / 16f);
            int startRow = (int)(surface / 16f) - SpawnProbeUpRows;
            for (int i = 0; i < SpawnProbeUpRows; i++) {
                int y = startRow + i;
                if (!WorldGen.InWorld(tileX, y, 20)) {
                    continue;
                }
                Tile tile = Framing.GetTileSafely(tileX, y);
                if (tile.HasTile
                    && (Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType])) {
                    roofY = y * 16f;
                    return roofY <= surface - 8f;
                }
            }
            roofY = 0f;
            return false;
        }

        //==================== 表现（各端本地，由 ai 重放） ====================

        /// <summary>状态变迁沿音画（迟入端首帧也吃一次沿）；音效极简：出现与同行全程无声，
        /// 冷视一声低吟，坠雾一声闷响；横推由触怒者本端在此重放（纯演出量）</summary>
        private void PlayStateCue() {
            switch ((int)State) {
                case StateStare:
                    //冷视拍：转向触怒者 + 低吟（Zombie3 变调）
                    int who = (int)MathHelper.Clamp(StateParam, 0f, Main.maxPlayers - 1);
                    if (Main.player[who]?.active == true) {
                        facing = Main.player[who].Center.X > NPC.Center.X ? 1 : -1;
                    }
                    SoundEngine.PlaySound(SoundID.Zombie3 with {
                        Volume = 0.7f, Pitch = -0.55f, MaxInstances = 2
                    }, NPC.Center);
                    break;
                case StateDissolve:
                    EmitDissolveMist();
                    if ((int)StateParam >= 1) {
                        //坠雾拍：闷响（雾面如水面，SplashWeak 压低）
                        SoundEngine.PlaySound(SoundID.SplashWeak with {
                            Volume = 0.9f, Pitch = -0.7f, MaxInstances = 2
                        }, NPC.Center);
                        int victim = (int)StateParam - 1;
                        if (!Main.dedServ && victim == Main.myPlayer) {
                            Player me = Main.LocalPlayer;
                            int dir = me.Center.X < NPC.Center.X ? -1 : 1;
                            me.velocity.X = dir * KiyumeYokaiMetrics.RidgeDropPushX;
                            //微抬让横推吃得进（贴地摩擦两帧吞干横速），坠雾由重力与黑暗完成
                            me.velocity.Y = Math.Min(me.velocity.Y,
                                -KiyumeYokaiMetrics.RidgeDropLiftY);
                        }
                    }
                    break;
            }
        }

        private void EmitDissolveMist() {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    NPC.Center + Main.rand.NextVector2Circular(12f, 20f),
                    new Vector2(Main.rand.NextFloat(-0.4f, 0.4f), -Main.rand.NextFloat(0.3f, 0.9f)),
                    MistTint * 0.7f, Main.rand.NextFloat(0.28f, 0.45f))
                    ?.Configure(Main.rand.Next(40, 70));
            }
        }

        public override void HitEffect(NPC.HitInfo hit) {
            //灵体受击：几缕冷灰，无血
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 3; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height,
                    DustID.Smoke, hit.HitDirection * 1.0f, -0.5f, 140, new Color(160, 152, 170), 0.85f);
                dust.noGravity = true;
            }
        }

        /// <summary>现形语法（本怪极性）：距离项近隐远显（同行者要在带距上可读，贴近渐薄逼出保持距离）；
        /// 浓度项以潮位归一替代局地 DensityAt——它活在雾面之上，局地采样恒稀薄，标准语法在屋脊层失义；
        /// 潮落向退场线时身形自然转薄，与退场门同源。冷视与坠雾拍强制现形</summary>
        private void UpdatePresentation() {
            NearestLivePlayer(out float dist);
            float near = DistanceRevealTerm(dist,
                KiyumeYokaiMetrics.RidgeFadeNearPx, KiyumeYokaiMetrics.RidgeFadeFarPx);
            float tide = MathHelper.Clamp(
                (TideNorm() - KiyumeYokaiMetrics.RidgeTideFadeFloor)
                / KiyumeYokaiMetrics.RidgeTideFadeSpan, 0f, 1f);
            float target = near * tide;
            if ((int)State is StateStare or StateDissolve) {
                target = Math.Max(target, 0.95f);
            }
            presentAlpha = MathHelper.Lerp(presentAlpha, MathHelper.Clamp(target, 0f, 1f), 0.08f);
        }

        public override void FindFrame(int frameHeight) {
            //冷视帧停是禁忌的可读拍；化雾同样定格（帧动会破坏「化开」的读感）
            if ((int)State is StateStare or StateDissolve) {
                return;
            }
            frameClock += 0.3f + MathF.Abs(NPC.velocity.X) * 0.35f;
            if (frameClock > 8f) {
                frameClock -= 8f;
                NPC.frame.Y += frameHeight;
            }
            if (NPC.frame.Y >= frameHeight * Main.npcFrameCount[Type]) {
                NPC.frame.Y = 0;
            }
        }

        //==================== 绘制（全接管：透明度全程显式给值，不依赖 NPC.alpha） ====================

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (presentAlpha < 0.02f) {
                return false;
            }
            Main.instance.LoadNPC(NPCID.Wraith);
            Texture2D tex = TextureAssets.Npc[NPCID.Wraith].Value;
            if (tex == null) {
                return false;
            }
            int frameH = tex.Height / Main.npcFrameCount[Type];
            int frameIdx = Math.Clamp(NPC.frame.Y / Math.Max(frameH, 1), 0,
                Main.npcFrameCount[Type] - 1);
            //源矩形上下各内缩 1px 防帧表渗色（姊妹同款）
            var source = new Rectangle(0, frameIdx * frameH + 1, tex.Width, frameH - 2);
            var topLeft = new Vector2(NPC.Center.X - tex.Width * BodyScale * 0.5f,
                NPC.Bottom.Y + 2f - source.Height * BodyScale);
            Vector2 drawPos = topLeft - screenPos;

            //冷视眼光渐升；带罚化雾保峰（那双眼是最后消失的东西）
            float eye = (int)State switch {
                StateStare => KiyumeYokaiMetrics.RidgeEyeGlowMax
                    * MathHelper.Clamp(StateTimer / 10f, 0f, 1f),
                StateDissolve when (int)StateParam >= 1 => KiyumeYokaiMetrics.RidgeEyeGlowMax,
                _ => 0f,
            };
            float dissolve = (int)State == StateDissolve
                ? MathHelper.Clamp(StateTimer / (float)KiyumeYokaiMetrics.RidgeDissolveTicks, 0f, 1f)
                : 0f;

            Effect hound = EffectLoader.KikasaHound?.Value;
            Texture2D noise = CWRAsset.PerlinNoise?.Value;
            if (hound == null || noise == null) {
                //着色器缺编：近黑冷灰剪影回退（HoundShade 同款语义）
                SpriteEffects fb = facing > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(tex, drawPos, source,
                    new Color(12, 10, 16) * (presentAlpha * 0.9f * (1f - dissolve)),
                    0f, Vector2.Zero, BodyScale, fb, 0f);
                return false;
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null,
                Main.GameViewMatrix.TransformationMatrix);
            GraphicsDevice gd = Main.instance.GraphicsDevice;
            gd.Textures[1] = noise;
            gd.SamplerStates[1] = SamplerState.LinearWrap;

            //参数链照抄井手/犬影实体态：uMode=1 暗影化，翻转在采样里做（不用 SpriteEffects）
            hound.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            hound.Parameters["uSeed"]?.SetValue(Seed);
            hound.Parameters["uUvRect"]?.SetValue(new Vector4(
                0f, source.Y / (float)tex.Height, 1f, source.Height / (float)tex.Height));
            hound.Parameters["uTexel"]?.SetValue(new Vector2(1f / tex.Width, 1f / tex.Height));
            hound.Parameters["uAspect"]?.SetValue(tex.Width / (float)source.Height);
            hound.Parameters["uFlipH"]?.SetValue(facing > 0 ? 1f : 0f);
            hound.Parameters["uFlipV"]?.SetValue(0f);
            hound.Parameters["uMode"]?.SetValue(1f);
            hound.Parameters["uSeamGate"]?.SetValue(0f);
            hound.Parameters["uWobble"]?.SetValue(0.010f);
            hound.Parameters["uEyeGlow"]?.SetValue(eye);
            hound.Parameters["uEyeAnchor"]?.SetValue(EyeAnchor);
            hound.Parameters["uDissolve"]?.SetValue(dissolve);
            hound.Parameters["uEdgeTint"]?.SetValue(EdgeTint);
            hound.CurrentTechnique = hound.Techniques["TechHound"];
            hound.CurrentTechnique.Passes[0].Apply();

            spriteBatch.Draw(tex, drawPos, source,
                Color.White * MathHelper.Clamp(presentAlpha * 1.25f, 0f, 1f),
                0f, Vector2.Zero, BodyScale, SpriteEffects.None, 0f);

            BeginDefault(spriteBatch);
            gd.Textures[1] = null;

#if DEBUG
            Utils.DrawBorderString(spriteBatch,
                $"状态 {(int)State}  抢脊 {stealTicks}  潮 {TideNorm():F2}",
                NPC.Top - screenPos + new Vector2(-38f, -34f),
                Color.LightGoldenrodYellow, 0.7f);
#endif
            return false;
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
}
