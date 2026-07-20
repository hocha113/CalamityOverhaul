using CalamityOverhaul.Content.UIs.EntryDecisions;
using InnoVault.GameSystem;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.QuestLogs
{
    /// <summary>
    /// 跨世界进入时的任务检测入世决策；本地单例，回答或世界切换后释放
    /// <br/><see cref="IsPending"/> 供 <see cref="QLPlayer.PostUpdate"/> 在未回答期间暂停任务更新
    /// </summary>
    internal sealed class QuestWorldDecision : EntryDecision
    {
        private static QuestWorldDecision current;

        /// <summary>存在未回答的任务检测决策</summary>
        public static bool IsPending => current != null;

        private bool answered;

        private QuestWorldDecision() { }

        /// <summary>请求任务检测确认，仅本地玩家 OnEnterWorld 触发</summary>
        public static void Request(Player owner) {
            if (Main.netMode == NetmodeID.Server) {
                return;
            }
            if (owner == null || owner.whoAmI != Main.myPlayer) {
                return;
            }
            if (current != null) {
                return;
            }
            current = new QuestWorldDecision();
            EntryDecisionManager.Register(current);
        }

        public override Color Accent => new(110, 185, 255);

        public override string PillText => EntryDecisionUI.QuestPill.Value;
        public override string CardTitle => EntryDecisionUI.QuestTitle.Value;
        public override string CardDesc => EntryDecisionUI.QuestDesc.Value;
        public override string ConfirmLabel => EntryDecisionUI.QuestConfirm.Value;
        public override string SkipLabel => EntryDecisionUI.QuestSkip.Value;
        public override string TrustLabel => EntryDecisionUI.QuestTrust.Value;

        public override bool StillValid => !answered;

        public override void Cancelled() {
            if (current == this) {
                current = null;
            }
        }

        public override void Confirm() {
            answered = true;
            Main.LocalPlayer.GetModPlayer<QLPlayer>().EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.6f, Pitch = 0.3f });
            CombatText.NewText(Main.LocalPlayer.Hitbox, new Color(100, 200, 255),
                EntryDecisionUI.QuestEnabled.Value, true);
        }

        public override void Skip() {
            answered = true;
            Main.LocalPlayer.GetModPlayer<QLPlayer>().DontCheckQuestInWorld = SaveWorld.WorldFullName;

            SoundEngine.PlaySound(SoundID.MenuClose with { Volume = 0.5f });
            CombatText.NewText(Main.LocalPlayer.Hitbox, new Color(200, 150, 100),
                EntryDecisionUI.QuestDisabled.Value, true);
        }

        public override void Trust() {
            answered = true;
            var qlPlayer = Main.LocalPlayer.GetModPlayer<QLPlayer>();
            qlPlayer.TrustCurrentQuestWorld();
            qlPlayer.EnableQuestCheckInCurrentWorld(runWorldEnterChecks: true);

            SoundEngine.PlaySound(SoundID.Item4 with { Volume = 0.7f, Pitch = 0.6f });
            CombatText.NewText(Main.LocalPlayer.Hitbox, new Color(150, 220, 255),
                EntryDecisionUI.QuestTrusted.Value, true);
        }

        public override void DrawIcon(SpriteBatch sb, Vector2 center, float size, float alpha) {
            Texture2D questIcon = QuestLog.QuestLogStart?.Value;
            if (questIcon == null) {
                return;
            }
            Rectangle frame = questIcon.GetRectangle(2, 3);
            float scale = size / System.Math.Max(frame.Width, frame.Height);
            sb.Draw(questIcon, center, frame, Color.White * alpha,
                0f, frame.Size() / 2f, scale, SpriteEffects.None, 0f);
        }
    }
}
