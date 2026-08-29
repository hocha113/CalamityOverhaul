using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.Golem
{
    /// <summary>
    /// 日核拳骨：石巨人残酷遗物。站定入石卫姿态（高减伤+免击退），
    /// 受击转化日核蓄能叠层，移动或双击下轰出日核重拳，伤害随层数上不封顶
    /// </summary>
    internal class SolarCoreFist : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期石巨人掉落物（镐锯/日耀石 ≈25金买价）的 4 倍档
            Item.value = Item.buyPrice(1, 0, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            SolarCoreFistPlayer mp = player.GetModPlayer<SolarCoreFistPlayer>();
            mp.Equipped = true;
            mp.RelicItem = Item;
        }
    }

    /// <summary>
    /// 石卫姿态状态机。逐帧逻辑在每个端点上对每名玩家模拟（站定判定吃同步速度），
    /// 层数只在所有者端的 OnHurt 里累积（该钩子仅在受伤玩家本机运行），
    /// 经 <see cref="SolarCoreFistNet"/> 转播给旁观端做可视化；
    /// 重拳弹幕、光环结算、减伤全部所有者端权威
    /// </summary>
    internal class SolarCoreFistPlayer : ModPlayer
    {
        #region 常量
        /// <summary>站定入姿态所需连续帧数（半秒）</summary>
        public const int StanceEntryFrames = 30;
        /// <summary>站定速度阈值（平方），低于视为站定</summary>
        public const float StandSpeedSq = 0.25f;
        /// <summary>打破姿态速度阈值（平方），滞回防抖</summary>
        public const float BreakSpeedSq = 0.81f;
        /// <summary>姿态减伤比</summary>
        public const float StanceDR = 0.75f;
        /// <summary>灼热光环点亮层数（满层）</summary>
        public const int AuraStacks = 8;
        /// <summary>光环半径（判定与 TechAura 可见环同源）</summary>
        public const float AuraRadius = 170f;
        /// <summary>光环结算间隔帧</summary>
        public const int AuraStrikeInterval = 20;
        /// <summary>重拳基础伤害</summary>
        public const int PunchBaseDamage = 1500;
        /// <summary>每层伤害增幅（对基础乘算）</summary>
        public const float PunchStackScale = 0.5f;
        #endregion

        #region 状态
        /// <summary>本帧装备中（ResetEffects 清）</summary>
        public bool Equipped;
        /// <summary>饰品实例，弹幕生成源用</summary>
        public Item RelicItem;
        /// <summary>连续站定帧数</summary>
        public int StanceTimer;
        /// <summary>石卫姿态生效中</summary>
        public bool InStance;
        /// <summary>日核蓄能层数，上不封顶；旁观端由网络写入</summary>
        public int ChargeStacks;

        //视觉包络（各端本地推进）
        /// <summary>石壳成形 0..1</summary>
        public float ShellForm;
        /// <summary>受击闪 0..1</summary>
        public float Flare;
        /// <summary>灼热光环渐入 0..1</summary>
        public float AuraGlow;

        private int auraStrikeTimer;
        private int punchCooldown;
        /// <summary>双击下的按键沿闩：PostUpdateEquips 记沿，PreUpdateMovement 姿态机消费，只活一帧</summary>
        private bool queuedStancePunch;
        /// <summary>上帧层数（各端统一的受击反馈/满层拍检测）</summary>
        private int prevStacksVisual;
        private bool netDirty;
        private int netThrottle;
        #endregion

        public override void Initialize() {
            Equipped = false;
            RelicItem = null;
            StanceTimer = 0;
            InStance = false;
            ChargeStacks = 0;
            ShellForm = 0f;
            Flare = 0f;
            AuraGlow = 0f;
            auraStrikeTimer = 0;
            punchCooldown = 0;
            queuedStancePunch = false;
            prevStacksVisual = 0;
            netDirty = false;
            netThrottle = 0;
        }

        public override void ResetEffects() => Equipped = false;

        //双击下的按键沿只在这里可见：原版在 Update 中段把 releaseDown 改写为"按住即 false"，
        //到 PreUpdateMovement 时沿已不可见、检测恒假（与血雾之瞳同病同修，反馈 #29）。
        //这里只记沿，是否出拳仍由姿态状态机裁决
        public override void PostUpdateEquips() {
            if (Player.whoAmI == Main.myPlayer && Equipped && !Player.dead
                && Player.controlDown && Player.releaseDown
                && Player.doubleTapCardinalTimer[0] > 0 && Player.doubleTapCardinalTimer[0] < 15) {
                queuedStancePunch = true;
            }
        }

        public override void UpdateDead() {
            //死亡蓄能清空，不打拳
            StanceTimer = 0;
            InStance = false;
            if (ChargeStacks != 0) {
                ChargeStacks = 0;
                MarkNetDirty(force: true);
            }
            ShellForm = Math.Max(ShellForm - 0.2f, 0f);
            AuraGlow = 0f;
            TickNet();
        }

        #region 主逻辑
        public override void PreUpdateMovement() {
            if (punchCooldown > 0) {
                punchCooldown--;
            }

            if (!Equipped) {
                if (InStance || ChargeStacks > 0) {
                    InStance = false;
                    StanceTimer = 0;
                    if (ChargeStacks != 0) {
                        ChargeStacks = 0;
                        MarkNetDirty(force: true);
                    }
                }
                UpdateVisualEnvelopes();
                TickNet();
                return;
            }

            float speedSq = Player.velocity.LengthSquared();
            bool standing = speedSq <= (InStance ? BreakSpeedSq : StandSpeedSq)
                && !Player.mount.Active && !Player.pulley && Player.grapCount == 0;

            if (standing) {
                bool wasInStance = InStance;
                StanceTimer++;
                InStance = StanceTimer >= StanceEntryFrames;
                if (InStance && !wasInStance) {
                    OnStanceEnter();
                }

                //入姿态前的聚石预兆
                if (!InStance && !VaultUtils.isServer && StanceTimer > 6 && Main.rand.NextBool(3)) {
                    Vector2 from = Player.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(34f, 52f);
                    Dust dust = Dust.NewDustPerfect(from, DustID.Stone, (Player.Center - from) * 0.06f, 120, default, 1.1f);
                    dust.noGravity = true;
                }
            }
            else {
                if (InStance) {
                    //移动打破姿态：有蓄能即向光标释放重拳
                    FirePunch();
                }
                InStance = false;
                StanceTimer = 0;
            }

            if (InStance) {
                Player.noKnockback = true;

                //双击下：原地释放重拳，姿态保持（与旋涡潜行/星尘守卫同一交互位）
                //末位消费闩：前置条件全过才尝试取同帧执行权，被别家抢走则本帧静默放弃
                if (punchCooldown <= 0 && queuedStancePunch
                    && Player.CWR().TryConsumeRelicDoubleTap(0)) {
                    FirePunch();
                }

                //满层灼热光环：结算只在所有者端，AddBuff/SimpleStrikeNPC 自带联机同步
                if (ChargeStacks >= AuraStacks && Player.whoAmI == Main.myPlayer) {
                    if (++auraStrikeTimer >= AuraStrikeInterval) {
                        auraStrikeTimer = 0;
                        AuraStrike();
                    }
                }
                else {
                    auraStrikeTimer = 0;
                }
            }

            //沿只活一帧：无论姿态是否消费，帧末清闩
            queuedStancePunch = false;
            UpdateVisualEnvelopes();
            TickNet();
        }

        /// <summary>姿态成立拍：石壳砸地成形</summary>
        private void OnStanceEnter() {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.DD2_OgreGroundPound with { Pitch = 0.25f, Volume = 0.65f }, Player.Center);
            SoundEngine.PlaySound(SoundID.Tink with { Pitch = -0.4f, Volume = 0.8f }, Player.Center);
            for (int i = 0; i < 14; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f);
                vel.Y -= 1.2f;
                Dust dust = Dust.NewDustPerfect(Player.Bottom + new Vector2(Main.rand.NextFloat(-16f, 16f), 0f),
                    DustID.Stone, vel, 90, default, 1.35f);
                dust.noGravity = Main.rand.NextBool();
            }
        }

        /// <summary>视觉包络与统一的层数变化反馈（所有端点一致推进）</summary>
        private void UpdateVisualEnvelopes() {
            ShellForm = InStance
                ? Math.Min(ShellForm + 1f / 12f, 1f)
                : Math.Max(ShellForm - 1f / 8f, 0f);
            Flare = Math.Max(Flare - 0.07f, 0f);

            bool auraOn = InStance && ChargeStacks >= AuraStacks;
            AuraGlow = auraOn
                ? Math.Min(AuraGlow + 0.06f, 1f)
                : Math.Max(AuraGlow - 0.08f, 0f);

            //层数上升的统一反馈：所有者由 OnHurt 立即触发，旁观端由网络包触发
            if (ChargeStacks > prevStacksVisual) {
                Flare = 1f;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit41 with { Pitch = 0.15f, Volume = 0.55f }, Player.Center);
                    int gained = Math.Min(ChargeStacks - prevStacksVisual, 4);
                    for (int i = 0; i < 5 + gained * 3; i++) {
                        Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5.5f);
                        PRTLoader.NewParticle<PRT_Spark>(Player.Center + Main.rand.NextVector2Circular(18f, 26f),
                            vel, Color.Lerp(new Color(255, 170, 60), new Color(255, 220, 130), Main.rand.NextFloat()),
                            Main.rand.NextFloat(0.8f, 1.3f)).Configure(true, Main.rand.Next(14, 24), Player);
                    }
                }
                //满层拍
                if (prevStacksVisual < AuraStacks && ChargeStacks >= AuraStacks && !VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.MaxMana with { Pitch = -0.2f, Volume = 0.9f }, Player.Center);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = -0.35f, Volume = 0.45f }, Player.Center);
                    for (int i = 0; i < 20; i++) {
                        float angle = MathHelper.TwoPi * i / 20f;
                        PRTLoader.NewParticle<PRT_Light>(Player.Center, angle.ToRotationVector2() * 5f,
                            new Color(255, 200, 90), 0.5f).Configure(Main.rand.Next(20, 32), opacity: 1.3f, squishStrenght: 2.2f);
                    }
                }
            }
            prevStacksVisual = ChargeStacks;

            //蓄能体光
            if (ChargeStacks > 0 && InStance) {
                float glow = Math.Min(ChargeStacks / (float)AuraStacks, 1f);
                Lighting.AddLight(Player.Center, new Vector3(1f, 0.6f, 0.22f) * (0.35f + 0.45f * glow));
            }
        }
        #endregion

        #region 受击与减伤
        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            if (InStance) {
                modifiers.FinalDamage *= 1f - StanceDR;
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!InStance) {
                return;
            }
            //每次受击 +1 层，攻击原始伤害每满 100 点额外 +1 层，上不封顶
            ChargeStacks += 1 + Math.Max(info.SourceDamage, 0) / 100;
            MarkNetDirty();
        }
        #endregion

        #region 重拳与光环
        /// <summary>释放日核重拳：演出各端本地播，弹幕仅所有者端生成</summary>
        private void FirePunch() {
            if (ChargeStacks <= 0 || punchCooldown > 0) {
                return;
            }
            punchCooldown = 12;

            //释放演出（无方向分量：远端玩家的光标不可知，方向表现交给弹幕自身）
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot with { Pitch = -0.2f, Volume = 0.95f }, Player.Center);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = 0.35f, Volume = 0.6f }, Player.Center);
                for (int i = 0; i < 18; i++) {
                    Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 9f);
                    PRTLoader.NewParticle<PRT_Light>(Player.Center, vel,
                        Color.Lerp(new Color(255, 150, 40), new Color(255, 230, 150), Main.rand.NextFloat()),
                        0.45f).Configure(Main.rand.Next(14, 26), opacity: 1.2f, squishStrenght: 2.4f);
                }
            }

            if (Player.whoAmI == Main.myPlayer) {
                Vector2 dir = Player.Center.To(Main.MouseWorld).SafeNormalize(Vector2.UnitX * Player.direction);
                float raw = PunchBaseDamage * (1f + PunchStackScale * ChargeStacks);
                int damage = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(raw);
                IEntitySource source = RelicItem != null
                    ? Player.GetSource_Accessory(RelicItem)
                    : Player.GetSource_Misc("SolarCoreFist");
                Projectile.NewProjectile(source, Player.Center, dir * 8f,
                    ModContent.ProjectileType<SolarCoreFistPunch>(), damage, 9f, Player.whoAmI, ChargeStacks);
            }

            ChargeStacks = 0;
            MarkNetDirty(force: true);
        }

        /// <summary>灼热光环结算（仅所有者端调用；打击与上buff自带网络同步）</summary>
        private void AuraStrike() {
            int auraDamage = 300 + 15 * ChargeStacks;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, Player.Center) > AuraRadius + npc.width * 0.5f) {
                    continue;
                }
                int dir = npc.Center.X > Player.Center.X ? 1 : -1;
                npc.SimpleStrikeNPC(auraDamage, dir, false, 2f, null, false, 0f, true);
                npc.AddBuff(BuffID.OnFire3, 240);
                npc.AddBuff(BuffID.Daybreak, 240);
            }
        }
        #endregion

        #region 层数同步
        private void MarkNetDirty(bool force = false) {
            netDirty = true;
            if (force) {
                netThrottle = 0;
            }
        }

        /// <summary>层数变化节流转播（仅所有者端出包）</summary>
        private void TickNet() {
            if (netThrottle > 0) {
                netThrottle--;
            }
            if (!netDirty || netThrottle > 0 || Player.whoAmI != Main.myPlayer) {
                return;
            }
            netDirty = false;
            netThrottle = 8;
            SolarCoreFistNet.SendStacks(Player, ChargeStacks);
        }
        #endregion
    }

    /// <summary>蓄能层数纯表现转播：旁观端石壳脉络/光环可视化用，权威值只在所有者端</summary>
    internal class SolarCoreFistNet : CWRNetChannel
    {
        internal static void SendStacks(Player owner, int stacks) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner == null
                || owner.whoAmI != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<SolarCoreFistNet>();
            packet.Write((byte)owner.whoAmI);
            packet.Write((ushort)Math.Clamp(stacks, 0, ushort.MaxValue));
            packet.Send();
        }

        public override void Receive(BinaryReader reader, int whoAmI) {
            //定长负载先读满，校验只做丢弃
            int declaredOwner = reader.ReadByte();
            int stacks = reader.ReadUInt16();

            if (Main.netMode == NetmodeID.Server) {
                //来源以连接为准，原样转播给除发送者外的所有人
                ModPacket packet = CWRNetWork.GetPacket<SolarCoreFistNet>();
                packet.Write((byte)whoAmI);
                packet.Write((ushort)stacks);
                packet.Send(-1, whoAmI);
                return;
            }
            if (Main.netMode != NetmodeID.MultiplayerClient
                || declaredOwner < 0 || declaredOwner >= Main.maxPlayers
                || declaredOwner == Main.myPlayer) {
                return;
            }
            Player owner = Main.player[declaredOwner];
            if (owner?.active != true) {
                return;
            }
            owner.GetModPlayer<SolarCoreFistPlayer>().ChargeStacks = stacks;
        }
    }
}
