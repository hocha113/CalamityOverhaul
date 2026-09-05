using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【禁戒套·沙魂仆影】禁忌风沙缚成的术甲：①命中积攒沙魂，满六层自风沙中唤出一具沙灵仆影（至多两具）
    /// ②仆影随行八秒，锁定最近敌人周期吐出沙旋弹。
    /// 原版套装奖励（禁忌风暴）保留，神赋叠加
    /// </summary>
    internal class GsForbiddenArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.AncientBattleArmorHat];

        public override int BodyID => ItemID.AncientBattleArmorShirt;

        public override int LegsID => ItemID.AncientBattleArmorPants;

        protected override string EndowLineFallback =>
            "Sandbound Shades: strikes build sand-soul; at 6 stacks a sand shade rises (up to 2) to haunt your side and spit whirling sand bolts";

        //禁戒沙金色板
        internal static readonly Color SandBright = new(255, 228, 156);
        internal static readonly Color SandMain = new(224, 180, 98);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => SandMain;

        protected override Color ThemeBright => SandBright;

        /// <summary>仆影上限</summary>
        private const int MaxShades = 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsForbiddenWraithProj>()
            || proj.type == ModContent.ProjectileType<GsForbiddenWraithBoltProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int shades = 0;
            int type = ModContent.ProjectileType<GsForbiddenWraithProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    shades++;
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f, Pitch = -0.4f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (shades >= MaxShades) {
                //已满编：续住既有仆影
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type) {
                        proj.timeLeft = Math.Max(proj.timeLeft, 480);
                    }
                }
                return;
            }
            int boltDamage = Math.Clamp((int)(damageDone * 0.35f), 8, 130);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithForbiddenEndow"),
                player.Center - new Vector2(0f, 40f), Vector2.Zero,
                ModContent.ProjectileType<GsForbiddenWraithProj>(),
                boltDamage, 0f, player.whoAmI, 0f, 0f, shades);
        }
    }

    /// <summary>
    /// 沙灵仆影：一具由风沙与咒能缚成的浮空灵体，随行佩戴者侧翼，
    /// 锁定最近敌人后周期吐出沙旋弹；借原版迷失之魂贴图默认绘制
    /// </summary>
    internal class GsForbiddenWraithProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.LostSoulFriendly;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>侧翼槽位（0=左 1=右）</summary>
        private ref float Slot => ref Projectile.ai[2];

        private float Seed => Projectile.identity * 0.7177f % 3.43f;

        /// <summary>吐弹周期</summary>
        private const int SpitInterval = 50;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.LostSoulFriendly];
        }

        public override void SetDefaults() {
            Projectile.width = 30;
            Projectile.height = 44;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>仆影本体不撞人，沙旋弹才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            //方案切走仆影散形
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsForbiddenArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //侧翼随行 + 沙浮呼吸
            float side = Slot == 0f ? -1f : 1f;
            Vector2 anchor = owner.Center + new Vector2(side * 74f, -44f + MathF.Sin(Life * 0.05f + Seed * 2f) * 8f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, anchor, 0.08f);
            Projectile.velocity = Vector2.Zero;

            //锁定最近敌并周期吐沙旋弹（佩戴者端裁定）
            NPC target = FindTarget();
            if (target != null) {
                Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? 1 : -1;
                if (Projectile.owner == Main.myPlayer && Life % SpitInterval == 0) {
                    Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                        Projectile.Center, vel,
                        ModContent.ProjectileType<GsForbiddenWraithBoltProj>(),
                        Projectile.damage, 1f, Projectile.owner);
                    Projectile.netUpdate = true;
                }
            }

            //沿用原版贴图帧数走最简帧计数，单帧贴图原地不动
            if (++Projectile.frameCounter >= 5) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 700f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Projectile.Center.Distance(npc.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    best = npc;
                }
            }
            return best;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //散形音
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.2f, MaxInstances = 3 }, Projectile.Center);
        }
    }

    /// <summary>
    /// 沙旋弹：仆影吐出的一口旋压风沙，出口猛、途中缓；借原版沙枪沙弹贴图默认绘制
    /// </summary>
    internal class GsForbiddenWraithBoltProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SandBallGun;

        private ref float Life => ref Projectile.ai[0];

        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //出口猛、途中缓（不匀速）
            if (Life > 12f) {
                Projectile.velocity *= 0.985f;
            }
            Projectile.rotation += 0.42f;
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.6f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
