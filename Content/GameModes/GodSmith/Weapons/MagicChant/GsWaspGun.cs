using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 黄蜂枪重铸：信息素猎标。材质身份：狩猎蜂群（琥珀毒针的活体箭雨）。
    /// 与 GsBeeGun（V 字编队/蜂后凝阵）签名互异：本杖不编队，走「标记猎杀」。<br/>
    /// ①「信息素」：正拍黄蜂命中挂猎标（攻击方本地层，可见：目标周身琥珀雾）；
    /// 在场己方黄蜂优先转向被标记目标（owner 端转向 + 节流同步）；<br/>
    /// ②满层强化「蜂后凝聚」：放出蜂后巨弹，蜿蜒巡航沿途每 20t 放出一只黄蜂；
    /// ③施法有枪口上跳与起手蜂鸣（音高随共鸣层）
    /// </summary>
    internal class GsWaspGun : GsChantScheme
    {
        public override int TargetItemID => ItemID.WaspGun;

        protected override string GsDescFallback =>
            "Reforged: on-beat wasps sting a pheromone mark into their prey; every wasp in the air hunts the marked target first" +
            "\nAt full resonance the next cast releases a queen wasp that cruises ahead, seeding hunters along her path";

        protected override float BaseDamageMult => 1.06f;

        protected override Color ChantColor => AmberMain;

        internal static readonly Color AmberBright = new(255, 222, 132);
        internal static readonly Color AmberMain = new(232, 164, 42);
        internal static readonly Color AmberDeep = new(122, 74, 16);

        /// <summary>原版黄蜂弹类型</summary>
        internal static int WaspType => ContentSamples.ItemsByType[ItemID.WaspGun].shoot;

        //==================== 动画法：枪口上跳 + 起手蜂鸣 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //枪口上跳：出手瞬间上踢 3px 并抬口，随动画进度回落（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation += new Vector2(-player.direction * 1.5f, -3f) * progress;
            player.itemRotation -= player.direction * 0.1f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手蜂鸣：音高随共鸣层（共鸣是 owner 本地量，远端听基准音即可）
            int resonance = player.whoAmI == Main.myPlayer ? Chant(player).Resonance : 0;
            SoundEngine.PlaySound(SoundID.Item32 with {
                Volume = 0.4f, Pitch = 0.05f * Math.Min(resonance, 6), MaxInstances = 3
            }, player.Center);
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 18f, -4f);
            PRTLoader.NewParticle<PRT_Spark>(tip, new Vector2(player.direction * 1.2f, -0.4f),
                AmberMain, Main.rand.NextFloat(0.2f, 0.3f))?.Configure(false, Main.rand.Next(8, 12));
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

        //==================== 飞行相：琥珀细影 + 猎标转向 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != WaspType) {
                return;
            }
            if (!VaultUtils.isServer) {
                Lighting.AddLight(proj.Center, AmberMain.ToVector3() * 0.12f);
                //蜂翅琥珀细影：正拍蜂更亮
                if (proj.timeLeft % (IsOnBeatProj(router) ? 5 : 9) == 0 && Main.rand.NextBool(2)) {
                    PRTLoader.NewParticle<PRT_Light>(proj.Center - proj.velocity * 0.4f,
                        -proj.velocity * 0.06f, AmberBright, Main.rand.NextFloat(0.04f, 0.07f))
                        ?.Configure(Main.rand.Next(8, 14), 0.6f);
                }
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
            if (!VaultUtils.isServer) {
                //命中反馈：琥珀毒屑
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(6f, 6f),
                        Main.rand.NextVector2Circular(1.8f, 1.8f), i % 2 == 0 ? AmberMain : AmberBright,
                        Main.rand.NextFloat(0.2f, 0.34f))?.Configure(true, Main.rand.Next(8, 14));
                }
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

        public override void DrawEffects(NPC npc, ref Color drawColor) {
            //猎标体表可见：琥珀雾缕缕（标记只在攻击方端存在，个人读数合法）
            if (Main.GameUpdateCount >= MarkUntil || Main.dedServ) {
                return;
            }
            if (Main.rand.NextBool(5)) {
                PRTLoader.NewParticle<PRT_Light>(
                    npc.Center + Main.rand.NextVector2Circular(npc.width * 0.45f, npc.height * 0.45f),
                    -Vector2.UnitY * Main.rand.NextFloat(0.3f, 0.7f),
                    GsWaspGun.AmberMain, Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(12, 20), 0.65f);
            }
        }
    }

    /// <summary>
    /// 蜂后巨弹：强化咏唱放出的琥珀巡航母体。蜿蜒推进（identity 定相正弦），
    /// owner 端沿途每 20t 放出一只原版黄蜂（0.5 倍）；
    /// 自绘琥珀双层辉体 + 定相拍动的翅影
    /// </summary>
    internal class GsWaspGunQueenProj : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        public override string LocalizationCategory => "GodSmithMagicChant";

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
            Projectile.rotation = Projectile.velocity.ToRotation();

            //owner 端沿途落蜂：每 20t 一只，向蜂后两侧交替甩出
            if (Projectile.IsOwnedByLocalPlayer() && Projectile.timeLeft % 20 == 0) {
                int waspDamage = Math.Max(1, (int)(Projectile.damage * 0.5f));
                float side = Projectile.timeLeft % 40 == 0 ? 1f : -1f;
                Vector2 vel = Projectile.velocity.SafeNormalize(Vector2.UnitX)
                    .RotatedBy(side * 0.8f) * 5.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, vel,
                    GsWaspGun.WaspType, waspDamage, Projectile.knockBack * 0.4f, Projectile.owner);
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(Projectile.Center, GsWaspGun.AmberMain.ToVector3() * 0.4f);
            if (Projectile.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.6f,
                    -Projectile.velocity * 0.1f + Main.rand.NextVector2Circular(0.4f, 0.4f),
                    GsWaspGun.AmberMain, Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
            }
            if (Projectile.timeLeft % 10 == 0) {
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
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(2.4f, 2.4f), GsWaspGun.AmberBright,
                        Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(12, 18));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.NPCDeath32 with { Volume = 0.5f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 8; i++) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Circular(1.5f, 1.5f), GsWaspGun.AmberMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(14, 24), 0.6f);
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            //琥珀双层辉体 + 定相拍动的翅影（A=0 加色，identity 定相，绘制零随机）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            float flap = MathF.Sin(Main.GlobalTimeWrappedHourly * 26f + Projectile.identity * 0.7f);
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.identity * 0.91f);

            //体辉双层
            Main.EntitySpriteDraw(glow, pos, null, GsWaspGun.AmberDeep with { A = 0 } * (0.6f * pulse), 0f,
                glow.Size() / 2f, 0.5f * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null, GsWaspGun.AmberBright with { A = 0 } * (0.55f * pulse), 0f,
                glow.Size() / 2f, 0.26f, SpriteEffects.None, 0);
            //翅影：体侧两片随拍动张合的斜辉
            Vector2 up = (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2();
            for (int s = -1; s <= 1; s += 2) {
                Vector2 wing = pos + up * s * (8f + 5f * MathF.Abs(flap));
                Main.EntitySpriteDraw(star, wing, null, GsWaspGun.AmberBright with { A = 0 } * (0.4f * MathF.Abs(flap) + 0.15f),
                    Projectile.rotation + s * (0.5f + 0.3f * flap), star.Size() / 2f,
                    new Vector2(0.22f, 0.09f), SpriteEffects.None, 0);
            }
            //头芒
            Main.EntitySpriteDraw(star, pos + Projectile.velocity.SafeNormalize(Vector2.UnitX) * 12f, null,
                Color.White with { A = 0 } * 0.5f, Projectile.rotation, star.Size() / 2f, 0.12f, SpriteEffects.None, 0);
            return false;
        }
    }
}
