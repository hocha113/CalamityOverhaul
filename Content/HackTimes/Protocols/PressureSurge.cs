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
    /// <summary>增压水域：给这片液体加压，泡在里面的东西一起被顶上天，玩家也不例外</summary>
    internal class PressureSurge : QuickHackDef
    {
        //加压区半径（8 格）
        private const float SurgeRadius = 128f;
        //上顶速度下限（负值向上；已有更快的上冲不覆盖）
        private const float LaunchVelocity = -14f;
        //横向阻尼，读作"水流只往上使劲"
        private const float LateralDamping = 0.9f;
        //撞顶伤害与冷却
        private const float SlamLifeRatio = 0.03f;
        private const float BossSlamLifeRatio = 0.0075f;
        private const int SlamCooldownFrames = 15;

        private static readonly Color Foam = new(150, 210, 255);

        //撞顶冷却账本，键是 NPC 槽位。窗口只有 15 帧，槽位复用最多让一跳提前，
        //且每次结算前都重新校验目标活着且带敌意；只在权威端写
        private static readonly Dictionary<int, ulong> slamCooldowns = [];
        private static readonly List<int> slamScratch = [];

        public override void SetDefaults() {
            UploadTime = 80;
            RamCost = 3;
            Category = QuickHackCategory.Control;
            SupportedTargets = HackTargetKind.Water;
            UnlockedByDefault = false;
        }

        public override int GetDuration() => 60 * 6;

        public override void Unload() {
            base.Unload();
            slamCooldowns.Clear();
            slamScratch.Clear();
        }

        public override bool CanApplyTo(IHackTarget target) {
            if (!base.CanApplyTo(target)) return false;
            return HackTargets.TryLiquid(target, out _, out _);
        }

        public override bool OnApply(IHackTarget target, Player caster) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return false;
            if (Main.netMode != NetmodeID.Server) {
                EmitBurst(HackTargets.TileWorldCenter(tileX, tileY));
            }
            return true;
        }

        public override void OnReplicatedApply(IHackTarget target, int elapsed) {
            if (HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) {
                EmitBurst(HackTargets.TileWorldCenter(tileX, tileY));
            }
        }

        public override bool OnTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryLiquid(target, out int tileX, out int tileY)) return true;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);

            //NPC/弹幕/掉落物的 AI 每端都在本地模拟，速度钳制要每端跑同一套
            //（只写权威端会表现成一顿一顿的橡皮筋），公式确定所以各端算出来一致；
            //撞顶伤害只在权威端结算，SimpleStrikeNPC 自带同步
            LaunchEntities(center);
            SlamCeilingHits(center);
            if (Main.netMode != NetmodeID.Server) {
                LaunchLocalPlayer(center);
                EmitColumn(center, elapsed);
            }
            return true;
        }

        public override void OnReplicatedTick(IHackTarget target, int elapsed) {
            if (!HackTargets.TryLiquidCoords(target, out int tileX, out int tileY)) return;
            Vector2 center = HackTargets.TileWorldCenter(tileX, tileY);
            LaunchEntities(center);
            LaunchLocalPlayer(center);
            EmitColumn(center, elapsed);
        }

        private static void LaunchEntities(Vector2 center) {
            float radiusSq = SurgeRadius * SurgeRadius;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || !IsWet(npc)) continue;
                if (Vector2.DistanceSquared(npc.Center, center) > radiusSq) continue;
                Launch(npc);
            }
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile projectile = Main.projectile[i];
                if (!projectile.active || !IsWet(projectile)) continue;
                if (Vector2.DistanceSquared(projectile.Center, center) > radiusSq) continue;
                Launch(projectile);
            }
            for (int i = 0; i < Main.maxItems; i++) {
                Item item = Main.item[i];
                if (!item.active || item.IsAir || !IsWet(item)) continue;
                if (Vector2.DistanceSquared(item.Center, center) > radiusSq) continue;
                Launch(item);
            }
        }

        /// <summary>
        /// 玩家的速度只能由他自己的客户端写（服务端推不动，别的客户端写了
        /// 也会被每帧差分盖回去），所以只碰本机玩家，每个端各自把自己顶上去
        /// </summary>
        private static void LaunchLocalPlayer(Vector2 center) {
            Player player = Main.LocalPlayer;
            if (player?.active != true || player.dead || player.ghost) return;
            if (!IsWet(player)) return;
            if (Vector2.DistanceSquared(player.Center, center)
                > SurgeRadius * SurgeRadius) {
                return;
            }
            Launch(player);
        }

        private static void Launch(Entity entity) {
            entity.velocity.Y = Math.Min(entity.velocity.Y, LaunchVelocity);
            entity.velocity.X *= LateralDamping;
        }

        private static bool IsWet(Entity entity)
            => entity.wet || entity.lavaWet || entity.honeyWet || entity.shimmerWet;

        //撞顶判定放宽到区上方一条竖直通道：被顶出液面的怪撞上天花板时早已不在水里
        private static void SlamCeilingHits(Vector2 center) {
            ulong frame = Main.GameUpdateCount;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.townNPC
                    || npc.dontTakeDamage || npc.immortal) {
                    continue;
                }
                //collideY + 上冲的旧速度 = 这一帧撞上了头顶的实心格
                if (!npc.collideY || npc.oldVelocity.Y > -6f) continue;
                Vector2 delta = npc.Center - center;
                if (Math.Abs(delta.X) > SurgeRadius
                    || delta.Y > SurgeRadius || delta.Y < -SurgeRadius * 3f) {
                    continue;
                }
                if (slamCooldowns.TryGetValue(i, out ulong next) && frame < next) continue;
                slamCooldowns[i] = frame + SlamCooldownFrames;

                //Water 目标拿不到 NpcScannable 那份 EffectMult 折扣，Boss 减免在这里给
                float ratio = NpcGroupHelper.IsBossTier(npc)
                    ? BossSlamLifeRatio
                    : SlamLifeRatio;
                int damage = Math.Max(20, (int)(npc.lifeMax * ratio));
                npc.SimpleStrikeNPC(damage, 0, false, 0f, null, false, 0f, true);
                if (Main.netMode != NetmodeID.Server) EmitSlam(npc.Top);
            }
            PruneCooldowns(frame);
        }

        private static void PruneCooldowns(ulong frame) {
            if (slamCooldowns.Count < 64) return;
            slamScratch.Clear();
            foreach (var pair in slamCooldowns) {
                if (pair.Value + 600 < frame) slamScratch.Add(pair.Key);
            }
            for (int i = 0; i < slamScratch.Count; i++) {
                slamCooldowns.Remove(slamScratch[i]);
            }
            slamScratch.Clear();
        }

        private static void EmitBurst(Vector2 center) {
            for (int i = 0; i < 16; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-2.5f, 2.5f),
                    Main.rand.NextFloat(-9f, -4f));
                PRTLoader.NewParticle<PRT_Spark>(center, vel, Foam, 1.1f)
                    ?.Configure(false, 24);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Splash with { Pitch = -0.35f }, center);
            }
        }

        //持续水柱：贴着区宽随机起竖直上抛，别做成从中心炸开的球
        private static void EmitColumn(Vector2 center, int elapsed) {
            if (elapsed % 3 != 0) return;
            Vector2 pos = center + new Vector2(
                Main.rand.NextFloat(-SurgeRadius * 0.85f, SurgeRadius * 0.85f),
                Main.rand.NextFloat(-12f, 12f));
            Vector2 vel = new(Main.rand.NextFloat(-0.6f, 0.6f),
                Main.rand.NextFloat(-8.5f, -5f));
            PRTLoader.NewParticle<PRT_Spark>(pos, vel, Foam, 0.75f)
                ?.Configure(false, 20);
        }

        private static void EmitSlam(Vector2 top) {
            for (int i = 0; i < 6; i++) {
                Vector2 vel = new(Main.rand.NextFloat(-3f, 3f),
                    Main.rand.NextFloat(0.5f, 2.5f));
                PRTLoader.NewParticle<PRT_Spark>(top, vel, Color.White, 0.8f)
                    ?.Configure(false, 12);
            }
        }
    }
}
