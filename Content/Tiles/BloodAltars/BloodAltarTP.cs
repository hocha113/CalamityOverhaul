using CalamityOverhaul.Common;
using InnoVault.TileProcessors;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace CalamityOverhaul.Content.Tiles.BloodAltars
{
    /// <summary>献祭仪式的阶段。数值会走 [SyncVar]，改动次序会破坏旧存档与联机兼容</summary>
    internal enum BloodAltarPhase : byte
    {
        Idle = 0,
        /// <summary>供奉：血浆汇入碗中</summary>
        Offering = 1,
        /// <summary>沸腾：池面翻涌，末尾定住蓄势</summary>
        Boil = 2,
        /// <summary>喷发：血柱冲天，中途定下血月</summary>
        Erupt = 3,
        /// <summary>常驻：血月被这座祭坛压住</summary>
        Active = 4,
        /// <summary>退潮：血面塌回，末帧放开夜晚</summary>
        Recede = 5,
    }

    internal class BloodAltarTP : TileProcessor, ICWRLoader, ILocalizedModType
    {
        public string LocalizationCategory => "Tiles";

        public static LocalizedText ApproachingText { get; private set; }
        public static LocalizedText InsufficientOfferingText { get; private set; }
        public static LocalizedText SacrificeDeathReason { get; private set; }

        public const int OrbCost = 50;
        public const int OfferingFrames = 35;
        public const int BoilFrames = 40;
        public const int EruptFrames = 30;
        public const int RecedeFrames = 48;
        /// <summary>喷发阶段的这一帧定下血月，也是屏幕血闪的同一帧</summary>
        public const int MoonRiseFrame = 18;

        public override int TargetTileID => ModContent.TileType<BloodAltar>();

        [SyncVar]
        public byte phaseRaw;
        [SyncVar]
        public int summonerPlayer = -1;

        /// <summary>阶段计时不同步：各端在收到阶段边沿后自行推进，演出因此不受单帧延迟影响</summary>
        public int PhaseTimer { get; private set; }
        public long AliveTime { get; private set; }
        public BloodAltarPhase Phase => (BloodAltarPhase)phaseRaw;
        /// <summary>腔口中心：血面 quad 与之重合，血柱的根在这之上的液面处</summary>
        public Vector2 BowlCenter => CenterInWorld + new Vector2(0f, BloodAltarFx.PoolOffsetY);

        /// <summary>本座祭坛是否正在压住夜晚</summary>
        public bool HoldsBloodMoon => Phase switch {
            BloodAltarPhase.Active or BloodAltarPhase.Recede => true,
            BloodAltarPhase.Erupt => PhaseTimer >= MoonRiseFrame,
            _ => false,
        };

        public bool RiteRunning => Phase is BloodAltarPhase.Offering or BloodAltarPhase.Boil or BloodAltarPhase.Erupt;

        internal int FrameIndex { get; private set; }
        internal bool HoverGlow { get; private set; }
        internal Color HoverGlowColor { get; private set; }
        /// <summary>客户端演出状态，服务端恒为 null</summary>
        internal BloodAltarRite Rite { get; private set; }

        private byte lastPhaseRaw = byte.MaxValue;
        private int glowTime;

        public override void SetStaticDefaults() {
            ApproachingText = this.GetLocalization(nameof(ApproachingText), () => "深红的注视正在降临...");
            InsufficientOfferingText = this.GetLocalization(nameof(InsufficientOfferingText), () => "你身上的血珠不够向深红之王进行朝贡...");
            SacrificeDeathReason = this.GetLocalization(nameof(SacrificeDeathReason), () => "{0}陷入了无尽的血与肉的狂想");
        }

        public override void SaveData(TagCompound tag) {
            //只存稳定态，仪式中途的某一帧重载后没有意义
            if (Phase is BloodAltarPhase.Active or BloodAltarPhase.Erupt) {
                tag["cwrBloodAltarLit"] = true;
            }
        }

        public override void LoadData(TagCompound tag) {
            if (tag.GetBool("cwrBloodAltarLit")) {
                phaseRaw = (byte)BloodAltarPhase.Active;
            }
        }

        public override void OnKill() {
            //夜晚的收场统一交给 BloodAltarWorldGuard：这里失活后就不再被算作持有者
            phaseRaw = (byte)BloodAltarPhase.Idle;
            Rite = null;
        }

        public override void Update() {
            //阶段边沿：权威端翻 phaseRaw、客户端由 SyncVar 收到，两条路都在这里收敛成一次入场
            if (lastPhaseRaw != phaseRaw) {
                lastPhaseRaw = phaseRaw;
                PhaseTimer = 0;
                OnPhaseEnter();
            }
            PhaseTimer++;
            AliveTime++;

            if (!VaultUtils.isClient) {
                AdvancePhaseOnAuthority();
                if (Phase == BloodAltarPhase.Active) {
                    DrawInLooseOrbs();
                }
            }

            UpdateFrame();
            if (!Main.dedServ) {
                UpdateHoverGlow();
                Rite ??= new BloodAltarRite();
                Rite.Tick(this);
                Lighting.AddLight(BowlCenter, Rite.LightColor);
            }
        }

        #region 交互

        /// <summary>
        /// 点火与熄灭都由点击者本地发起：血珠是他自己的背包（服务端写不了别人的背包），
        /// 因此这里只在归属客户端上生效，其它端等 [SyncVar] 到达。<br/>
        /// 阶段推进仍归权威端，跨端一致性由那条路径保证
        /// </summary>
        public override bool? RightClick(int i, int j, Tile tile, Player player) {
            //服务端与旁观客户端都会收到这次派发，但只有出血的那台机器有资格动手
            if (VaultUtils.isServer || player == null || !player.active || player.whoAmI != Main.myPlayer) {
                return true;
            }
            //仪式进行中不吃输入，免得半途重置
            if (RiteRunning || Phase == BloodAltarPhase.Recede) {
                return true;
            }

            if (Phase == BloodAltarPhase.Active) {
                SetPhase(BloodAltarPhase.Recede);
                SendData();
                return true;
            }

            if (CountOrbs(player) < OrbCost) {
                RejectOffering(player);
                return true;
            }
            ConsumeOrbs(player);
            summonerPlayer = player.whoAmI;
            SetPhase(BloodAltarPhase.Offering);
            SendData();
            return true;
        }

        private static int CountOrbs(Player player) {
            int orbNum = 0;
            foreach (Item orb in player.inventory) {
                if (orb.type == CWRID.Item_BloodOrb) {
                    orbNum += orb.stack;
                }
            }
            return orbNum;
        }

        private static void ConsumeOrbs(Player player) {
            int remaining = OrbCost;
            foreach (Item orb in player.inventory) {
                if (orb.type != CWRID.Item_BloodOrb) {
                    continue;
                }
                int take = Math.Min(orb.stack, remaining);
                orb.stack -= take;
                remaining -= take;
                if (orb.stack <= 0) {
                    orb.TurnToAir();
                }
                if (remaining <= 0) {
                    return;
                }
            }
        }

        private void RejectOffering(Player player) {
            VaultUtils.Text(InsufficientOfferingText.Value, Color.DarkRed);
            PlayerDeathReason pd = PlayerDeathReason.ByCustomReason(SacrificeDeathReason.ToNetworkText(player.name));
            player.Hurt(pd, 50, 0);
            if (!Main.dedServ) {
                BloodAltarRite.PlayRejectBeat(BowlCenter);
            }
        }

        #endregion

        #region 阶段

        private void SetPhase(BloodAltarPhase phase) {
            phaseRaw = (byte)phase;
            //本端立刻收敛，避免自己等一帧才入场
            lastPhaseRaw = phaseRaw;
            PhaseTimer = 0;
            OnPhaseEnter();
        }

        private void AdvancePhaseOnAuthority() {
            switch (Phase) {
                case BloodAltarPhase.Offering when PhaseTimer >= OfferingFrames:
                    SetPhase(BloodAltarPhase.Boil);
                    SendData();
                    break;
                case BloodAltarPhase.Boil when PhaseTimer >= BoilFrames:
                    SetPhase(BloodAltarPhase.Erupt);
                    SendData();
                    break;
                case BloodAltarPhase.Erupt when PhaseTimer >= EruptFrames:
                    SetPhase(BloodAltarPhase.Active);
                    SendData();
                    break;
                case BloodAltarPhase.Recede when PhaseTimer >= RecedeFrames:
                    SetPhase(BloodAltarPhase.Idle);
                    SendData();
                    break;
            }
        }

        private void OnPhaseEnter() {
            if (Main.dedServ) {
                return;
            }
            Rite ??= new BloodAltarRite();
            Rite.OnPhaseEnter(this);
        }

        private void UpdateFrame() {
            switch (Phase) {
                case BloodAltarPhase.Offering:
                    FrameIndex = 1;
                    break;
                case BloodAltarPhase.Boil:
                    FrameIndex = 2;
                    break;
                case BloodAltarPhase.Erupt:
                case BloodAltarPhase.Active:
                    VaultUtils.ClockFrame(ref frameClock, 6, BloodAltar.FrameCount - 1);
                    FrameIndex = frameClock;
                    break;
                case BloodAltarPhase.Recede:
                    FrameIndex = PhaseTimer < RecedeFrames / 2 ? 2 : 1;
                    break;
                default:
                    frameClock = 0;
                    FrameIndex = 0;
                    break;
            }
        }

        private int frameClock;

        private void UpdateHoverGlow() {
            //点燃后不再描边：它已经自己在发光了
            HoverGlow = HoverTP && Phase == BloodAltarPhase.Idle;
            if (!HoverGlow) {
                glowTime = 0;
                return;
            }
            glowTime++;
            HoverGlowColor = Color.Red * MathF.Abs(MathF.Sin(glowTime * 0.04f));
        }

        #endregion

        #region 血珠吸收（权威端）

        /// <summary>
        /// 血月期间把散落的血珠拖进碗里再塞进近处箱子。<br/>
        /// 物品位置与箱子内容都是服务端权威，客户端一行都不碰，只在本地画牵引表现
        /// </summary>
        private void DrawInLooseOrbs() {
            Vector2 bowl = BowlCenter;
            foreach (Item orb in Main.ActiveItems) {
                if (orb.type != CWRID.Item_BloodOrb) {
                    continue;
                }

                Vector2 toBowl = orb.Center.To(bowl);
                if (toBowl.LengthSquared() > 32f * 32f) {
                    orb.velocity = Vector2.Zero;
                    orb.position += toBowl.UnitVector() * 8f;
                    if (VaultUtils.isServer && AliveTime % 5 == 0) {
                        NetMessage.SendData(MessageID.SyncItem, number: orb.whoAmI);
                    }
                    continue;
                }

                Chest chest = Position.FindClosestChest(600, false);
                if (chest == null) {
                    //无处安放：让它停在碗口，别抖
                    orb.velocity = Vector2.Zero;
                    continue;
                }
                DepositIntoChest(chest, orb);
                orb.TurnToAir();
                if (VaultUtils.isServer) {
                    NetMessage.SendData(MessageID.SyncItem, number: orb.whoAmI);
                }
            }
        }

        /// <summary>AddItem 不回报落到哪一格，故前后比对一次再逐格同步</summary>
        private static void DepositIntoChest(Chest chest, Item orb) {
            int chestIndex = Array.IndexOf(Main.chest, chest);
            if (chest.item == null) {
                return;
            }

            int slots = chest.item.Length;
            Span<int> beforeType = slots <= 64 ? stackalloc int[slots] : new int[slots];
            Span<int> beforeStack = slots <= 64 ? stackalloc int[slots] : new int[slots];
            for (int s = 0; s < slots; s++) {
                Item slot = chest.item[s];
                beforeType[s] = slot?.type ?? 0;
                beforeStack[s] = slot?.stack ?? 0;
            }

            chest.AddItem(orb);
            Lighting.AddLight(new Vector2(chest.x, chest.y) * 16f, TorchID.Red);

            if (!VaultUtils.isServer || chestIndex < 0) {
                return;
            }
            for (int s = 0; s < slots; s++) {
                Item slot = chest.item[s];
                if ((slot?.type ?? 0) != beforeType[s] || (slot?.stack ?? 0) != beforeStack[s]) {
                    NetMessage.SendData(MessageID.SyncChestItem, number: chestIndex, number2: s);
                }
            }
        }

        #endregion

        //三层都跑在 PostDrawTiles 的同一个批里，即物块贴图之上：
        //地表血纹压最下，碗内血面在中间，血柱与供奉物在最上
        public override void BackDraw(SpriteBatch spriteBatch) => Rite?.DrawUnderAltar(spriteBatch, this);

        public override void Draw(SpriteBatch spriteBatch) => Rite?.DrawPool(spriteBatch, this);

        public override void FrontDraw(SpriteBatch spriteBatch) => Rite?.DrawOverAltar(spriteBatch, this);

        //阶段与召唤者全走 [SyncVar]：框架在 TileProcessorInstanceDoSendData 里
        //自动接在 SendData/ReceiveData 之后收发，故这里不需要手写任何字节
    }

    /// <summary>
    /// 血月的世界状态只由权威端维护：任一祭坛在持有时压住夜晚，最后一座松手时收场。<br/>
    /// 放在 ModSystem 而非 SingleInstanceUpdate 里，是因为最后一座祭坛被拆掉后
    /// SingleInstanceUpdate 会随实例数归零而停止调用，收场帧就丢了
    /// </summary>
    internal sealed class BloodAltarWorldGuard : ModSystem
    {
        private static bool heldLastTick;

        public override void OnWorldLoad() => heldLastTick = false;

        public override void OnWorldUnload() => heldLastTick = false;

        public override void PostUpdateEverything() {
            if (VaultUtils.isClient) {
                return;
            }

            bool held = AnyAltarHolding();
            if (held) {
                AssertBloodNight();
            }
            else if (heldLastTick) {
                ReleaseBloodNight();
            }
            heldLastTick = held;
        }

        private static bool AnyAltarHolding() {
            var list = TileProcessorLoader.TP_InWorld;
            //列表随时可能被顶替，倒序遍历
            for (int i = list.Count - 1; i >= 0; i--) {
                if (i >= list.Count) {
                    continue;
                }
                if (list[i] is BloodAltarTP altar && altar.Active && altar.HoldsBloodMoon) {
                    return true;
                }
            }
            return false;
        }

        private static void AssertBloodNight() {
            bool changed = false;
            //原版在 time>32400 时会把夜晚翻成白天并顺手清掉血月，这里重开一夜而不是把 time 卡在越界值上对撞
            if (Main.dayTime) {
                Main.dayTime = false;
                Main.time = 0.0;
                changed = true;
            }
            if (!Main.bloodMoon) {
                Main.bloodMoon = true;
                changed = true;
            }
            if (Main.moonPhase != 5) {
                Main.moonPhase = 5;
                changed = true;
            }
            if (changed && VaultUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }

        private static void ReleaseBloodNight() {
            Main.bloodMoon = false;
            Main.dayTime = true;
            Main.time = 0.0;
            if (VaultUtils.isServer) {
                NetMessage.SendData(MessageID.WorldData);
            }
        }
    }
}
