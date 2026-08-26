using CalamityOverhaul.Content.Narrative;
using CalamityOverhaul.Content.Narrative.Data.Modules;
using CalamityOverhaul.Content.Scenarios.OldDuke.Campsites;
using CalamityOverhaul.OtherMods.BossChecklist;
using InnoVault.GameSystem;
using InnoVault.Narrative.Runtime;
using Microsoft.Xna.Framework.Graphics;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Scenarios.OldDuke
{
    /// <summary>老公爵NPC行为覆盖</summary>
    internal class ModifyOldDuke : NPCOverride, ILocalizedModType
    {
        public override int TargetID => CWRID.NPC_OldDuke;

        #region AI状态定义

        private enum OldDukeAIState
        {
            FriendlyApproach = 0,
            DialoguePause = 1,
            LeavingDive = 2,
            StartBattle = 3
        }

        private ref float State => ref ai[0];
        private ref float Timer => ref ai[1];
        private ref float SubState => ref ai[2];
        private ref float LeavingDiveFlag => ref ai[3];

        #endregion

        #region 字段和属性

        public string LocalizationCategory => "NPCModifys";
        public static LocalizedText LeavingDiveText { get; private set; }

        private bool canDraw;
        private int deadCounter;
        private bool hasTriggeredDialogue;

        #endregion

        #region 生命周期

        public override bool CanOverride() {
            if (!CWRRef.Has) {
                return false;
            }
            if (!VaultUtils.isSinglePlayer) {
                return false;
            }
            if (CWRRef.GetBossRushActive()) {
                return false;
            }
            return base.CanOverride();
        }

        public override void SetStaticDefaults() {
            LeavingDiveText = this.GetLocalization(nameof(LeavingDiveText), () => "老公爵潜入了水中...");
        }

        public override void SetProperty() {
            canDraw = false;
            deadCounter = 0;
            hasTriggeredDialogue = false;
        }

        #endregion

        #region 状态判断辅助方法

        private bool IsInLeavingDive => LeavingDiveFlag == 1f;

        private static bool ShouldEnterStoryMode(Player target) {
            return OldDukeStorySync.GetState(target) switch {
                OldDukeInteractionState.NotMet => true,
                OldDukeInteractionState.Met => true,
                OldDukeInteractionState.DeclinedCooperation => true,
                _ => false
            };
        }

        private static bool ShouldLeaveAfterCooperation(Player target)
            => OldDukeStorySync.GetState(target) == OldDukeInteractionState.AcceptedCooperation;

        private static bool ShouldRedirectToCampsite(Player target) {
            return OldDukeCampsite.IsGenerated
                && !OldDukeCampsite.WannaToFight
                && target.whoAmI == Main.myPlayer
                && !VaultUtils.isServer;
        }

        #endregion

        #region 帧动画

        public override bool FindFrame(int frameHeight) {
            if (npc.friendly && npc.dontTakeDamage) {
                npc.frameCounter += 0.08f;
                npc.frameCounter %= Main.npcFrameCount[npc.type] - 1;
                int frame = (int)npc.frameCounter;
                npc.frame.Y = frame * frameHeight;
                return false;
            }
            return base.FindFrame(frameHeight);
        }

        #endregion

        #region 死亡处理（战败潜水）

        public override bool? CheckDead() {
            LeavingDiveFlag = 1f;
            npc.life = npc.lifeMax;
            npc.dontTakeDamage = true;
            if (!VaultUtils.isClient) {
                npc.DropItem();
            }
            if (deadCounter == 0) {
                deadCounter++;
                CWRRef.OldDukeOnKill(npc);
            }
            VaultUtils.Text(LeavingDiveText.Value, Color.YellowGreen);
            foreach (var g in Main.gore) {
                g.active = false;
            }
            return false;
        }

        #endregion

        #region 网络消息处理

        internal static void StartCampsiteFindMeScenarioNetWork(BinaryReader reader, int whoAmI)
            => OldDukeTriggerService.HandleStartCampsiteFindMeScenario(reader, whoAmI);

        #endregion

        #region 主AI逻辑

        public override bool AI() {
            canDraw = true;
            npc.alpha = 0;

            if (IsInLeavingDive) {
                State = (float)OldDukeAIState.LeavingDive;
                return RunStorylineAI();
            }

            if (CWRRef.GetBossRushActive()) {
                return true;
            }

            npc.TargetClosest();
            Player target = Main.player[npc.target];

            if (OldDukeCampsite.WannaToFight) {
                KillDukeSummonerProjectiles();
                return true;
            }

            if (ShouldEnterStoryMode(target)) {
                OldDukeStorySync.MarkMetIfNeeded(target);
                return RunStorylineAI();
            }

            if (State == (float)OldDukeAIState.StartBattle) {
                return RunStorylineAI();
            }

            if (ShouldRedirectToCampsite(target)) {
                ExecuteCampsiteRedirect();
                return false;
            }

            if (ShouldLeaveAfterCooperation(target)) {
                if (State != (float)OldDukeAIState.LeavingDive) {
                    State = (float)OldDukeAIState.LeavingDive;
                    Timer = 0;
                    SubState = 0;
                }
                return RunStorylineAI();
            }

            KillDukeSummonerProjectiles();
            return true;
        }

        private void ExecuteCampsiteRedirect() {
            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }
            npc.active = false;
            npc.netUpdate = true;

            OldDukeTriggerService.TriggerCampsiteScenario();

            if (VaultUtils.isClient) {
                ModPacket packet = CWRNetWork.GetPacket<OldDukeTriggerService>();
                packet.Write(npc.whoAmI);
                packet.Send();
            }
        }

        private static void KillDukeSummonerProjectiles() {
            foreach (var p in Main.ActiveProjectiles) {
                if (p.type == CWRID.Proj_OverlyDramaticDukeSummoner) {
                    p.active = false;
                    p.netUpdate = true;
                }
            }
        }

        #endregion

        #region 剧情AI状态机

        private bool RunStorylineAI() {
            npc.TargetClosest();
            Player target = Main.player[npc.target];

            npc.friendly = true;
            npc.dontTakeDamage = true;
            npc.damage = 0;

            switch ((OldDukeAIState)State) {
                case OldDukeAIState.FriendlyApproach:
                    HandleFriendlyApproach(target);
                    break;
                case OldDukeAIState.DialoguePause:
                    HandleDialoguePause(target);
                    break;
                case OldDukeAIState.LeavingDive:
                    HandleLeavingDive();
                    break;
                case OldDukeAIState.StartBattle:
                    HandleStartBattle();
                    break;
            }

            return false;
        }

        private void HandleFriendlyApproach(Player target) {
            Vector2 targetPos = target.Center + new Vector2(0, -300);
            Vector2 toTarget = npc.Center.To(targetPos);

            const float speed = 25f;
            const float inertia = 20f;
            npc.velocity = (npc.velocity * (inertia - 1f) + toTarget.SafeNormalize(Vector2.Zero) * speed) / inertia;

            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            if (toTarget.Length() < 50f) {
                State = (float)OldDukeAIState.DialoguePause;
                Timer = 0;
                npc.velocity *= 0.9f;

                if (!hasTriggeredDialogue && !VaultUtils.isServer && npc.target == Main.myPlayer) {
                    hasTriggeredDialogue = true;
                    NarrativeRouter.Begin<FirstMetOldDuke>();
                }
            }
        }

        private void HandleDialoguePause(Player target) {
            Timer++;
            npc.velocity *= 0.95f;

            float floatOffset = (float)System.Math.Sin(Timer * 0.05f) * 2f;
            npc.position.Y += floatOffset * 0.1f;

            npc.spriteDirection = npc.direction = target.Center.X < npc.Center.X ? 1 : -1;

            bool isLocalTarget = !VaultUtils.isServer && npc.target == Main.myPlayer;
            if (isLocalTarget
                && !NarrativeRouter.IsActive<FirstMetOldDuke>()
                && !NarrativeRunner.IsBusy
                && Timer > 60) {
                switch (OldDukeStorySync.GetState(target)) {
                    case OldDukeInteractionState.AcceptedCooperation:
                    case OldDukeInteractionState.DeclinedCooperation:
                        TransitionToState(OldDukeAIState.LeavingDive);
                        SoundEngine.PlaySound(SoundID.Splash, npc.Center);
                        npc.netUpdate = true;
                        break;
                    case OldDukeInteractionState.ChoseToFight:
                        TransitionToState(OldDukeAIState.StartBattle);
                        SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.2f }, npc.Center);
                        npc.netUpdate = true;
                        break;
                }
            }
        }

        private void HandleLeavingDive() {
            Timer++;
            if (npc.ModNPC is not null) {
                npc.ModNPC.Music = -1;
            }

            if (SubState == 0) {
                npc.velocity.Y += 0.5f;
                if (npc.velocity.Y > 20f) {
                    npc.velocity.Y = 20f;
                }
                npc.velocity.X = (float)System.Math.Sin(Timer * 0.1f) * 3f;
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                if (Timer % 5 == 0) {
                    for (int i = 0; i < 2; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Water,
                            Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-1f, 1f),
                            100, default, Main.rand.NextFloat(1f, 2f));
                    }
                }

                if (Timer > 60) {
                    SubState = 1;
                    Timer = 0;
                    npc.netUpdate = true;
                }
            }
            else if (SubState == 1) {
                if (npc.alpha >= 255 || Timer > 120) {
                    FinalizeDespawn();
                }
            }
        }

        private void HandleStartBattle() {
            Timer++;

            if (Timer < 60) {
                npc.velocity.Y = -2f;
                npc.velocity.X *= 0.98f;

                if (Timer % 10 < 5) {
                    for (int i = 0; i < 3; i++) {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.GreenTorch,
                            Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f),
                            100, default, Main.rand.NextFloat(1.5f, 2.5f));
                    }
                }
            }
            else {
                RestoreCombatState();
            }
        }

        #endregion

        #region 状态转换辅助方法

        private void TransitionToState(OldDukeAIState newState) {
            State = (float)newState;
            Timer = 0;
            SubState = 0;
        }

        private void FinalizeDespawn() {
            CWRRef.StopAcidRain();
            hasTriggeredDialogue = false;
            LeavingDiveFlag = 0f;

            if (BCKRef.Has) {
                BCKRef.SetActiveNPCEntryFlags(npc.whoAmI, -1);
            }
            npc.active = false;
            npc.netUpdate = true;
        }

        private void RestoreCombatState() {
            CWRRef.StopAcidRain();
            hasTriggeredDialogue = false;

            npc.friendly = false;
            npc.dontTakeDamage = false;
            npc.damage = npc.defDamage;
            npc.alpha = 0;
            npc.rotation = 0;

            State = 0;
            Timer = 0;
            SubState = 0;
            LeavingDiveFlag = 0;
            npc.ai[0] = 0;
            npc.ai[1] = 0;
            npc.ai[2] = 0;
            npc.ai[3] = 0;

            npc.netUpdate = true;
        }

        #endregion

        #region 绘制

        public override bool? Draw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (!canDraw) {
                return false;
            }
            return base.Draw(spriteBatch, screenPos, drawColor);
        }

        #endregion
    }
}
