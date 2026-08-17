using CalamityOverhaul.Content.Cyberwares;
using CalamityOverhaul.Content.Cyberwares.Implementation.Sandevistans;
using CalamityOverhaul.Content.HackTimes.SelfRigs;
using CalamityOverhaul.Content.HackTimes.Targets;
using CalamityOverhaul.Content.RAMSystems;
using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using System;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 自体扫描目标：目标恒为玩家本人。<br/>
    /// 悬停探测只会产出本机玩家（<see cref="Targets.SelfRigTargetType"/>），
    /// 但复制端会拿着施术者索引重建实例，所以这里按任意玩家索引实现，
    /// 各协议再用 <c>PlayerIndex == Main.myPlayer</c> 区分拥有者与旁观者
    /// </summary>
    internal class SelfRigScannable : IHackTarget
    {
        public int PlayerIndex { get; }

        public SelfRigScannable(int playerIndex) {
            PlayerIndex = playerIndex;
        }

        /// <summary>索引合法且玩家在场，取不到就 null</summary>
        internal Player ResolvePlayer() {
            if (PlayerIndex < 0 || PlayerIndex >= Main.maxPlayers) return null;
            Player player = Main.player[PlayerIndex];
            return player?.active == true ? player : null;
        }

        #region IScannable

        public Vector2 WorldCenter => ResolvePlayer()?.Center ?? Vector2.Zero;

        public bool IsValid {
            get {
                Player player = ResolvePlayer();
                return player != null && !player.dead && !player.ghost;
            }
        }

        public bool IsHackable => true;

        public int ScanRowCount => 9;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            Player player = ResolvePlayer();
            if (player == null) return;

            //1 RAM 当前/上限
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
            labels[0] = SelfRigScanText.RamLabel.Value;
            values[0] = $"{ram.DisplayCurrent} / {ram.MaxRam}";
            colors[0] = ram.IsLocked ? HackTheme.Danger
                : ram.Ratio > 0.5f ? HackTheme.Accent
                : ram.Ratio > 0.25f ? HackTheme.Uploading : HackTheme.Danger;

            //2 回复率，被神经超频压零时标红
            labels[1] = SelfRigScanText.RecoveryLabel.Value;
            values[1] = $"{ram.RecoveryRate:0.00}/s";
            colors[1] = ram.RecoveryRate <= 0.001f ? HackTheme.Danger : HackTheme.TextBright;

            //3 义体容量
            CyberwarePlayer cyber = player.GetModPlayer<CyberwarePlayer>();
            labels[2] = SelfRigScanText.CyberCapacityLabel.Value;
            values[2] = $"{cyber.UsedCapacity} / {cyber.MaxCapacity}";
            colors[2] = cyber.UsedCapacity >= cyber.MaxCapacity
                ? HackTheme.Uploading : HackTheme.TextBright;

            //4 已装义体数
            labels[3] = SelfRigScanText.ImplantCountLabel.Value;
            values[3] = $"{CountImplants(cyber)}";
            colors[3] = HackTheme.TextBright;

            //5 手持存电
            labels[4] = SelfRigScanText.HeldChargeLabel.Value;
            Item held = player.HeldItem;
            if (held?.IsAir == false && held.CWR()?.StorageUE == true) {
                values[4] = $"{(int)held.CWR().UEValue} UE";
                colors[4] = held.CWR().UEValue >= SelfRigPlayer.TransmuteUEPerRam
                    ? HackTheme.Accent : HackTheme.TextDim;
            }
            else {
                values[4] = SelfRigScanText.NoneText.Value;
                colors[4] = HackTheme.TextDim;
            }

            //6 Sandevistan 冷却
            labels[5] = SelfRigScanText.SandeCooldownLabel.Value;
            SandevistanPlayer sande = Sandevistan.GetState(player);
            if (sande != null && sande.HasValidEquipment) {
                values[5] = $"{(int)sande.CurrentCooldown} / {(int)sande.MaxCooldown}";
                colors[5] = sande.IsActive ? HackTheme.Uploading : HackTheme.AccentAlt;
            }
            else {
                values[5] = SelfRigScanText.NoneText.Value;
                colors[5] = HackTheme.TextDim;
            }

            //7 盘上最凶那只役鬼与复苏度；休眠中改显示剩余秒数
            labels[6] = SelfRigScanText.WraithLabel.Value;
            WraithPlayer wraith = player.GetModPlayer<WraithPlayer>();
            SelfRigPlayer rig = player.GetModPlayer<SelfRigPlayer>();
            string key = wraith.HighestRevivalKey;
            if (rig.DormantFrames > 0 && !string.IsNullOrEmpty(rig.DormantKey)) {
                values[6] = SelfRigScanText.DormantFormat.Format(
                    (rig.DormantFrames + 59) / 60);
                colors[6] = HackTheme.Danger;
            }
            else if (!string.IsNullOrEmpty(key)
                && WraithRegistry.TryGetUsable(key, out WraithDefinition def)) {
                float revival = wraith.GetRevival(key);
                values[6] = $"{def.DisplayName.Value} {(int)(revival * 100)}%";
                colors[6] = revival >= WraithPlayer.RevivalDangerLine
                    ? HackTheme.Danger : HackTheme.TextBright;
            }
            else {
                values[6] = SelfRigScanText.NoneText.Value;
                colors[6] = HackTheme.TextDim;
            }

            //8 侵蚀阶
            labels[7] = SelfRigScanText.ErosionLabel.Value;
            values[7] = $"{(int)(wraith.Erosion * 100)}% · T{wraith.ErosionTier}";
            colors[7] = wraith.ErosionTier >= 2 ? HackTheme.Danger
                : wraith.ErosionTier == 1 ? HackTheme.Uploading : HackTheme.TextBright;

            //9 已持有协议数
            labels[8] = SelfRigScanText.ProtocolLabel.Value;
            values[8] = $"{player.GetModPlayer<HackTimePlayer>().OwnedProtocols.Count}"
                + $" / {QuickHackDef.Count}";
            colors[8] = HackTheme.AccentAlt;
        }

        private static int CountImplants(CyberwarePlayer cyber) {
            Item[] equipped = cyber?.EquippedCyberwares;
            if (equipped == null) return 0;
            int count = 0;
            for (int i = 0; i < equipped.Length; i++) {
                if (equipped[i]?.IsAir == false) count++;
            }
            return count;
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<SelfRigTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                Player player = ResolvePlayer();
                if (player == null) return Vector2.Zero;
                return new Vector2(
                    Math.Max(player.width, 32) * 0.6f + 26f,
                    Math.Max(player.height, 48) * 0.6f + 26f);
            }
        }

        public string LockFrameTitle => ResolvePlayer()?.name ?? string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            Player player = ResolvePlayer();
            if (player == null) return false;
            RAMPlayer ram = player.GetModPlayer<RAMPlayer>();
            if (ram.MaxRam <= 0) return false;
            text = SelfRigScanText.RamLockFormat.Format((int)(ram.Ratio * 100));
            color = ram.Ratio > 0.5f ? HackTheme.AccentAlt
                : ram.Ratio > 0.25f ? HackTheme.Uploading : HackTheme.Danger;
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            //自我目标恒等：只能骇自己
            if (casterIndex != PlayerIndex) return false;
            return HackEffectTracker.ApplyAuthorityEffect(hack, this, casterIndex,
                0, 0, 0f, 0) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is SelfRigScannable s && s.PlayerIndex == PlayerIndex;
        }

        #endregion
    }

    /// <summary>自体扫描面板行文本；键位于 UI 主题文件的 SelfRigScanText 段</summary>
    internal class SelfRigScanText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText RamLabel { get; private set; }
        public static LocalizedText RecoveryLabel { get; private set; }
        public static LocalizedText CyberCapacityLabel { get; private set; }
        public static LocalizedText ImplantCountLabel { get; private set; }
        public static LocalizedText HeldChargeLabel { get; private set; }
        public static LocalizedText SandeCooldownLabel { get; private set; }
        public static LocalizedText WraithLabel { get; private set; }
        public static LocalizedText ErosionLabel { get; private set; }
        public static LocalizedText ProtocolLabel { get; private set; }
        public static LocalizedText NoneText { get; private set; }
        public static LocalizedText RamLockFormat { get; private set; }
        public static LocalizedText DormantFormat { get; private set; }
        public static LocalizedText TransmuteGainFormat { get; private set; }
        public static LocalizedText DriveSettleText { get; private set; }

        public override void SetStaticDefaults() {
            RamLabel = this.GetLocalization(nameof(RamLabel), () => "RAM");
            RecoveryLabel = this.GetLocalization(nameof(RecoveryLabel), () => "RECOVERY");
            CyberCapacityLabel = this.GetLocalization(nameof(CyberCapacityLabel), () => "CYBERWARE");
            ImplantCountLabel = this.GetLocalization(nameof(ImplantCountLabel), () => "IMPLANTS");
            HeldChargeLabel = this.GetLocalization(nameof(HeldChargeLabel), () => "HELD CHARGE");
            SandeCooldownLabel = this.GetLocalization(nameof(SandeCooldownLabel), () => "SANDE CD");
            WraithLabel = this.GetLocalization(nameof(WraithLabel), () => "WRAITH");
            ErosionLabel = this.GetLocalization(nameof(ErosionLabel), () => "EROSION");
            ProtocolLabel = this.GetLocalization(nameof(ProtocolLabel), () => "PROTOCOLS");
            NoneText = this.GetLocalization(nameof(NoneText), () => "—");
            RamLockFormat = this.GetLocalization(nameof(RamLockFormat), () => "RAM {0}%");
            DormantFormat = this.GetLocalization(nameof(DormantFormat), () => "DORMANT {0}s");
            TransmuteGainFormat = this.GetLocalization(nameof(TransmuteGainFormat), () => "+{0} RAM");
            DriveSettleText = this.GetLocalization(nameof(DriveSettleText), () => "Erosion deepens; wraith dormant");
        }
    }
}
