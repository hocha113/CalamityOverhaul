using CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin.Projectiles;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.SlimeKin
{
    /// <summary>
    /// 残酷模式史莱姆族行为机制层（主题：弹性与黏化）。
    /// 叠加在原版 AI 之上，不接管：蓄力压扁超级跳（预告实体→锁点跃击→落地冲击波+黏化带）、
    /// 死亡分裂弹跳凝胶（自爆弹幕，非 NPC）。
    /// 决策只在权威端跑，客户端一律通过同步弹幕实体看到状态；数值增强由 GameModeNPC 统一负责，此处只加行为
    /// </summary>
    internal class SlimeKinNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        //==== 蓄力压扁超级跳 ====
        /// <summary>压扁蓄力预告帧（公平契约 ≥30，各档位一律不缩短）</summary>
        internal const int TelegraphFrames = 36;
        /// <summary>弹道解算的期望飞行帧</summary>
        private const int JumpFlightFrames = 42;
        /// <summary>原版 NPC 每帧重力</summary>
        private const float NpcGravity = 0.3f;
        private const float MaxHorizontalLaunch = 12f;
        /// <summary>向上初速上限</summary>
        private const float MaxVerticalLaunch = 14f;
        /// <summary>向上初速下限，保证真正离地</summary>
        private const float MinVerticalLaunch = 5f;
        private const float TriggerMinDist = 100f;
        private const float TriggerMaxDist = 560f;
        /// <summary>目标高于自身此差值时也可蓄跳（打平台挂机位）</summary>
        private const float TriggerAboveBy = 64f;
        /// <summary>触发条件不满足时的复查间隔（避免每帧扫描）</summary>
        private const int TriggerRetryFrames = 40;
        /// <summary>滞空至少此帧数才认落地（滤掉起跳帧误判）</summary>
        private const int LandingMinAirFrames = 9;
        /// <summary>滞空超时兜底（卡井/被击飞），直接回收状态</summary>
        private const int AirborneTimeoutFrames = 300;
        /// <summary>跳跃冷却，档位只调频率不换机制</summary>
        private static readonly int[] JumpCooldownByTier = [480, 420, 360];

        //==== 落地产物 ====
        /// <summary>冲击波伤害 = 已缩放 npc.damage × 此值</summary>
        private const float WaveDamageFrac = 0.6f;
        /// <summary>冲击波单侧射程，档位只加强度</summary>
        private static readonly float[] WaveRangeByTier = [300f, 330f, 360f];
        /// <summary>黏化带全局并发上限，超限跳过本次生成</summary>
        private const int PatchCap = 6;

        //==== 死亡分裂弹跳凝胶 ====
        private const float BombDamageFrac = 0.55f;
        private static readonly int[] GelBombCountByTier = [2, 2, 3];
        /// <summary>体型小于此值只裂 1 颗</summary>
        private const float SmallSlimeScaleGate = 0.7f;
        /// <summary>弹跳凝胶全局并发上限</summary>
        private const int GelBombCap = 6;

        //==== 并发闸 ====
        /// <summary>同时处于蓄力/滞空的史莱姆数上限，超限跳过触发</summary>
        private const int ConcurrentSpecialCap = 6;

        /// <summary>
        /// 目标类型表（只放正宿主类型；绿/红/紫/黄/黑/幼体/丛林是 BlueSlime 的 netID 变体，
        /// Slimeling/Slimer2 归于腐化宿主，自动被类型过滤覆盖）
        /// </summary>
        internal static readonly HashSet<int> SlimeTypes = [
            NPCID.BlueSlime, NPCID.MotherSlime, NPCID.LavaSlime, NPCID.CorruptSlime,
            NPCID.Slimer, NPCID.IlluminantSlime, NPCID.ToxicSludge, NPCID.IceSlime,
            NPCID.Crimslime, NPCID.SpikedJungleSlime, NPCID.SpikedIceSlime, NPCID.SandSlime,
        ];

        private enum Phase : byte
        {
            Idle,
            Telegraph,
            Airborne,
        }

        /// <summary>本个体生成时绑定的档位，0 = 未绑定（中途切模式不影响已出生个体）</summary>
        private int boundTier;
        /// <summary>机制状态；服务端决策私产，客户端不得用它驱动画面</summary>
        private Phase phase;
        private int cooldown;
        private int phaseTimer;
        private int airFrames;
        private float prevVelY;
        /// <summary>预告即承诺：蓄力开始那帧锁定的落点，此后不再重瞄</summary>
        private Vector2 lockedAim;

        /// <summary>正处于蓄力或滞空（供全局并发计数）</summary>
        internal bool SpecialBusy => phase != Phase.Idle;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
            => lateInstantiation && SlimeTypes.Contains(entity.type);

        public override void SetDefaults(NPC npc) {
            boundTier = 0;
            //决策全在权威端，客户端实例不绑档（空闲 PostAI 一支路早退，性能红线）
            if (VaultUtils.isClient) {
                return;
            }
            int tier = GameModeSystem.EffectiveTier;
            if (tier <= 0) {
                return;
            }
            boundTier = tier;
            //出生错拍：避免同屏群体同帧齐跳。whoAmI 在 SetDefaults 期尚不可靠
            //（原版在 NewNPC 尾部/UpdateNPC 才盖戳），改用权威端随机；冷却是服务端私产，无同步语义
            cooldown = 120 + Main.rand.Next(180);
        }

        /// <summary>机制资格（每个机制入口都要过；雕像怪/Pinky 在此排除）</summary>
        private static bool Eligible(NPC npc) {
            if (npc.friendly || npc.townNPC || npc.immortal || npc.dontTakeDamage) {
                return false;
            }
            if (npc.lifeMax <= 5 || npc.damage <= 0) {
                return false;
            }
            if (npc.boss || npc.realLife >= 0) {
                return false;
            }
            if (npc.SpawnedFromStatue) {
                return false;
            }
            //粉史莱姆保持原版（简报排除项）
            return npc.netID != NPCID.Pinky;
        }

        public override void PostAI(NPC npc) {
            if (boundTier <= 0) {
                return;
            }
            //决策只在权威端；客户端通过同步的预告/弹幕实体与 npc 运动看到一切
            if (VaultUtils.isClient) {
                return;
            }

            switch (phase) {
                case Phase.Idle:
                    if (--cooldown > 0) {
                        return;
                    }
                    TryStartTelegraph(npc);
                    return;
                case Phase.Telegraph:
                    TickTelegraph(npc);
                    return;
                case Phase.Airborne:
                    TickAirborne(npc);
                    return;
            }
        }

        private void TryStartTelegraph(NPC npc) {
            cooldown = TriggerRetryFrames;
            if (!Eligible(npc)) {
                return;
            }
            //飞行个体（带翼 Slimer 等 noGravity 形态）不做地面蓄跳；落翼后自然恢复资格
            if (npc.noGravity || npc.velocity.Y != 0f) {
                return;
            }
            if (!npc.HasValidTarget) {
                return;
            }
            Player target = Main.player[npc.target];
            Vector2 diff = target.Center - npc.Center;
            float hDist = Math.Abs(diff.X);
            bool inBand = hDist > TriggerMinDist && hDist < TriggerMaxDist;
            bool above = diff.Y < -TriggerAboveBy && hDist < TriggerMaxDist;
            if (!inBand && !above) {
                return;
            }
            if (!Collision.CanHitLine(npc.position, npc.width, npc.height,
                target.position, target.width, target.height)) {
                return;
            }
            if (CountBusySlimes() >= ConcurrentSpecialCap) {
                return;
            }

            lockedAim = target.Center;
            phase = Phase.Telegraph;
            phaseTimer = TelegraphFrames;
            float aimAngle = (lockedAim - npc.Center).ToRotation();
            //预告即实体：压扁蓄力体随 npc 同步到所有端
            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero,
                ModContent.ProjectileType<SlimeSquashOmen>(), 0, 0f, Main.myPlayer,
                npc.whoAmI, aimAngle, SlimeKinFlavor.PackColor(SlimeKinFlavor.GelColor(npc)));
        }

        private void TickTelegraph(NPC npc) {
            if (--phaseTimer > 0) {
                return;
            }
            //起跳：向锁定点弹道解算（不重瞄），只在脉冲帧置 netUpdate。
            //通用提速层（GameModeNPC.PostAI）按 velocity×SpeedBonus 追加位移，
            //滞空实际位移被放大 ×(1+SpeedBonus)：目标位移除回、重力项不除
            //（速度仍按原版重力演化），落点才兑现预告承诺；只补机制注入，不碰常态移速
            Vector2 start = npc.Bottom;
            Vector2 d = (lockedAim - start) / (1f + GameModeTuning.SpeedBonus(boundTier));
            float t = JumpFlightFrames;
            float vx = MathHelper.Clamp(d.X / t, -MaxHorizontalLaunch, MaxHorizontalLaunch);
            float vy = MathHelper.Clamp((d.Y - 0.5f * NpcGravity * t * t) / t,
                -MaxVerticalLaunch, -MinVerticalLaunch);
            npc.velocity = new Vector2(vx, vy);
            npc.netUpdate = true;
            phase = Phase.Airborne;
            airFrames = 0;
            prevVelY = vy;
        }

        private void TickAirborne(NPC npc) {
            airFrames++;
            //从下落转为支撑 = 落地（排除顶头撞天花板的瞬时归零）
            bool landed = npc.velocity.Y == 0f && prevVelY > 0f;
            if (landed && airFrames >= LandingMinAirFrames) {
                Land(npc);
                return;
            }
            if (airFrames > AirborneTimeoutFrames) {
                phase = Phase.Idle;
                cooldown = JumpCooldown(npc);
                return;
            }
            prevVelY = npc.velocity.Y;
        }

        /// <summary>落地：双向冲击波 + 黏化减速带（声音与粉尘由产物实体在各端自播）</summary>
        private void Land(NPC npc) {
            phase = Phase.Idle;
            cooldown = JumpCooldown(npc);

            Vector2 ground = npc.Bottom;
            float packed = SlimeKinFlavor.PackColor(SlimeKinFlavor.GelColor(npc));
            float sizeScale = MathHelper.Clamp(npc.scale, 0.5f, 1.6f);
            int waveDamage = (int)(npc.damage * WaveDamageFrac);
            float range = WaveRangeByTier[boundTier - 1] * MathHelper.Clamp(npc.scale, 0.6f, 1.2f);
            for (int dir = -1; dir <= 1; dir += 2) {
                Projectile.NewProjectile(npc.GetSource_FromAI(), ground + new Vector2(dir * 14f, -6f),
                    Vector2.Zero, ModContent.ProjectileType<SlimeQuakeWave>(), waveDamage, 0f,
                    Main.myPlayer, dir, range, packed);
            }

            if (CountProjOfType(ModContent.ProjectileType<SlimeGooPatch>()) < PatchCap) {
                //ai[0] = 档位×10 + 风味（黏化带按档位定存续，本地各端确定性还原）
                float flavorTier = boundTier * 10 + (int)SlimeKinFlavor.FlavorOf(npc);
                Projectile.NewProjectile(npc.GetSource_FromAI(), ground, Vector2.Zero,
                    ModContent.ProjectileType<SlimeGooPatch>(), 0, 0f, Main.myPlayer,
                    flavorTier, packed, sizeScale);
            }
        }

        /// <summary>死亡分裂：弹跳凝胶自爆弹（禁止生成 NPC；前 40 帧无害是死亡预告）</summary>
        public override void OnKill(NPC npc) {
            //OnKill 本就权威端钩子，按仓库惯例再拦一层保平
            if (VaultUtils.isClient || boundTier <= 0) {
                return;
            }
            if (!Eligible(npc)) {
                return;
            }
            //母史莱姆的死亡分裂是原版幼体，不再叠凝胶
            if (npc.type == NPCID.MotherSlime) {
                return;
            }

            int bombType = ModContent.ProjectileType<SlimeGelBomb>();
            int room = GelBombCap - CountProjOfType(bombType);
            if (room <= 0) {
                return;
            }
            int want = GelBombCountByTier[boundTier - 1];
            if (npc.scale < SmallSlimeScaleGate) {
                want = 1;
            }
            if (want > room) {
                want = room;
            }

            int damage = (int)(npc.damage * BombDamageFrac);
            float packed = SlimeKinFlavor.PackColor(SlimeKinFlavor.GelColor(npc));
            float flavor = (int)SlimeKinFlavor.FlavorOf(npc);
            float sizeScale = MathHelper.Clamp(npc.scale, 0.5f, 1.4f);
            for (int i = 0; i < want; i++) {
                //权威端一次性摇散射向，弹体后续物理各端确定性模拟
                float lerp = want <= 1 ? 0.5f : i / (float)(want - 1);
                Vector2 vel = new Vector2(MathHelper.Lerp(-2.6f, 2.6f, lerp) + Main.rand.NextFloat(-0.7f, 0.7f),
                    -Main.rand.NextFloat(4.5f, 7f));
                Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, vel,
                    bombType, damage, 0f, Main.myPlayer, flavor, packed, sizeScale);
            }
        }

        private int JumpCooldown(NPC npc) => JumpCooldownByTier[boundTier - 1] + npc.whoAmI * 13 % 120;

        /// <summary>全局并发计数；只在冷却到期的触发尝试帧调用，非每帧扫描</summary>
        private static int CountBusySlimes() {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC n = Main.npc[i];
                if (n.active && SlimeTypes.Contains(n.type)
                    && n.TryGetGlobalNPC(out SlimeKinNPC slime) && slime.SpecialBusy) {
                    count++;
                }
            }
            return count;
        }

        private static int CountProjOfType(int type) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                if (Main.projectile[i].active && Main.projectile[i].type == type) {
                    count++;
                }
            }
            return count;
        }
    }
}
