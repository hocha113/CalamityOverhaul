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
    /// 接管重置族的公共基类：族名 ArmorsReset，恒接管原版；
    /// 汇集各套共用的小工具（命中回血、武器面板取值、爆炸生成、找最近敌人）。
    /// 视觉一律走原版粒子与原版贴图，不自绘
    /// </summary>
    internal abstract class GsResetArmorScheme : GodSmithArmorScheme
    {
        public override string GsFamily => "ArmorsReset";

        public sealed override bool OverridesVanilla => true;

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

        /// <summary>武器面板伤害：手持武器经玩家加成后的显示伤害；手上不是武器时退回这次命中的源伤害</summary>
        protected static int WeaponPanelDamage(Player player, in NPC.HitInfo hit) {
            Item held = player.HeldItem;
            if (held != null && !held.IsAir && held.damage > 0) {
                return Math.Max(1, player.GetWeaponDamage(held));
            }
            return Math.Max(1, hit.SourceDamage);
        }

        /// <summary>武器面板伤害（无命中上下文版本，冲刺撞击等用）；手上不是武器时按 20 计</summary>
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

        /// <summary>在目标处引爆一记盔甲爆炸（owner 侧生成；size 为方形判定边长，style 见 <see cref="GsArmorBlastProj"/>）</summary>
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
    /// 盔甲爆炸：一帧判定的方形爆炸区（ai[0] = 边长，ai[1] = 视觉风格 <see cref="Style"/>），
    /// 只用原版粒子、烟雾残块与音效，不绘制本体。各套的命中/受击触发共用，
    /// 命中自身不再触发套装效果（各方案在 IsOwnEndowProj 过滤）
    /// </summary>
    internal class GsArmorBlastProj : ModProjectile
    {
        /// <summary>爆炸视觉风格：决定粒子种类与音效</summary>
        internal enum Style
        {
            Fire,
            Ice,
            Crystal,
            Spore,
            Holy,
            Vortex,
            Nebula,
            Solar,
            Shell,
        }

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Grenade;

        private ref float Size => ref Projectile.ai[0];

        private Style VisualStyle => (Style)(int)Projectile.ai[1];

        public override void SetDefaults() {
            Projectile.width = 96;
            Projectile.height = 96;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
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
            int count = size / 6;
            Style style = VisualStyle;
            if (style == Style.Fire || style == Style.Solar) {
                //原版手雷式爆炸表现：烟尘 + 火焰 + 烟雾残块 + 爆炸音
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);
                int flame = style == Style.Solar ? DustID.SolarFlare : DustID.Torch;
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
                return;
            }

            //其余风格：单一材质粒子迸散 + 对应音效
            int dustType = DustID.Stone;
            SoundStyle sound = SoundID.Item14;
            switch (style) {
                case Style.Ice:
                    dustType = DustID.IceTorch;
                    sound = SoundID.Item27;
                    break;
                case Style.Crystal:
                    dustType = DustID.PinkCrystalShard;
                    sound = SoundID.Item27;
                    break;
                case Style.Spore:
                    dustType = DustID.GlowingMushroom;
                    break;
                case Style.Holy:
                    dustType = DustID.Enchanted_Gold;
                    sound = SoundID.Item4;
                    break;
                case Style.Vortex:
                    dustType = DustID.Vortex;
                    sound = SoundID.Item92;
                    break;
                case Style.Nebula:
                    dustType = DustID.PurpleTorch;
                    sound = SoundID.Item88;
                    break;
            }
            SoundEngine.PlaySound(sound, Projectile.Center);
            for (int i = 0; i < count * 2; i++) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 100, default, 1.8f);
                dust.noGravity = style != Style.Shell;
                dust.velocity *= 3f;
            }
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }
}
