using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Reset
{
    /// <summary>
    /// 盔甲 proc 弹幕的族标记：带此接口的弹幕命中时不再触发任何接管方案的命中钩子
    /// （族级防自喂，替代逐套的 IsOwnEndowProj 白名单）
    /// </summary>
    internal interface IGsArmorProc { }

    /// <summary>
    /// 接管重置族的公共基类：族名 ArmorsReset，恒接管原版。<br/>
    /// R3 之法（详 Doc/plans/GODSMITH/R3/R3-00-ARMOR-IDENTITY.md）：单件属性走原版职业属性；
    /// 套装奖励第一句 = 原版奖励；每套一条由材质推出的签名机制，伤害类型跟随套装职业；
    /// proc 伤害统一走 <see cref="ProcDamage"/>，视觉一律借原版弹幕贴图与原版粒子，不自绘。<br/>
    /// 镶嵌链：被继承的档只给物性，防御与胸甲挖掘只在 <see cref="IsWorn"/> 时给
    /// </summary>
    internal abstract class GsResetArmorScheme : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsReset";

        public sealed override bool OverridesVanilla => true;

        /// <summary>族内一切 proc 弹幕都不回喂任何方案</summary>
        public sealed override bool IsOwnEndowProj(Projectile proj) => proj.ModProjectile is IGsArmorProc;

        /// <summary>本方案此刻是否穿在身上（而非经镶嵌链被继承）</summary>
        protected bool IsWorn(GodSmithArmorPlayer state) => state.ActiveScheme == this;

        /// <summary>proc 伤害统一公式：按这次命中的比例取值并夹在 [min, cap]</summary>
        protected static int ProcDamage(int damageDone, float ratio, int min, int cap)
            => Math.Clamp((int)(damageDone * ratio), min, cap);

        /// <summary>
        /// 只在穿戴者本端生成 proc 弹幕（远端靠弹幕同步看到）；返回 null 表示本端不是 owner
        /// </summary>
        protected static Projectile SpawnProc(Player player, string context, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback, float ai0 = 0f, float ai1 = 0f, float ai2 = 0f) {
            if (player.whoAmI != Main.myPlayer) {
                return null;
            }
            int index = Projectile.NewProjectile(player.GetSource_Misc(context), position, velocity,
                type, Math.Max(1, damage), knockback, player.whoAmI, ai0, ai1, ai2);
            return index >= 0 && index < Main.maxProjectiles ? Main.projectile[index] : null;
        }

        //==================== 每方案计数器（镶嵌链上多套共存时各用各的） ====================

        /// <summary>本方案在该玩家身上的计数</summary>
        protected int Tally(Player player) => player.GetModPlayer<GsArmorTallyPlayer>().Get(this);

        /// <summary>写计数并记下时间戳</summary>
        protected void SetTally(Player player, int value) => player.GetModPlayer<GsArmorTallyPlayer>().Set(this, value);

        /// <summary>计数 +1；达到 full 时归零并返回 true（满层释放拍）</summary>
        protected bool TallyUp(Player player, int full) {
            GsArmorTallyPlayer tally = player.GetModPlayer<GsArmorTallyPlayer>();
            int next = tally.Get(this) + 1;
            if (next >= full) {
                tally.Set(this, 0);
                return true;
            }
            tally.Set(this, next);
            return false;
        }

        /// <summary>距上次写计数是否已超过 frames 帧（衰退判定）</summary>
        protected bool TallyStale(Player player, int frames) {
            GsArmorTallyPlayer tally = player.GetModPlayer<GsArmorTallyPlayer>();
            return tally.Get(this) > 0 && Main.GameUpdateCount - tally.Stamp(this) > (uint)frames;
        }

        /// <summary>距上次写计数经过的帧数（从未写过按极大值计）</summary>
        protected uint TallyAge(Player player) {
            GsArmorTallyPlayer tally = player.GetModPlayer<GsArmorTallyPlayer>();
            uint stamp = tally.Stamp(this);
            return stamp == 0 ? uint.MaxValue : Main.GameUpdateCount - stamp;
        }

        /// <summary>独立于计数的时间标记（记「最近一次受击」这类事件）</summary>
        protected void MarkNow(Player player) => player.GetModPlayer<GsArmorTallyPlayer>().Mark(this);

        /// <summary>距上次 <see cref="MarkNow"/> 经过的帧数（从未标记按极大值计）</summary>
        protected uint MarkAge(Player player) {
            uint stamp = player.GetModPlayer<GsArmorTallyPlayer>().MarkStamp(this);
            return stamp == 0 ? uint.MaxValue : Main.GameUpdateCount - stamp;
        }

        public override void OnEndowLost(Player player, GodSmithArmorPlayer state) {
            state.ClearScratch();
            player.GetModPlayer<GsArmorTallyPlayer>().Clear();
        }

        //==================== 共用小工具 ====================

        /// <summary>
        /// 命中回血：按伤害比例治疗并封顶，走按方案键的冷却表；只在攻击方端（命中钩子）调用，
        /// Heal 自带同步
        /// </summary>
        protected void HealOnHit(Player player, GodSmithArmorPlayer state, int damageDone, float ratio, int cap, int cooldownFrames) {
            if (player.whoAmI != Main.myPlayer || player.statLife >= player.statLifeMax2) {
                return;
            }
            int heal = Math.Clamp((int)(damageDone * ratio), 1, cap);
            if (!state.TryUseCooldown(this, cooldownFrames)) {
                return;
            }
            player.Heal(heal);
        }

        /// <summary>武器面板伤害（无命中上下文版本，冲刺撞击用）；手上不是武器时按 20 计</summary>
        protected static int WeaponPanelDamage(Player player) {
            Item held = player.HeldItem;
            if (held != null && !held.IsAir && held.damage > 0) {
                return Math.Max(1, player.GetWeaponDamage(held));
            }
            return 20;
        }

        /// <summary>
        /// 冲刺撞击判定（只在穿戴者本端调用）：玩家碰撞箱外扩 inflate 像素内的敌人各触发一次 onHit，
        /// 同一敌人在 cooldown 帧内不重复触发
        /// </summary>
        protected static void DashCollide(Player player, float inflate, int cooldown, Action<NPC> onHit) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            GsArmorDashPlayer dash = player.GetModPlayer<GsArmorDashPlayer>();
            Rectangle box = player.Hitbox;
            box.Inflate((int)inflate, (int)inflate);
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.friendly || npc.dontTakeDamage || npc.immortal || !npc.Hitbox.Intersects(box)) {
                    continue;
                }
                if (dash.TryClaimHit(npc.whoAmI, cooldown)) {
                    onHit(npc);
                }
            }
        }

        /// <summary>在目标处引爆一记冲刺撞击爆炸（owner 侧生成；size 为方形判定边长）</summary>
        protected static void SpawnBlast(Player player, Vector2 center, int damage, float size, string context,
            GsArmorBlastProj.Style style = GsArmorBlastProj.Style.Fire) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            Projectile.NewProjectile(player.GetSource_Misc(context), center, Vector2.Zero,
                ModContent.ProjectileType<GsArmorBlastProj>(), Math.Max(1, damage), 6f, player.whoAmI, size, (float)style);
        }

        /// <summary>距 from 最近、可被追踪的存活敌人；无则 null</summary>
        protected static NPC NearestEnemy(Vector2 from, float range, int skipWhoAmI = -1) {
            NPC best = null;
            float bestDist = range;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (npc.whoAmI == skipWhoAmI || npc.friendly || npc.dontTakeDamage || npc.immortal || npc.lifeMax <= 5) {
                    continue;
                }
                float dist = from.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        /// <summary>受击信息里的攻击方 NPC（不是 NPC 打的返回 null）</summary>
        protected static NPC HurtSourceNPC(in Player.HurtInfo info) {
            int index = info.DamageSource.SourceNPCIndex;
            if (index < 0 || index >= Main.maxNPCs) {
                return null;
            }
            NPC npc = Main.npc[index];
            return npc.active ? npc : null;
        }

        /// <summary>在两点之间铺一串原版粒子（电弧/丝线一类的连线表现，只在客户端）</summary>
        protected static void DustLine(Vector2 from, Vector2 to, int dustType, float scale, int step = 8) {
            if (Main.dedServ) {
                return;
            }
            float length = from.Distance(to);
            int count = Math.Max(2, (int)(length / step));
            for (int i = 0; i <= count; i++) {
                Vector2 pos = Vector2.Lerp(from, to, i / (float)count);
                Dust dust = Dust.NewDustPerfect(pos, dustType, Vector2.Zero, 0, default, scale);
                dust.noGravity = true;
            }
        }
    }

    /// <summary>
    /// 每方案计数器 ModPlayer：按方案键存整型计数与最近写入帧，供镶嵌链上多套同时积攒时互不干扰；
    /// 只在穿戴者本端读写（命中钩子在攻击方端执行）
    /// </summary>
    internal class GsArmorTallyPlayer : ModPlayer
    {
        private readonly Dictionary<GodSmithArmorScheme, int> tallies = [];
        private readonly Dictionary<GodSmithArmorScheme, uint> stamps = [];
        private readonly Dictionary<GodSmithArmorScheme, uint> marks = [];

        internal int Get(GodSmithArmorScheme key) => tallies.TryGetValue(key, out int value) ? value : 0;

        internal uint Stamp(GodSmithArmorScheme key) => stamps.TryGetValue(key, out uint stamp) ? stamp : 0;

        internal void Set(GodSmithArmorScheme key, int value) {
            tallies[key] = value;
            stamps[key] = Main.GameUpdateCount;
        }

        internal uint MarkStamp(GodSmithArmorScheme key) => marks.TryGetValue(key, out uint stamp) ? stamp : 0;

        internal void Mark(GodSmithArmorScheme key) => marks[key] = Main.GameUpdateCount;

        internal void Clear() {
            tallies.Clear();
            stamps.Clear();
            marks.Clear();
        }
    }

    /// <summary>
    /// 冲刺撞击类套装奖励的每玩家状态（水晶刺客/日耀共用）：撞击去重表、自建冲刺计时与必暴击计数。
    /// 只在穿戴者本端读写
    /// </summary>
    internal class GsArmorDashPlayer : ModPlayer
    {
        /// <summary>npc.whoAmI → 上次撞击帧</summary>
        private readonly Dictionary<int, uint> lastDashHit = [];

        /// <summary>自建冲刺计时：原版 dashDelay 只在起跳帧为 -1，用它起表后倒数当作冲刺持续期</summary>
        internal int DashFrames;

        /// <summary>剩余必定暴击次数</summary>
        internal int GuaranteedCrits;

        /// <summary>该敌人是否可被本次冲刺撞击（cooldown 帧内同一敌人只算一次）</summary>
        internal bool TryClaimHit(int npcWhoAmI, int cooldown) {
            if (lastDashHit.TryGetValue(npcWhoAmI, out uint last) && Main.GameUpdateCount - last < (uint)cooldown) {
                return false;
            }
            lastDashHit[npcWhoAmI] = Main.GameUpdateCount;
            return true;
        }

        public override void ResetEffects() {
            if (DashFrames > 0) {
                DashFrames--;
            }
        }
    }

    /// <summary>
    /// 冲刺撞击爆炸（日耀专用）：一帧判定的方形爆炸区（ai[0] = 边长，ai[1] = 风格 <see cref="Style"/>），
    /// 只用原版粒子、烟雾残块与音效，不绘制本体
    /// </summary>
    internal class GsArmorBlastProj : ModProjectile, IGsArmorProc
    {
        /// <summary>爆炸风格：普通火焰 / 日耀焰</summary>
        internal enum Style
        {
            Fire,
            Solar,
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Grenade;

        private ref float Size => ref Projectile.ai[0];

        private Style VisualStyle => (Style)(int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 3;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            if (Projectile.localAI[0] != 0f) {
                return;
            }
            Projectile.localAI[0] = 1f;

            //按参数撑开判定区，保持中心不动
            int size = (int)MathHelper.Clamp(Size, 48f, 220f);
            Vector2 center = Projectile.Center;
            Projectile.width = size;
            Projectile.height = size;
            Projectile.Center = center;

            if (Main.dedServ) {
                return;
            }
            //原版手雷式爆炸表现：烟尘 + 火焰 + 烟雾残块 + 爆炸音
            int count = size / 6;
            int flame = VisualStyle == Style.Solar ? DustID.SolarFlare : DustID.Torch;
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
            for (int i = 0; i < count; i++) {
                Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.6f);
                smoke.velocity *= 1.4f;
            }
            for (int i = 0; i < count; i++) {
                Dust fire = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, flame, 0f, 0f, 100, default, 2.4f);
                fire.noGravity = true;
                fire.velocity *= 4f;
                Dust ember = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, flame, 0f, 0f, 100, default, 1.4f);
                ember.velocity *= 2f;
            }
            for (int i = 0; i < 2; i++) {
                Gore gore = Gore.NewGoreDirect(Projectile.GetSource_FromThis(),
                    Projectile.Center + Main.rand.NextVector2Circular(size * 0.3f, size * 0.3f),
                    Main.rand.NextVector2Circular(2f, 2f), Main.rand.Next(61, 64));
                gore.velocity *= 0.5f;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
