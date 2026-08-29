using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using System;
using System.IO;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.WallOfFlesh
{
    /// <summary>
    /// 饕餮之喉：血肉墙残酷遗物。全伤害吸血(血珠逆流回体，不占药水疲劳，
    /// 但每秒结算封顶为生命上限的 2%，月噬期间被彻底封禁，超量血珠洒落在地)，
    /// 周期性伸出血肉巨舌攫取范围内最远的敌人拖到面前，咬合施加腐锯(持续撕裂)，
    /// 拖到面前的敌人吃下一击双倍(处刑窗口)，处刑一击引爆残余腐锯折现
    /// </summary>
    internal class GluttonousThroat : BaseBrutalRelic
    {
        public override void SetDefaults() {
            base.SetDefaults();
            //同期参照：血肉墙徽章类掉落卖 2 金(买价 10 金)，取 4 倍档
            Item.value = Item.buyPrice(0, 40, 0, 0);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            GluttonousThroatPlayer mp = player.GetModPlayer<GluttonousThroatPlayer>();
            mp.Equipped = true;
            mp.EquipItem = Item;
        }
    }

    /// <summary>
    /// 饕餮之喉的玩家侧状态：吸血池、舌攫冷却、处刑结算。
    /// 全部实例字段，触发类动作(生成弹幕)只在拥有者客户端执行
    /// </summary>
    internal class GluttonousThroatPlayer : ModPlayer
    {
        /// <summary>吸血比例(全伤害)。系数保饱满手感，真闸是下面的每秒结算上限</summary>
        public const float LeechRatio = 0.08f;
        /// <summary>每秒吸血结算上限＝生命上限的此比例(400血=8HP/s，随进度自然成长)</summary>
        public const float LeechCapRatio = 0.02f;
        /// <summary>舌攫冷却(tick)＝9秒，同时钳制处刑窗频率</summary>
        public const int TongueCooldown = 540;
        /// <summary>无目标时的重试间隔(tick)</summary>
        public const int RetryGap = 20;
        /// <summary>舌攫索敌半径(px)，68 格</summary>
        public const float TongueRange = 1088f;
        /// <summary>处刑窗口时长(tick)</summary>
        public const int MarkWindow = 150;
        /// <summary>血珠生成最小间隔(tick)，多次命中合池</summary>
        public const int OrbGapTicks = 7;
        /// <summary>单颗血珠携带治疗上限(防御性封顶，池内余量下一颗接续)</summary>
        public const int OrbHealCap = 4000;

        /// <summary>本帧已装备</summary>
        public bool Equipped;
        /// <summary>饰品物品实例(生成源用)</summary>
        public Item EquipItem;
        /// <summary>待结算吸血池</summary>
        private float pendingLeech;
        /// <summary>每秒结算预算(令牌桶：逐帧回充，血珠触体扣减，容量＝1秒额度)</summary>
        private float leechBudget;
        /// <summary>最近一次命中位置(血珠出生点)</summary>
        private Vector2 lastHitPos;
        /// <summary>血珠生成间隔计时</summary>
        private int orbGap;
        /// <summary>舌攫冷却计时</summary>
        private int tongueCD = 60;

        public override void ResetEffects() {
            Equipped = false;
            EquipItem = null;
        }

        public override void UpdateDead() {
            pendingLeech = 0f;
        }

        public override void PostUpdateEquips() {
            if (!Equipped) {
                pendingLeech = 0f;
                leechBudget = 0f;
                tongueCD = Math.Max(tongueCD, 60);
                return;
            }
            //触发类动作只走拥有者客户端(服务端 myPlayer=255 恒不匹配)
            if (Main.myPlayer != Player.whoAmI) {
                return;
            }

            //预算逐帧回充：令牌桶，容量=1秒额度。闲置满桶后的首个滑动秒最多结算 2 倍
            //(满桶瞬泄+当秒回充)，长程均值恒≤上限(结算本就只在 owner 端)
            float cap = Player.statLifeMax2 * LeechCapRatio;
            leechBudget = MathF.Min(leechBudget + cap / 60f, cap);

            UpdateLeechOrbSpawn();
            UpdateTongueTrigger();
        }

        /// <summary>
        /// 血珠触体结算闸(仅 owner 端调用)：月噬期间彻底封禁(返回 0)；
        /// 否则按每秒预算放行，超出部分由调用方洒落为坠地血滴
        /// </summary>
        internal int SettleLeech(int requested) {
            if (requested <= 0 || Player.HasBuff(BuffID.MoonLeech)) {
                return 0;
            }
            int granted = Math.Min(requested, (int)leechBudget);
            if (granted > 0) {
                leechBudget -= granted;
            }
            return granted;
        }

        #region 吸血
        /// <summary>吸血池按节流吐出血珠弹幕，血珠到体才真正回血</summary>
        private void UpdateLeechOrbSpawn() {
            if (orbGap > 0) {
                orbGap--;
            }
            if (pendingLeech < 1f || orbGap > 0) {
                return;
            }
            int amount = (int)MathF.Min(pendingLeech, OrbHealCap);
            pendingLeech -= amount;
            orbGap = OrbGapTicks;
            Projectile.NewProjectile(Player.GetSource_Accessory(EquipItem), lastHitPos,
                Vector2.Zero, ModContent.ProjectileType<GluttonousLeechOrb>(),
                0, 0f, Player.whoAmI, amount, Main.rand.Next(1000));
        }

        /// <summary>吸血资格：友方/靶子/雕像刷怪不吸(防挂机农场，不影响强度上限)</summary>
        private static bool LeechEligible(NPC target) {
            return !target.friendly && target.type != NPCID.TargetDummy
                && !target.SpawnedFromStatue && !target.immortal;
        }
        #endregion

        #region 舌攫触发
        /// <summary>冷却归零时攫取范围内最远的可追敌人；无目标只烧短重试间隔</summary>
        private void UpdateTongueTrigger() {
            if (tongueCD > 0) {
                tongueCD--;
                return;
            }
            if (Player.dead
                || Player.ownedProjectileCounts[ModContent.ProjectileType<GluttonousTongueProj>()] > 0) {
                return;
            }

            NPC target = SelectFarthestTarget();
            if (target == null) {
                tongueCD = RetryGap;
                return;
            }
            tongueCD = TongueCooldown;
            Projectile.NewProjectile(Player.GetSource_Accessory(EquipItem), Player.Center,
                Vector2.Zero, ModContent.ProjectileType<GluttonousTongueProj>(),
                0, 0f, Player.whoAmI, target.whoAmI, target.type);
        }

        /// <summary>范围内最远的可追敌人(把远处的狙击手拽到脸上处刑)</summary>
        private NPC SelectFarthestTarget() {
            NPC best = null;
            float bestDist = -1f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                float dist = Player.Distance(npc.Center);
                if (dist > TongueRange || dist <= bestDist) {
                    continue;
                }
                bestDist = dist;
                best = npc;
            }
            return best;
        }
        #endregion

        #region 命中结算(伤害计算端=拥有者客户端)
        public override void ModifyHitNPCWithItem(Item item, NPC target, ref NPC.HitModifiers modifiers)
            => ApplyExecution(target, ref modifiers);

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers)
            => ApplyExecution(target, ref modifiers);

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => SettleHit(target, damageDone);

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => SettleHit(target, damageDone);

        /// <summary>处刑窗口内对该敌人的下一击翻倍</summary>
        private void ApplyExecution(NPC target, ref NPC.HitModifiers modifiers) {
            if (!Equipped) {
                return;
            }
            if (target.GetGlobalNPC<GluttonousThroatGlobalNPC>().MarkValidFor(Player.whoAmI)) {
                modifiers.FinalDamage *= 2f;
            }
        }

        /// <summary>命中收尾：吸血入池 + 处刑标记消费(附带引爆残余腐锯)与视网膜锁定演出</summary>
        private void SettleHit(NPC target, int damageDone) {
            if (!Equipped) {
                return;
            }

            GluttonousThroatGlobalNPC mark = target.GetGlobalNPC<GluttonousThroatGlobalNPC>();
            if (mark.MarkValidFor(Player.whoAmI)) {
                mark.MarkConsumed = true;
                RequestRotsawDetonate(target);
                if (!VaultUtils.isServer) {
                    GluttonousRetinaRender.RequestFlash(target.whoAmI, target.Center);
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.55f, Volume = 0.9f }, target.Center);
                    SoundEngine.PlaySound(SoundID.NPCDeath12 with { Pitch = -0.35f, Volume = 0.8f }, target.Center);
                    WofMotionFX.SpawnBloodBurst(target.Center, 1.2f,
                        (target.Center - Player.Center).SafeNormalize(Vector2.UnitX));
                    WofMotionFX.CameraPunch(target.Center, 4.5f, 12, "GluttonousExecute",
                        target.Center - Player.Center);
                }
            }

            if (LeechEligible(target) && damageDone > 0) {
                pendingLeech += damageDone * LeechRatio;
                lastHitPos = target.Center;
            }
        }

        /// <summary>
        /// 处刑引爆：目标残余腐锯折现为一次性伤害并提前终结腐锯。
        /// 腐锯早停必须落在服务端(客户端 DelBuff 不广播，会被下次 buff 同步顶回)，
        /// 联机走 <see cref="GluttonousDetonateNet"/> 请求，本端只先出演出拍
        /// </summary>
        private void RequestRotsawDetonate(NPC target) {
            if (target.FindBuffIndex(ModContent.BuffType<RotsawRendDebuff>()) < 0) {
                return;
            }
            if (Main.netMode == NetmodeID.SinglePlayer) {
                GluttonousDetonateNet.Detonate(target, Player.whoAmI);
            }
            else {
                GluttonousDetonateNet.SendDetonate(target, Player.whoAmI);
            }
            //引爆演出：放大版血浆喷泉(本端即时，其余客户端由服务端转播补拍)
            if (!VaultUtils.isServer) {
                WofMotionFX.SpawnBloodBurst(target.Center, 1.8f,
                    (target.Center - Player.Center).SafeNormalize(Vector2.UnitX));
            }
        }
        #endregion
    }

    /// <summary>
    /// 腐锯：饕餮之喉咬合施加的撕裂减益。每秒 40 点生命撕裂，
    /// 由服务端 AddBuff 施加并走原版减益同步；处刑一击可引爆残余时长折现
    /// (削甲身份已让渡世吞，防御归零删除)
    /// </summary>
    internal class RotsawRendDebuff : ModBuff
    {
        /// <summary>持续时长(tick)</summary>
        public const int Duration = 300;
        /// <summary>每秒撕裂生命</summary>
        public const int DotPerSecond = 40;

        public override string Texture => CWRConstant.Item_BrutalRelic + "RotsawRendDebuff";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            npc.GetGlobalNPC<GluttonousThroatGlobalNPC>().RotsawActive = true;
        }
    }

    /// <summary>
    /// 处刑引爆转播信道：owner 请求 → 服务端复验并权威结算(折现伤害 + 腐锯早停) →
    /// 其余客户端补演出。伤害经 SimpleStrikeNPC 自带同步；腐锯早停必须在服务端 DelBuff
    /// (netMode==Server 才广播 NPCBuffs)。目标身份沿本件既有契约：下标 + 类型双验。
    /// 服务端限频 30t/请求端(合法节奏为舌攫 9s 一次)；空转(无腐锯)不转播演出
    /// </summary>
    internal class GluttonousDetonateNet : CWRNetChannel
    {
        /// <summary>服务端各请求端最近受理帧(限频用，仅服务端读写，客户端槽位无意义)</summary>
        private static readonly long[] lastDetonateFrame = new long[Main.maxPlayers];
        /// <summary>服务端受理间隔下限(tick)</summary>
        private const int DetonateGapTicks = 30;

        public override void Receive(BinaryReader reader, int whoAmI) {
            //先读完全部载荷再校验早退
            int owner = reader.ReadByte();
            int npcIndex = reader.ReadByte();
            int npcType = reader.ReadInt32();
            Vector2 pos = reader.ReadVector2();

            if (Main.netMode == NetmodeID.Server) {
                //限频先行：恶意端高频请求连伪造日志都不给刷
                if (Main.GameUpdateCount < lastDetonateFrame[whoAmI] + DetonateGapTicks) {
                    return;
                }
                lastDetonateFrame[whoAmI] = Main.GameUpdateCount;
                if (owner != whoAmI) {
                    CWRMod.Instance.Logger.Info($"GluttonousThroat detonate spoof dropped: claim={owner} actual={whoAmI}");
                    return;
                }
                if (npcIndex < 0 || npcIndex >= Main.maxNPCs) {
                    return;
                }
                NPC npc = Main.npc[npcIndex];
                //槽位复用/同帧死亡竞态：静默丢弃，不视作错误
                if (npc?.active != true || npc.type != npcType) {
                    return;
                }
                //空转(目标已无腐锯)不转播，堵死旁观者演出刷屏面
                if (!Detonate(npc, owner)) {
                    return;
                }
                ModPacket relay = CWRNetWork.GetPacket<GluttonousDetonateNet>();
                relay.Write((byte)owner);
                relay.Write((byte)npcIndex);
                relay.Write(npcType);
                relay.WriteVector2(npc.Center);
                relay.Send(ignoreClient: whoAmI);
                return;
            }

            //其余客户端：只补引爆演出(请求端已本地出拍)
            if (owner < 0 || owner >= Main.maxPlayers || owner == Main.myPlayer) {
                return;
            }
            Player plr = Main.player[owner];
            Vector2 dir = plr?.active == true
                ? (pos - plr.Center).SafeNormalize(Vector2.UnitX)
                : Vector2.UnitX;
            WofMotionFX.SpawnBloodBurst(pos, 1.8f, dir);
        }

        /// <summary>owner 端请求引爆(联机)；须在标记消费同帧调用</summary>
        internal static void SendDetonate(NPC target, int owner) {
            if (Main.netMode != NetmodeID.MultiplayerClient || owner != Main.myPlayer) {
                return;
            }
            ModPacket packet = CWRNetWork.GetPacket<GluttonousDetonateNet>();
            packet.Write((byte)owner);
            packet.Write((byte)target.whoAmI);
            packet.Write(target.type);
            packet.WriteVector2(target.Center);
            packet.Send();
        }

        /// <summary>
        /// 权威端结算(单机/服务端)：残余腐锯时长 × 40HP/s 的 50% 折现
        /// (即 buffTime/3，满时长 5s 上限 100 点，属 DoT 折现非新增输出)，随后腐锯提前结束。
        /// 返回是否真的爆掉了一层腐锯(false=空转，调用方不应转播演出)
        /// </summary>
        internal static bool Detonate(NPC target, int byPlayer) {
            int idx = target.FindBuffIndex(ModContent.BuffType<RotsawRendDebuff>());
            if (idx < 0) {
                return false;
            }
            int damage = target.buffTime[idx] / 3;
            target.DelBuff(idx);
            if (damage <= 0) {
                return true;
            }
            int dir = byPlayer >= 0 && byPlayer < Main.maxPlayers && Main.player[byPlayer]?.active == true
                ? (target.Center.X >= Main.player[byPlayer].Center.X ? 1 : -1)
                : 0;
            target.SimpleStrikeNPC(damage, dir, false, 0f, null, false, 0f, true);
            return true;
        }
    }

    /// <summary>
    /// 敌人侧状态：腐锯撕裂、拖拽冻结、拖拽免疫、处刑标记。
    /// 拖拽/标记均由各端从同步的舌攫弹幕状态本地推得，无需额外网络包；
    /// 标记消费(拥有者本地)后远端准星最多多亮到窗口自然结束
    /// </summary>
    internal class GluttonousThroatGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>拖拽冻结时间戳：不早于此刻则跳过自驱 AI，位移由服务端书写</summary>
        public long DragHoldUntil = -1;
        /// <summary>拖拽免疫时间戳：释放拍写入(释放后 1 秒)，期间舌攫只咬不拖，防无限风筝链</summary>
        public long DragImmuneUntil = -1;
        /// <summary>处刑窗口结束时间戳</summary>
        public long MarkUntil = -1;
        /// <summary>处刑窗口归属玩家</summary>
        public int MarkOwner = -1;
        /// <summary>本窗口已被消费(翻倍已触发)</summary>
        public bool MarkConsumed;
        /// <summary>本帧腐锯生效(减益 Update 点亮，ResetEffects 清)</summary>
        public bool RotsawActive;

        /// <summary>处刑窗口对该玩家有效</summary>
        public bool MarkValidFor(int playerWhoAmI) {
            return MarkOwner == playerWhoAmI && !MarkConsumed && Main.GameUpdateCount <= MarkUntil;
        }

        /// <summary>标记准星可见(远端在消费后最多多亮到窗口结束)</summary>
        public bool MarkVisible => MarkOwner >= 0 && !MarkConsumed && Main.GameUpdateCount <= MarkUntil;

        public override void ResetEffects(NPC npc) {
            RotsawActive = false;
            //可见标记盖渲染帧戳，准星绘制层无戳早退
            if (MarkVisible) {
                GluttonousRetinaRender.MarkStamp.Stamp();
            }
        }

        public override bool PreAI(NPC npc) {
            //被巨舌攫住：冻结自驱 AI(含重力/寻路)，速度与位置由舌攫弹幕服务端书写
            return Main.GameUpdateCount > DragHoldUntil;
        }

        public override void UpdateLifeRegen(NPC npc, ref int damage) {
            if (!RotsawActive) {
                return;
            }
            if (npc.lifeRegen > 0) {
                npc.lifeRegen = 0;
            }
            //lifeRegen 单位为 0.5HP/s：40HP/s → 80
            npc.lifeRegen -= RotsawRendDebuff.DotPerSecond * 2;
            int tick = RotsawRendDebuff.DotPerSecond / 4;
            if (damage < tick) {
                damage = tick;
            }
        }
    }
}
