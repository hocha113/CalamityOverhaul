using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.PvP.UI;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 弹道倒戈（芯片档）：防守方接下来三发主动射出的弹幕出膛后调头，只追打它的原主。<br/>
    /// <b>红线：这条不剥夺操作</b>——防守方随时可以停火，停火即安全；召唤物/哨兵/持握弹
    /// 一律豁免（持续源头会瞬间烧完额度，且玩家没按键）。条目描述必须写明停火可解。<br/>
    /// "接下来三发"的计数在<b>防守方本机</b>（他开火他计数）：
    /// <see cref="BallisticTurncoatProjectile.OnSpawn"/> 在生成包发出前打标记
    /// （HackConvertedProjectile 的既有形状），标记走 ExtraAI 随首包到达各端，
    /// 各端各自压平阵营旗并做确定性回转；命中判定与自伤只在防守方本机跑
    /// （PvP 生命归属方写），伤害额度经 <see cref="HackPvPRules.ClampLifeDamage"/>
    /// 在打标一刻锁死
    /// </summary>
    internal class BallisticTurncoat : PlayerHackDef
    {
        /// <summary>被劫持的弹幕数</summary>
        internal const int MaxShots = 3;
        /// <summary>回击伤害 = 原伤害的一半（再进全程 120 预算）</summary>
        internal const float ReturnDamageRatio = 0.5f;

        /// <summary>晶粒纹：躯体射出的弹道走到半途画一个大回环，箭头调头指回躯体</summary>
        internal const string Die =
            "M -0.72 -0.34 L -0.34 -0.34 M -0.72 0.46 L -0.34 0.46 "
            + "M -0.72 -0.34 Q -0.80 0.06 -0.72 0.46 M -0.34 -0.34 Q -0.26 0.06 -0.34 0.46 "
            + "M -0.28 -0.10 L 0.18 -0.10 "
            + "M 0.18 -0.10 Q 0.62 -0.10 0.62 0.16 Q 0.62 0.42 0.18 0.42 "
            + "M 0.18 0.42 L -0.12 0.42 M -0.12 0.42 L 0.02 0.32 M -0.12 0.42 L 0.02 0.52 "
            + "M -0.02 -0.18 L 0.06 -0.18 M -0.14 -0.18 L -0.10 -0.18";

        /// <summary>防守方侧 per-effect 状态：剩余额度与已锁定的回击伤害预算</summary>
        internal sealed class TurncoatState
        {
            public int Remaining = MaxShots;
            /// <summary>已锁定的回击伤害合计（ClampLifeDamage 的 alreadyDealt 输入）</summary>
            public int PlannedDamage;
        }

        public override void SetDefaults() {
            UploadTime = 150;
            RamCost = 5;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 300;

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            effect.ProtocolState = new TurncoatState();
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.8f, Pitch = -0.35f },
                defender.Center);
            return true;
        }

        //额度耗尽提前收账（"3 发先到"分支；到 300f 自然到期是另一分支）
        public override bool OnDefenderTick(Player defender, PlayerHackEffect effect)
            => effect.ProtocolState is not TurncoatState state || state.Remaining > 0;

        /// <summary>
        /// 防守方本机打标入口（<see cref="BallisticTurncoatProjectile.OnSpawn"/> 调进来）。
        /// 消耗一发额度并在此刻锁死回击伤害——弹幕落地可能在效果到期之后，
        /// 预算在这里结不在命中时结，账才不会跨过效果生命周期漂移
        /// </summary>
        internal static bool TryMark(Projectile projectile, out int casterIndex,
            out string casterName, out int returnDamage) {
            casterIndex = -1;
            casterName = string.Empty;
            returnDamage = 0;
            if (Main.dedServ || projectile.owner != Main.myPlayer
                || Main.LocalPlayer?.active != true) {
                return false;
            }
            var ledger = Main.LocalPlayer.GetModPlayer<PlayerHackLedger>();
            for (int i = 0; i < ledger.ActiveEffects.Count; i++) {
                PlayerHackEffect effect = ledger.ActiveEffects[i];
                if (effect.Hack is not BallisticTurncoat
                    || effect.ProtocolState is not TurncoatState state
                    || state.Remaining <= 0) {
                    continue;
                }
                state.Remaining--;
                int half = Math.Max((int)(projectile.damage * ReturnDamageRatio), 1);
                returnDamage = HackPvPRules.ClampLifeDamage(half, state.PlannedDamage);
                state.PlannedDamage += returnDamage;
                casterIndex = effect.CasterIndex;
                casterName = effect.CasterName;
                return true;
            }
            return false;
        }

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            if (effect.ProtocolState is not TurncoatState state) return;
            //准星旁计数：开火决策发生在准星上，警示就贴在准星旁。
            //UIHandle 批（InterfaceScaleType.UI）下 Main.mouseX/Y 已是 UI 空间，
            //MouseScreen 直接可用——再除 UIScale 会双重缩放（口径同 PlayerHackHud）
            Vector2 anchor = Main.MouseScreen + new Vector2(26f, 22f);
            HackTheme.DrawBadge(spriteBatch, anchor,
                HijackCounter.Format(state.Remaining, MaxShots), PvPTheme.Hostile, 0.95f);
        }

        internal LocalizedText HijackCounter
            => this.GetLocalization(nameof(HijackCounter), () => "HIJACKED {0}/{1}");

        internal LocalizedText DeathReason
            => this.GetLocalization(nameof(DeathReason),
                () => "{0}'s shot was turned by {1} and came home.");

        public override string GlyphDiePath => Die;
    }
}
