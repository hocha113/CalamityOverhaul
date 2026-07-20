using CalamityOverhaul.Content.HackTimes.Targets;
using System;
using Terraria;

namespace CalamityOverhaul.Content.HackTimes.Scannables
{
    /// <summary>NPC 扫描 + IHackTarget</summary>
    internal class NpcScannable : IHackTarget
    {
        public int NpcIndex { get; }

        public NpcScannable(int npcIndex) {
            NpcIndex = npcIndex;
        }

        #region IScannable

        public Vector2 WorldCenter {
            get {
                if (NpcIndex < 0 || NpcIndex >= Main.maxNPCs) return Vector2.Zero;
                return Main.npc[NpcIndex].Center;
            }
        }

        public bool IsValid {
            get {
                if (NpcIndex < 0 || NpcIndex >= Main.maxNPCs) return false;
                return Main.npc[NpcIndex].active;
            }
        }

        public bool IsHackable => true;

        public int ScanRowCount => 6;

        public void BuildScanData(string[] labels, string[] values, Color[] colors) {
            if (NpcIndex < 0 || NpcIndex >= Main.maxNPCs) return;
            NPC npc = Main.npc[NpcIndex];
            if (!npc.active) return;

            //TYPE
            labels[0] = HackTime.TypeLabel.Value;
            if (npc.townNPC) {
                values[0] = HackTime.TownNpc.Value;
                colors[0] = HackTheme.Accent;
            }
            else if (npc.CountsAsACritter) {
                values[0] = HackTime.PassiveCritter.Value;
                colors[0] = HackTheme.Accent;
            }
            else if (npc.friendly) {
                values[0] = HackTime.FriendlyUnit.Value;
                colors[0] = HackTheme.AccentAlt;
            }
            else if (npc.boss) {
                values[0] = HackTime.BossClass.Value;
                colors[0] = HackTheme.Danger;
            }
            else if (npc.lifeMax > 5000) {
                values[0] = HackTime.EliteUnit.Value;
                colors[0] = HackTheme.Uploading;
            }
            else if (npc.damage <= 0) {
                values[0] = HackTime.NeutralEntity.Value;
                colors[0] = HackTheme.TextDim;
            }
            else {
                values[0] = HackTime.HostileEntity.Value;
                colors[0] = HackTheme.TextBright;
            }

            //THREAT，相对玩家生命/防御/减伤
            float relThreat = ComputeRelativeThreat(npc);
            labels[1] = HackTime.ThreatLabel.Value;
            if (relThreat >= 40f) {
                values[1] = HackTime.ThreatExtreme.Value;
                colors[1] = HackTheme.Danger;
            }
            else if (relThreat >= 20f) {
                values[1] = HackTime.ThreatHigh.Value;
                colors[1] = HackTheme.Uploading;
            }
            else if (relThreat >= 8f) {
                values[1] = HackTime.ThreatModerate.Value;
                colors[1] = HackTheme.AccentAlt;
            }
            else {
                values[1] = HackTime.ThreatLow.Value;
                colors[1] = HackTheme.Accent;
            }

            //HP
            labels[2] = "HP";
            values[2] = $"{npc.life:N0} / {npc.lifeMax:N0}";
            float hpPct = (float)npc.life / Math.Max(npc.lifeMax, 1);
            colors[2] = hpPct > 0.5f ? HackTheme.Accent
                : hpPct > 0.25f ? HackTheme.Uploading : HackTheme.Danger;

            //DEF
            labels[3] = HackTime.DefLabel.Value;
            values[3] = $"{npc.defense}";
            colors[3] = HackTheme.TextBright;

            //DMG
            labels[4] = HackTime.DmgLabel.Value;
            values[4] = $"{npc.damage}";
            colors[4] = HackTheme.TextBright;

            //KB.RES
            labels[5] = HackTime.KbResLabel.Value;
            values[5] = $"{npc.knockBackResist:F2}";
            colors[5] = npc.knockBackResist >= 0.9f ? HackTheme.Danger
                : npc.knockBackResist >= 0.5f ? HackTheme.Uploading : HackTheme.TextBright;
        }

        private static bool IsNonCombatNpc(NPC npc) {
            return npc.townNPC || npc.friendly || npc.CountsAsACritter;
        }

        /// <summary>相对玩家的威胁值，扫描行与档案星级共用</summary>
        public static float ComputeRelativeThreat(NPC npc) {
            if (IsNonCombatNpc(npc)) return 0f;
            Player localPlayer = Main.LocalPlayer;
            float playerDR = Math.Clamp(localPlayer.endurance, 0f, 0.99f);
            //有效单次伤害
            float effectiveDmg = Math.Max(1f, npc.damage - localPlayer.statDefense * 0.5f) * (1f - playerDR);
            //单次命中 HP 占比
            float hitImpact = effectiveDmg / Math.Max(localPlayer.statLifeMax, 1);
            //HP 比取 log2 压缩 Boss 数值
            float hpRatio = (float)npc.lifeMax / Math.Max(localPlayer.statLifeMax, 1);
            float durabilityIndex = MathF.Log2(1f + hpRatio);
            //NPC自身防御系数
            float defenseIndex = npc.defense / 50f;
            return hitImpact * 50f + durabilityIndex * 5f + defenseIndex * 5f;
        }

        /// <summary>威胁星级 0..5，档案面板菱形刻度用</summary>
        public static int ComputeThreatPips(NPC npc) {
            float t = ComputeRelativeThreat(npc);
            if (t >= 40f) return 5;
            if (t >= 20f) return 4;
            if (t >= 8f) return 3;
            if (t >= 3f) return 2;
            return t > 0f ? 1 : 0;
        }

        #endregion

        #region IHackTarget

        public HackTargetType TargetType => HackTargetType.Get<NpcTargetType>();

        public Vector2 LockFrameHalfSize {
            get {
                if (!IsValid) return Vector2.Zero;
                NPC npc = Main.npc[NpcIndex];
                return new Vector2(
                    Math.Max(npc.width, 32) * 0.6f + 28f,
                    Math.Max(npc.height, 32) * 0.6f + 28f);
            }
        }

        public string LockFrameTitle => IsValid ? Main.npc[NpcIndex].FullName : string.Empty;

        public bool TryGetLockFrameStatus(out string text, out Color color) {
            text = null;
            color = default;
            if (!IsValid) return false;
            NPC npc = Main.npc[NpcIndex];
            if (npc.lifeMax <= 0) return false;

            float hpPct = (float)npc.life / npc.lifeMax;
            text = HackTime.HpFormat.Format((int)(hpPct * 100));
            color = hpPct > 0.5f ? HackTheme.AccentAlt
                : hpPct > 0.25f ? HackTheme.Uploading : HackTheme.Danger;
            return true;
        }

        public bool ApplyHack(QuickHackDef hack, Player caster) {
            //NPC 协议走效果追踪器
            int casterIndex = caster?.whoAmI ?? Main.myPlayer;
            return HackEffectTracker.ApplyNpcEffect(hack, NpcIndex, casterIndex) != null;
        }

        public bool TargetEquals(IHackTarget other) {
            return other is NpcScannable n && n.NpcIndex == NpcIndex;
        }

        #endregion
    }
}
