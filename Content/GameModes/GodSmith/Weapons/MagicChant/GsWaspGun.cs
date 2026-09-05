using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 黄蜂枪重铸：信息素猎标。材质身份：狩猎蜂群（琥珀毒针的活体箭雨）。
    /// 与 GsBeeGun（V 字编队/蜂后凝阵）签名互异：本杖不编队，走「标记猎杀」。<br/>
    /// ①「信息素」：正拍黄蜂命中挂猎标（攻击方本地层）；
    /// 在场己方黄蜂优先转向被标记目标（owner 端转向 + 节流同步）；<br/>
    /// ②满层强化「蜂后凝聚」：放出蜂后巨弹，蜿蜒巡航沿途每 20t 放出一只黄蜂；
    /// ③施法有枪口上跳与起手蜂鸣（音高随共鸣层）
    /// </summary>
    internal class GsWaspGun : GsChantScheme
    {
        public override int TargetItemID => ItemID.WaspGun;

        protected override string GsDescFallback =>
            "Reforged: on-beat wasps sting a pheromone mark into their prey; every wasp in the air hunts the marked target first\nAt full resonance the next cast releases a queen wasp that cruises ahead, seeding hunters along her path";
        protected override float BaseDamageMult => 1.06f;

        /// <summary>原版黄蜂弹类型</summary>
        internal static int WaspType => ContentSamples.ItemsByType[ItemID.WaspGun].shoot;

        //==================== 动画法：枪口上跳 + 起手蜂鸣 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //枪口上跳：出手瞬间上踢 3px 并抬口，随动画进度回落（绝对剖面 0.1·p，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -3f) * progress;
            GsMagicKickMath.ApplyKickDiff(player, 0.1f * progress, 0.1f * ((player.itemAnimation + 1) / n));
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手蜂鸣：音高随共鸣层（共鸣是 owner 本地量，远端听基准音即可）
            int resonance = player.whoAmI == Main.myPlayer ? Chant(player).Resonance : 0;
            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.4f,
                Pitch = 0.05f * Math.Min(resonance, 6),
                MaxInstances = 3
            }, player.Center);
        }

        //==================== 强化咏唱：蜂后凝聚 ====================

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //蜂后巨弹替换本发（1.4 倍蜿蜒巡航），沿途落蜂由蜂后自己负责
            SoundEngine.PlaySound(SoundID.Item97 with { Volume = 0.8f, Pitch = -0.2f }, player.Center);
            int queenDamage = Math.Max(1, (int)(damage * 1.4f));
            Projectile.NewProjectile(source, position, velocity.SafeNormalize(Vector2.UnitX) * 7f,
                ModContent.ProjectileType<GsWaspGunQueenProj>(), queenDamage, knockback * 1.5f, player.whoAmI);
            return false;
        }

        //==================== 飞行相：猎标转向 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != WaspType) {
                return;
            }
            //猎标转向：owner 端把黄蜂掰向最近的被标记目标（每 20t 节流 netUpdate 修正远端）
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            NPC marked = FindMarkedTarget(proj.Center, 640f);
            if (marked == null) {
                return;
            }
            float speed = proj.velocity.Length();
            Vector2 wanted = (marked.Center - proj.Center).SafeNormalize(Vector2.UnitX);
            proj.velocity = Vector2.Lerp(proj.velocity.SafeNormalize(Vector2.UnitX), wanted, 0.12f)
                .SafeNormalize(Vector2.UnitX) * speed;
            if (proj.timeLeft % 20 == 0) {
                proj.netUpdate = true;
            }
        }

        /// <summary>最近的带猎标目标（猎标是攻击方本地量，本函数只在 owner 端有意义）</summary>
        internal static NPC FindMarkedTarget(Vector2 from, float range) {
            NPC best = null;
            float bestDist = range * range;
            uint now = Main.GameUpdateCount;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy()) {
                    continue;
                }
                GsWaspGunNPC mark = npc.GetGlobalNPC<GsWaspGunNPC>();
                if (mark.MarkUntil <= now) {
                    continue;
                }
                float d = Vector2.DistanceSquared(npc.Center, from);
                if (d < bestDist) {
                    bestDist = d;
                    best = npc;
                }
            }
            return best;
        }

        //==================== 命中：挂猎标 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != WaspType) {
                return;
            }
            if (!proj.IsOwnedByLocalPlayer()) {
                return;
            }
            target.AddBuff(BuffID.Poisoned, 180);
            //正拍蜂针注入信息素：4 秒猎标
            if (router.MarkData is FormOnBeat or FormEmpower) {
                target.GetGlobalNPC<GsWaspGunNPC>().MarkUntil = Main.GameUpdateCount + 240;
            }
        }
    }

    /// <summary>
    /// 信息素猎标（攻击方本地量：命中钩子只在攻击方端执行，
    /// 转向裁决在 owner 端、弹道经节流 netUpdate 过线）
    /// </summary>
    internal class GsWaspGunNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>猎标失效时刻</summary>
        internal uint MarkUntil;
    }

    /// <summary>
    /// 蜂后巨弹：强化咏唱放出的琥珀巡航母体。蜿蜒推进（identity 定相正弦），
    /// owner 端沿途每 20t 放出一只原版黄蜂（0.5 倍）；本体沿用原版黄蜂贴图默认绘制（多帧）
    /// </summary>
    internal class GsWaspGunQueenProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Wasp;

        public override string LocalizationCategory => "GodSmithMagicChant";

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.Wasp];
        }

        public override void SetDefaults() {
            Projectile.width = Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 24;
            Projectile.timeLeft = 180;
        }

        public override void AI() {
            //蜿蜒巡航：基速上叠正弦摆身（identity 定相，各端确定性）
            Projectile.velocity = Projectile.velocity.RotatedBy(
                MathF.Sin(Projectile.timeLeft * 0.16f + Projectile.identity * 1.3f) * 0.045f);
            //原版黄蜂贴图朝右：面左时翻面并补 π
            Projectile.spriteDirection = Projectile.direction = Projectile.velocity.X >= 0f ? 1 : -1;
            Projectile.rotation = Projectile.velocity.ToRotation() + (Projectile.spriteDirection == -1 ? MathHelper.Pi : 0f);
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            //owner 端沿途落蜂：每 20t 一只，向蜂后两侧交替甩出
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft % 20 == 0) {
                int waspDamage = Math.Max(1, (int)(Projectile.damage * 0.5f));
                float side = Projectile.timeLeft % 40 == 0 ? 1f : -1f;
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                    .RotatedBy(side * 0.8f) * 5.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    GsWaspGun.WaspType, waspDamage, Projectile.knockBack * 0.4f, Projectile.owner);
            }

            if (!VaultUtils.isServer && Projectile.timeLeft % 10 == 0) {
                SoundEngine.PlaySound(SoundID.Item32 with { Volume = 0.16f, Pitch = -0.3f, MaxInstances = 2 }, Projectile.Center);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Poisoned, 300);
            //蜂后蜇刺即挂猎标（owner 命中路径）
            if (Projectile.IsOwnedByLocalPlayer()) {
                target.GetGlobalNPC<GsWaspGunNPC>().MarkUntil = Main.GameUpdateCount + 240;
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.NPCHit32 with { Volume = 0.5f, Pitch = 0.3f, MaxInstances = 3 }, target.Center);
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath32 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
        }
    }
}
