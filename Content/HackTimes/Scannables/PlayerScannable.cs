using CalamityOverhaul.Content.HackTimes.PvP;
using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>
    /// 敌对玩家扫描目标（PvP）。行分两级：<b>本地已知</b>（原版全量同步，零成本）
    /// 与<b>探针行</b>（服务端才知道，选中时经 ScanProbe 限频拉取，1 次/60f）。
    /// 探针刻意降精度（RAM 只给段位）——侦察给的是态势不是仪表读数。<br/>
    /// <b>扫描静默</b>：防守方不知道被扫；上传才吵（DefenderNotice → 被骇横幅）
    /// </summary>
    internal class PlayerScannable : IHackTarget
    {
        public int PlayerIndex { get; }

        public PlayerScannable(int playerIndex) {
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

        public bool IsHackable {
            get {
                Player target = ResolvePlayer();
                if (target == null) return false;
                //服务端上下文没有"本机攻击方"可言，只做结构校验；
                //成对准入由 ValidateAuthorityRequest 的 Player 分支全量重验
                if (Main.netMode == NetmodeID.Server) {
                    return target.hostile;
                }
                return HackPvPRules.CanTarget(Main.LocalPlayer, target, out _);
            }
        }

        public int ScanRowCount => 8;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            Player player = ResolvePlayer();
            if (player == null) return;

            //选中期间保持探针新鲜（客户端与服务端各有 60f 限频，重复调用无害）
            PlayerHackMirror.RequestProbe(PlayerIndex);
            PlayerProbeData? probe = PlayerHackMirror.GetProbe(PlayerIndex);
            bool fresh = probe?.IsFresh == true;
            string probing = PlayerScanText.ProbingText.Value;

            //1 身份：玩家名 + 队伍色
            labels[0] = PlayerScanText.NameLabel.Value;
            values[0] = player.name;
            colors[0] = player.team > 0 && player.team < Main.teamColor.Length
                ? Main.teamColor[player.team] : HackTheme.TextBright;

            //2 敌我态：PvP 开关与队伍关系（本地已知）
            labels[1] = PlayerScanText.StanceLabel.Value;
            bool hackable = IsHackable;
            if (!player.hostile) {
                values[1] = PlayerScanText.StancePeaceful.Value;
                colors[1] = HackTheme.TextDim;
            }
            else if (Main.LocalPlayer.team != 0
                && Main.LocalPlayer.team == player.team) {
                values[1] = PlayerScanText.StanceAlly.Value;
                colors[1] = HackTheme.AccentAlt;
            }
            else {
                values[1] = hackable
                    ? PlayerScanText.StanceHostile.Value
                    : PlayerScanText.StanceSealed.Value;
                colors[1] = hackable ? HackTheme.Danger : HackTheme.TextDim;
            }

            //3 HP（msg 16 全员可见）
            labels[2] = PlayerScanText.HpLabel.Value;
            values[2] = $"{player.statLife} / {player.statLifeMax2}";
            float hpRatio = player.statLifeMax2 > 0
                ? player.statLife / (float)player.statLifeMax2 : 0f;
            colors[2] = hpRatio > 0.5f ? HackTheme.TextBright
                : hpRatio > 0.25f ? HackTheme.Uploading : HackTheme.Danger;

            //4 防御（探针：远端客户端对其他玩家不完整跑装备重算，本地值不可信）
            labels[3] = PlayerScanText.DefenseLabel.Value;
            values[3] = fresh ? $"{probe.Value.Defense}" : probing;
            colors[3] = fresh ? HackTheme.TextBright : HackTheme.TextDim;

            //5 RAM 段位（探针，5 档；权威只回发本人是总线契约，这行是服务端转述）
            labels[4] = PlayerScanText.RamLabel.Value;
            values[4] = fresh ? RamBandText(probe.Value.RamBand) : probing;
            colors[4] = !fresh ? HackTheme.TextDim
                : probe.Value.RamBand <= 1 ? HackTheme.Danger
                : probe.Value.RamBand >= 3 ? HackTheme.Accent : HackTheme.Uploading;

            //6 义体概览：已装数量 / 防火墙检出（探针；装配表服务端拥有）
            labels[5] = PlayerScanText.CyberLabel.Value;
            values[5] = !fresh ? probing
                : probe.Value.FirewallDetected
                    ? PlayerScanText.CyberFirewallFormat.Format(probe.Value.ImplantCount)
                    : $"{probe.Value.ImplantCount}";
            colors[5] = fresh && probe.Value.FirewallDetected
                ? HackTheme.Uploading : fresh ? HackTheme.TextBright : HackTheme.TextDim;

            //7 协议库：持有协议数（探针；OwnedSnapshot 服务端有副本）
            labels[6] = PlayerScanText.ProtocolLabel.Value;
            values[6] = fresh ? $"{probe.Value.ProtocolCount}" : probing;
            colors[6] = fresh ? HackTheme.AccentAlt : HackTheme.TextDim;

            //8 威胁评估：星级（本地 HP/进度 + 探针防御合成）
            labels[7] = PlayerScanText.ThreatLabel.Value;
            int stars = ComputeThreatStars(player,
                fresh ? probe.Value.Defense : -1,
                fresh ? probe.Value.ImplantCount : 0);
            values[7] = new string('★', stars) + new string('☆', 5 - stars);
            colors[7] = stars >= 4 ? HackTheme.Danger
                : stars >= 3 ? HackTheme.Uploading : HackTheme.TextNormal;
        }

        private static string RamBandText(byte band) => band switch {
            0 => PlayerScanText.RamBandEmpty.Value,
            1 => PlayerScanText.RamBandLow.Value,
            2 => PlayerScanText.RamBandHalf.Value,
            3 => PlayerScanText.RamBandHigh.Value,
            _ => PlayerScanText.RamBandFull.Value,
        };

        /// <summary>玩家变体的威胁星级：血量/防御/义体粗合成，1..5</summary>
        private static int ComputeThreatStars(Player player, int probedDefense,
            int implants) {
            float score = player.statLifeMax2 / 120f;
            if (probedDefense >= 0) score += probedDefense / 18f;
            score += implants * 0.5f;
            return Math.Clamp((int)(score * 0.5f) + 1, 1, 5);
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<PlayerTargetType>();

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
            if (player == null || player.statLifeMax2 <= 0) return false;
            int percent = (int)(player.statLife * 100f / player.statLifeMax2);
            text = HackTime.HpFormat.Format(percent);
            color = percent > 50 ? HackTheme.AccentAlt
                : percent > 25 ? HackTheme.Uploading : HackTheme.Danger;
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            //玩家效果没有本机直施通道：一切走服务端授予 → DefenderApply 管线
            //（接口成员当前全家无调用方，这里是防御性堵死）
            return false;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is PlayerScannable p && p.PlayerIndex == PlayerIndex;
        }

        #endregion
    }

    /// <summary>玩家扫描面板行文本</summary>
    internal class PlayerScanText : ModSystem, ILocalizedModType
    {
        public string LocalizationCategory => "UI";

        public static LocalizedText NameLabel { get; private set; }
        public static LocalizedText StanceLabel { get; private set; }
        public static LocalizedText StancePeaceful { get; private set; }
        public static LocalizedText StanceAlly { get; private set; }
        public static LocalizedText StanceHostile { get; private set; }
        public static LocalizedText StanceSealed { get; private set; }
        public static LocalizedText HpLabel { get; private set; }
        public static LocalizedText DefenseLabel { get; private set; }
        public static LocalizedText RamLabel { get; private set; }
        public static LocalizedText RamBandEmpty { get; private set; }
        public static LocalizedText RamBandLow { get; private set; }
        public static LocalizedText RamBandHalf { get; private set; }
        public static LocalizedText RamBandHigh { get; private set; }
        public static LocalizedText RamBandFull { get; private set; }
        public static LocalizedText CyberLabel { get; private set; }
        public static LocalizedText CyberFirewallFormat { get; private set; }
        public static LocalizedText ProtocolLabel { get; private set; }
        public static LocalizedText ThreatLabel { get; private set; }
        public static LocalizedText ProbingText { get; private set; }

        public override void SetStaticDefaults() {
            NameLabel = this.GetLocalization(nameof(NameLabel), () => "OPERATOR");
            StanceLabel = this.GetLocalization(nameof(StanceLabel), () => "STANCE");
            StancePeaceful = this.GetLocalization(nameof(StancePeaceful), () => "PVP OFF");
            StanceAlly = this.GetLocalization(nameof(StanceAlly), () => "SAME TEAM");
            StanceHostile = this.GetLocalization(nameof(StanceHostile), () => "HOSTILE");
            StanceSealed = this.GetLocalization(nameof(StanceSealed), () => "SEALED");
            HpLabel = this.GetLocalization(nameof(HpLabel), () => "VITALS");
            DefenseLabel = this.GetLocalization(nameof(DefenseLabel), () => "DEFENSE");
            RamLabel = this.GetLocalization(nameof(RamLabel), () => "RAM BAND");
            RamBandEmpty = this.GetLocalization(nameof(RamBandEmpty), () => "EMPTY");
            RamBandLow = this.GetLocalization(nameof(RamBandLow), () => "LOW");
            RamBandHalf = this.GetLocalization(nameof(RamBandHalf), () => "HALF");
            RamBandHigh = this.GetLocalization(nameof(RamBandHigh), () => "HIGH");
            RamBandFull = this.GetLocalization(nameof(RamBandFull), () => "FULL");
            CyberLabel = this.GetLocalization(nameof(CyberLabel), () => "CYBERWARE");
            CyberFirewallFormat = this.GetLocalization(nameof(CyberFirewallFormat),
                () => "{0} · FIREWALL");
            ProtocolLabel = this.GetLocalization(nameof(ProtocolLabel), () => "PROTOCOLS");
            ThreatLabel = this.GetLocalization(nameof(ThreatLabel), () => "THREAT");
            ProbingText = this.GetLocalization(nameof(ProbingText), () => "PROBING…");
        }
    }
}
