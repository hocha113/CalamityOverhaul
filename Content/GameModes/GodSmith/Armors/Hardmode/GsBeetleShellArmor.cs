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
    /// 【甲虫壳甲·圣甲壁垒 ★A】防御向甲虫胸甲的独立神赋，与鳞甲分立：①受击积攒壳晶
    /// ②每满三层凝出一枚轨道壳片（至多三枚）绕身巡守 ③持盾期间再受击，即碎一枚壳片朝来袭方向
    /// 迸出扇形甲屑弹反并震开冲击环。原版套装奖励（甲虫忍耐减伤）保留，神赋叠加
    /// </summary>
    internal class GsBeetleShellArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.BeetleHelmet];

        public override int BodyID => ItemID.BeetleShell;

        public override int LegsID => ItemID.BeetleLeggings;

        protected override string EndowLineFallback =>
            "Scarab Bulwark: taking hits builds shell-crystal; every 3 stacks condenses an orbiting shell plate (up to 3), and further hits shatter a plate into a retaliating fan of chitin shrapnel";

        //壳甲玄铁 + 琥珀色板
        internal static readonly Color ShellAmber = new(255, 214, 130);
        internal static readonly Color ShellBrown = new(146, 100, 52);
        internal static readonly Color ShellDark = new(52, 38, 26);
        internal static readonly Color ShellEmerald = new(112, 220, 168);

        protected override int FullCharge => 3;

        protected override Color ThemeMain => ShellBrown;

        protected override Color ThemeBright => ShellAmber;

        protected override bool ChargeOnHit => false;

        protected override bool ChargeOnHurt => true;

        /// <summary>壳片上限</summary>
        private const int MaxPlates = 3;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsBeetleShellWardProj>()
            || proj.type == ModContent.ProjectileType<GsBeetleShellShrapnelProj>();

        private static int CountPlates(Player player) {
            int count = 0;
            int type = ModContent.ProjectileType<GsBeetleShellWardProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner == player.whoAmI && proj.type == type && proj.ai[0] == 0f) {
                    count++;
                }
            }
            return count;
        }

        /// <summary>受击积攒型不走命中释放；凝壳时机全在 OnEndowHurt 内闭环</summary>
        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) { }

        public override void UpdateEndowment(Player player, GodSmithArmorPlayer state) {
            //壳晶满且未满编：凝一枚壳片（受击端=佩戴者端）
            if (state.EndowCharge < FullCharge) {
                return;
            }
            int plates = CountPlates(player);
            if (plates >= MaxPlates) {
                return;
            }
            state.EndowCharge = 0;
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.6f, Pitch = -0.25f }, player.Center);
                for (int i = 0; i < 6; i++) {
                    PRTLoader.NewParticle<PRT_Spark>(player.Center + Main.rand.NextVector2Circular(14f, 18f),
                        -Vector2.UnitY * Main.rand.NextFloat(0.6f, 1.6f),
                        i % 2 == 0 ? ShellAmber : ShellEmerald, 0.35f)?.Configure(false, 14);
                }
            }
            if (player.whoAmI == Main.myPlayer) {
                //壳片弹反力道以防御折算：甲愈坚，反愈狠
                int shrapnelDamage = Math.Clamp(player.statDefense * 2, 30, 170);
                Projectile.NewProjectile(player.GetSource_Misc("GodSmithBeetleShellEndow"),
                    player.Center, Vector2.Zero,
                    ModContent.ProjectileType<GsBeetleShellWardProj>(),
                    shrapnelDamage, 0f, player.whoAmI, 0f, 0f, plates);
            }
        }

        public override void OnEndowHurt(Player player, GodSmithArmorPlayer state, in Player.HurtInfo info) {
            //持盾优先：碎一枚壳片弹反，之后照常积攒壳晶
            int type = ModContent.ProjectileType<GsBeetleShellWardProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type || proj.ai[0] != 0f) {
                    continue;
                }
                proj.ai[0] = 1f;
                //记录来袭方向，扇形弹反朝它张开
                proj.ai[1] = info.HitDirection;
                proj.netUpdate = true;
                break;
            }
            base.OnEndowHurt(player, state, info);
        }
    }

    /// <summary>
    /// 轨道壳片：壳晶凝成的一片弧面甲板，绕佩戴者巡守，甲面高光缓扫；
    /// 佩戴者受击时转入碎裂态：朝来袭方向迸出五片扇形甲屑并震开冲击环
    /// </summary>
    internal class GsBeetleShellWardProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        /// <summary>0=巡守 1=碎裂弹反</summary>
        private ref float State => ref Projectile.ai[0];

        /// <summary>来袭方向（±1，碎裂态）</summary>
        private ref float HitDir => ref Projectile.ai[1];

        /// <summary>轨道槽位</summary>
        private ref float Slot => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[0];

        private float Seed => Projectile.identity * 0.6779f % 3.17f;

        private float VisualFade => MathHelper.Clamp(Life / 10f, 0f, 1f);

        public override void SetDefaults() {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
        }

        /// <summary>壳片本体不伤人，甲屑才伤人</summary>
        public override bool? CanDamage() => false;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (State == 1f) {
                //碎裂弹反：扇形甲屑 + 冲击环（佩戴者端裁定）
                if (Projectile.owner == Main.myPlayer) {
                    float baseAng = HitDir >= 0f ? 0f : MathHelper.Pi;
                    for (int i = 0; i < 5; i++) {
                        float ang = baseAng + (i - 2) * 0.30f;
                        Projectile.NewProjectile(Projectile.GetSource_FromAI(),
                            owner.Center + ang.ToRotationVector2() * 16f,
                            ang.ToRotationVector2() * Main.rand.NextFloat(10f, 13f),
                            ModContent.ProjectileType<GsBeetleShellShrapnelProj>(),
                            Projectile.damage, 3f, Projectile.owner);
                    }
                }
                Projectile.Kill();
                return;
            }

            //方案切走壳片散架
            if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsBeetleShellArmor) {
                if (Projectile.owner == Main.myPlayer) {
                    Projectile.Kill();
                }
                return;
            }
            Projectile.timeLeft = 60;

            //绕身巡守：壳面始终朝外
            float orbitAng = Life * 0.03f + Slot * MathHelper.TwoPi / 3f + Seed;
            Vector2 offset = orbitAng.ToRotationVector2() * new Vector2(48f, 40f);
            Projectile.Center = Vector2.Lerp(Projectile.Center, owner.Center + offset, 0.25f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = orbitAng + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, GsBeetleShellArmor.ShellAmber.ToVector3() * 0.08f);
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.55f, Pitch = 0.1f, MaxInstances = 3 }, Projectile.Center);
            //碎壳屑 + 冲击环闪
            for (int i = 0; i < 7; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsBeetleShellArmor.ShellAmber : GsBeetleShellArmor.ShellBrown,
                    Main.rand.NextFloat(0.3f, 0.48f))?.Configure(true, Main.rand.Next(14, 24));
            }
            PRTLoader.NewParticle<PRT_Light>(Main.player[Projectile.owner].Center, Vector2.Zero,
                GsBeetleShellArmor.ShellAmber, 0.2f)?.Configure(10, 0.7f);
        }

        //==================== 绘制：弧面甲板 + 琥珀高光缓扫 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (crescent == null || star == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crescent.Size() * 0.5f;
            float breathe = 1f + MathF.Sin(Life * 0.09f + Seed * 3f) * 0.04f;

            //玄铁甲底（真 alpha 占体积）
            Main.EntitySpriteDraw(crescent, pos, null,
                GsBeetleShellArmor.ShellDark * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.155f, 0.11f) * breathe, SpriteEffects.None, 0);
            //棕壳主面
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsBeetleShellArmor.ShellBrown with { A = 0 }) * (0.9f * fade), Projectile.rotation, origin,
                new Vector2(0.13f, 0.085f) * breathe, SpriteEffects.None, 0);
            //翠沿细光
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsBeetleShellArmor.ShellEmerald with { A = 0 }) * (0.4f * fade), Projectile.rotation, origin,
                new Vector2(0.10f, 0.05f) * breathe, SpriteEffects.None, 0);
            //琥珀高光点沿弧缓扫（identity 相位）
            float sweep = MathF.Sin(Life * 0.11f + Seed * 5f);
            Vector2 gleam = pos + (Projectile.rotation - MathHelper.PiOver2 + sweep * 0.5f).ToRotationVector2() * 8f;
            Main.EntitySpriteDraw(star, gleam, null,
                (GsBeetleShellArmor.ShellAmber with { A = 0 }) * (0.7f * fade), 0f, star.Size() * 0.5f,
                0.15f, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>
    /// 甲屑：碎壳迸出的尖锐甲片，旋进带曳光，命中钉入迸屑
    /// </summary>
    internal class GsBeetleShellShrapnelProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "CrescentEdge01";

        private ref float Life => ref Projectile.ai[0];

        private float Seed => Projectile.identity * 0.9043f % 4.03f;

        private float VisualFade => Math.Min(
            MathHelper.Clamp(Life / 3f, 0f, 1f),
            MathHelper.Clamp(Projectile.timeLeft / 5f, 0f, 1f));

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 34;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
        }

        public override void AI() {
            Life++;
            //急出缓落：中段微降速带一点下坠
            if (Life > 10f) {
                Projectile.velocity *= 0.96f;
                Projectile.velocity.Y += 0.12f;
            }
            Projectile.rotation += 0.5f * (Projectile.velocity.X >= 0f ? 1f : -1f);
            if (!Main.dedServ && Life % 3 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    -Projectile.velocity * 0.05f, GsBeetleShellArmor.ShellAmber,
                    Main.rand.NextFloat(0.16f, 0.26f))?.Configure(false, Main.rand.Next(6, 10));
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 3.5f),
                    Main.rand.NextBool() ? GsBeetleShellArmor.ShellAmber : GsBeetleShellArmor.ShellBrown,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：旋进甲片 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D crescent = CWRAsset.CrescentEdge01?.Value;
            if (crescent == null) {
                return false;
            }
            float fade = VisualFade;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 origin = crescent.Size() * 0.5f;
            float wob = 1f + MathF.Sin(Life * 0.5f + Seed * 4f) * 0.08f;

            Main.EntitySpriteDraw(crescent, pos, null,
                GsBeetleShellArmor.ShellDark * (0.85f * fade), Projectile.rotation, origin,
                new Vector2(0.085f, 0.06f) * wob, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsBeetleShellArmor.ShellBrown with { A = 0 }) * fade, Projectile.rotation, origin,
                new Vector2(0.07f, 0.045f) * wob, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(crescent, pos, null,
                (GsBeetleShellArmor.ShellAmber with { A = 0 }) * (0.7f * fade), Projectile.rotation, origin,
                new Vector2(0.05f, 0.026f) * wob, SpriteEffects.None, 0);
            return false;
        }
    }
}
