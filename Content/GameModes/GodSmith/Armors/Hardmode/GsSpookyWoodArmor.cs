using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【阴森木套·万圣巡灯】阴森木雕成的引魂甲：①命中（含仆从）积攒魂火，满六层点起一盏南瓜巡灯（至多两盏）
    /// ②巡灯绕主巡游八秒，锁定最近敌喷出三连鬼火。
    /// 原版套装奖励（+1 仆从栏等）保留，神赋叠加
    /// </summary>
    internal class GsSpookyWoodArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.SpookyHelmet];

        public override int BodyID => ItemID.SpookyBreastplate;

        public override int LegsID => ItemID.SpookyLeggings;

        protected override string EndowLineFallback =>
            "Hallow's Patrol: strikes build soulfire; at 6 stacks a jack-o'-lantern rises (up to 2) to patrol around you and spit triple ghostflame at the nearest foe";

        //阴森橙 + 鬼绿色板
        internal static readonly Color SpookyOrange = new(255, 152, 64);
        internal static readonly Color SpookyGreen = new(150, 255, 132);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => SpookyOrange;

        protected override Color ThemeBright => SpookyGreen;

        /// <summary>巡灯上限</summary>
        private const int MaxLanterns = 2;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsSpookyLanternProj>()
            || proj.type == ModContent.ProjectileType<GsSpookyLanternWispProj>();

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int lanterns = 0;
            int type = ModContent.ProjectileType<GsSpookyLanternProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type) {
                    lanterns++;
                }
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.6f, Pitch = -0.55f }, player.Center);
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (lanterns >= MaxLanterns) {
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type) {
                        proj.timeLeft = Math.Max(proj.timeLeft, 480);
                    }
                }
                return;
            }
            int wispDamage = Math.Clamp((int)(damageDone * 0.30f), 8, 120);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithSpookyEndow"),
                player.Center - new Vector2(0f, 50f), Vector2.Zero,
                ModContent.ProjectileType<GsSpookyLanternProj>(),
                wispDamage, 0f, player.whoAmI, 0f, 0f, lanterns);
        }
    }

    /// <summary>
    /// 南瓜巡灯：一盏浮空的杰克南瓜灯，绕佩戴者宽轨巡游，
    /// 锁定最近敌后每 70 帧喷出三连鬼火；借原版南瓜灯发射器弹幕贴图默认绘制
    /// </summary>
    internal class GsSpookyLanternProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlamingJack;

        private ref float Life => ref Projectile.ai[0];

        /// <summary>巡游槽位（0/1 反相）</summary>
        private ref float Slot => ref Projectile.ai[2];

        /// <summary>连喷记数（>0 时每 6 帧喷一发）</summary>
        private ref float VolleyLeft => ref Projectile.localAI[0];

        private ref float VolleyTargetIndex => ref Projectile.localAI[1];

        private float Seed => Projectile.identity * 0.7717f % 3.59f;

        /// <summary>喷火周期</summary>
        private const int SpitInterval = 70;

        public override void SetStaticDefaults() {
            Main.projFrames[Type] = Main.projFrames[ProjectileID.FlamingJack];
        }

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 480;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>灯体不撞人，鬼火才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsSpookyWoodArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }

            //宽轨巡游：慢椭圆 + 浮沉
            float ang = Life * 0.02f + Slot * MathHelper.Pi + Seed;
            Vector2 orbit = new(MathF.Cos(ang) * 92f, MathF.Sin(ang) * 54f - 40f + MathF.Sin(Life * 0.045f + Seed * 3f) * 7f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center + orbit, 0.07f);
            Projectile.velocity = Vector2.Zero;

            //锁定与三连喷（佩戴者端裁定起喷，逐发在后续帧吐出）
            if (Projectile.owner == Main.myPlayer) {
                if (Life % SpitInterval == 0) {
                    NPC target = FindTarget();
                    if (target != null) {
                        VolleyLeft = 3f;
                        VolleyTargetIndex = target.whoAmI;
                    }
                }
                if (VolleyLeft > 0f && Life % 6 == 0) {
                    VolleyLeft--;
                    NPC target = VolleyTargetIndex >= 0 && VolleyTargetIndex < Main.maxNPCs
                        ? Main.npc[(int)VolleyTargetIndex] : null;
                    if (target != null && target.active) {
                        Vector2 vel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 10.5f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center, vel.RotatedBy(Main.rand.NextFloat(-0.08f, 0.08f)),
                            ModContent.ProjectileType<GsSpookyLanternWispProj>(),
                            Projectile.damage, 1f, Projectile.owner);
                        if (!Main.dedServ) {
                            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 4 }, Projectile.Center);
                        }
                    }
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
            float bestDist = 600f;
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
            //熄灯音
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.45f, Pitch = -0.2f, MaxInstances = 3 }, Projectile.Center);
        }
    }

    /// <summary>
    /// 鬼火：巡灯喷出的一口鬼绿焰，蛇形游进；借原版诅咒焰贴图默认绘制
    /// </summary>
    internal class GsSpookyLanternWispProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CursedFlameFriendly;

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.8629f % 3.97f;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //蛇形游进：垂直于速度的正弦摆
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.velocity += dir.RotatedBy(MathHelper.PiOver2) * MathF.Sin(Life * 0.4f + Seed * 4f) * 0.5f;
            Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * MathHelper.Clamp(Projectile.velocity.Length(), 9f, 12f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.25f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);
        }
    }
}
