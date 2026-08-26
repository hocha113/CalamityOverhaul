using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalMoonLord.Rendering;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.MoonLord
{
    /// <summary>
    /// 黑闪印记：残酷月总遗物。周身运行黑闪节拍，胸前印记环收缩读秒，
    /// 窗口内命中触发黑闪（巨幅倍增+必定暴击+全屏冲击帧）；
    /// 连闪叠层放大倍率并缩短节拍，失手（空窗/脱拍）清空连闪
    /// </summary>
    internal class BlackFlashSigil : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期月总掉落物（约 50 金购价）的 4 倍
            Item.value = Item.buyPrice(2, 0, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.GetModPlayer<BlackFlashSigilPlayer>().Equipped = true;
        }
    }

    /// <summary>
    /// 黑闪节拍状态机：全部状态在实例字段，判定只在所有者端执行
    /// （伤害修改经原版 StrikeNPC 广播落到各端，无需服务器权威）。
    /// 与机械骷髅王遗物（连击充能爆发窗口）的区隔：这里是离散时机判定的单发极值，
    /// 没有资源条、没有持续窗口，核心体验是节奏与惩罚
    /// </summary>
    internal class BlackFlashSigilPlayer : ModPlayer
    {
        #region 参数
        /// <summary>零层节拍周期（帧）</summary>
        public const int BasePeriod = 96;
        /// <summary>每层连闪缩短的周期（帧）</summary>
        public const int PeriodPerStack = 4;
        /// <summary>周期下限（帧）</summary>
        public const int MinPeriod = 54;
        /// <summary>判定窗口宽度（帧），位于周期末尾</summary>
        public const int WindowFrames = 14;
        /// <summary>黑闪后的余隙（帧）：跟刀与同帧多段命中不判失手</summary>
        public const int GraceFrames = 8;
        /// <summary>窗口前摇：印记收缩读秒时长（帧）</summary>
        public const int TelegraphFrames = 36;
        /// <summary>连闪层数上限</summary>
        public const int MaxStacks = 12;
        /// <summary>黑闪基础倍率</summary>
        public const float MultBase = 8f;
        /// <summary>每层连闪追加倍率</summary>
        public const float MultPerStack = 2.5f;
        /// <summary>进入领域感（弹幕减速暗示+日蚀轮廓）的层数门槛</summary>
        public const int DomainStacks = 6;
        /// <summary>入战热度时长（帧）：静息不读秒不出声</summary>
        private const int CombatHeatFrames = 300;

        /// <summary>黑金电弧金端（系列鎏金的印记变体）</summary>
        internal static readonly Color GoldArc = new(255, 208, 92);
        /// <summary>黑闪红（对齐月总 BlackFlashRed 语汇）</summary>
        internal static readonly Color FlashRed = new(255, 46, 58);
        #endregion

        #region 状态（全部实例字段，禁 static）
        /// <summary>本帧装备中，物品钩子逐帧点亮</summary>
        public bool Equipped;
        /// <summary>节拍计时（帧），黑闪与整拍回绕时归零</summary>
        public int BeatTimer;
        /// <summary>连闪层数</summary>
        public int Stacks;
        /// <summary>入战热度：任何被判定的命中或挥动武器刷新</summary>
        private int combatHeat;
        /// <summary>黑闪余辉（渲染读取，触发瞬间置满）</summary>
        public int FlashGlow;
        /// <summary>失手碎裂闪烁（渲染读取）</summary>
        public int BreakFlicker;
        /// <summary>渲染层淡入淡出（各端本地平滑，静息归零）</summary>
        public float VisualFade;
        /// <summary>旁观者电弧强度（净通道写入，本地衰减）</summary>
        private float remoteArc;
        private int remoteArcLife;
        #endregion

        #region 派生量
        /// <summary>当前节拍周期：连闪越高越短</summary>
        public int Period => Math.Max(BasePeriod - Stacks * PeriodPerStack, MinPeriod);
        /// <summary>窗口起点帧</summary>
        public int WindowStart => Period - WindowFrames;
        /// <summary>已入战（读秒、判定、演出的总开关）</summary>
        public bool Armed => Equipped && (combatHeat > 0 || Stacks > 0);
        /// <summary>判定窗口开启（窗口内命中会立刻黑闪并归零计时，故窗内恒未消耗）</summary>
        public bool WindowOpen => Armed && BeatTimer >= WindowStart;

        /// <summary>收缩前摇进度 0..1（0=尚未进入前摇），渲染用</summary>
        public float TelegraphT {
            get {
                int start = WindowStart - TelegraphFrames;
                if (!Armed || BeatTimer < start) {
                    return 0f;
                }
                return MathHelper.Clamp((BeatTimer - start) / (float)TelegraphFrames, 0f, 1f);
            }
        }

        /// <summary>窗口开度 0..1，渲染用</summary>
        public float WindowT => WindowOpen
            ? 1f - (BeatTimer - WindowStart) / (float)WindowFrames * 0.35f
            : 0f;

        /// <summary>体表电弧强度 0..1：自身层数 / 旁观者转播 / 黑闪余辉取最大</summary>
        public float ArcLevel {
            get {
                float v = Stacks / (float)MaxStacks;
                if (remoteArcLife > 0) {
                    v = Math.Max(v, remoteArc * (0.35f + 0.65f * remoteArcLife / 240f));
                }
                if (FlashGlow > 0) {
                    v = Math.Max(v, 0.75f * FlashGlow / 26f);
                }
                return v;
            }
        }

        /// <summary>印记锚点：胸前</summary>
        internal Vector2 SigilCenter()
            => Player.MountedCenter + new Vector2(0f, -5f * Player.gravDir);
        #endregion

        public override void ResetEffects() => Equipped = false;

        public override void UpdateDead() => ResetRhythm();

        private void ResetRhythm() {
            Stacks = 0;
            BeatTimer = 0;
            combatHeat = 0;
            FlashGlow = 0;
        }

        public override void PreUpdateMovement() {
            TickPresentation();

            if (Player.whoAmI != Main.myPlayer) {
                return;
            }
            if (!Equipped) {
                if (Stacks != 0 || BeatTimer != 0) {
                    ResetRhythm();
                }
                return;
            }

            //挥动带伤害的物品视作入战（不必等第一次命中）
            if (Player.itemAnimation > 0 && Player.HeldItem?.IsAir == false && Player.HeldItem.damage > 0) {
                combatHeat = CombatHeatFrames;
            }
            if (combatHeat > 0) {
                combatHeat--;
            }
            if (!Armed) {
                BeatTimer = 0;
                return;
            }

            BeatTimer++;
            int windowStart = WindowStart;

            //读秒三声爬升 + 开窗脆响（可内化的节拍语言）
            if (!VaultUtils.isServer) {
                if (BeatTimer == windowStart - TelegraphFrames) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = -0.6f }, Player.Center);
                }
                else if (BeatTimer == windowStart - 12) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.4f, Pitch = -0.15f }, Player.Center);
                }
                else if (BeatTimer == windowStart) {
                    SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.6f }, Player.Center);
                    PRTLoader.NewParticle<PRT_Light>(SigilCenter(), Vector2.Zero, GoldArc, 0.42f)
                        ?.Configure(10, 1.2f);
                }
            }

            //整拍回绕：窗口空过即失手（黑闪命中会先把计时归零，走不到这里）
            if (BeatTimer >= Period) {
                if (Stacks > 0) {
                    BreakCombo();
                }
                BeatTimer = 0;
            }

            //高层领域感：日蚀轮廓低强度续租（克制），只染所有者本地屏幕
            if (Stacks >= DomainStacks && !VaultUtils.isServer) {
                MLordEclipseSky.ReportBossDrive(-1, 0.10f + Stacks * 0.02f, ArcLevel * 0.4f);
            }
        }

        /// <summary>表现计时衰减：各端（含旁观副本）逐帧走</summary>
        private void TickPresentation() {
            if (FlashGlow > 0) {
                FlashGlow--;
            }
            if (BreakFlicker > 0) {
                BreakFlicker--;
            }
            if (remoteArcLife > 0) {
                remoteArcLife--;
                if (remoteArcLife == 0) {
                    remoteArc = 0f;
                }
            }
            float fadeTarget = Equipped && (Armed || ArcLevel > 0.01f) ? 1f : 0f;
            VisualFade = MathHelper.Lerp(VisualFade, fadeTarget, 0.12f);
            if (VisualFade < 0.01f) {
                VisualFade = 0f;
            }
        }

        #region 判定
        /// <summary>召唤栏产物不参与节拍（玩家无法掌控其出手时机），鞭类仍判</summary>
        private static bool ProjEligible(Projectile proj) {
            if (proj == null) {
                return true;
            }
            if (proj.minion || proj.sentry || proj.minionSlots > 0f) {
                return false;
            }
            //召唤物派生弹（本体 minion=false 的仆从射弹）同样不判；鞭（SummonMeleeSpeed）保留
            if (proj.DamageType.CountsAsClass(DamageClass.Summon)
                && proj.DamageType != DamageClass.SummonMeleeSpeed) {
                return false;
            }
            return true;
        }

        private static bool TargetEligible(NPC target) => target != null && !target.friendly;

        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
            => ModifyJudged(null, target, ref modifiers);

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
            => ModifyJudged(proj, target, ref modifiers);

        /// <summary>窗口内命中：巨幅倍增 + 必定暴击（纯读取，消耗在 OnHit 落账）</summary>
        private void ModifyJudged(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (!Equipped || Player.whoAmI != Main.myPlayer
                || !ProjEligible(proj) || !TargetEligible(target) || !WindowOpen) {
                return;
            }
            modifiers.FinalDamage *= MultBase + MultPerStack * Stacks;
            modifiers.SetCrit();
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => JudgeHit(null, target);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => JudgeHit(proj, target);

        private void JudgeHit(Projectile proj, NPC target) {
            if (!Equipped || Player.whoAmI != Main.myPlayer
                || !ProjEligible(proj) || !TargetEligible(target)) {
                return;
            }

            bool wasArmed = Armed;
            combatHeat = CombatHeatFrames;
            if (!wasArmed) {
                //静息被唤醒：该击中性，节拍即刻起跳，首个窗口在一次完整收缩后到来
                BeatTimer = Math.Max(WindowStart - TelegraphFrames, 0);
                return;
            }
            if (WindowOpen) {
                TriggerFlash(target);
                return;
            }
            //黑闪/回绕后的余隙：同帧多段与紧跟的跟刀不罚
            if (BeatTimer < GraceFrames) {
                return;
            }
            BreakCombo();
        }

        /// <summary>黑闪落账：叠层、归零节拍（下个窗口更快）、演出与转播</summary>
        private void TriggerFlash(NPC target) {
            Stacks = Math.Min(Stacks + 1, MaxStacks);
            BeatTimer = 0;
            FlashGlow = 26;
            PlayFlashFX(Player, target.Center, Stacks);
            BlackFlashSigilNet.SendFlash(Player.whoAmI, target.Center, (byte)Stacks);
        }

        /// <summary>失手：清空连闪，印记碎裂反馈（纯本地，旁观者无需知晓）</summary>
        private void BreakCombo() {
            if (Stacks <= 0) {
                return;
            }
            Stacks = 0;
            BreakFlicker = 18;
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.4f, Pitch = 0.15f }, Player.Center);
            SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f, Pitch = -0.9f }, Player.Center);
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f);
                vel.Y -= 1.2f;
                PRTLoader.NewParticle<PRT_Spark>(SigilCenter(), vel,
                    Color.Lerp(FlashRed, new Color(90, 40, 50), Main.rand.NextFloat()),
                    Main.rand.NextFloat(0.6f, 1f))?.Configure(true, Main.rand.Next(12, 20));
            }
        }
        #endregion

        #region 黑闪演出（本地共用：所有者与转播端同一份）
        /// <summary>
        /// 复用月总黑闪语汇：全屏红黑负片冲击帧（MLordBlackFlashFX，2 帧定格 + 冲击波）、
        /// 定向震屏、天体星尘爆、黑金电弧四溅。定格只影响本地演出，不冻结逻辑帧
        /// </summary>
        internal static void PlayFlashFX(Player owner, Vector2 impact, int stacks) {
            if (VaultUtils.isServer) {
                return;
            }
            float tier = stacks / (float)MaxStacks;
            Vector2 dir = (impact - owner.Center).SafeNormalize(Vector2.UnitX);

            //全屏黑闪（内部自带距离门控与衰减）
            MLordBlackFlashFX.PushFlash(impact);
            if (Vector2.Distance(impact, Main.LocalPlayer.Center) < 1600f) {
                MLordScreenFX.Punch(impact, 5.5f + tier * 4f, 12, dir);
            }
            MLordScreenFX.StarBurst(impact, 0.85f + tier * 0.5f, 9 + stacks);

            //黑金电弧向掷向锥形迸出，金红双色
            int sparkCount = 13 + stacks * 2;
            for (int i = 0; i < sparkCount; i++) {
                Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.85f, 0.85f))
                    * Main.rand.NextFloat(4f, 12.5f);
                Color c = Main.rand.NextBool()
                    ? Color.Lerp(GoldArc, Color.White, Main.rand.NextFloat(0.3f))
                    : FlashRed;
                PRTLoader.NewParticle<PRT_Spark>(impact, vel, c,
                    Main.rand.NextFloat(1f, 1.9f))?.Configure(false, Main.rand.Next(14, 26));
            }
            //体表回响：印记处短促金弧
            for (int i = 0; i < 5; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f);
                PRTLoader.NewParticle<PRT_Spark>(owner.MountedCenter, vel, GoldArc,
                    Main.rand.NextFloat(0.7f, 1.1f))?.Configure(false, Main.rand.Next(10, 16), owner);
            }

            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.95f, Pitch = -0.15f }, impact);
            SoundEngine.PlaySound(SoundID.Thunder with { Volume = 0.5f, Pitch = 0.35f }, impact);
            SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.5f, Pitch = -0.6f }, impact);
        }

        /// <summary>净通道落地：旁观者端记录电弧强度并播放同一份黑闪演出</summary>
        internal void ApplyRemoteFlash(Vector2 impact, int stacks) {
            remoteArc = MathHelper.Clamp(stacks / (float)MaxStacks, 0f, 1f);
            remoteArcLife = 240;
            FlashGlow = 26;
            PlayFlashFX(Player, impact, stacks);
        }
        #endregion
    }

    /// <summary>
    /// 黑闪演出转播：所有者客户端 → 服务器中继 → 其余客户端。
    /// 纯表现包（演出可见性条款），不承载任何数值权威
    /// </summary>
    internal class BlackFlashSigilNet : CWRNetChannel
    {
        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读净负载再守卫
            int owner = reader.ReadByte();
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            int stacks = reader.ReadByte();

            if (Main.netMode == NetmodeID.Server) {
                if (owner != whoAmI) {
                    return;
                }
                ModPacket relay = CWRNetWork.GetPacket<BlackFlashSigilNet>();
                relay.Write((byte)owner);
                relay.Write(x);
                relay.Write(y);
                relay.Write((byte)stacks);
                relay.Send(-1, whoAmI);
                return;
            }
            if (owner < 0 || owner >= Main.maxPlayers || owner == Main.myPlayer) {
                return;
            }
            Player player = Main.player[owner];
            if (player?.active != true) {
                return;
            }
            player.GetModPlayer<BlackFlashSigilPlayer>()
                .ApplyRemoteFlash(new Vector2(x, y), Math.Clamp(stacks, 0, BlackFlashSigilPlayer.MaxStacks));
        }

        /// <summary>所有者端发送（单人无包，本地演出已在触发处播放）</summary>
        internal static void SendFlash(int owner, Vector2 impact, byte stacks) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<BlackFlashSigilNet>();
            packet.Write((byte)owner);
            packet.Write(impact.X);
            packet.Write(impact.Y);
            packet.Write(stacks);
            packet.Send();
        }
    }
}
