using CalamityOverhaul.Common;
using CalamityOverhaul.Content.HackTimes.Scannables;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>
    /// 重力反转：以施术者为心的 1400x900 px 移动力场，区内敌怪与掉落物
    /// 往上掉，敌方弹幕的重力同样反转；玩家不受影响（义体补偿，设计意图）。<br/>
    /// 字段归属端：敌怪速度只在权威端写（NPC 仿真是服务端权威，客户端写会
    /// 跟同步打架）；掉落物与敌方弹幕在每个端按同一确定性规则各写各的
    /// （它们在各端本地仿真，规则一致仿真就一致）；视觉每端自绘。<br/>
    /// 区域锚点取施术者而非施放光标，World 身份零负载，光标座标过不了网，
    /// 施术者位置是全端同步的现成锚
    /// </summary>
    internal class GravityInvert : QuickHackDef
    {
        private const float ZoneHalfWidth = 700f;
        private const float ZoneHalfHeight = 450f;
        private const float NpcLift = 0.60f;
        private const float NpcLiftCap = -12f;
        private const float ItemLift = 0.45f;
        private const float ItemLiftCap = -10f;
        private const float ProjLift = 0.35f;
        private const float ProjLiftCap = -10f;
        private const int CeilingHitCooldown = 15;

        //撞顶伤害冷却：槽位→上次结算帧。世界级账本，最后一个力场关闭时清空，
        //切世界由 WorldHackLedgerCleanup 兜底；上限 Main.maxNPCs 条，不会长大
        private static readonly Dictionary<int, ulong> ceilingHitAt = [];
        private static int activeZones;
        //帧驱动去重：OnTick 按效果逐个进来，真正的力场处理一帧只跑一遍，
        //在第一个到达的 tick 里遍历全部力场，这样每个力场都能拿到
        //自己效果上的 CasterIndex（OnTick 签名里拿不到效果实例）
        private static ulong lastAuthorityPassFrame = ulong.MaxValue;
        private static ulong lastClientPassFrame = ulong.MaxValue;

        private static readonly Color LiftGlow = new(150, 255, 190);

        public override void SetDefaults() {
            UploadTime = 180;
            RamCost = 7;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.World;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 8;

        public override void Unload() {
            base.Unload();
            ClearLedger();
        }

        /// <summary>切世界清账：冷却帧标与力场计数属于上一个世界</summary>
        internal static void ClearLedger() {
            ceilingHitAt.Clear();
            activeZones = 0;
            lastAuthorityPassFrame = ulong.MaxValue;
            lastClientPassFrame = ulong.MaxValue;
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            return target is WorldScannable;
        }

        public override bool CanApplyTo(IHackTarget target, Player caster) {
            if (!CanApplyTo(target) || caster == null) return false;
            //同一施术者只准一个力场：World 目标彼此 TargetEquals，
            //队列去重只挡上传中的，这里把已生效的也挡掉
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (effect.Active && effect.Hack is GravityInvert
                    && effect.CasterIndex == caster.whoAmI) {
                    return false;
                }
            }
            return true;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (target is not WorldScannable || caster == null) return false;
            if (Main.netMode != NetmodeID.MultiplayerClient) activeZones++;
            if (Main.netMode != NetmodeID.Server) EmitOpenCue(caster.Center);
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (Main.dedServ) return;
            //钩子签名拿不到效果实例，但复制表此刻已含刚落地的这条
            //取激活号最大（分配是单调的）的那条反查施术者，就地播开场声画，
            //位置化音效对远处旁观者自然衰减
            ActiveHackEffect newest = null;
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active || !effect.Replicated
                    || effect.Hack is not GravityInvert) {
                    continue;
                }
                if (newest == null
                    || effect.ActivationId > newest.ActivationId) {
                    newest = effect;
                }
            }
            if (newest == null || newest.CasterIndex < 0
                || newest.CasterIndex >= Main.maxPlayers) {
                return;
            }
            Player caster = Main.player[newest.CasterIndex];
            if (caster?.active == true) EmitOpenCue(caster.Center);
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            ulong frame = Main.GameUpdateCount;
            if (lastAuthorityPassFrame == frame) return true;
            lastAuthorityPassFrame = frame;
            RunZonePass(authority: true, frame);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            ulong frame = Main.GameUpdateCount;
            if (lastClientPassFrame == frame) return;
            lastClientPassFrame = frame;
            RunZonePass(authority: false, frame);
        }

        public override void OnRemove(IHackTarget target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            activeZones = Math.Max(0, activeZones - 1);
            if (activeZones == 0) ceilingHitAt.Clear();
        }

        //力场归属随效果生命周期走，远端无需清账（客户端不持有账本）

        #region 力场逐帧处理

        /// <summary>
        /// 一帧一遍：遍历追踪器里全部重力反转效果，按各自施术者的位置张力场。
        /// 权威趟写 NPC/掉落物/弹幕并结算撞顶；客户端趟只写掉落物与弹幕
        /// （本地仿真需要同样的力，NPC 不碰，位置由服务端同步说了算）
        /// </summary>
        private static void RunZonePass(bool authority, ulong frame) {
            IReadOnlyList<ActiveHackEffect> effects
                = HackEffectTracker.AllActiveTileEffects;
            for (int i = 0; i < effects.Count; i++) {
                ActiveHackEffect effect = effects[i];
                if (!effect.Active || effect.Hack is not GravityInvert) continue;
                if (authority == effect.Replicated) continue;
                if (effect.CasterIndex < 0
                    || effect.CasterIndex >= Main.maxPlayers) {
                    continue;
                }
                Player caster = Main.player[effect.CasterIndex];
                if (caster?.active != true) continue;
                ProcessZone(caster.Center, authority, frame);
            }
        }

        private static void ProcessZone(Vector2 center, bool authority, ulong frame) {
            float left = center.X - ZoneHalfWidth;
            float right = center.X + ZoneHalfWidth;
            float top = center.Y - ZoneHalfHeight;
            float bottom = center.Y + ZoneHalfHeight;

            if (authority) {
                LiftNpcs(left, right, top, bottom, frame);
            }
            LiftItems(left, right, top, bottom);
            LiftHostileProjectiles(left, right, top, bottom);
            if (!Main.dedServ) DrawZoneCue(center, frame);
        }

        private static bool Inside(Vector2 pos, float left, float right,
            float top, float bottom) {
            return pos.X >= left && pos.X <= right
                && pos.Y >= top && pos.Y <= bottom;
        }

        private static void LiftNpcs(float left, float right, float top,
            float bottom, ulong frame) {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.noGravity) continue;
                if (!Inside(npc.Center, left, right, top, bottom)) continue;

                npc.velocity.Y = MathF.Max(npc.velocity.Y - NpcLift, NpcLiftCap);

                //撞顶：上升中被 tile 顶住（collideY 由原版碰撞标记）
                if (!npc.collideY || npc.oldVelocity.Y >= -4f) continue;
                if (ceilingHitAt.TryGetValue(i, out ulong last)
                    && frame - last < CeilingHitCooldown) {
                    continue;
                }
                ceilingHitAt[i] = frame;
                int dmg = Math.Max(20, (int)(npc.lifeMax * 0.03f));
                npc.SimpleStrikeNPC(dmg, 0, false, 0f, null, false, 0f, true);
                if (!Main.dedServ) EmitCeilingImpact(npc.Top);
            }
        }

        private static void LiftItems(float left, float right, float top,
            float bottom) {
            for (int i = 0; i < Main.maxItems; i++) {
                Item item = Main.item[i];
                if (item?.active != true || item.IsAir) continue;
                if (!Inside(item.Center, left, right, top, bottom)) continue;
                item.velocity.Y = MathF.Max(item.velocity.Y - ItemLift,
                    ItemLiftCap);
            }
        }

        private static void LiftHostileProjectiles(float left, float right,
            float top, float bottom) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj?.active != true || !proj.hostile) continue;
                if (!Inside(proj.Center, left, right, top, bottom)) continue;
                proj.velocity.Y = MathF.Max(proj.velocity.Y - ProjLift,
                    ProjLiftCap);
            }
        }

        #endregion

        #region 表现

        //边界向上流动的光尘：底边与两侧各起一缕，读作力场的"呼吸"
        private static void DrawZoneCue(Vector2 center, ulong frame) {
            if (frame % 5 != 0) return;
            float x = center.X + Main.rand.NextFloat(-ZoneHalfWidth, ZoneHalfWidth);
            PRTLoader.NewParticle<PRT_Spark>(
                new Vector2(x, center.Y + ZoneHalfHeight),
                new Vector2(0f, Main.rand.NextFloat(-3.2f, -1.6f)),
                LiftGlow, 0.7f)?.Configure(false, 30);

            float side = Main.rand.NextBool()
                ? center.X - ZoneHalfWidth
                : center.X + ZoneHalfWidth;
            PRTLoader.NewParticle<PRT_Spark>(
                new Vector2(side, center.Y + Main.rand.NextFloat(
                    -ZoneHalfHeight, ZoneHalfHeight)),
                new Vector2(0f, -1.4f), LiftGlow * 0.7f, 0.5f)
                ?.Configure(false, 24);
        }

        private static void EmitOpenCue(Vector2 center) {
            for (int i = 0; i < 20; i++) {
                Vector2 pos = center + new Vector2(
                    Main.rand.NextFloat(-ZoneHalfWidth, ZoneHalfWidth),
                    Main.rand.NextFloat(-ZoneHalfHeight, ZoneHalfHeight));
                PRTLoader.NewParticle<PRT_Spark>(pos,
                    new Vector2(0f, Main.rand.NextFloat(-2.8f, -1.2f)),
                    LiftGlow, 0.8f)?.Configure(false, 34);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.Hacker with { Pitch = -0.4f },
                    center);
            }
        }

        private static void EmitCeilingImpact(Vector2 top) {
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Spark>(top,
                    new Vector2(Main.rand.NextFloat(-2f, 2f),
                        Main.rand.NextFloat(0.5f, 2f)),
                    LiftGlow, 0.8f)?.Configure(true, 18);
            }
        }

        #endregion
    }

    /// <summary>
    /// 世界组协议的静态账本清扫。HackTime.Reset 只认它出生时就有的那几个
    /// Clear 调用（共用文件不动，见补丁规格），切世界的清账由这里自理
    /// </summary>
    internal class WorldHackLedgerCleanup : ModSystem
    {
        public override void OnWorldUnload() => ClearAll();

        public override void Unload() => ClearAll();

        private static void ClearAll() {
            GravityInvert.ClearLedger();
            StormInject.ClearLedger();
            DielSkip.ClearWorldCooldown();
        }
    }
}
