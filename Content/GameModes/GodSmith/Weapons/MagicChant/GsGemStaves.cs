using CalamityOverhaul.Common;
using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 宝石七杖共用子模板：棱彩分裂。正拍宝石弹命中按施法层数裂出本色碎晶
    /// （1 + 层数/2 枚，各 0.35 倍，每杖一签名碎晶行为）；满层强化「全彩咏唱」：
    /// 一次射三发（本杖 + 色环相邻两色弹体，各 0.8 倍）。材质身份：棱晶折光。<br/>
    /// 色环序：紫晶-黄玉-蓝玉-翡翠-红玉-钻石-琥珀（首尾相接）
    /// </summary>
    internal abstract class GsGemStaffScheme : GsChantScheme
    {
        /// <summary>色环序号 0~6</summary>
        protected abstract int GemIndex { get; }

        /// <summary>七色宝石弹幕类型（色环序）</summary>
        internal static readonly int[] GemBoltTypes = [
            ProjectileID.AmethystBolt, ProjectileID.TopazBolt, ProjectileID.SapphireBolt,
            ProjectileID.EmeraldBolt, ProjectileID.RubyBolt, ProjectileID.DiamondBolt,
            ProjectileID.AmberBolt,
        ];

        /// <summary>七色宝石主题色（色环序）</summary>
        internal static readonly Color[] GemColors = [
            new(196, 122, 255), new(255, 214, 92), new(96, 152, 255),
            new(112, 235, 146), new(255, 104, 116), new(224, 242, 255),
            new(255, 172, 72),
        ];

        /// <summary>形态：棱彩碎晶（黄玉杖 MarkData2 = 追踪目标 whoAmI，其余杖无参）</summary>
        protected const float FormShard = 10f;
        /// <summary>形态：红玉小爆</summary>
        protected const float FormBurst = 12f;
        /// <summary>红玉小爆判定边长（px）：Resize 与爆环绘制同源</summary>
        protected const int BurstBoxPx = 80;
        /// <summary>红玉小爆判定窗（帧）：timeLeft 与爆环扩张进度同源</summary>
        protected const int BurstLifeTicks = 6;
        /// <summary>形态：钻石折光短线</summary>
        protected const float FormRay = 13f;
        /// <summary>形态：琥珀滞留尘域</summary>
        protected const float FormAmberField = 14f;

        protected sealed override Color ChantColor => GemColors[GemIndex];

        //==================== 全彩咏唱 ====================

        protected sealed override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //本杖 + 色环相邻两色，各 0.8 倍微扇齐射；异色弹同样打上强化标，走本杖签名分裂
            int[] volley = [type, GemBoltTypes[(GemIndex + 6) % 7], GemBoltTypes[(GemIndex + 1) % 7]];
            int volleyDamage = Math.Max(1, (int)(damage * 0.8f));
            for (int i = 0; i < volley.Length; i++) {
                float off = MathHelper.ToRadians(-6f + 6f * i);
                Projectile.NewProjectile(source, position, velocity.RotatedBy(off),
                    volley[i], volleyDamage, knockback, player.whoAmI);
            }
            return false;
        }

        //==================== 棱彩分裂 ====================

        public sealed override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            Color gem = ColorOfProj(proj.type);
            if (!VaultUtils.isServer) {
                //命中相：棱晶迸散
                for (int i = 0; i < 4; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(2.5f, 2.5f), gem, Main.rand.NextFloat(0.5f, 0.8f))
                        ?.Configure(gem, Main.rand.Next(12, 20), 0.1f, 0.8f);
                }
                PRTLoader.NewParticle<PRT_Light>(target.Center, Vector2.Zero, gem, 0.13f)?.Configure(8, 0.7f);
            }

            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            //签名碎晶的二段行为（红玉小爆/钻石折光/琥珀尘域在碎晶命中或消亡处）
            if (router.MarkData == FormShard) {
                OnShardHit(proj, target);
                return;
            }
            if (router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            //正拍弹体命中：按施法层数裂 1 + 层/2 枚碎晶（翡翠签名再 +1）
            int count = 1 + (int)(router.MarkData2 / 2f) + ShardCountBonus;
            int shardDamage = Math.Max(1, (int)(proj.damage * 0.35f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < count; i++) {
                float off = MathHelper.Lerp(-0.6f, 0.6f, count == 1 ? 0.5f : i / (float)(count - 1));
                Vector2 vel = dir.RotatedBy(off) * 7.5f * ShardSpeedMult;
                QueueForm(Main.player[proj.owner], FormShard, ShardParamFor(target));
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    proj.type, shardDamage, proj.knockBack * 0.3f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    ConfigureShard(Main.projectile[idx]);
                }
            }
        }

        /// <summary>碎晶初速倍率（蓝玉直射覆写）</summary>
        protected virtual float ShardSpeedMult => 1f;

        /// <summary>碎晶数量加成（翡翠签名）</summary>
        protected virtual int ShardCountBonus => 0;

        /// <summary>碎晶形态参数（黄玉写追踪目标）</summary>
        protected virtual float ShardParamFor(NPC hitTarget) => 0f;

        /// <summary>碎晶出生改制（owner 端，紫晶穿透等）</summary>
        protected virtual void ConfigureShard(Projectile shard) {
            shard.scale *= 0.62f;
            shard.timeLeft = Math.Min(shard.timeLeft, 50);
        }

        /// <summary>碎晶命中的二段行为（红玉小爆/钻石折光；owner 端）</summary>
        protected virtual void OnShardHit(Projectile shard, NPC target) { }

        /// <summary>碎晶追加 AI（黄玉追踪；各端执行）</summary>
        protected virtual void ShardPostAI(Projectile shard, GodSmithProjRouter router) { }

        //==================== 四相演出 ====================

        public sealed override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (router.MarkData == FormShard) {
                ShardPostAI(proj, router);
            }
            else if (router.MarkData == FormBurst || router.MarkData == FormAmberField) {
                //驻场形态定身
                proj.velocity = Vector2.Zero;
            }
            if (VaultUtils.isServer) {
                return;
            }
            Color gem = ColorOfProj(proj.type);
            Lighting.AddLight(proj.Center, gem.ToVector3() * 0.24f);
            //飞行相：棱晶折光闪，正拍与碎晶更密
            bool hot = router.MarkData is FormOnBeat or FormEmpower or FormShard;
            int interval = hot ? 5 : 8;
            if (proj.timeLeft % interval == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center + Main.rand.NextVector2Circular(4f, 4f),
                    -proj.velocity * 0.04f, gem, Main.rand.NextFloat(0.35f, 0.6f))
                    ?.Configure(gem, Main.rand.Next(10, 16), 0.08f, 0.7f);
            }
            //琥珀尘域：滞留期的尘粒缓旋
            if (router.MarkData == FormAmberField && proj.timeLeft % 3 == 0) {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                PRTLoader.NewParticle<PRT_Light>(proj.Center + ang.ToRotationVector2() * Main.rand.NextFloat(6f, 30f),
                    ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.8f,
                    GemColors[6], 0.07f)?.Configure(10, 0.6f);
            }
        }

        public sealed override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            //红玉小爆自绘：判定同源的扩张爆环替代未放大的弹体（镜像水矢涟漪画法），
            //半径终点 = BurstBoxPx/2，与 Resize 的判定框同一常量
            if (router.MarkData != FormBurst) {
                return null;
            }
            float t = 1f - proj.timeLeft / (float)BurstLifeTicks;
            Color gem = ColorOfProj(proj.type);
            ShockRingDraw.Draw(Main.spriteBatch, proj.Center,
                MathHelper.Lerp(12f, BurstBoxPx * 0.5f, t), 7f,
                Color.Lerp(gem, Color.White, 0.6f), gem, Color.Lerp(gem, Color.Black, 0.65f),
                0.85f * (1f - t * t), squish: 1f, innerGlow: 0.35f, timeSeed: proj.identity * 0.37f);
            return false;
        }

        public sealed override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            //琥珀签名：碎晶消亡处滞留尘域（owner 生成，全端可见）
            if (router.MarkData == FormShard && GemIndex == 6 && proj.IsOwnedByLocalPlayer()) {
                QueueForm(Main.player[proj.owner], FormAmberField);
                int idx = Projectile.NewProjectile(proj.GetSource_FromThis(), proj.Center, Vector2.Zero,
                    proj.type, Math.Max(1, (int)(proj.damage * 0.45f)), 0f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Projectile field = Main.projectile[idx];
                    field.timeLeft = 72;
                    field.scale *= 1.5f;
                    field.Resize(60, 60);
                    //尘域多跳：0.3s 一跳
                    field.localNPCHitCooldown = 18;
                    field.netUpdate = true;
                }
            }
            //余痕相：光屑缓落，活得比弹体久
            if (VaultUtils.isServer) {
                return;
            }
            Color gem = ColorOfProj(proj.type);
            int count = router.MarkData == FormShard ? 2 : 3;
            for (int i = 0; i < count; i++) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.3f, 0.9f)),
                    gem, Main.rand.NextFloat(0.07f, 0.11f))?.Configure(Main.rand.Next(16, 26), 0.65f);
            }
        }

        /// <summary>按弹幕类型取宝石色（全彩咏唱的异色弹用各自本色演出）</summary>
        protected static Color ColorOfProj(int projType) {
            for (int i = 0; i < GemBoltTypes.Length; i++) {
                if (GemBoltTypes[i] == projType) {
                    return GemColors[i];
                }
            }
            return GemColors[5];
        }
    }

    /// <summary>紫晶法杖：碎晶多穿一目标（穿透 +1）</summary>
    internal class GsAmethystStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.AmethystStaff;
        protected override int GemIndex => 0;
        protected override float BaseDamageMult => 1.12f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into piercing amethyst shards on hit;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override void ConfigureShard(Projectile shard) {
            base.ConfigureShard(shard);
            if (shard.penetrate > 0) {
                shard.penetrate++;
            }
        }
    }

    /// <summary>黄玉法杖：碎晶追踪最近敌（每帧最多转 6 度）</summary>
    internal class GsTopazStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.TopazStaff;
        protected override int GemIndex => 1;
        protected override float BaseDamageMult => 1.11f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into homing topaz shards on hit;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override float ShardParamFor(NPC hitTarget) {
            //追踪目标 owner 端锁定进 MarkData2 随生成包过线，各端一致转向
            NPC next = FindNearestEnemy(hitTarget.Center, 260f, hitTarget.whoAmI);
            return next?.whoAmI ?? hitTarget.whoAmI;
        }

        protected override void ShardPostAI(Projectile shard, GodSmithProjRouter router) {
            int who = (int)router.MarkData2;
            if (who < 0 || who >= Main.maxNPCs) {
                return;
            }
            NPC target = Main.npc[who];
            if (target.active && target.CanBeChasedBy()) {
                SteerTowards(shard, target.Center, MathHelper.ToRadians(6f));
            }
        }
    }

    /// <summary>蓝玉法杖：碎晶以 1.5 倍速直射（穿刺水色流光）</summary>
    internal class GsSapphireStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.SapphireStaff;
        protected override int GemIndex => 2;
        protected override float BaseDamageMult => 1.10f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into swift sapphire lances on hit;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override float ShardSpeedMult => 1.5f;
    }

    /// <summary>翡翠法杖：多裂一枚碎晶（生机盈余）</summary>
    internal class GsEmeraldStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.EmeraldStaff;
        protected override int GemIndex => 3;
        protected override float BaseDamageMult => 1.09f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts burst into one extra emerald shard on hit;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override int ShardCountBonus => 1;
    }

    /// <summary>红玉法杖：碎晶命中迸出 40px 小爆</summary>
    internal class GsRubyStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.RubyStaff;
        protected override int GemIndex => 4;
        protected override float BaseDamageMult => 1.08f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into ruby shards that detonate in a small blast;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override void OnShardHit(Projectile shard, NPC target) {
            QueueForm(Main.player[shard.owner], FormBurst);
            int idx = Projectile.NewProjectile(shard.GetSource_FromThis(), target.Center, Vector2.Zero,
                shard.type, shard.damage, 0f, shard.owner);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile burst = Main.projectile[idx];
                burst.timeLeft = BurstLifeTicks;
                burst.Resize(BurstBoxPx, BurstBoxPx);
                burst.netUpdate = true;
            }
        }
    }

    /// <summary>钻石法杖：碎晶命中折射两道高速光线</summary>
    internal class GsDiamondStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.DiamondStaff;
        protected override int GemIndex => 5;
        protected override float BaseDamageMult => 1.06f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into diamond shards that refract twin light rays;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";

        protected override void OnShardHit(Projectile shard, NPC target) {
            Vector2 dir = shard.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = dir.RotatedBy(i == 0 ? 0.5f : -0.5f) * 14f;
                QueueForm(Main.player[shard.owner], FormRay);
                int idx = Projectile.NewProjectile(shard.GetSource_FromThis(), target.Center, vel,
                    shard.type, Math.Max(1, (int)(shard.damage * 0.5f)), 0f, shard.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].timeLeft = 14;
                    Main.projectile[idx].scale *= 0.55f;
                    Main.projectile[idx].netUpdate = true;
                }
            }
        }
    }

    /// <summary>琥珀法杖：碎晶消亡处滞留 1.2s 琥珀尘域（多跳低伤）</summary>
    internal class GsAmberStaff : GsGemStaffScheme
    {
        public override int TargetItemID => ItemID.AmberStaff;
        protected override int GemIndex => 6;
        protected override float BaseDamageMult => 1.12f;
        protected override string GsDescFallback =>
            "Reforged: on-beat bolts shatter into amber shards that linger as motes of stinging dust;" +
            "\nat full resonance the next cast fires three gem bolts of neighboring hues";
        //尘域行为在模板 GsProjOnKill 按 GemIndex 分支实现
    }
}
