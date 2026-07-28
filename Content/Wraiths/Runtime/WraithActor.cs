using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using InnoVault.Concurrent;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 显形态基类。子类覆写事件钩子与 DrawBody；权威决策，客户端同步+本地视觉。<br/>
    /// 身份经 FindByActorType 反查；并行密封串行
    /// </summary>
    public abstract class WraithActor : Actor
    {
        //权威经增量同步下发的存在状态，客户端检测翻转后重置本地过渡计时
        [SyncVar]
        private int presenceRaw = (int)WraithPresence.Materializing;
        //死机窗口时长，BeginHalt 时由权威写入，两端各自用 presenceTimer 对表
        [SyncVar]
        private int haltDuration;
        //挣脱源玩家 whoAmI，-1=常规显形
        [SyncVar]
        private int escapedOwner = -1;
        //挣脱源玩家名，槽位被复用（原主下线、新人顶位）时的二次校验，与 escapedOwner 同步写
        [SyncVar]
        private string escapedOwnerName = "";

        private WraithPresence lastSeenPresence = WraithPresence.Materializing;
        private int presenceTimer;
        private int presentTimer;
        //过渡起点强度，打断显形时自当下衰减
        private float presenceBaseStrength = 1f;
        private bool discovered;
        private WraithDefinition definition;
        private readonly List<IWraithBehavior> behaviors = [];
        //吞没回执节流(客户端视觉),见 WraithSwallow
        internal int SwallowCooldown;

        //感知边沿状态，仅权威端读写
        private readonly bool[] gazeState = new bool[Main.maxPlayers];
        private readonly bool[] nearState = new bool[Main.maxPlayers];
        private readonly bool[] touchState = new bool[Main.maxPlayers];
        private bool sensorsWereActive;

        public sealed override ParallelExecutionKind ParallelKind => ParallelExecutionKind.Serial;

        /// <summary>存在状态，权威端经 <see cref="SetPresence"/> 推进</summary>
        public WraithPresence Presence => (WraithPresence)presenceRaw;

        /// <summary>死机窗口中</summary>
        public bool IsHalted => Presence == WraithPresence.Halted;

        /// <summary>死机窗口剩余帧数，非死机为 0；两端本地对表，仅作演出与提示参考</summary>
        public int HaltRemaining => IsHalted ? Math.Max(haltDuration - presenceTimer, 0) : 0;

        /// <summary>反噬挣脱态</summary>
        public bool IsEscaped => escapedOwner >= 0;

        /// <summary>挣脱源玩家，失效或槽位复用为 null</summary>
        public Player EscapedOwnerPlayer {
            get {
                if (escapedOwner < 0 || escapedOwner >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[escapedOwner];
                if (player == null || !player.active) {
                    return null;
                }
                //槽位复用，名字不符则原主离场
                if (!string.IsNullOrEmpty(escapedOwnerName) && player.name != escapedOwnerName) {
                    return null;
                }
                return player;
            }
        }

        /// <summary>显形强度 0~1</summary>
        public float PresenceStrength { get; private set; }

        /// <summary>生成锚点</summary>
        public Vector2 SpawnAnchor { get; private set; }

        /// <summary>任意玩家正在注视，仅权威端有效</summary>
        public bool GazedByAnyPlayer { get; private set; }

        /// <summary>视觉相位，两端各自推进，只做摆动/闪烁</summary>
        public float VisualPhase { get; private set; }

        /// <summary>本实体对应的定义，类型反查，注册缺失时为 null（实体自灭）</summary>
        public WraithDefinition Definition => definition ??= WraithRegistry.FindByActorType(GetType());

        /// <summary>是否权威端（服务器或单人）</summary>
        public bool IsAuthority => !VaultUtils.isClient;

        public override void OnSpawn(params object[] args) {
            WraithDefinition def = Definition;
            Width = def?.HitboxWidth ?? 60;
            Height = def?.HitboxHeight ?? 90;
            DrawExtendMode = 400;
            DrawLayer = ActorDrawLayer.Default;

            SpawnAnchor = Position;
            //勿重置 presenceRaw，晚加入 SyncVar 会先到
            lastSeenPresence = Presence;
            presenceTimer = 0;
            presentTimer = 0;
            discovered = false;
            VisualPhase = Main.rand.NextFloat(MathHelper.TwoPi);

            behaviors.Clear();
            def?.BuildBehaviors(behaviors);
        }

        //生成包/晚加入快照携带非 SyncVar 的内部状态，两端过渡节拍对齐
        public override void SendExtraData(BinaryWriter writer) {
            writer.WriteVector2(SpawnAnchor);
            writer.Write(presenceTimer);
            writer.Write(presentTimer);
            writer.Write(presenceBaseStrength);
            writer.Write(discovered);
        }

        public override void ReceiveExtraData(BinaryReader reader) {
            SpawnAnchor = reader.ReadVector2();
            presenceTimer = reader.ReadInt32();
            presentTimer = reader.ReadInt32();
            presenceBaseStrength = reader.ReadSingle();
            discovered = reader.ReadBoolean();
            //对齐基准，防首帧误判翻转
            lastSeenPresence = Presence;
        }

        public override void AI() {
            if (Definition == null) {
                //无定义则权威清理
                if (IsAuthority) {
                    RequestKill();
                }
                return;
            }

            //死机冻相位
            if (!IsHalted) {
                VisualPhase += 0.045f;
                if (VisualPhase > MathHelper.TwoPi) {
                    VisualPhase -= MathHelper.TwoPi;
                }
            }

            UpdatePresence();

            if (!Main.dedServ) {
                WraithSwallow.Update(this);
            }

            if (IsAuthority) {
                if (SensorsActive) {
                    UpdateSensors();
                }
                else if (sensorsWereActive) {
                    ResetSensorStates();
                }
                if (Presence == WraithPresence.Dematerializing) {
                    Velocity *= 0.88f;
                }
                else if (IsHalted) {
                    //死机停行为
                    Velocity *= 0.72f;
                }
                else {
                    foreach (IWraithBehavior behavior in behaviors) {
                        behavior.Update(this);
                    }
                    if (IsEscaped) {
                        UpdateEscaped();
                    }
                }
                OnAuthorityUpdate();
            }

            Lighting.AddLight(Center, Definition.BaseColor.ToVector3() * (0.32f * PresenceStrength));
        }

        //====存在状态推进====

        private void UpdatePresence() {
            //客户端翻转重置过渡计时
            if (Presence != lastSeenPresence) {
                WraithPresence previous = lastSeenPresence;
                lastSeenPresence = Presence;
                presenceTimer = 0;
                presenceBaseStrength = PresenceStrength;
                //远端死机入场拍
                if (Presence == WraithPresence.Halted && previous != WraithPresence.Halted) {
                    PlayHaltCue();
                }
            }
            presenceTimer++;

            WraithDefinition def = Definition;
            int materializeFrames = Math.Max(def.MaterializeFrames, 1);
            int dematerializeFrames = Math.Max(def.DematerializeFrames, 1);

            switch (Presence) {
                case WraithPresence.Materializing: {
                    float t = MathHelper.Clamp(presenceTimer / (float)materializeFrames, 0f, 1f);
                    PresenceStrength = t * t * (3f - 2f * t);
                    if (IsAuthority && presenceTimer >= materializeFrames) {
                        SetPresence(WraithPresence.Present);
                        OnFullyMaterialized();
                        TryMarkDiscovered();
                    }
                    break;
                }
                case WraithPresence.Present: {
                    PresenceStrength = 1f;
                    presentTimer++;
                    if (IsAuthority) {
                        //迟到发现补登
                        if (!discovered && presentTimer % 30 == 0) {
                            TryMarkDiscovered();
                        }
                        int limit = def.PresentDurationLimit;
                        if (limit > 0 && presentTimer >= limit) {
                            BeginDematerialize();
                        }
                    }
                    break;
                }
                case WraithPresence.Dematerializing: {
                    //自过渡起点衰减
                    float t = MathHelper.Clamp(presenceTimer / (float)dematerializeFrames, 0f, 1f);
                    PresenceStrength = presenceBaseStrength * (1f - t * t);
                    if (IsAuthority && presenceTimer >= dematerializeFrames) {
                        RequestKill();
                    }
                    break;
                }
                case WraithPresence.Halted: {
                    //死机凝滞，窗尽权威裁决
                    PresenceStrength = 1f;
                    if (IsAuthority && haltDuration > 0 && presenceTimer >= haltDuration) {
                        OnHaltExpired();
                    }
                    break;
                }
                default:
                    PresenceStrength = 0f;
                    break;
            }
        }

        private void SetPresence(WraithPresence presence) {
            presenceRaw = (int)presence;
            lastSeenPresence = presence;
            presenceTimer = 0;
            presenceBaseStrength = PresenceStrength;
            NetUpdate = true;
        }

        /// <summary>进消散，仅权威</summary>
        public void BeginDematerialize() {
            if (!IsAuthority || Presence == WraithPresence.Dematerializing) {
                return;
            }
            SetPresence(WraithPresence.Dematerializing);
            OnBeginDematerialize();
        }

        //====死机窗口====

        /// <summary>进死机窗，仅权威；duration≤0 取定义默认</summary>
        public void BeginHalt(int durationTicks = -1) {
            if (!IsAuthority || IsHalted
                || (Presence != WraithPresence.Materializing && Presence != WraithPresence.Present)) {
                return;
            }
            haltDuration = durationTicks > 0 ? durationTicks : Math.Max(Definition?.HaltWindowTicks ?? 60 * 8, 1);
            SetPresence(WraithPresence.Halted);
            OnHaltBegin();
            //单人本地播入场拍
            if (!Main.dedServ) {
                PlayHaltCue();
            }
        }

        /// <summary>解除死机，仅权威</summary>
        public void EndHalt() {
            if (!IsAuthority || !IsHalted) {
                return;
            }
            haltDuration = 0;
            SetPresence(WraithPresence.Present);
            OnHaltEnd();
        }

        /// <summary>死机入场拍</summary>
        private void PlayHaltCue() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.9f, Volume = 0.6f, MaxInstances = 2 }, Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.6f, Volume = 0.25f, MaxInstances = 2 }, Center);
            if (WraithSystemText.HaltPopup != null) {
                CombatText.NewText(HitBox, Definition?.EyeColor ?? new Color(120, 220, 200), WraithSystemText.HaltPopup.Value, true);
            }
        }

        //====反噬挣脱====

        /// <summary>标挣脱态，仅权威；默认缠主人</summary>
        internal void MarkEscaped(int playerWhoAmI) {
            if (!IsAuthority || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
                return;
            }
            escapedOwner = playerWhoAmI;
            escapedOwnerName = Main.player[playerWhoAmI]?.name ?? "";
            NetUpdate = true;
            OnBacklashEscape(EscapedOwnerPlayer);
        }

        /// <summary>挣脱默认驱动，缠主人；主人失效则消散</summary>
        protected virtual void UpdateEscaped() {
            Player owner = EscapedOwnerPlayer;
            if (owner == null || owner.dead) {
                BeginDematerialize();
                return;
            }
            float distance = Vector2.Distance(owner.Center, Center);
            if (distance > 520f) {
                Vector2 pull = (owner.Center - Center).SafeNormalize(Vector2.Zero) * MathF.Min((distance - 420f) * 0.02f, 6f);
                Velocity = Vector2.Lerp(Velocity, pull, 0.06f);
            }
        }

        //====感知与事件====

        /// <summary>感知窗口，默认仅 Present；死机/消散关</summary>
        protected virtual bool SensorsActive => Presence == WraithPresence.Present;

        private void ResetSensorStates() {
            Array.Clear(gazeState);
            Array.Clear(nearState);
            Array.Clear(touchState);
            GazedByAnyPlayer = false;
            sensorsWereActive = false;
        }

        private void UpdateSensors() {
            sensorsWereActive = true;
            WraithDefinition def = Definition;
            bool anyGaze = false;
            Rectangle hitbox = HitBox;
            float approachSq = def.ApproachRadius * def.ApproachRadius;
            float retreatSq = def.RetreatRadius * def.RetreatRadius;

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player player = Main.player[i];
                bool alive = player != null && player.active && !player.dead;

                bool gazed = alive && WraithSensors.IsGazedBy(player, this, def.GazeRange);
                anyGaze |= gazed;
                if (gazed != gazeState[i]) {
                    gazeState[i] = gazed;
                    if (gazed) {
                        OnGazeStart(player);
                    }
                    else if (alive) {
                        OnGazeEnd(player);
                    }
                }

                //接近/脱离双半径迟滞
                float distSq = alive ? Vector2.DistanceSquared(player.Center, Center) : float.MaxValue;
                bool near = alive && (nearState[i] ? distSq < retreatSq : distSq < approachSq);
                if (near != nearState[i]) {
                    nearState[i] = near;
                    if (near) {
                        OnPlayerApproach(player);
                    }
                    else if (alive) {
                        OnPlayerRetreat(player);
                    }
                }

                bool touch = alive && hitbox.Intersects(player.Hitbox);
                if (touch != touchState[i]) {
                    touchState[i] = touch;
                    if (touch) {
                        OnTouch(player);
                    }
                }
            }
            GazedByAnyPlayer = anyGaze;
        }

        private void TryMarkDiscovered() {
            if (discovered) {
                return;
            }
            WraithDefinition def = Definition;
            float radiusSq = def.DiscoverRadius * def.DiscoverRadius;
            foreach (Player player in Main.ActivePlayers) {
                if (!player.dead && Vector2.DistanceSquared(player.Center, Center) < radiusSq) {
                    discovered = true;
                    WraithWorldProgress.MarkEncounter(def.Key);
                    break;
                }
            }
        }

        //====事件钩子（仅权威端触发，默认空实现，怪谈规则=子类覆写这些）====

        /// <summary>某玩家开始注视</summary>
        protected virtual void OnGazeStart(Player player) { }
        /// <summary>某玩家移开视线</summary>
        protected virtual void OnGazeEnd(Player player) { }
        /// <summary>某玩家进入接近半径</summary>
        protected virtual void OnPlayerApproach(Player player) { }
        /// <summary>接近过的玩家退出脱离半径</summary>
        protected virtual void OnPlayerRetreat(Player player) { }
        /// <summary>某玩家碰到实体</summary>
        protected virtual void OnTouch(Player player) { }
        /// <summary>显形过渡完成</summary>
        protected virtual void OnFullyMaterialized() { }
        /// <summary>消散过渡开始</summary>
        protected virtual void OnBeginDematerialize() { }
        /// <summary>进入死机窗口</summary>
        protected virtual void OnHaltBegin() { }
        /// <summary>死机被解除，窗尽不走这里</summary>
        protected virtual void OnHaltEnd() { }
        /// <summary>死机窗尽未消耗，默认消散</summary>
        protected virtual void OnHaltExpired() => BeginDematerialize();
        /// <summary>被标挣脱态，owner 可能 null</summary>
        protected virtual void OnBacklashEscape(Player owner) { }
        /// <summary>权威每帧尾，子类挂逻辑</summary>
        protected virtual void OnAuthorityUpdate() { }

        //====绘制====

        public sealed override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (PresenceStrength > 0.003f) {
                DrawBody(spriteBatch, drawColor);
            }
            //无贴图跳过默认绘制
            return false;
        }

        /// <summary>本体绘制，默认三层雾影+双眼</summary>
        public virtual void DrawBody(SpriteBatch spriteBatch, Color lightColor) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Rectangle src = new(0, 0, 1, 1);
            WraithDefinition def = Definition;
            Color body = def?.BaseColor ?? new Color(150, 160, 185);
            Color eye = def?.EyeColor ?? new Color(120, 220, 200);

            float alpha = PresenceStrength;
            Vector2 center = Center - Main.screenPosition;
            Vector2 size = Size;
            float bob = MathF.Sin(VisualPhase) * 4f;
            Vector2 half = new(0.5f);

            //三层雾体
            for (int i = 0; i < 3; i++) {
                float sway = MathF.Sin(VisualPhase * (0.8f + i * 0.31f) + i * 2.1f) * (3f + i * 2f);
                float yOffset = size.Y * (0.30f - i * 0.27f);
                Vector2 pos = center + new Vector2(sway, bob + yOffset);
                Vector2 scale = new(size.X * (0.92f - i * 0.18f), size.Y * 0.46f);
                spriteBatch.Draw(pixel, pos, src, body * (alpha * (0.34f - i * 0.06f)), 0f, half, scale, SpriteEffects.None, 0f);
            }

            //鬼火眼
            const float EyeSideFactor = 0.14f;
            if (alpha > 0.5f) {
                float eyeA = (alpha - 0.5f) * 2f;
                float flick = 0.75f + 0.25f * MathF.Sin(VisualPhase * 6.3f);
                Vector2 eyeBase = center + new Vector2(0f, bob - size.Y * 0.24f);
                for (int side = -1; side <= 1; side += 2) {
                    Vector2 eyePos = eyeBase + new Vector2(side * size.X * EyeSideFactor, 0f);
                    spriteBatch.Draw(pixel, eyePos, src, eye * (eyeA * 0.35f * flick), 0f, half, new Vector2(7f, 5f), SpriteEffects.None, 0f);
                    spriteBatch.Draw(pixel, eyePos, src, eye * (eyeA * 0.95f * flick), 0f, half, new Vector2(3.2f, 2.4f), SpriteEffects.None, 0f);
                }
            }
        }
    }
}
