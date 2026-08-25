using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Melee.DestroyersBladeEXs
{
    /// <summary>
    /// 毁灭者之刃EX 的玩家状态机。潜猎协议:持刀停手片刻进入潜行,移速上升、
    /// 仇恨下降,靠近猎物即可发动毁灭者之撕咬;撕咬命中进入歼灭协议(强化窗口)。
    /// 状态全挂玩家不挂物品实例(物品字段会被 tML 重克隆抹掉)
    /// </summary>
    internal class DestroyerEXPlayer : ModPlayer
    {
        /// <summary>最后一次出手的帧计数(挥砍/撕咬都算),潜行由停手时长驱动</summary>
        public uint LastAttackTick;
        /// <summary>连击拍号 0~2,停手超时回首拍</summary>
        public int ComboStage;
        /// <summary>上次挥砍帧计数,连击超时判据</summary>
        public uint LastSwingTick;
        /// <summary>撕咬冷却(帧)</summary>
        public int BiteCooldown;
        /// <summary>撕咬隐身的自愈标记:化身期间逐帧刷新,断更即自动恢复绘制</summary>
        public uint BiteHideTick;

        private bool wasReady;
        private bool wasEmpowered;
        private int markerTimer;

        /// <summary>停手多少帧后进入潜行</summary>
        public const int StalkDelay = 45;
        /// <summary>潜行移速加成</summary>
        public const float StalkMoveSpeed = 0.28f;
        /// <summary>撕咬索敌半径(px)</summary>
        public const float BiteRange = 430f;
        /// <summary>撕咬冷却时长(帧)</summary>
        public const int BiteCooldownTime = 300;
        /// <summary>歼灭协议持续帧数,8秒</summary>
        public const int FrenzyDuration = 480;
        /// <summary>歼灭协议武器伤害倍率</summary>
        public const float FrenzyDamageMul = 1.3f;

        public bool Empowered => Player.HasBuff<DestroyerFrenzyBuff>();

        public bool HoldingBlade => Player.HeldItem != null
            && Player.HeldItem.type == ModContent.ItemType<DestroyersBladeEX>();

        /// <summary>潜行判定:持刀、停手够久、不在撕咬中、不在歼灭窗口</summary>
        public bool StalkActive => HoldingBlade && !Player.dead
            && Main.GameUpdateCount >= LastAttackTick + StalkDelay
            && Player.ownedProjectileCounts[ModContent.ProjectileType<DestroyerBiteProj>()] == 0
            && !Empowered;

        /// <summary>撕咬就绪:潜行中、冷却转完、附近有可咬目标</summary>
        public bool BiteReady => StalkActive && BiteCooldown <= 0 && FindBiteTarget() >= 0;

        /// <summary>出手记账:挥砍与撕咬都会打断潜行</summary>
        public void NoteAttack() => LastAttackTick = Main.GameUpdateCount;

        /// <summary>撕咬命中后进入歼灭协议</summary>
        public void GrantFrenzy()
            => Player.AddBuff(ModContent.BuffType<DestroyerFrenzyBuff>(), FrenzyDuration);

        /// <summary>索敌:玩家为圆心找最近的可追猎目标,要求视线通畅</summary>
        public int FindBiteTarget() {
            int best = -1;
            float bestDist = BiteRange;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Player.Center);
                if (dist < bestDist && Collision.CanHitLine(Player.Center, 1, 1, npc.Center, 1, 1)) {
                    bestDist = dist;
                    best = i;
                }
            }
            return best;
        }

        public override void PostUpdateMiscEffects() {
            if (BiteCooldown > 0) {
                BiteCooldown--;
            }

            //连击停手超时回首拍
            if (Main.GameUpdateCount > LastSwingTick + 90) {
                ComboStage = 0;
            }

            if (!HoldingBlade) {
                wasReady = false;
                return;
            }

            //潜行 buff 挂短时限逐帧续,条件断开即自然熄灭
            if (StalkActive) {
                Player.AddBuff(ModContent.BuffType<DestroyerStalkBuff>(), 4, quiet: true);
            }

            if (VaultUtils.isServer) {
                return;
            }

            //撕咬就绪的边沿提示与目标标记,只对本机玩家演
            bool ready = BiteReady;
            if (Player.whoAmI == Main.myPlayer) {
                if (ready && !wasReady) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.7f, Pitch = -0.7f }, Player.Center);
                    SoundEngine.PlaySound(SoundID.Item22 with { Volume = 0.4f, Pitch = -0.5f, MaxInstances = 2 }, Player.Center);
                    PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero,
                        new Color(255, 40, 30), 0f)?.Configure(0.05f, 0.55f, 14);
                }
                if (ready && ++markerTimer >= 26) {
                    //猎物头顶的红灯锁定标记
                    markerTimer = 0;
                    int target = FindBiteTarget();
                    if (target >= 0) {
                        NPC npc = Main.npc[target];
                        PRTLoader.NewParticle<PRT_Spark>(npc.Top - Vector2.UnitY * 14f,
                            Vector2.UnitY * 1.2f, new Color(255, 30, 25), 1.4f)?.Configure(false, 16);
                        PRTLoader.NewParticle<PRT_StarPulseRing>(npc.Center, Vector2.Zero,
                            new Color(200, 20, 18), 0f)?.Configure(0.03f, 0.3f, 10);
                    }
                }
            }
            wasReady = ready;

            //歼灭协议激活爆发,各端凭 buff 同步各自演
            bool emp = Empowered;
            if (emp && !wasEmpowered) {
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.8f, Pitch = -0.15f, MaxInstances = 2 }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 0.6f, Pitch = -0.3f, MaxInstances = 2 }, Player.Center);
                PRTLoader.NewParticle<PRT_StarPulseRing>(Player.Center, Vector2.Zero,
                    new Color(255, 60, 40), 0f)?.Configure(0.1f, 1.2f, 20);
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = (MathHelper.TwoPi * i / 10f).ToRotationVector2() * Main.rand.NextFloat(3f, 7f);
                    PRTLoader.NewParticle<PRT_SparkAlpha>(Player.Center, vel,
                        Main.rand.NextBool() ? Color.White : new Color(255, 60, 40),
                        Main.rand.NextFloat(1.2f, 2f))?.Configure(false, Main.rand.Next(12, 20));
                }
            }
            wasEmpowered = emp;
        }
    }

    /// <summary>潜猎协议:潜行状态,移速上升、仇恨下降,伺机撕咬</summary>
    internal class DestroyerStalkBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "DestroyerStalk";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            player.moveSpeed += DestroyerEXPlayer.StalkMoveSpeed;
            player.runAcceleration *= 1.15f;
            player.aggro -= 400;

            if (VaultUtils.isServer) {
                return;
            }
            //残影 + 沉暗雾息 + 偶发的红灯瞬闪:掠食机械潜伏的呼吸
            player.armorEffectDrawShadow = true;
            if (Main.rand.NextBool(11)) {
                PRTLoader.NewParticle<PRT_GhostRainMist>(
                    player.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), -6f),
                    new Vector2(-player.velocity.X * 0.04f, -Main.rand.NextFloat(0.2f, 0.5f)),
                    new Color(16, 4, 6) * 0.7f, Main.rand.NextFloat(0.4f, 0.65f))?.Configure(Main.rand.Next(30, 50));
            }
            if (Main.rand.NextBool(90)) {
                PRTLoader.NewParticle<PRT_Spark>(
                    player.Center + new Vector2(player.direction * 4f, -6f),
                    Vector2.Zero, new Color(255, 30, 25), 0.9f)?.Configure(false, 10);
            }
            Lighting.AddLight(player.Center, new Vector3(0.16f, 0.02f, 0.02f));
        }
    }

    /// <summary>歼灭协议:撕咬得手后的强化窗口,弹幕全面增强并获得追踪</summary>
    internal class DestroyerFrenzyBuff : ModBuff
    {
        public override string Texture => CWRConstant.Buff + "DestroyerFrenzy";

        public override void SetStaticDefaults() => Main.buffNoSave[Type] = true;

        public override void Update(Player player, ref int buffIndex) {
            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(player.Center, new Vector3(0.5f, 0.12f, 0.08f));
            //白红电花沿身跳动
            if (Main.rand.NextBool(6)) {
                Vector2 at = player.Center + Main.rand.NextVector2Circular(22f, 30f);
                PRTLoader.NewParticle<PRT_SparkAlpha>(at,
                    Main.rand.NextVector2Circular(1.5f, 1.5f) - Vector2.UnitY * 0.8f,
                    Main.rand.NextBool(3) ? Color.White : new Color(255, 70, 50),
                    Main.rand.NextFloat(0.8f, 1.4f))?.Configure(false, Main.rand.Next(8, 14));
            }
        }
    }
}
