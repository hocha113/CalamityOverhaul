using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.HackTimes.Protocols
{
    /// <summary>电解水域：给这片液体通电，泡在里面的东西一起吃</summary>
    internal class Electrolysis : QuickHackDef
    {
        //触电判定半径（像素）
        private const float ShockRadius = 176f;
        //每几帧结算一次
        private const int ShockInterval = 20;
        //每跳按最大生命的比例，Boss 级另算
        private const float ShockLifeRatio = 0.012f;
        private const float BossShockLifeRatio = 0.003f;

        private static readonly Color Arc = new(120, 220, 255);
        //共享血池的体节只该吃一份，否则一条蠕虫泡在水里等于被乘上体节数
        private static readonly HashSet<int> shockedAnchors = [];

        public override void SetDefaults() {
            UploadTime = 90;
            RamCost = 4;
            Category = QuickHackCategory.Lethal;
            SupportedTargets = HackTargetKind.Water;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 6;

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            //微光不是电解质，通电只会把它当普通水处理，语义与观感都不成立
            return Main.tile[tileX, tileY].LiquidType != LiquidID.Shimmer;
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            if (Main.netMode != NetmodeID.Server) {
                EmitCharge(HackTargets.TileWorldCenter(tileX, tileY));
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitCharge(HackTargets.TileWorldCenter(tileX, tileY));
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return true;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);

            if (elapsed % ShockInterval == 0) {
                ShockSubmerged(center);
            }
            if (Main.netMode != NetmodeID.Server) EmitArc(center, elapsed);
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitArc(HackTargets.TileWorldCenter(tileX, tileY), elapsed);
            }
        }

        //只打真的泡在液体里的敌人，站在岸上不该挨电
        private static void ShockSubmerged(Vector2 center) {
            shockedAnchors.Clear();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.townNPC
                    || npc.dontTakeDamage || npc.immortal) {
                    continue;
                }
                if (Vector2.DistanceSquared(npc.Center, center)
                    > ShockRadius * ShockRadius) {
                    continue;
                }
                if (!npc.wet && !npc.lavaWet && !npc.honeyWet) continue;
                if (!shockedAnchors.Add(NpcGroupHelper.GetAnchorIndex(npc))) continue;

                //Water 目标拿不到 NpcScannable 那份 EffectMult 折扣，Boss 的减免只能在这里给
                float ratio = NpcGroupHelper.IsBossTier(npc)
                    ? BossShockLifeRatio
                    : ShockLifeRatio;
                int damage = Math.Max(24, (int)(npc.lifeMax * ratio));
                npc.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true);
            }
            shockedAnchors.Clear();
        }

        private static void EmitCharge(Vector2 center) {
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2CircularEdge(4.5f, 2.2f);
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Arc, 1.1f)
                    ?.Configure(false, 22);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(CWRSound.ShortCircuit with { Pitch = -0.3f }, center);
            }
        }

        //电弧贴着水面横向乱窜，别做成从中心炸开的球
        private static void EmitArc(Vector2 center, int elapsed) {
            if (elapsed % 4 != 0) return;
            Vector2 pos = center + new Vector2(
                Main.rand.NextFloat(-ShockRadius * 0.8f, ShockRadius * 0.8f),
                Main.rand.NextFloat(-10f, 10f));
            Vector2 vel = new(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-1.2f, -0.2f));
            PRTLoader.NewParticle<PRT_Spark>(pos, vel, Arc, 0.7f)?.Configure(false, 14);
        }
    }
}
