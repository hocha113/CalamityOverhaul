using CalamityOverhaul.Common;
using CalamityOverhaul.Content.Wraiths.Core;
using InnoVault.Actors;
using InnoVault.Concurrent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;

namespace CalamityOverhaul.Content.Wraiths.Runtime
{
    /// <summary>
    /// 厉鬼显形态实体基类。显形/消散过渡、感知边沿事件、行为积木驱动与占位绘制都在这里，
    /// 子类通常只覆写事件钩子与 <see cref="DrawBody"/>。
    /// 权威模型沿用 Actor 惯例：决策只在服务器/单人推进，客户端吃内建同步 + 本地视觉预测。
    /// 身份经 <see cref="WraithRegistry.FindByActorType"/> 反查，因此子类与定义一一对应，生成后即可用，无需额外同步。
    /// 并行策略密封为串行：行为积木与感知走 Main.rand / Collision，均非线程安全
    /// </summary>
    public abstract class WraithActor : Actor
    {
        //权威经增量同步下发的存在状态，客户端检测翻转后重置本地过渡计时
        [SyncVar]
        private int presenceRaw = (int)WraithPresence.Materializing;
        //死机窗口时长，BeginHalt 时由权威写入，两端各自用 presenceTimer 对表
        [SyncVar]
        private int haltDuration;
        //反噬挣脱态：挣脱自哪名玩家的载体，-1=常规显形；挣脱是据点制的唯一合法例外
        [SyncVar]
        private int escapedOwner = -1;
        //挣脱源玩家名，槽位被复用（原主下线、新人顶位）时的二次校验，与 escapedOwner 同步写
        [SyncVar]
        private string escapedOwnerName = "";

        private WraithPresence lastSeenPresence = WraithPresence.Materializing;
        private int presenceTimer;
        private int presentTimer;
        //过渡起点强度:消散可能打断显形,自当时强度衰减而不是从 1 重来
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

        /// <summary>处于死机窗口（鬼律第九条：唯一的"战胜"形态）</summary>
        public bool IsHalted => Presence == WraithPresence.Halted;

        /// <summary>死机窗口剩余帧数，非死机为 0；两端本地对表，仅作演出与提示参考</summary>
        public int HaltRemaining => IsHalted ? Math.Max(haltDuration - presenceTimer, 0) : 0;

        /// <summary>反噬挣脱态：自某玩家的载体挣脱显形</summary>
        public bool IsEscaped => escapedOwner >= 0;

        /// <summary>挣脱源玩家，非挣脱态、玩家已失效或槽位已被他人顶替（名字对不上）为 null</summary>
        public Player EscapedOwnerPlayer {
            get {
                if (escapedOwner < 0 || escapedOwner >= Main.maxPlayers) {
                    return null;
                }
                Player player = Main.player[escapedOwner];
                if (player == null || !player.active) {
                    return null;
                }
                //槽位复用防御:挣脱时记下的名字与现占位者不符,视为原主已离场
                if (!string.IsNullOrEmpty(escapedOwnerName) && player.name != escapedOwnerName) {
                    return null;
                }
                return player;
            }
        }

        /// <summary>显形强度 0~1（平滑曲线），绘制透明度与灯光直接乘它</summary>
        public float PresenceStrength { get; private set; }

        /// <summary>生成锚点，游荡行为的圆心，经 ExtraData 同步给晚加入端</summary>
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
            //presenceRaw 不在此重置:克隆出的新实例本就是 Materializing,而客户端网络生成路径
            //在 OnSpawn 之前已读入权威 SyncVar,这里硬写会把晚加入收到的状态覆盖掉
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
            //权威 SyncVar 已先套用，对齐基准防止首帧误判翻转
            lastSeenPresence = Presence;
        }

        public override void AI() {
            if (Definition == null) {
                //注册表查不到定义:孤儿实体,权威端直接清理
                if (IsAuthority) {
                    RequestKill();
                }
                return;
            }

            //死机=冻在当下:视觉相位停摆,摆动/闪烁全部凝固
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
                    //死机期间行为积木停驱,残速迅速凝滞
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
            //客户端:权威状态翻转经增量同步到达时重置本地过渡计时,并以当下强度为新过渡起点
            if (Presence != lastSeenPresence) {
                WraithPresence previous = lastSeenPresence;
                lastSeenPresence = Presence;
                presenceTimer = 0;
                presenceBaseStrength = PresenceStrength;
                //远端的死机入场拍(权威端由 BeginHalt 直接播,不走翻转检测)
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
                        //迟到的发现补登:显形完成时没人在场,之后有人靠近
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
                    //自过渡起点衰减:显形中途被打断不会先跳满再消散
                    float t = MathHelper.Clamp(presenceTimer / (float)dematerializeFrames, 0f, 1f);
                    PresenceStrength = presenceBaseStrength * (1f - t * t);
                    if (IsAuthority && presenceTimer >= dematerializeFrames) {
                        RequestKill();
                    }
                    break;
                }
                case WraithPresence.Halted: {
                    //死机:全形在场但凝滞;窗口尽由权威裁决(默认自然消散)
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

        /// <summary>进入消散过渡，结束后实体销毁；仅权威端有效，消散中重复调用无事发生</summary>
        public void BeginDematerialize() {
            if (!IsAuthority || Presence == WraithPresence.Dematerializing) {
                return;
            }
            SetPresence(WraithPresence.Dematerializing);
            OnBeginDematerialize();
        }

        //====死机窗口====

        /// <summary>
        /// 进入死机窗口；仅权威端、自 Materializing/Present 有效（消散中的鬼追不回来）。
        /// durationTicks &lt;=0 取定义的 <see cref="WraithDefinition.HaltWindowTicks"/>
        /// </summary>
        public void BeginHalt(int durationTicks = -1) {
            if (!IsAuthority || IsHalted
                || (Presence != WraithPresence.Materializing && Presence != WraithPresence.Present)) {
                return;
            }
            haltDuration = durationTicks > 0 ? durationTicks : Math.Max(Definition?.HaltWindowTicks ?? 60 * 8, 1);
            SetPresence(WraithPresence.Halted);
            OnHaltBegin();
            //单人时权威即本地画面,入场拍在此播;多人客户端走翻转检测
            if (!Main.dedServ) {
                PlayHaltCue();
            }
        }

        /// <summary>解除死机回到在场（规则允许"惊醒"的鬼用）；仅权威端、死机中有效</summary>
        public void EndHalt() {
            if (!IsAuthority || !IsHalted) {
                return;
            }
            haltDuration = 0;
            SetPresence(WraithPresence.Present);
            OnHaltEnd();
        }

        /// <summary>死机入场拍：凝滞音 + 状态浮字（各端本地演出）</summary>
        private void PlayHaltCue() {
            SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.9f, Volume = 0.6f, MaxInstances = 2 }, Center);
            SoundEngine.PlaySound(SoundID.NPCDeath6 with { Pitch = 0.6f, Volume = 0.25f, MaxInstances = 2 }, Center);
            if (WraithSystemText.HaltPopup != null) {
                CombatText.NewText(HitBox, Definition?.EyeColor ?? new Color(120, 220, 200), WraithSystemText.HaltPopup.Value, true);
            }
        }

        //====反噬挣脱====

        /// <summary>
        /// 标为反噬挣脱态；仅权威端。挣脱是据点制的唯一合法例外：它挣脱的据点是刀本身，
        /// 因而显形在载体主人身边并缠着不走（默认 <see cref="UpdateEscaped"/> 的贴身漂移）
        /// </summary>
        internal void MarkEscaped(int playerWhoAmI) {
            if (!IsAuthority || playerWhoAmI < 0 || playerWhoAmI >= Main.maxPlayers) {
                return;
            }
            escapedOwner = playerWhoAmI;
            escapedOwnerName = Main.player[playerWhoAmI]?.name ?? "";
            NetUpdate = true;
            OnBacklashEscape(EscapedOwnerPlayer);
        }

        /// <summary>
        /// 挣脱态默认驱动（权威端，非死机/消散时每帧）：缠着载体主人不走，
        /// 主人失效（下线/死亡）则失去缠附对象自然消散。主题化表现覆写本方法
        /// </summary>
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

        /// <summary>
        /// 感知事件窗口，默认仅完全显形期间开启：半透明的成形体不触发触碰/凝视事件，
        /// 消散中不再响应，死机中同样关闭（凝滞之物不再看人，仪式交互走 WraithRites 的主动判距）。
        /// 想让"成形中就怕被看"的主题放宽窗口，覆写本属性
        /// </summary>
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

                //接近/脱离用双半径迟滞,贴着阈值抖动不会刷事件
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
        /// <summary>死机被解除（EndHalt 惊醒路径，窗口自然到期不走这里）</summary>
        protected virtual void OnHaltEnd() { }
        /// <summary>死机窗口到期未被消耗，默认自然消散；"带着执念缩回去"这类主题收场覆写它</summary>
        protected virtual void OnHaltExpired() => BeginDematerialize();
        /// <summary>被标为反噬挣脱态（owner 可能为 null：极端时序下主人已离场）</summary>
        protected virtual void OnBacklashEscape(Player owner) { }
        /// <summary>权威端每帧尾调用，子类自定逻辑挂这里而不是重写 AI</summary>
        protected virtual void OnAuthorityUpdate() { }

        //====绘制====

        public sealed override bool PreDraw(SpriteBatch spriteBatch, ref Color drawColor) {
            if (PresenceStrength > 0.003f) {
                DrawBody(spriteBatch, drawColor);
            }
            //实体没有注册贴图,永远跳过框架默认绘制
            return false;
        }

        //死机提示的键名缓存:键位变更低频,粗粒度定期刷新即可,不逐帧查绑定表(客户端 UI 态)
        private static string promptKeyCache;
        private static uint promptKeyCachedAt;

        private static string ResolvePromptKeyName() {
            if (promptKeyCache == null || Main.GameUpdateCount - promptKeyCachedAt > 60) {
                promptKeyCachedAt = Main.GameUpdateCount;
                promptKeyCache = CWRKeySystem.Wraith_Power?.GetAssignedKeys() is { Count: > 0 } keys
                    ? keys[0] : CWRKeySystem.Notbound.Value;
            }
            return promptKeyCache;
        }

        /// <summary>
        /// 死机窗口提示：本地玩家持载体走近时，头顶浮现借力键仪式提示，
        /// 窗口越接近尽头脉动越急（框架级演出，主题化时可整体覆写）
        /// </summary>
        public override void PostDraw(SpriteBatch spriteBatch, Color drawColor) {
            if (!IsHalted || Main.dedServ || PresenceStrength < 0.8f) {
                return;
            }
            Player local = Main.LocalPlayer;
            if (local == null || !local.active || local.dead) {
                return;
            }
            if (!WraithVessels.ResolveHeld(local).IsValid
                || Vector2.DistanceSquared(local.Center, Center) > WraithRites.RiteRange * WraithRites.RiteRange) {
                return;
            }

            string text = WraithSystemText.RitePrompt?.Format(ResolvePromptKeyName()) ?? ResolvePromptKeyName();

            float remaining01 = haltDuration > 0 ? HaltRemaining / (float)haltDuration : 1f;
            float pulseSpeed = MathHelper.Lerp(9f, 3f, remaining01);
            float pulse = 0.62f + 0.38f * MathF.Sin((float)Main.timeForVisualEffects * 0.05f * pulseSpeed);
            const float TextScale = 0.85f;
            Vector2 size = FontAssets.MouseText.Value.MeasureString(text) * TextScale;
            Vector2 pos = Center - Main.screenPosition + new Vector2(-size.X * 0.5f, -Size.Y * 0.5f - 36f);
            Utils.DrawBorderString(spriteBatch, text, pos, (Definition?.EyeColor ?? new Color(120, 220, 200)) * pulse, TextScale);
        }

        /// <summary>
        /// 本体绘制，默认给一具无主题的三层雾影 + 双眼占位；主题化时整体覆写。
        /// 批次已在世界空间（GameViewMatrix）开好，直接以 Center - Main.screenPosition 绘制
        /// </summary>
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

            //三层雾体:自下而上收窄,各层异相位横摆
            for (int i = 0; i < 3; i++) {
                float sway = MathF.Sin(VisualPhase * (0.8f + i * 0.31f) + i * 2.1f) * (3f + i * 2f);
                float yOffset = size.Y * (0.30f - i * 0.27f);
                Vector2 pos = center + new Vector2(sway, bob + yOffset);
                Vector2 scale = new(size.X * (0.92f - i * 0.18f), size.Y * 0.46f);
                spriteBatch.Draw(pixel, pos, src, body * (alpha * (0.34f - i * 0.06f)), 0f, half, scale, SpriteEffects.None, 0f);
            }

            //鬼火眼:显形过半才睁,带闪烁;眼距系数为固定常量,逐帧零分配
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
