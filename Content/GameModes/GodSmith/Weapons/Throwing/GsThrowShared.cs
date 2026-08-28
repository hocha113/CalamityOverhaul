using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>
    /// 每弹幕本地状态包(挂 router.LocalState)。各端各持一份不过线;
    /// 回收/返还记账只在 owner 端消费,表现类计数各端自算
    /// </summary>
    internal class GsThrowProjState
    {
        /// <summary>武器直射的主弹幕(owner 端打标时立;承签子弹幕没有)</summary>
        public bool IsPrimary;
        /// <summary>本次投掷已触发免耗,不再参与任何返还通道(防净增刷件)</summary>
        public bool FreeThrow;
        /// <summary>AoE 返还闩锁(每弹幕至多返还一次)</summary>
        public bool RefundGranted;
        /// <summary>本弹幕命中次数</summary>
        public int HitCount;
        /// <summary>最近一次命中的世界帧(OnKill 死因判定:同帧=穿透耗尽)</summary>
        public uint LastHitTick;
        /// <summary>引信重设闩(雷火组各端首帧统一改 timeLeft)</summary>
        public bool FuseSet;
        /// <summary>弹跳计数(弹力雷)</summary>
        public int Bounces;
        /// <summary>子类自由整数</summary>
        public int Custom;
        /// <summary>子类自由浮点</summary>
        public float CustomF;
        /// <summary>子类一次性闩锁</summary>
        public bool Latch;
    }

    /// <summary>
    /// 投掷族每玩家状态:连投层数、暴击返还冷却、域治疗限频。
    /// 全部是本地玩家态(命中类钩子只在攻击方端执行,层数天然只属于本机玩家),不入包不存档
    /// </summary>
    internal class GsThrowPlayer : ModPlayer
    {
        /// <summary>连投层数(命中续窗)</summary>
        public int ComboCount;
        /// <summary>连投绑定的武器物品 ID(换武器清零)</summary>
        public int ComboItemType;
        /// <summary>连投窗口截止帧</summary>
        public uint ComboUntil;
        private uint critRefundReadyAt;
        private uint healWindowStart;
        private int healedInWindow;

        /// <summary>命中续窗:+1 层并刷新 120f 窗口;换武器或断窗自动清零</summary>
        public void AddCombo(int itemType) {
            if (ComboItemType != itemType || Main.GameUpdateCount > ComboUntil) {
                ComboCount = 0;
                ComboItemType = itemType;
            }
            ComboCount++;
            ComboUntil = Main.GameUpdateCount + GsThrowScheme.ComboWindow;
        }

        /// <summary>当前对该武器有效的连投层数</summary>
        public int ComboFor(int itemType)
            => ComboItemType == itemType && Main.GameUpdateCount <= ComboUntil ? ComboCount : 0;

        /// <summary>连投攻速:3/6/9 层 → 1.10/1.18/1.25</summary>
        public float SpeedMulFor(int itemType) {
            int c = ComboFor(itemType);
            return c >= 9 ? 1.25f : c >= 6 ? 1.18f : c >= 3 ? 1.10f : 1f;
        }

        /// <summary>暴击返还的 30f 冷却闸</summary>
        public bool TryCritRefund() {
            if (Main.GameUpdateCount < critRefundReadyAt) {
                return false;
            }
            critRefundReadyAt = Main.GameUpdateCount + 30;
            return true;
        }

        /// <summary>域治疗限频:每 60 帧窗口至多 cap 点</summary>
        public bool TryZoneHeal(int amount, int cap) {
            if (Main.GameUpdateCount - healWindowStart >= 60) {
                healWindowStart = Main.GameUpdateCount;
                healedInWindow = 0;
            }
            if (healedInWindow + amount > cap) {
                return false;
            }
            healedInWindow += amount;
            return true;
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
            => TryBloodZoneLeech(target);

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
            => TryBloodZoneLeech(target);

        /// <summary>血雾域:本机玩家在域内的任意命中吸血 1HP(3/s 上限)。自建钩子须自查模式旗标</summary>
        private void TryBloodZoneLeech(NPC target) {
            if (!GameModeSystem.GodSmithActive || Player.whoAmI != Main.myPlayer
                || target.friendly || target.type == NPCID.TargetDummy
                || Player.statLife >= Player.statLifeMax2) {
                return;
            }
            if (!GsZoneProj.PlayerInZone(Player, GsZoneProj.KindBlood) || !TryZoneHeal(1, 3)) {
                return;
            }
            Player.statLife = Math.Min(Player.statLife + 1, Player.statLifeMax2);
            Player.HealEffect(1);
        }
    }

    /// <summary>
    /// 投掷族 NPC 记账(逐实例)。所有计层字段是攻击方本地量:
    /// 命中类钩子只在攻击方端执行,层数只属于本机玩家,伤害结算也发生在本机,联机自洽。
    /// 域增伤不存字段,走几何查询(域弹幕位置各端同步,结算端算出的结果天然一致)
    /// </summary>
    internal class GsThrowGlobalNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>蜂怒截止帧:再被蜂群命中 +25%</summary>
        public uint BeeRageUntil;
        /// <summary>毒刀叠层与续窗</summary>
        public int PoisonStacks;
        public uint PoisonWindowUntil;
        /// <summary>腐蚀截止帧:本玩家对其 +5 穿甲</summary>
        public uint CorrodeUntil;
        /// <summary>失温层与续窗(霜冻匕首鱼)</summary>
        public int ChillStacks;
        public uint ChillWindowUntil;
        /// <summary>粘性雷蚀灼标记截止帧</summary>
        public uint StickyMarkUntil;
        /// <summary>标枪同目标连击层与续窗</summary>
        public int JavelinStacks;
        public uint JavelinWindowUntil;

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers) {
            //自建钩子:模式旗标自查
            if (!GameModeSystem.GodSmithActive) {
                return;
            }
            float mul = GsZoneProj.DamageTakenMulFor(npc);
            if (mul > 1f) {
                modifiers.FinalDamage *= mul;
            }
        }
    }

    /// <summary>
    /// 投掷消耗族方案基类:回收与连投经济的共享框架。<br/>
    /// 三条回收通道(互斥,免耗的那次投掷不再参与任何返还):<br/>
    /// a. 概率不消耗:GsShoot(owner 端)掷骰,GsConsumeItem 返 false;<br/>
    /// b. 落地回收体:弹幕未命中而亡时按死因掷 <see cref="GsRecoveryPickup"/>;<br/>
    /// c. 暴击直接返还(30f 冷却);AoE 武器替换为「单次命中 ≥3 敌返还一件」。<br/>
    /// 护栏:任何通道概率 ≤65%;手持堆叠 &lt;10 时免耗 +15%(库存告急保护);回收体 ≤10/玩家。<br/>
    /// 连投:命中 +1 层(120f 续窗),3/6/9 层攻速 1.10/1.18/1.25,9 层再 +8% 初速。<br/>
    /// 关键钩子已密封防断接线,机制个性走 GsThrow* 扩展点
    /// </summary>
    internal abstract class GsThrowScheme : GodSmithScheme
    {
        public sealed override string GsFamily => "Throwing";

        /// <summary>连投命中续窗(帧)</summary>
        public const int ComboWindow = 120;
        /// <summary>全域回收封顶:任何单通道概率不得超过它</summary>
        public const float RecoverCap = 0.65f;

        /// <summary>族金色(回收体、免耗回声、手部满转读数共用)</summary>
        internal static readonly Color GsGold = new(255, 214, 120);
        internal static readonly Color GsGoldPale = new(255, 240, 190);

        //==================== 经济参数(子类按计划覆写) ====================

        /// <summary>概率不消耗(0~0.30)</summary>
        protected virtual float NoConsumeChance => 0f;
        /// <summary>撞墙/贴墙钉入死亡时的回收体概率</summary>
        protected virtual float RecoverOnTileChance => 0f;
        /// <summary>超时消亡时的回收体概率</summary>
        protected virtual float RecoverOnFadeChance => 0f;
        /// <summary>暴击命中直接返还一件</summary>
        protected virtual bool CritRefund => false;
        /// <summary>AoE 返还:单次命中 ≥3 敌返还一件(雷火组用,替代暴击返还)</summary>
        protected virtual bool AoERefund => false;
        /// <summary>超时回收改为直接回库(刺球:不生成拾取物)</summary>
        protected virtual bool DirectRefundOnFade => false;
        /// <summary>伤害行(GsModifyWeaponDamage)</summary>
        protected virtual float DamageMul => 1f;
        /// <summary>暴击行</summary>
        protected virtual int CritAdd => 0;
        /// <summary>参与族连投轴(攻速与 9 层初速)</summary>
        protected virtual bool JoinsCombo => true;
        /// <summary>族演出主色(暗影焰刀覆写为紫)</summary>
        protected virtual Color ComboGlowColor => GsGold;
        /// <summary>消耗接管闸:false 时本次消耗判定不介入(雪球被雪球炮当弹药消耗时关)</summary>
        protected virtual bool ConsumeGateOpen(Item item, Player player) => true;

        //==================== 机制扩展点 ====================

        protected virtual void GsThrowModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) { }

        protected virtual bool? GsThrowShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) => null;

        /// <summary>主弹幕打标完成后(owner 端;st 已立好 IsPrimary/FreeThrow)</summary>
        protected virtual void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) { }

        protected virtual void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) { }

        /// <summary>命中扩展(攻击方端;承签子弹幕也会进来,用 st.IsPrimary 或 proj.type 区分)</summary>
        protected virtual void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) { }

        /// <summary>消亡扩展(各端都跑;粒子守 !VaultUtils.isServer,权威逻辑守 owner)</summary>
        protected virtual void GsThrowOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) { }

        protected virtual void GsThrowHold(Item item, Player player) { }

        /// <summary>
        /// OnKill 回收概率(owner 端调用)。默认:超时走 Fade,提前死且非命中耗尽走 Tile,命中耗尽不回收。
        /// 嵌入枪/刺球等特殊死法覆写本方法
        /// </summary>
        protected virtual float RecoverChanceOnKill(Projectile proj, int timeLeft, GsThrowProjState st, bool diedOnHit)
            => timeLeft <= 1 ? RecoverOnFadeChance : diedOnHit ? 0f : RecoverOnTileChance;

        //==================== 密封接线:连投与数值 ====================

        public sealed override float GsUseSpeedMultiplier(Item item, Player player)
            => JoinsCombo ? player.GetModPlayer<GsThrowPlayer>().SpeedMulFor(item.type) : 1f;

        public sealed override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage) {
            if (DamageMul != 1f) {
                damage *= DamageMul;
            }
        }

        public sealed override void GsModifyWeaponCrit(Item item, Player player, ref float crit) {
            if (CritAdd != 0) {
                crit += CritAdd;
            }
        }

        public sealed override void GsHoldItem(Item item, Player player) {
            //满转读数:9 层时手位金焰,个人反馈
            if (player.whoAmI == Main.myPlayer && JoinsCombo && !VaultUtils.isServer
                && Main.GameUpdateCount % 9 == 0
                && player.GetModPlayer<GsThrowPlayer>().ComboFor(item.type) >= 9) {
                PRTLoader.NewParticle<PRT_Spark>(player.itemLocation + Main.rand.NextVector2Circular(4f, 4f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.4f),
                    ComboGlowColor, Main.rand.NextFloat(0.2f, 0.34f))?.Configure(false, 12);
            }
            GsThrowHold(item, player);
        }

        //==================== 密封接线:射击与消耗 ====================

        /// <summary>本次投掷的免耗骰结果;GsShoot(owner 端)写,GsConsumeItem(myPlayer 守门)消费</summary>
        private bool pendingFree;

        public sealed override void GsModifyShootStats(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (JoinsCombo && player.GetModPlayer<GsThrowPlayer>().ComboFor(item.type) >= 9) {
                velocity *= 1.08f;   //满转:初速 +8%,弧线更平
            }
            GsThrowModifyShoot(item, player, ref position, ref velocity, ref type, ref damage, ref knockback);
        }

        public sealed override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //射击链只在 owner 端执行:此处掷免耗骰,库存操作客户端权威
            float chance = EffectiveNoConsume(item);
            pendingFree = item.consumable && chance > 0f && Main.rand.NextFloat() < chance;
            return GsThrowShoot(item, player, source, position, velocity, type, damage, knockback);
        }

        public sealed override bool? GsConsumeItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer || !ConsumeGateOpen(item, player)) {
                return null;
            }
            if (pendingFree) {
                pendingFree = false;
                if (!VaultUtils.isServer) {
                    //免耗回声:手位金闪(个人反馈)
                    PRTLoader.NewParticle<PRT_Sparkle>(player.itemLocation, -Vector2.UnitY * 0.6f,
                        ComboGlowColor, 0.5f)?.Configure(ComboGlowColor, 14, 0.04f, 0.7f);
                }
                return false;
            }
            return null;
        }

        //==================== 密封接线:弹幕命中与消亡 ====================

        public sealed override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            st.IsPrimary = true;
            st.FreeThrow = pendingFree;
            GsThrowOnSpawn(proj, router, st);
        }

        public sealed override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router)
            => GsThrowModifyHit(proj, target, ref modifiers, router);

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            GsThrowProjState st = router.GetOrCreateState<GsThrowProjState>();
            st.HitCount++;
            st.LastHitTick = Main.GameUpdateCount;
            if (proj.owner == Main.myPlayer && st.IsPrimary) {
                Player player = Main.player[proj.owner];
                if (JoinsCombo) {
                    player.GetModPlayer<GsThrowPlayer>().AddCombo(TargetItemID);
                }
                if (!st.FreeThrow && target.type != NPCID.TargetDummy) {
                    if (CritRefund && hit.Crit && player.GetModPlayer<GsThrowPlayer>().TryCritRefund()) {
                        RefundOne(player, target.Center);
                    }
                    if (AoERefund && !st.RefundGranted && st.HitCount >= 3) {
                        st.RefundGranted = true;
                        RefundOne(player, target.Center);
                    }
                }
            }
            GsThrowOnHit(proj, target, hit, damageDone, router, st);
        }

        public sealed override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //回收体判定:owner 权威;主弹幕且非免耗投掷才参与
            if (proj.owner == Main.myPlayer
                && router.LocalState is GsThrowProjState st && st.IsPrimary && !st.FreeThrow) {
                bool diedOnHit = st.LastHitTick != 0 && st.LastHitTick == Main.GameUpdateCount;
                float chance = Math.Min(RecoverChanceOnKill(proj, timeLeft, st, diedOnHit), RecoverCap);
                if (chance > 0f && Main.rand.NextFloat() < chance) {
                    if (DirectRefundOnFade && timeLeft <= 1) {
                        RefundOne(Main.player[proj.owner], proj.Center);
                    }
                    else {
                        SpawnRecovery(proj);
                    }
                }
            }
            GsThrowOnKill(proj, timeLeft, router);
        }

        //==================== 共享工具 ====================

        /// <summary>库存告急保护 + 65% 封顶后的有效免耗率</summary>
        protected float EffectiveNoConsume(Item item) {
            float c = NoConsumeChance;
            if (c > 0f && item.stack < 10) {
                c += 0.15f;
            }
            return Math.Min(c, RecoverCap);
        }

        /// <summary>owner 侧直接返还一件(背包优先的 GiveItem,联机安全;服务器不写客户端背包)</summary>
        protected void RefundOne(Player player, Vector2 at) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            player.GiveItem(player.GetSource_Misc("GsThrowRefund"), TargetItemID, 1);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.6f }, at);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(at + Main.rand.NextVector2Circular(6f, 6f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), -Main.rand.NextFloat(1f, 2.2f)),
                        ComboGlowColor, Main.rand.NextFloat(0.25f, 0.4f))?.Configure(false, 16);
                }
            }
        }

        /// <summary>owner 侧在弹幕处生成回收体;超过 10 颗时最旧一颗强制磁吸</summary>
        protected void SpawnRecovery(Projectile from) => SpawnRecoveryAt(from.GetSource_FromThis(), from.Center, from.owner);

        /// <summary>owner 侧在指定位置生成回收体(必掉类路径直接用)</summary>
        protected void SpawnRecoveryAt(IEntitySource source, Vector2 pos, int owner) {
            if (owner != Main.myPlayer) {
                return;
            }
            Player player = Main.player[owner];
            int type = ModContent.ProjectileType<GsRecoveryPickup>();
            if (player.ownedProjectileCounts[type] >= 10) {
                //超员:最旧的一颗强制磁吸,腾位
                Projectile oldest = null;
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == type && p.owner == owner
                        && (oldest == null || p.timeLeft < oldest.timeLeft)) {
                        oldest = p;
                    }
                }
                if (oldest != null && oldest.ai[1] == 0f) {
                    oldest.ai[1] = 1f;
                    oldest.netUpdate = true;
                }
            }
            Vector2 vel = new(Main.rand.NextFloat(-1.2f, 1.2f), -Main.rand.NextFloat(2f, 3.2f));
            Projectile.NewProjectile(source, pos, vel, type, 0, 0f, owner, TargetItemID, 0f);
        }

        /// <summary>数「本玩家嵌入在该目标身上」的指定类型弹幕(原版嵌入弹幕 ai[0]==1、ai[1]=目标编号)</summary>
        protected static int CountStuckOn(NPC target, int owner, int projType) {
            int count = 0;
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == projType && p.owner == owner
                    && p.ai[0] == 1f && (int)p.ai[1] == target.whoAmI) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>该弹幕当前处于嵌入态(原版嵌入弹幕通用语义)</summary>
        protected static bool IsStuck(Projectile proj) => proj.ai[0] == 1f;
    }
}
