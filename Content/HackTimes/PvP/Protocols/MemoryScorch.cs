using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.PvP.UI;
using CalamityOverhaul.Content.PRTTypes;
using CalamityOverhaul.Content.RAMSystems;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.PvP.Protocols
{
    /// <summary>
    /// 内存烧蚀（芯片档）：落地即烧防守方 3 RAM，随后十秒回复率归零。<br/>
    /// <b>落点是 RAM——服务端拥有的资源</b>，烧蚀与封回复全在权威通道结算
    /// （<see cref="OnAuthorityGranted"/> / <see cref="OnAuthorityRevoked"/>，结算落点表 §1.4）；
    /// 防守方通道只做本机表现，不碰任何数值。防守方 HUD 的 RAM 弧随权威快照自然掉格。<br/>
    /// 封回复走既有 <see cref="IRamModifierProvider"/> 通道（PrivilegeRamSuppressor 同款）：
    /// 大负数经 RecomputeEffectiveCore 的 [0, Max] 夹取落成零回复，
    /// <b>只封自然回复，不封消费与退款</b>——防守方的回溯/卸载弹药不受影响（反挫败底线）。<br/>
    /// 授予从未转正（防守方拒绝/回执超时）时把烧掉的 RAM 全额还给防守方
    /// </summary>
    internal class MemoryScorch : PlayerHackDef
    {
        /// <summary>基础烧蚀量，落地前仍过 <see cref="HackPvPRules.ClampRamScorch"/></summary>
        internal const int ScorchAmount = 3;

        private static readonly Color Ember = new(255, 140, 60);

        /// <summary>晶粒纹：躯体旁一列内存格，顶格断裂上蹿火舌——弹药库正在烧</summary>
        internal const string Die =
            "M -0.70 -0.44 L -0.30 -0.44 M -0.70 0.44 L -0.30 0.44 "
            + "M -0.70 -0.44 Q -0.78 0 -0.70 0.44 M -0.30 -0.44 Q -0.22 0 -0.30 0.44 "
            + "M -0.22 0 L 0.10 0 "
            + "M 0.10 0.16 L 0.62 0.16 L 0.62 0.40 L 0.10 0.40 Z "
            + "M 0.10 -0.12 L 0.62 -0.12 L 0.62 0.08 L 0.10 0.08 Z "
            + "M 0.10 -0.20 L 0.34 -0.20 M 0.46 -0.26 L 0.62 -0.26 "
            + "M 0.20 -0.28 L 0.28 -0.48 L 0.36 -0.34 L 0.44 -0.58 L 0.52 -0.40";

        /// <summary>授予账载荷：实烧数额，未转正撤销时按它退还防守方</summary>
        private sealed class ScorchLedger
        {
            public float Burned;
        }

        public override void SetDefaults() {
            UploadTime = 110;
            RamCost = 4;
            Category = QuickHackCategory.Control;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 600;

        #region 权威通道（服务端写 RAM）

        public override void OnAuthorityGranted(Player caster, Player defender,
            PlayerHackGrant grant) {
            var ledger = new ScorchLedger();
            grant.AuthorityState = ledger;
            //烧蚀到多少算多少：TryConsume 全有或全无，逐级降额（ScorchRam 同款形状）
            for (int burn = HackPvPRules.ClampRamScorch(ScorchAmount); burn > 0; burn--) {
                if (RamSystem.TryConsume(defender, burn, out float paid)) {
                    ledger.Burned = paid;
                    break;
                }
            }
            MemoryScorchSeal.Begin(defender.whoAmI, grant.Duration);
        }

        public override void OnAuthorityRevoked(PlayerHackGrant grant,
            PlayerHackRemoveReason reason) {
            MemoryScorchSeal.End(grant.DefenderIndex);
            //从未落地（防守方拒绝/回执超时）→ 烧掉的 RAM 原路退还；
            //已转正的不退（合法烧过），提前拔除只解封不返款
            if (grant.Confirmed || grant.AuthorityState is not ScorchLedger ledger
                || ledger.Burned <= 0f) {
                return;
            }
            if (grant.DefenderIndex < 0 || grant.DefenderIndex >= Main.maxPlayers) return;
            Player defender = Main.player[grant.DefenderIndex];
            //槽位复用双检：名字对不上说明已经换人，不能把退款打给新占位者
            if (defender?.active != true || defender.name != grant.DefenderName) return;
            RamSystem.Restore(defender, ledger.Burned, out _);
        }

        #endregion

        #region 防守方通道（纯表现，数值一个不碰）

        public override bool OnDefenderApply(Player defender, PlayerHackEffect effect) {
            SoundEngine.PlaySound(CWRSound.Hacker with { Volume = 0.75f, Pitch = -0.2f },
                defender.Center);
            for (int i = 0; i < 12; i++) {
                Vector2 pos = defender.Center + new Vector2(
                    Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-20f, 4f));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(Main.rand.NextFloat(-0.7f, 0.7f),
                        Main.rand.NextFloat(-2.4f, -0.8f)),
                    Ember, Main.rand.NextFloat(0.6f, 1f))?.Configure(false, 22);
            }
            return true;
        }

        public override void DrawDefenderOverlay(SpriteBatch spriteBatch, Player defender,
            PlayerHackEffect effect) {
            //资源区旁一枚角标：告知"慢"是被封不是掉帧（卡操作投诉三铁律其二）
            HackTheme.DrawBadge(spriteBatch,
                new Vector2(HackTheme.UIScreenW - 118f, 78f),
                SealTag.Value, PvPTheme.HostileAlt, 0.9f);
        }

        #endregion

        //各端表现：防守方身上零星飘烧蚀余烬（密度随剩余时间衰减）
        public override void OnSpectatorTick(Player defender, int casterIndex,
            int elapsed, int duration) {
            if (Main.dedServ || duration <= 0 || !Main.rand.NextBool(14)) return;
            float life = 1f - elapsed / (float)duration;
            Vector2 pos = defender.Center + Main.rand.NextVector2Circular(16f, 22f);
            PRTLoader.NewParticle<PRT_Spark>(pos,
                new Vector2(0f, Main.rand.NextFloat(-1.6f, -0.6f)),
                Ember * (0.4f + life * 0.6f), 0.5f + life * 0.3f)?.Configure(false, 18);
        }

        internal Terraria.Localization.LocalizedText SealTag
            => this.GetLocalization(nameof(SealTag), () => "RAM SEALED");

        public override string GlyphDiePath => Die;
    }

    /// <summary>
    /// 内存烧蚀的封回复通道。照 PrivilegeRamSuppressor 的既有形状：
    /// IRamModifierProvider + ICWRLoader 自持注册，大负数回复加成经
    /// RecomputeEffectiveCore 的 [0, Max] 夹取落成零回复，不动任何现有文件。<br/>
    /// 记账是<b>服务端世界级</b>的 per-player 计数 + 到期帧双保险：计数由授予/撤销对称驱动
    /// （每条授予必经 Revoke 退场），到期帧兜住任何漏减——残留封印活不过自己的时长
    /// </summary>
    internal sealed class MemoryScorchSeal : IRamModifierProvider, ICWRLoader
    {
        //并发烧蚀（多攻击方对同一防守方）用计数叠，boundFrame 只是泄漏保险
        private static readonly int[] sealCount = new int[Main.maxPlayers];
        private static readonly ulong[] boundFrame = new ulong[Main.maxPlayers];

        public int MaxRamBonus => 0;

        public float RecoveryRateBonus => -10000f;

        public bool IsActive(Player player) {
            int index = player?.whoAmI ?? -1;
            if (index < 0 || index >= Main.maxPlayers) return false;
            return sealCount[index] > 0 && Main.GameUpdateCount < boundFrame[index];
        }

        internal static void Begin(int defenderIndex, int durationFrames) {
            if (defenderIndex < 0 || defenderIndex >= Main.maxPlayers) return;
            sealCount[defenderIndex]++;
            //+60f 松弛：看门狗宽限内的迟到撤销不该让封印先漏气
            ulong bound = Main.GameUpdateCount + (ulong)Math.Max(durationFrames, 0) + 60;
            if (bound > boundFrame[defenderIndex]) boundFrame[defenderIndex] = bound;
        }

        internal static void End(int defenderIndex) {
            if (defenderIndex < 0 || defenderIndex >= Main.maxPlayers) return;
            if (sealCount[defenderIndex] > 0) sealCount[defenderIndex]--;
            if (sealCount[defenderIndex] == 0) boundFrame[defenderIndex] = 0;
        }

        internal static void Reset() {
            Array.Clear(sealCount);
            Array.Clear(boundFrame);
        }

        void ICWRLoader.LoadData() => RamSystem.RegisterProvider(this);

        void ICWRLoader.UnLoadData() {
            RamSystem.UnregisterProvider(this);
            Reset();
        }
    }

    /// <summary>
    /// 封印账的世界级清账。PlayerHackAuthority.Reset 清授予账时不走 Revoke，
    /// 计数不自清就会漏进下一个世界
    /// </summary>
    internal sealed class MemoryScorchSealSystem : ModSystem
    {
        public override void OnWorldUnload() => MemoryScorchSeal.Reset();
    }
}
