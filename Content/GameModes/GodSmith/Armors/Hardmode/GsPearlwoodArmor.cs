using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Armors.Hardmode
{
    /// <summary>
    /// 【珍珠木套·珠光庇佑】圣光浸染的珍珠木甲：①命中积攒珠光，每满六层凝成一枚环绕珍珠（至多三枚）
    /// ②受击时一枚珍珠代主碎裂，迸出五枚追踪珠屑扑咬伤你之敌 ③珍珠虹彩随光流转。
    /// 原版套装奖励保留，神赋叠加
    /// </summary>
    internal class GsPearlwoodArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.PearlwoodHelmet];

        public override int BodyID => ItemID.PearlwoodBreastplate;

        public override int LegsID => ItemID.PearlwoodGreaves;

        protected override string EndowLineFallback =>
            "Pearl Aegis: strikes build luster; every 6 stacks condenses an orbiting pearl (up to 3) that shatters in your defense, loosing five homing pearl shards";

        //珍珠虹彩色板
        internal static readonly Color PearlBright = new(255, 250, 240);
        internal static readonly Color PearlPink = new(250, 214, 222);
        internal static readonly Color PearlGreen = new(198, 240, 222);
        internal static readonly Color PearlDeep = new(172, 134, 146);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => PearlPink;

        protected override Color ThemeBright => PearlBright;

        /// <summary>环绕珍珠上限</summary>
        private const int MaxPearls = 3;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsPearlwoodWardPearlProj>()
            || proj.type == ModContent.ProjectileType<GsPearlwoodWardShardProj>();

        private static int CountPearls(Player player) {
            int count = 0;
            int type = ModContent.ProjectileType<GsPearlwoodWardPearlProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type && proj.ai[0] == 0f) {
                    count++;
                }
            }
            return count;
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int pearls = CountPearls(player);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item29 with { Volume = 0.55f, Pitch = 0.55f }, player.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Sparkle>(player.Center + Main.rand.NextVector2Circular(20f, 24f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.5f, 1.2f),
                        i % 2 == 0 ? PearlBright : PearlPink, Main.rand.NextFloat(0.4f, 0.6f))
                        ?.Configure(PearlPink, Main.rand.Next(14, 22), 0.08f, 0.7f);
                }
            }
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (pearls >= MaxPearls) {
                //满珠时重淬既有珍珠（续时）
                int type = ModContent.ProjectileType<GsPearlwoodWardPearlProj>();
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner == player.whoAmI && proj.type == type) {
                        proj.timeLeft = Math.Max(proj.timeLeft, 60);
                    }
                }
                return;
            }
            int pearlDamage = Math.Clamp((int)(damageDone * 0.25f), 5, 90);
            Projectile.NewProjectile(player.GetSource_Misc("GodSmithPearlwoodEndow"),
                player.Center - new Vector2(0f, 30f), Vector2.Zero,
                ModContent.ProjectileType<GsPearlwoodWardPearlProj>(),
                pearlDamage, 0f, player.whoAmI, 0f, 0f, pearls);
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //有珠先碎珠代主反击，无珠才崩层
            int type = ModContent.ProjectileType<GsPearlwoodWardPearlProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type || proj.ai[0] != 0f) {
                    continue;
                }
                //命珍珠转入碎裂态（受击在佩戴者端结算，即弹幕 owner 端）
                proj.ai[0] = 1f;
                proj.netUpdate = true;
                return;
            }
            if (state.EndowCharge > 0) {
                state.EndowCharge = Math.Max(0, state.EndowCharge - 1);
            }
        }
    }

    /// <summary>
    /// 环绕珍珠：凝珠光而成的一枚虹彩珍珠，绕佩戴者缓旋呼吸；
    /// 佩戴者受击时代主碎裂，迸出五枚追踪珠屑。珠体三层虹彩随光流转，粉绿交替
    /// </summary>
    internal class GsPearlwoodWardPearlProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        /// <summary>0=环绕 1=碎裂</summary>
        private ref float State => ref Projectile.ai[0];

        private ref float Slot => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.7541f % 3.67f;

        private float VisualFade => MathHelper.Clamp(Life / 12f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>珍珠本体不伤人，珠屑才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (State == 1f) {
                //碎裂态：迸出珠屑后自灭（佩戴者端裁定生成）
                if (Projectile.owner == Main.myPlayer) {
                    for (int i = 0; i < 5; i++) {
                        float ang = MathHelper.TwoPi * i / 5f + Seed;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            Projectile.Center, ang.ToRotationVector2() * Main.rand.NextFloat(5f, 7f),
                            ModContent.ProjectileType<GsPearlwoodWardShardProj>(),
                            Projectile.damage, 1f, Projectile.owner);
                    }
                }
                Projectile.Kill();
                return;
            }

            //方案切走即失去珠光维系
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsPearlwoodArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }
            Projectile.timeLeft = 60;

            //绕主缓旋 + 呼吸浮沉
            float ang2 = Life * 0.024f + Slot * MathHelper.TwoPi / 3f + Seed;
            Vector2 offset = ang2.ToRotationVector2() * new Vector2(52f, 34f);
            offset.Y += MathF.Sin(Life * 0.06f + Slot * 1.7f) * 6f;
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center + offset, 0.2f);
            Projectile.velocity = Vector2.Zero;
            Lighting.AddLight(Projectile.Center, GsPearlwoodArmor.PearlPink.ToVector3() * 0.12f);

            if (!Main.dedServ && Main.rand.NextBool(30)) {
                PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, new Vector2(0f, -0.3f),
                    GsPearlwoodArmor.PearlBright, 0.35f)
                    ?.Configure(GsPearlwoodArmor.PearlPink, 16, 0.05f, 0.6f);
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 3 }, Projectile.Center);
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4.5f),
                    Main.rand.NextBool() ? GsPearlwoodArmor.PearlBright : GsPearlwoodArmor.PearlPink,
                    Main.rand.NextFloat(0.28f, 0.45f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        //==================== 绘制：三层虹彩珍珠，粉绿流转 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = core.Size() * 0.5f;
            //虹彩流转：粉与绿按 identity 相位互渗
            float sheen = MathF.Sin(Life * 0.05f + Seed * 4f) * 0.5f + 0.5f;
            Color iris = Color.Lerp(GsPearlwoodArmor.PearlPink, GsPearlwoodArmor.PearlGreen, sheen);
            float breathe = 1f + MathF.Sin(Life * 0.08f + Seed) * 0.06f;

            //珠底暗晕
            Main.EntitySpriteDraw(core, pos, null,
                GsPearlwoodArmor.PearlDeep * (0.6f * fade), 0f, origin,
                new Vector2(0.17f, 0.18f) * breathe, SpriteEffects.None, 0);
            //虹彩珠身
            Main.EntitySpriteDraw(core, pos, null,
                (iris with { A = 0 }) * (0.9f * fade), 0f, origin,
                new Vector2(0.13f, 0.14f) * breathe, SpriteEffects.None, 0);
            //湿亮高光（偏心）
            Main.EntitySpriteDraw(core, pos - new Vector2(2.5f, 3f) * breathe, null,
                (GsPearlwoodArmor.PearlBright with { A = 0 }) * (0.8f * fade), 0f, origin,
                new Vector2(0.05f, 0.055f), SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 珠屑：碎珠迸出的虹彩细屑，短暂散开后咬向最近敌人；命中碎作珠光
    /// </summary>
    internal class GsPearlwoodWardShardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "Extra_98";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.6899f % 3.23f;

        private const int ScatterFrames = 8;

        private float VisualFade => MathHelper.Clamp(Life / 4f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 70;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            if (Life > ScatterFrames) {
                NPC target = FindTarget();
                if (target != null) {
                    Vector2 want = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 12f;
                    float turn = MathHelper.Clamp((Life - ScatterFrames) / 20f, 0.06f, 0.18f);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, want, turn);
                }
                else {
                    Projectile.velocity *= 0.96f;
                }
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, GsPearlwoodArmor.PearlPink.ToVector3() * (0.15f * VisualFade));
        }

        private NPC FindTarget() {
            NPC best = null;
            float bestDist = 460f;
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
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsPearlwoodArmor.PearlBright, 0.1f)?.Configure(7, 0.7f);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3f),
                    GsPearlwoodArmor.PearlPink, Main.rand.NextFloat(0.2f, 0.35f))
                    ?.Configure(true, Main.rand.Next(10, 16));
            }
        }

        //==================== 绘制：虹彩细屑 + 速度拉伸 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D core = CWRAsset.Extra_98?.Value;
            if (core == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = core.Size() * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.03f, 0.05f, 0.4f);
            float sheen = MathF.Sin(Life * 0.2f + Seed * 5f) * 0.5f + 0.5f;
            Color iris = Color.Lerp(GsPearlwoodArmor.PearlPink, GsPearlwoodArmor.PearlGreen, sheen);

            Main.EntitySpriteDraw(core, pos, null,
                GsPearlwoodArmor.PearlDeep * (0.5f * fade), Projectile.rotation, origin,
                new Vector2(0.09f + stretch * 0.5f, 0.075f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                (iris with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.07f + stretch * 0.4f, 0.055f), SpriteEffects.None, 0);
            Main.EntitySpriteDraw(core, pos, null,
                (GsPearlwoodArmor.PearlBright with { A = 0 }) * (0.75f * fade), Projectile.rotation, origin,
                new Vector2(0.045f + stretch * 0.25f, 0.03f), SpriteEffects.None, 0);
            return false;
        }
    }
}
