using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles
{
    /// <summary>
    /// 水蛭通道（生命吸取全接管）。锁定至多 3 个视线内目标拉血丝持续抽血；
    /// 白热带附真实回复 +2HP/s（owner 端结算自身，原版只加再生）。<br/>
    /// 同步契约：3 个目标 whoAmI 按字节编进 ai[0]（255 = 空位，2^24 内 float 精确），
    /// owner 扫描写入、变化才 netUpdate；远端解码同一份列表画血丝，画与判同源
    /// </summary>
    internal class GsDrainTetherProj : GsConduitHeldProj
    {
        internal const float DrainRadius = 420f;
        private const int MaxTargets = 3;
        private const int EmptySlot = 255;

        protected override int BoundItemID => ItemID.SoulDrain;
        protected override float ManaPerSecond => 5f;
        protected override float HeatPerTick => 0.9f;
        protected override int HitCooldown => 15;
        protected override float TickDamageCoef => 0.35f;
        protected override bool UseChannelFlag => true;

        private readonly int[] targetCache = new int[MaxTargets];
        private uint lastScanTick;

        //==================== 目标编码（ai[0]，随原生同步走） ====================

        private static float Encode(Span<int> t) => t[0] + t[1] * 256 + t[2] * 65536;

        private void Decode() {
            int v = (int)Projectile.ai[0];
            targetCache[0] = v & 0xFF;
            targetCache[1] = (v >> 8) & 0xFF;
            targetCache[2] = (v >> 16) & 0xFF;
        }

        /// <summary>解码后取第 i 个锁定目标；无效返回 null</summary>
        private NPC LockedTarget(int i) {
            int who = targetCache[i];
            if (who == EmptySlot || who < 0 || who >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[who];
            if (!npc.active || npc.dontTakeDamage || npc.DistanceSQ(Owner.MountedCenter) > DrainRadius * DrainRadius * 1.44f) {
                return null;
            }
            return npc;
        }

        protected override void OwnerExtraTick(GsHeatPlayer hp) {
            //每 10t 重扫锁定列表：视线内最近 3 个可追击目标
            if (Main.GameUpdateCount - lastScanTick < 10) {
                return;
            }
            lastScanTick = Main.GameUpdateCount;

            Span<int> picked = [EmptySlot, EmptySlot, EmptySlot];
            Span<float> dists = [float.MaxValue, float.MaxValue, float.MaxValue];
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float d = npc.DistanceSQ(Owner.MountedCenter);
                if (d > DrainRadius * DrainRadius || npc.whoAmI >= EmptySlot) {
                    continue;
                }
                if (!Collision.CanHitLine(Owner.MountedCenter, 1, 1, npc.Center, 1, 1)) {
                    continue;
                }
                //按距离插入前三
                for (int slot = 0; slot < MaxTargets; slot++) {
                    if (d < dists[slot]) {
                        for (int k = MaxTargets - 1; k > slot; k--) {
                            dists[k] = dists[k - 1];
                            picked[k] = picked[k - 1];
                        }
                        dists[slot] = d;
                        picked[slot] = npc.whoAmI;
                        break;
                    }
                }
            }
            float encoded = Encode(picked);
            if (Projectile.ai[0] != encoded) {
                Projectile.ai[0] = encoded;
                Projectile.netUpdate = true;
            }

            //白热带真回复：+2HP/s，只在实际抽到血时结算（owner 写自己 statLife，客户端权威）
            if (hp.InWhiteHot && picked[0] != EmptySlot && Main.GameUpdateCount % 30 == 0) {
                Owner.Heal(1);
            }
        }

        protected override void ChannelAI(float collapse01) {
            Decode();
            if (Projectile.localAI[1] == 1f && !VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.5f }, Projectile.Center);
            }
            if (VaultUtils.isServer) {
                return;
            }
            if (Projectile.localAI[1] % 45 == 0 && collapse01 <= 0f && targetCache[0] != EmptySlot) {
                SoundEngine.PlaySound(SoundID.Item13 with { Volume = 0.3f, Pitch = -0.35f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        protected override bool? DamageGate() => Projectile.localAI[1] >= 4f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            //只判锁定列表：远端画的血丝与 owner 判的目标是同一份 ai[0]
            for (int i = 0; i < MaxTargets; i++) {
                NPC npc = LockedTarget(i);
                if (npc != null && npc.Hitbox == targetHitbox) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(ref Color lightColor) {
            //先画法器本体（法杖斜握），每条血丝一笔拉伸原版束条压在其上（与判定读同一份锁定列表）
            DrawWeaponBody();
            Decode();
            float alpha = 0.9f - 0.7f * MathHelper.Clamp(Projectile.localAI[0] / CollapseTicks, 0f, 1f);
            Vector2 muzzle = Projectile.Center;
            for (int i = 0; i < MaxTargets; i++) {
                NPC npc = LockedTarget(i);
                if (npc == null) {
                    continue;
                }
                Vector2 delta = npc.Center - muzzle;
                DrawBeamStrip(ProjectileID.LastPrismLaser, muzzle, delta, delta.Length(), 6f, lightColor * alpha);
            }
            return false;
        }
    }
}
