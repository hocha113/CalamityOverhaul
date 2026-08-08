using CalamityOverhaul.Content.Wraiths.Core;
using CalamityOverhaul.Content.Wraiths.Runtime;
using InnoVault.Cinematics;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Wraiths.Deaths
{
    /// <summary>
    /// 厉鬼夺身死亡演出的每玩家状态机：前兆 → 显形 → 处决 → 余韵，全程约 3 秒。<br/>
    /// 服务器持权威计时并在处决帧执行 <see cref="WraithLethality.Kill"/>；
    /// 状态经 <see cref="WraithNet"/> 的 RevivalDeathState 广播。演出期间玩家被钉住、
    /// 免疫外部伤害但不可被拯救；玩家死亡后余韵继续存活至演出收尾。
    /// </summary>
    internal sealed class WraithRevivalDeathPlayer : ModPlayer
    {
        //演出总长硬上限，防坏包把余韵拖死
        private const int MaxSeizeFrames = 60 * 10;

        private string activeKey = string.Empty;
        private int timer;
        private uint revision;
        private byte seed;
        private bool executed;
        private Vector2 deathAnchor;
        private WraithDeathPerformance performance;
        private bool presentationStarted;

        internal bool Active => timer > 0 && !string.IsNullOrEmpty(activeKey);
        internal string ActiveKey => activeKey;
        internal int SeizeTimer => timer;
        internal uint SeizeRevision => revision;
        internal byte SeizeSeed => seed;
        internal bool Executed => executed;
        internal Vector2 DeathAnchor => deathAnchor;

        private int OmenEndFrame => performance?.OmenEndFrame ?? 42;
        private int ExecuteFrame => performance?.ExecuteFrame ?? 126;
        private int TotalFrames => Math.Min(performance?.TotalFrames ?? 186, MaxSeizeFrames);

        internal WraithSeizePhase Phase {
            get {
                if (!Active) {
                    return WraithSeizePhase.None;
                }
                if (timer <= OmenEndFrame) {
                    return WraithSeizePhase.Omen;
                }
                return timer < ExecuteFrame ? WraithSeizePhase.Manifest : WraithSeizePhase.Linger;
            }
        }

        internal float PhaseProgress {
            get {
                switch (Phase) {
                    case WraithSeizePhase.Omen:
                        return MathHelper.Clamp(timer / (float)Math.Max(OmenEndFrame, 1), 0f, 1f);
                    case WraithSeizePhase.Manifest:
                        return MathHelper.Clamp((timer - OmenEndFrame)
                            / (float)Math.Max(ExecuteFrame - OmenEndFrame, 1), 0f, 1f);
                    case WraithSeizePhase.Linger:
                        return MathHelper.Clamp((timer - ExecuteFrame)
                            / (float)Math.Max(TotalFrames - ExecuteFrame, 1), 0f, 1f);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>权威端开始夺身。已在夺身中或玩家已死时拒绝。</summary>
        internal bool TryBeginAuthority(WraithDefinition definition) {
            if (Main.netMode == NetmodeID.MultiplayerClient || definition == null
                || Active || Player.dead) {
                return false;
            }
            activeKey = definition.Key;
            timer = 1;
            executed = false;
            seed = (byte)Main.rand.Next(256);
            deathAnchor = Player.Center;
            revision++;
            if (revision == 0) {
                revision = 1;
            }
            performance = null;
            presentationStarted = false;
            EnsurePerformance();
            if (Main.netMode == NetmodeID.Server) {
                WraithNet.SendRevivalDeathState(Player.whoAmI);
            }
            return true;
        }

        /// <summary>客户端应用服务器广播的夺身快照；空 Key 表示结束。</summary>
        internal void ApplyReplicated(uint stateRevision, int stateTimer, string key,
            byte stateSeed, bool stateExecuted) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                return;
            }
            //按修订号拒绝旧演出的迟到包
            if (revision != 0 && unchecked((int)(stateRevision - revision)) < 0) {
                return;
            }
            bool fresh = stateRevision != revision;
            revision = stateRevision;
            seed = stateSeed;
            executed = stateExecuted;
            if (string.IsNullOrEmpty(key) || stateTimer <= 0) {
                EndSeizure(broadcast: false);
                return;
            }
            if (fresh || activeKey != key) {
                activeKey = key;
                performance = null;
                presentationStarted = false;
                deathAnchor = Player.Center;
            }
            EnsurePerformance();
            timer = Math.Clamp(stateTimer, 1, TotalFrames);
        }

        /// <summary>按 Key 建演出实例；帧表在服务器端也需要，故各端都实例化，表现钩子只在客户端调。</summary>
        private void EnsurePerformance() {
            if (performance != null || !Active) {
                return;
            }
            WraithRegistry.TryGet(activeKey, out WraithDefinition definition);
            performance = definition?.CreateDeathPerformance() ?? new GenericSeizurePerformance();
            performance.Host = this;
        }

        private void StartPresentation() {
            if (presentationStarted || Main.dedServ || !Active) {
                return;
            }
            presentationStarted = true;
            EnsurePerformance();
            performance.OnBegin();
            if (Player.whoAmI == Main.myPlayer) {
                CutsceneDirector.Play<WraithDeathCutscene, WraithRevivalDeathPlayer>(
                    this, Player, restartSameClip: false);
            }
        }

        public override void PreUpdateMovement() {
            if (!Active || Player.dead) {
                return;
            }
            EnsurePerformance();
            StartPresentation();

            //钉住与免疫：夺身不可被打断，也不可被外力杀死
            Player.noItems = true;
            Player.noBuilding = true;
            Player.noKnockback = true;
            Player.immune = true;
            if (Player.immuneTime < 2) {
                Player.immuneTime = 2;
            }
            performance?.UpdatePlayerMotion();
            if (Player.whoAmI == Main.myPlayer) {
                LockLocalControls();
            }
            deathAnchor = Player.Center;

            if (!Main.dedServ) {
                performance?.Update();
            }

            //处决帧：本地表现 + 权威击杀
            if (timer == ExecuteFrame - 1) {
                if (!Main.dedServ) {
                    performance?.OnExecute();
                }
            }
            if (timer >= ExecuteFrame && !executed
                && Main.netMode != NetmodeID.MultiplayerClient) {
                executed = true;
                deathAnchor = Player.Center;
                WraithRegistry.TryGet(activeKey, out WraithDefinition definition);
                if (definition != null) {
                    WraithLethality.Kill(Player, definition);
                }
                if (Main.netMode == NetmodeID.Server) {
                    WraithNet.SendRevivalDeathState(Player.whoAmI);
                }
            }

            AdvanceTimer();
        }

        public override void UpdateDead() {
            if (!Active) {
                return;
            }
            EnsurePerformance();
            StartPresentation();
            if (!executed) {
                //免疫之外的死亡（如规则死亡连锁）：直接进入余韵
                executed = true;
                deathAnchor = Player.Center;
                if (timer < ExecuteFrame) {
                    timer = ExecuteFrame;
                }
            }
            if (!Main.dedServ) {
                performance?.Update();
            }
            AdvanceTimer();
        }

        private void AdvanceTimer() {
            timer++;
            if (timer > TotalFrames) {
                EndSeizure(broadcast: true);
            }
        }

        private void LockLocalControls() {
            Player.controlJump = false;
            Player.controlDown = false;
            Player.controlLeft = false;
            Player.controlRight = false;
            Player.controlUp = false;
            Player.controlUseItem = false;
            Player.controlUseTile = false;
            Player.controlThrow = false;
            Player.controlHook = false;
            Player.controlMount = false;
        }

        private void EndSeizure(bool broadcast) {
            bool wasActive = Active;
            activeKey = string.Empty;
            timer = 0;
            executed = false;
            performance = null;
            presentationStarted = false;
            if (!wasActive) {
                return;
            }
            if (broadcast && Main.netMode == NetmodeID.Server) {
                WraithNet.SendRevivalDeathState(Player.whoAmI);
            }
            if (!Main.dedServ && Player.whoAmI == Main.myPlayer
                && CutsceneDirector.CurrentClip is WraithDeathCutscene) {
                CutsceneDirector.Stop();
            }
        }

        /// <summary>供 <see cref="WraithDeathRender"/> 调用的世界空间绘制。</summary>
        internal void DrawPerformance(SpriteBatch sb) {
            if (Active && presentationStarted) {
                performance?.Draw(sb);
            }
        }

        /// <summary>裸设备图元层绘制（精灵批次开始前）。</summary>
        internal void DrawPerformancePrimitive(GraphicsDevice device) {
            if (Active && presentationStarted) {
                performance?.DrawPrimitive(device);
            }
        }

        /// <summary>供运镜片段读取的当前演出；未激活时为 null。</summary>
        internal WraithDeathPerformance CurrentPerformance => Active ? performance : null;

        /// <summary>本帧演出是否要求隐藏玩家本体。</summary>
        internal bool HidesPlayerNow => Active && presentationStarted
            && performance?.HidesPlayer == true;

        public override void OnRespawn() => EndSeizure(broadcast: true);

        public override void OnEnterWorld() => EndSeizure(broadcast: false);

        public override void PlayerDisconnect() => EndSeizure(broadcast: false);

        public override void SyncPlayer(int toWho, int fromWho, bool newPlayer) {
            if (Main.netMode == NetmodeID.Server && Active) {
                WraithNet.SendRevivalDeathState(Player.whoAmI, toWho);
            }
        }
    }
}
