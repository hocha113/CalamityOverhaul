using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.CrimsonRendSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniAnnihilates;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDismembers;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniDomains;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFinaleSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniFlashSteps;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniOmokages;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniSakuraFlights;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.OniZanshinSlashs;
using CalamityOverhaul.Content.LegendWeapon.OnikiriLegend.UI;
using CalamityOverhaul.Content.Scenarios.Himayo;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.OnikiriLegend
{
    /// <summary>
    /// 鬼切资源层,气力+架势,owner 端自治不进网络/存档.
    /// 右键疾走;表世界可衔樱流;交还帧开追斩窗;
    /// <see cref="CWRKeySystem.WeponSkill_R"/> 处决;里世界点选肢解.
    /// HUD 经 <see cref="OnikiriResourceSource"/> 只读
    /// </summary>
    internal class OnikiriPlayer : ModPlayer
    {
        //====调参常量====
        public const float VigorMax = 100f;
        /// <summary>神威疾走的气力开销</summary>
        public const float DashVigorCost = 30f;
        /// <summary>每帧自然回气(约 6/s)</summary>
        private const float VigorRegenPerTick = 0.10f;
        /// <summary>消耗后回气延迟(帧),防右键无脑连打</summary>
        private const int VigorRegenDelayTicks = 48;
        /// <summary>连段每命中一敌回气</summary>
        private const float VigorPerComboHit = 2f;

        public const float StanceMax = 100f;
        /// <summary>灭世一闪的架势门槛与开销</summary>
        public const float AnnihilateCost = 50f;
        /// <summary>连段每命中一敌蓄势</summary>
        private const float StancePerComboHit = 2.5f;
        /// <summary>疾走穿身格挡每敌蓄势</summary>
        private const float StancePerParry = 12f;
        /// <summary>单次冲刺穿身蓄势封顶</summary>
        private const float StanceParryCapPerDash = 36f;

        /// <summary>疾走墨痕伤害系数:定位是位移+格挡工具,不与连段争输出</summary>
        private const float DashDamageMul = 0.65f;
        /// <summary>居合越过光标的固定余量(px)</summary>
        private const float DashCursorOvershoot = 220f;
        /// <summary>灭世一闪伤害倍率(单次巨额结算)</summary>
        private const float AnnihilateDamageMul = 5f;
        /// <summary>冲刺基础再触发锁(帧)，长距离时会自动延长到覆盖完整位移</summary>
        private const int DashRefireLockTicks = 14;

        /// <summary>樱流化身每帧耗气(疾走衔接的持续飞行,气尽自动回卷),满气冲刺后余量约可飞 1.4s</summary>
        private const float SakuraDrainPerTick = 0.8f;
        /// <summary>樱流入飞门槛:低于此气力不衔接,疾走照常收势</summary>
        private const float SakuraMinVigor = 10f;
        /// <summary>樱流巡航速度(px/帧),模块钳制上限 48;从疾走高速骤降到此,是"化形"的减速拍</summary>
        private const float SakuraFlightSpeed = 40f;

        /// <summary>追斩资格时长(帧),交还操控后保留 1.5 秒</summary>
        private const int ZanshinWindowTicks = 90;
        /// <summary>追斩伤害倍率:层级卡在连段单拍与灭世一闪(5x)之间</summary>
        private const float ZanshinDamageMul = 2f;
        /// <summary>追斩命中回架势(每敌),比连段单拍(2.5)厚,喂处决循环</summary>
        private const float StancePerZanshinHit = 6f;
        /// <summary>锵后仍算"同帧"的宽限(帧):此窗内出刀视同与结算压拍,震屏减半</summary>
        private const int ZanshinSyncSlackTicks = 2;
        /// <summary>终结乱舞焦点距离钳制(与疾走射程同量级,演出保持在可读范围)</summary>
        private const float FinaleFocusMaxDist = 800f;
        /// <summary>终结乱舞光标磁吸半径(按精确碰撞箱距离衡量)</summary>
        private const float FinaleMagnetRadius = 200f;
        /// <summary>光标点名允许略超射程的余量:玩家明确指着谁就成全谁</summary>
        private const float FinaleCursorSlack = 260f;
        /// <summary>命中记忆容量与保鲜期(帧):近 5 秒打过谁,处决就认得谁</summary>
        private const int HitMemoryCapacity = 8;
        private const int HitMemoryLifeTicks = 300;

        /// <summary>肢解伤害倍率(终斩刀线/媒介脉冲单次结算);代价是反噬(僵直+必定伤害)而非资源</summary>
        private const float DismemberDamageMul = 2.5f;
        /// <summary>肢解射程(与处决同量级)</summary>
        private const float DismemberRange = 800f;
        /// <summary>点名真身的贴身容差(碰撞箱边距):点在身上=明确要斩真身,压过挂在它身上的纸</summary>
        private const float DirectPickPad = 16f;
        /// <summary>媒介点选的光标容差(点到纸面矩形距离)</summary>
        private const float PaperMagnetPad = 60f;

        //====状态(owner 端自治)====
        internal float Vigor = VigorMax;
        internal float Stance;
        private int vigorRegenDelay;
        private int dashLock;
        private bool prevMouseRight;
        //本次冲刺穿身蓄势的已得量与已计根:蠕虫全身只算一条
        private float dashParryGained;
        private readonly HashSet<int> parriedRoots = [];
        private int readyCueTimer;
        //====追斩资格(owner 端自治)====
        private int zanshinWindow;          //剩余帧数,0=关
        private int zanshinJudgeCountdown;  //距锵帧数,窗开着时持续递减(负值=锵已过)
        private bool zanshinHasMarks;       //开窗时疾走带墨痕:锵前按下走缓冲,同帧释放
        private bool zanshinPending;        //左键意图已受理,带墨痕时挂起等锵
        private bool zanshinInputBuffered;  //疾走/樱流控身期间按下左键,交还帧兑现
        private bool prevMouseLeft;         //Shoot 路径按下沿鉴别,防资格期自动重用

        //====命中记忆:处决智能选点的第二层依据====
        private struct HitMemory
        {
            public int NpcId;
            public int NpcType;
            public int Tick;
        }
        private readonly HitMemory[] hitMemory = new HitMemory[HitMemoryCapacity];

        public override void OnEnterWorld() {
            Vigor = VigorMax;
            Stance = 0f;
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
        }

        public override void OnRespawn() {
            Vigor = VigorMax;
            Stance = 0f;
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
        }

        public override void PostUpdate() {
#if DEBUG
            Vigor = VigorMax;
#endif
            if (Main.dedServ || Player.whoAmI != Main.myPlayer) {
                return;
            }

            //试炼门禁硬倒计时,与招式无关,反噬僵直也推进
            HimayoStorySync.TickTrialUnlockSafety(Player);

            if (vigorRegenDelay > 0) {
                vigorRegenDelay--;
            }
            else {
                Vigor = Math.Min(VigorMax, Vigor + VigorRegenPerTick);
            }
            if (dashLock > 0) {
                dashLock--;
            }
            TickZanshinWindow();

            bool justRight = Main.mouseRight && !prevMouseRight;
            prevMouseRight = Main.mouseRight;
            //左键沿供 TryZanshinStrike 的 Shoot 路径鉴别:ItemCheck 先于 PostUpdate,
            //此处更新后,下一帧的物品使用读到的仍是"上一帧是否按着"
            prevMouseLeft = Main.mouseLeft;

            //反噬僵直期间万籁俱寂:招式与领域输入全部静默,规避疾走/翻转拆散钉死
            if (OniPlayerDismember.IsLocked(Player)) {
                return;
            }

            Item item = Player.GetItem();
            bool holding = item != null && item.Alives() && item.type == ModContent.ItemType<OnikiriItem>();
            if (holding && zanshinWindow <= 0 && Main.mouseLeft
                && (dashLock > 0 || OniSakuraFlight.ControlsOwner(Player.whoAmI))) {
                zanshinInputBuffered = true;
            }
            HandleDomainInput(holding);
            if (holding) {
                ManageSakuraFlight();
            }
            if (!holding || Player.dead || Player.CCed) {
                return;
            }
            //点鬼簿/铭刻仪式演出中不受理招式输入
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                return;
            }

            ReleaseZanshinPending(item);
            ReadyCue();

            if (justRight && !Player.mouseInterface && !Player.cursorItemIconEnabled) {
                TryDash(item);
            }
            if (CWRKeySystem.Onikiri_Execute.JustPressed) {
                TryExecute(item);
            }
        }

        //==================== 鬼域 ====================

        /// <summary>
        /// 领域键 <see cref="CWRKeySystem.Legend_Domain"/> 开阖(持刀);
        /// <see cref="CWRKeySystem.Onikiri_DomainFlip"/> 翻转,域开时不持刀也受理;
        /// 骇客时停/点鬼簿/铭刻中不受理
        /// </summary>
        private void HandleDomainInput(bool holding) {
            if (Player.dead) {
                return;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (HackTime.Active) {
                return;
            }
            if ((OniRegisterUI.Instance?.IsOpen ?? false) || (OniEngraveRiteUI.Instance?.Active ?? false)) {
                return;
            }

            if (holding && CWRKeySystem.Legend_Domain.JustPressed) {
                if (!OniDomain.TryToggle(Player, out bool busy) && busy) {
                    OniTalismanHud.NotifyDomainDenied();
                }
            }
            //中键默认绑定:悬停在鬼眼上时 mouseInterface 为真,让位给眼的点击受理,防同帧双发
            if ((holding || domain.AnyActive) && CWRKeySystem.Onikiri_DomainFlip.JustPressed && !Player.mouseInterface) {
                if (!OniDomain.TryFlip(Player, out bool busy) && busy) {
                    OniTalismanHud.NotifyDomainDenied();
                }
            }
        }

        //==================== 神威疾走 ====================

        private void TryDash(Item item) {
            //再触发锁内静默(是节拍不是资源问题);骑乘时位移权在坐骑;樱流握有本体时不受理
            if (dashLock > 0 || Player.mount?.Active == true
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return;
            }
            if (Vigor < DashVigorCost - 0.01f) {
                OniTalismanHud.NotifyVigorDenied();
                return;
            }

            Vigor -= DashVigorCost;
            vigorRegenDelay = VigorRegenDelayTicks;
            dashParryGained = 0f;
            parriedRoots.Clear();
            //新位移开始,上一窗作废
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = Main.mouseLeft;

            ShootState state = Player.GetShootState();
            Vector2 aim = Main.MouseWorld - Player.Center;
            float distance = aim.Length() + DashCursorOvershoot;
            dashLock = Math.Max(DashRefireLockTicks, OniFlashStep.CalculateTravelFrames(distance));
            OniFlashStep.Fire(Player, aim, (int)(state.WeaponDamage * DashDamageMul)
                , state.WeaponKnockback, distance, source: Player.GetSource_ItemUse(item));
        }

        //==================== 樱流化身 ====================

        /// <summary>
        /// 疾走衔樱流,<see cref="OniFlashStep"/> 停止帧(owner);
        /// 需表世界+最低气力,失败静默
        /// </summary>
        internal bool TryChainSakuraFlight(Vector2 direction, IEntitySource source) {
            if (Player.whoAmI != Main.myPlayer || Player.mount?.Active == true) {
                return false;
            }
            //上一次飞行的控制器(含余晖期)未消亡则拒绝:模块每玩家仅一个,拿旧实例不算衔接成功
            if (OniSakuraFlight.AnyFor(Player.whoAmI)) {
                return false;
            }
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Omote || domain.WorldIsUra) {
                return false;
            }
            if (Vigor < SakuraMinVigor - 0.01f) {
                return false;
            }
            int flightFrames = (int)(Vigor / SakuraDrainPerTick);
            if (OniSakuraFlight.Fire(Player, direction, SakuraFlightSpeed,
                flightFrames, source, seamless: true) == null) {
                return false;
            }
            //化樱起飞,疾走的旧窗作废;落地(ReleaseOwner)会开新窗
            zanshinWindow = 0;
            zanshinPending = false;
            return true;
        }

        /// <summary>
        /// 樱流飞行的经济与手势(owner 端,每帧):逐帧抽气并压住回气延迟;
        /// 松手、气尽或领域离开表世界稳态均发出回卷,重组收尾由模块自理
        /// </summary>
        private void ManageSakuraFlight() {
            if (!OniSakuraFlight.IsTraveling(Player.whoAmI)) {
                return;
            }
            vigorRegenDelay = Math.Max(vigorRegenDelay, VigorRegenDelayTicks);
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (!Main.mouseRight || Vigor <= 0.01f
                || domain.Phase != OniDomainPhase.Omote || domain.WorldIsUra) {
                OniSakuraFlight.RequestStop(Player);
                return;
            }
            Vigor = Math.Max(0f, Vigor - SakuraDrainPerTick);
        }

        //==================== 残心追斩 ====================

        /// <summary>交还帧开追斩资格(owner),控身期间的左键在此转为挂起</summary>
        internal void OpenZanshinWindow(int judgeDelay, int markCount) {
            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            bool bufferedInput = zanshinInputBuffered || Main.mouseLeft;
            zanshinWindow = ZanshinWindowTicks;
            zanshinJudgeCountdown = Math.Max(judgeDelay, 0);
            zanshinHasMarks = markCount > 0;
            zanshinPending = bufferedInput;
            zanshinInputBuffered = false;
        }

        /// <summary>追斩窗每帧推进:锵倒计时递减(负值=锵已过),窗口过期清挂起</summary>
        private void TickZanshinWindow() {
            if (zanshinWindow <= 0) {
                return;
            }
            zanshinWindow--;
            zanshinJudgeCountdown--;
            if (zanshinWindow <= 0) {
                zanshinPending = false;
                zanshinInputBuffered = false;
            }
        }

        /// <summary>
        /// 追斩按下沿;<see cref="OnikiriItem.Shoot"/> 传 edgeVerified=false,
        /// <see cref="CrimsonRendSlash"/> 传 true;false 则回退连段
        /// </summary>
        internal bool TryZanshinStrike(Item item, bool edgeVerified) {
            if (Player.whoAmI != Main.myPlayer || zanshinWindow <= 0) {
                return false;
            }
            if (!edgeVerified && (!Main.mouseLeft || prevMouseLeft)) {
                return false;
            }
            if (zanshinPending) {
                //已挂起等锵,窗内重复点击吸收,不落回连段
                return true;
            }
            //硬占刀权的演出(灭世大挥/终结乱舞开场等)期间不抢出手,落回连段的既有让位缓冲
            if (Player.mount?.Active == true || OniPlayerDismember.IsLocked(Player)
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)
                || OniBladeOccupancy.AnyHardOccupant(Player)
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniZanshinSlash>()] > 0) {
                return false;
            }

            if (zanshinHasMarks && zanshinJudgeCountdown > 0) {
                zanshinPending = true;
                return true;
            }
            return FireZanshin(item);
        }

        /// <summary>挂起的追斩在交还后释放,带墨痕则押到锵帧;
        /// 等锵期间玩家另起大招/化樱则弃挂起,大动作优先</summary>
        private void ReleaseZanshinPending(Item item) {
            if (!zanshinPending) {
                return;
            }
            if (OniBladeOccupancy.AnyHardOccupant(Player) || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                zanshinPending = false;
                zanshinWindow = 0;
                return;
            }
            if (!zanshinHasMarks || zanshinJudgeCountdown <= 0) {
                FireZanshin(item);
            }
        }

        /// <summary>追斩出刀:瞄准角与领域变体(表世界=樱衣)都在释放帧采样,锵同帧(含宽限)震屏减半</summary>
        private bool FireZanshin(Item item) {
            zanshinWindow = 0;
            zanshinPending = false;
            zanshinInputBuffered = false;
            ShootState state = Player.GetShootState();
            Vector2 aim = Main.MouseWorld - Player.Center;
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            bool sakura = domain.Phase == OniDomainPhase.Omote && !domain.WorldIsUra;
            bool synced = zanshinHasMarks && zanshinJudgeCountdown <= 0
                && zanshinJudgeCountdown >= -ZanshinSyncSlackTicks;
            Projectile zanshin = OniZanshinSlash.Fire(Player, aim
                , (int)(state.WeaponDamage * ZanshinDamageMul), state.WeaponKnockback
                , sakura, synced, Player.GetSource_ItemUse(item));
            if (zanshin == null) {
                return false;
            }
            CrimsonRendSlash.FindController(Player)?.ConsumeZanshinInput();
            return true;
        }

        /// <summary>追斩命中:回架势 + 记入命中记忆(<see cref="OniZanshinSlash"/>.OnHitNPC 调用)</summary>
        internal void OnZanshinHit(NPC target) {
            Stance = Math.Min(StanceMax, Stance + StancePerZanshinHit);
            RecordHit(target);
        }

        //==================== 处决 ====================

        private void TryExecute(Item item) {
            //演出进行中静默忽略:满屏刀光本身就是"正在忙"的答复;化樱期间人不在,刀也不在
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniFinaleSlash>()] > 0
                || Player.ownedProjectileCounts[ModContent.ProjectileType<OniAnnihilate>()] > 0
                || OniSakuraFlight.ControlsOwner(Player.whoAmI)) {
                return;
            }

            ShootState state = Player.GetShootState();
            if (Stance >= StanceMax - 0.01f) {
                //蓄满:终结乱舞,焦点=光标定区域+小半径磁吸
                Stance = 0f;
                Vector2 focus = ComputeFinaleFocus(out Vector2 aim);
                OniFinaleSlash.Fire(Player, focus, aim, state.WeaponDamage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetFinaleScale(item)
                    , source: Player.GetSource_ItemUse(item));
            }
            else if (Stance >= AnnihilateCost - 0.01f) {
                //过半,灭世一闪,尺寸恒 1.0
                Stance -= AnnihilateCost;
                Vector2 aim = Main.MouseWorld - Player.Center;
                OniAnnihilate.Fire(Player, Player.Center, aim, (int)(state.WeaponDamage * AnnihilateDamageMul)
                    , state.WeaponKnockback, source: Player.GetSource_ItemUse(item));
            }
            else {
                OniTalismanHud.NotifyStanceDenied();
            }
        }

        //==================== 肢解 ====================

        /// <summary>
        /// 里世界肢解点选(按下沿),真身或媒介;
        /// 成功落刀后 <see cref="OniPlayerDismember"/> 反噬;miss 回退连段;owner 端
        /// </summary>
        internal bool TryClickDismember(Item item) {
            if (Player.whoAmI != Main.myPlayer) {
                return false;
            }
            //演出或反噬僵直中不受理:裂成两半的人拔不了刀
            if (Player.ownedProjectileCounts[ModContent.ProjectileType<OniSeverStrike>()] > 0
                || OniPlayerDismember.IsLocked(Player)) {
                return false;
            }
            //肢解只在里世界成立;表世界左键就是普攻
            OniDomainPlayer domain = Player.GetModPlayer<OniDomainPlayer>();
            if (domain.Phase != OniDomainPhase.Ura || !domain.WorldIsUra) {
                return false;
            }

            ShootState state = Player.GetShootState();
            int damage = (int)(state.WeaponDamage * DismemberDamageMul);
            Vector2 mouse = Main.MouseWorld;

            //一层:点在真身碰撞箱上 → 直接肢解,反噬上身
            NPC target = PickDismemberTarget(mouse, DirectPickPad);
            if (target != null) {
                OniSeverStrike.Fire(Player, target, AimAngleFrom(target.Center), damage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetBladeScale(item)
                    , source: Player.GetSource_ItemUse(item));
                return true;
            }

            //二层:点在媒介纸面上 → 点锚斩纸(落刀成功同样反噬上身)
            OmokageEntry paper = OniOmokage.PickEntryNear(mouse, PaperMagnetPad);
            if (paper != null && Vector2.Distance(Player.Center, paper.AnchorCenter) <= DismemberRange) {
                //落刀点收拢进纸面有效范围,拔刀方向=玩家→落刀点
                Vector2 local = mouse - paper.AnchorCenter;
                local.X = MathHelper.Clamp(local.X, -paper.PaperHalf.X * 0.4f, paper.PaperHalf.X * 0.4f);
                local.Y = MathHelper.Clamp(local.Y, -paper.PaperHalf.Y * 0.4f, paper.PaperHalf.Y * 0.4f);
                Vector2 cutPoint = paper.AnchorCenter + local;
                OniSeverStrike.FireAtPoint(Player, cutPoint, AimAngleFrom(cutPoint), damage
                    , state.WeaponKnockback, scale: OnikiriOverride.GetBladeScale(item)
                    , source: Player.GetSource_ItemUse(item));
                return true;
            }

            return false;
        }

        /// <summary>拔刀方向:玩家→落点;重合时退回鼠标方向,再退回朝向</summary>
        private float AimAngleFrom(Vector2 point) {
            Vector2 aim = point - Player.Center;
            if (aim.LengthSquared() < 1f) {
                aim = Main.MouseWorld - Player.Center;
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return aim.ToRotation();
        }

        /// <summary>肢解目标点选,蠕虫节段排除(头部可)</summary>
        private NPC PickDismemberTarget(Vector2 cursor, float pad) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || CWRLoad.WormBodys.Contains(npc.type)) {
                    continue;
                }
                float d = DistanceToHitbox(npc, cursor);
                if (d > pad) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > DismemberRange) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f ? root.lifeMax > bestLife : d < bestD);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>
        /// 终结乱舞焦点级联,光标直选→命中记忆→在场 boss→光标钳射程
        /// </summary>
        private Vector2 ComputeFinaleFocus(out Vector2 aim) {
            Vector2 mouse = Main.MouseWorld;
            NPC picked = PickAtCursor(mouse) ?? PickFromHitMemory() ?? PickBossInRange(mouse);

            Vector2 focus;
            if (picked != null) {
                focus = picked.Center;
            }
            else {
                focus = mouse;
                Vector2 toMouse = focus - Player.Center;
                float dist = toMouse.Length();
                if (dist > FinaleFocusMaxDist) {
                    focus = Player.Center + toMouse * (FinaleFocusMaxDist / dist);
                }
            }

            aim = focus - Player.Center;
            if (aim.LengthSquared() < 1f) {
                aim = mouse - Player.Center;
            }
            if (aim.LengthSquared() < 1f) {
                aim = Vector2.UnitX * Player.direction;
            }
            return focus;
        }

        /// <summary>光标直选最要紧者,可略超射程(<see cref="FinaleCursorSlack"/>)</summary>
        private NPC PickAtCursor(Vector2 cursor) {
            NPC best = null;
            bool bestBoss = false;
            float bestLife = 0f;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float d = DistanceToHitbox(npc, cursor);
                if (d > FinaleMagnetRadius) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist + FinaleCursorSlack) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null
                    || (root.boss != bestBoss
                        ? root.boss
                        : Math.Abs(root.lifeMax - bestLife) > 1f ? root.lifeMax > bestLife : d < bestD);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestLife = root.lifeMax;
                    bestD = d;
                }
            }
            return best;
        }

        /// <summary>命中记忆,近 5 秒,优先 boss</summary>
        private NPC PickFromHitMemory() {
            int now = (int)Main.GameUpdateCount;
            NPC best = null;
            bool bestBoss = false;
            int bestTick = int.MinValue;
            for (int i = 0; i < hitMemory.Length; i++) {
                ref HitMemory mem = ref hitMemory[i];
                if (mem.Tick <= 0 || now - mem.Tick > HitMemoryLifeTicks
                    || mem.NpcId < 0 || mem.NpcId >= Main.maxNPCs) {
                    continue;
                }
                NPC npc = Main.npc[mem.NpcId];
                //槽位可能已被新生的别的 NPC 复用,校验类型防串号
                if (!npc.active || npc.type != mem.NpcType || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist) {
                    continue;
                }
                NPC root = RootOf(npc);
                bool better = best == null || (root.boss != bestBoss ? root.boss : mem.Tick > bestTick);
                if (better) {
                    best = npc;
                    bestBoss = root.boss;
                    bestTick = mem.Tick;
                }
            }
            return best;
        }

        /// <summary>在场 boss 兜底,取离光标最近</summary>
        private NPC PickBossInRange(Vector2 cursor) {
            NPC best = null;
            float bestD = float.MaxValue;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy() || !RootOf(npc).boss) {
                    continue;
                }
                if (Vector2.Distance(Player.Center, npc.Center) > FinaleFocusMaxDist) {
                    continue;
                }
                float d = Vector2.Distance(cursor, npc.Center);
                if (d < bestD) {
                    bestD = d;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>点到碰撞箱的精确距离(大体型 boss 的箱边也吸得住,不吃"中心太远"的亏)</summary>
        private static float DistanceToHitbox(NPC npc, Vector2 point) {
            Rectangle box = npc.Hitbox;
            Vector2 nearest = new(MathHelper.Clamp(point.X, box.Left, box.Right),
                MathHelper.Clamp(point.Y, box.Top, box.Bottom));
            return Vector2.Distance(point, nearest);
        }

        /// <summary>蠕虫类归主体(boss 旗/最大生命都看头)</summary>
        private static NPC RootOf(NPC npc)
            => npc.realLife >= 0 && npc.realLife < Main.maxNPCs ? Main.npc[npc.realLife] : npc;

        /// <summary>记入命中记忆:去重刷新,满则顶掉最旧</summary>
        private void RecordHit(NPC npc) {
            if (npc == null || !npc.active) {
                return;
            }
            int now = (int)Main.GameUpdateCount;
            int slot = -1;
            for (int i = 0; i < hitMemory.Length; i++) {
                if (hitMemory[i].NpcId == npc.whoAmI && hitMemory[i].NpcType == npc.type) {
                    slot = i;
                    break;
                }
            }
            if (slot < 0) {
                slot = 0;
                for (int i = 1; i < hitMemory.Length; i++) {
                    if (hitMemory[i].Tick < hitMemory[slot].Tick) {
                        slot = i;
                    }
                }
            }
            hitMemory[slot] = new HitMemory { NpcId = npc.whoAmI, NpcType = npc.type, Tick = now };
        }

        //==================== 资源增益(玩法挂点调用,owner 端) ====================

        /// <summary>连段命中:回气 + 蓄势 + 记入命中记忆(<see cref="CrimsonRendSlash.OnHitNPC"/> 调用)</summary>
        internal void OnComboHit(NPC target) {
            Vigor = Math.Min(VigorMax, Vigor + VigorPerComboHit);
            Stance = Math.Min(StanceMax, Stance + StancePerComboHit);
            RecordHit(target);
        }

        /// <summary>疾走穿身即格挡:蓄势 + 记入命中记忆(<see cref="OniFlashStep"/> 标记成功时调用);
        /// 蓄势按 realLife 归主体只算一条,单次冲刺封顶;记忆不受封顶影响</summary>
        internal void OnDashParry(NPC npc) {
            RecordHit(npc);
            int root = npc.realLife >= 0 ? npc.realLife : npc.whoAmI;
            if (!parriedRoots.Add(root) || dashParryGained >= StanceParryCapPerDash - 0.01f) {
                return;
            }
            float gain = Math.Min(StancePerParry, StanceParryCapPerDash - dashParryGained);
            dashParryGained += gain;
            Stance = Math.Min(StanceMax, Stance + gain);
        }

        /// <summary>满架势身周绯焰提示</summary>
        private void ReadyCue() {
            if (Stance < StanceMax - 0.01f) {
                readyCueTimer = 0;
                return;
            }
            if (++readyCueTimer < 26) {
                return;
            }
            readyCueTimer = 0;
            Vector2 pos = Player.Center + new Vector2(Main.rand.NextFloat(-16f, 16f), Main.rand.NextFloat(-8f, 20f));
            PRTLoader.NewParticle<PRT_CrimsonSpark>(pos, -Vector2.UnitY * Main.rand.NextFloat(0.7f, 1.5f)
                , new Color(255, 96, 58), Main.rand.NextFloat(0.2f, 0.32f))
                ?.Configure(Main.rand.Next(20, 32), affectedByGravity: false);
        }
    }

    /// <summary>把 <see cref="OnikiriPlayer"/> 的数值接给 HUD 的数据入口(只读)</summary>
    internal sealed class OnikiriResourceSource : IOniVigorSource, IOniStanceSource
    {
        public bool TryGetVigor(Player player, out OniVigorSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                snapshot = new OniVigorSnapshot(okp.Vigor, OnikiriPlayer.VigorMax);
                return true;
            }
            snapshot = default;
            return false;
        }

        public bool TryGetStance(Player player, out OniStanceSnapshot snapshot) {
            if (player != null && player.active && player.TryGetModPlayer(out OnikiriPlayer okp)) {
                snapshot = new OniStanceSnapshot(okp.Stance, OnikiriPlayer.StanceMax);
                return true;
            }
            snapshot = default;
            return false;
        }
    }

    /// <summary>装载期把真实数据源挂进 HUD 入口,演示源退休;卸载时退回</summary>
    internal sealed class OnikiriResourceLoader : ICWRLoader
    {
        void ICWRLoader.SetupData() {
            OnikiriResourceSource source = new();
            OniVigor.SetSource(source);
            OniStance.SetSource(source);
        }

        void ICWRLoader.UnLoadData() {
            OniVigor.SetSource(null);
            OniStance.SetSource(null);
        }
    }
}
