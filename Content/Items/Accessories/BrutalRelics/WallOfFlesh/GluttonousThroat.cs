using CalamityOverhaul.Content.NPCs.BrutalNPCs.BrutalWallOfFlesh.Rendering;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Accessories.BrutalRelics.WallOfFlesh
{
    /// <summary>
    /// 饕餮之喉：血肉墙残酷遗物。全伤害超模吸血(血珠逆流回体，独立于药水疲劳、
    /// 无视月噬)，周期性伸出血肉巨舌攫取范围内最远的敌人拖到面前，
    /// 咬合施加腐锯(防御归零+高额撕裂)，拖到面前的敌人吃下一击双倍(处刑窗口)
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
        /// <summary>吸血比例(全伤害)</summary>
        public const float LeechRatio = 0.12f;
        /// <summary>舌攫冷却(tick)</summary>
        public const int TongueCooldown = 360;
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
                tongueCD = Math.Max(tongueCD, 60);
                return;
            }
            //触发类动作只走拥有者客户端(服务端 myPlayer=255 恒不匹配)
            if (Main.myPlayer != Player.whoAmI) {
                return;
            }

            UpdateLeechOrbSpawn();
            UpdateTongueTrigger();
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

        /// <summary>命中收尾：吸血入池 + 处刑标记消费与视网膜锁定演出</summary>
        private void SettleHit(NPC target, int damageDone) {
            if (!Equipped) {
                return;
            }

            GluttonousThroatGlobalNPC mark = target.GetGlobalNPC<GluttonousThroatGlobalNPC>();
            if (mark.MarkValidFor(Player.whoAmI)) {
                mark.MarkConsumed = true;
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
        #endregion
    }

    /// <summary>
    /// 腐锯：饕餮之喉咬合施加的撕裂减益。防御归零 + 每秒 180 点生命撕裂，
    /// 由服务端 AddBuff 施加并走原版减益同步
    /// </summary>
    internal class RotsawRendDebuff : ModBuff
    {
        /// <summary>持续时长(tick)</summary>
        public const int Duration = 300;
        /// <summary>每秒撕裂生命</summary>
        public const int DotPerSecond = 180;

        public override string Texture => CWRConstant.VaultPlaceholder2;

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex) {
            //防御归零：本帧生效，原版每帧自 defDefense 重置，减益过期即自愈
            npc.defense = 0;
            npc.GetGlobalNPC<GluttonousThroatGlobalNPC>().RotsawActive = true;
        }
    }

    /// <summary>
    /// 敌人侧状态：腐锯撕裂、拖拽冻结、处刑标记。
    /// 拖拽/标记均由各端从同步的舌攫弹幕状态本地推得，无需额外网络包；
    /// 标记消费(拥有者本地)后远端准星最多多亮到窗口自然结束
    /// </summary>
    internal class GluttonousThroatGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>拖拽冻结时间戳：不早于此刻则跳过自驱 AI，位移由服务端书写</summary>
        public long DragHoldUntil = -1;
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
            //lifeRegen 单位为 0.5HP/s：180HP/s → 360
            npc.lifeRegen -= RotsawRendDebuff.DotPerSecond * 2;
            int tick = RotsawRendDebuff.DotPerSecond / 4;
            if (damage < tick) {
                damage = tick;
            }
        }
    }
}
