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
    /// 【秘银套·砧上灵剑】秘银砧上自锻之甲：①命中积攒淬锋，每满六层在肩侧锻出一柄悬浮灵剑（至多三柄）
    /// ②三剑满编后再攒满，下一击号令三剑错拍齐刺目标 ③灵剑刺出即耗尽，锻打循环重新开始。
    /// 原版套装奖励保留，神赋叠加
    /// </summary>
    internal class GsMythrilArmor : GsArmorsBChargeScheme
    {
        public override int[] HeadIDs => [ItemID.MythrilHood, ItemID.MythrilHelmet, ItemID.MythrilHat];

        public override int BodyID => ItemID.MythrilChainmail;

        public override int LegsID => ItemID.MythrilGreaves;

        protected override string EndowLineFallback =>
            "Anvilborn Blades: strikes build temper; every 6 stacks forges a hovering mythril blade (up to 3), and with all three forged the next full charge commands a staggered triple thrust";

        //秘银青绿色板
        internal static readonly Color MythrilBright = new(196, 255, 232);
        internal static readonly Color MythrilMain = new(84, 214, 170);
        internal static readonly Color MythrilDeep = new(26, 108, 88);

        protected override int FullCharge => 6;

        protected override Color ThemeMain => MythrilMain;

        protected override Color ThemeBright => MythrilBright;

        /// <summary>灵剑满编数</summary>
        private const int MaxBlades = 3;

        protected override bool IsOwnProc(Projectile proj)
            => proj.type == ModContent.ProjectileType<GsMythrilForgeBladeProj>();

        /// <summary>数出佩戴者名下仍在轨道上的灵剑</summary>
        private static int CountOrbitBlades(Player player, out int total) {
            int orbit = 0;
            total = 0;
            int type = ModContent.ProjectileType<GsMythrilForgeBladeProj>();
            foreach (Projectile proj in Main.ActiveProjectiles) {
                if (proj.owner != player.whoAmI || proj.type != type) {
                    continue;
                }
                total++;
                if (proj.ai[0] == 0f) {
                    orbit++;
                }
            }
            return orbit;
        }

        protected override void ReleaseEndow(Player player, GodSmithArmorPlayer state, NPC target,
            in NPC.HitInfo hit, int damageDone) {
            int orbit = CountOrbitBlades(player, out int total);

            if (total < MaxBlades) {
                //锻新剑：砧上一记清脆锻响
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.8f, Pitch = 0.4f }, player.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(player.Center - new Vector2(0f, 30f),
                            Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.5f, 4f),
                            Main.rand.NextBool() ? MythrilBright : MythrilMain,
                            Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 20));
                    }
                }
                if (player.whoAmI == Main.myPlayer) {
                    int bladeDamage = Math.Clamp((int)(damageDone * 0.45f), 12, 180);
                    Projectile.NewProjectile(player.GetSource_Misc("GodSmithMythrilEndow"),
                        player.Center - new Vector2(0f, 36f), Vector2.Zero,
                        ModContent.ProjectileType<GsMythrilForgeBladeProj>(),
                        bladeDamage, 2f, player.whoAmI, 0f, -1f, total);
                }
                return;
            }

            if (orbit <= 0) {
                return;
            }
            //满编齐刺：三剑错拍点名目标
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.6f, Pitch = -0.15f }, player.Center);
            }
            if (player.whoAmI == Main.myPlayer) {
                int type = ModContent.ProjectileType<GsMythrilForgeBladeProj>();
                int stagger = 0;
                foreach (Projectile proj in Main.ActiveProjectiles) {
                    if (proj.owner != player.whoAmI || proj.type != type || proj.ai[0] != 0f) {
                        continue;
                    }
                    proj.ai[0] = 1f;
                    proj.ai[1] = target.whoAmI;
                    //受令后 ai[2] 从槽位改义为错拍延迟，随生成包/netUpdate 过线
                    proj.ai[2] = stagger * 5f;
                    stagger++;
                    proj.netUpdate = true;
                }
            }
        }
    }

    /// <summary>
    /// 秘银灵剑：砧上锻出的悬浮之剑，轨道态绕肩缓旋、剑身流转秘银冷辉；
    /// 受令后压剑蓄势数帧，随即错拍突刺点名目标，刺出即碎为淬锋星屑
    /// </summary>
    internal class GsMythrilForgeBladeProj : ModProjectile
    {
        public override string Texture => CWRConstant.Masking + "LightShot";

        /// <summary>0=轨道悬浮 1=受令突刺</summary>
        private ref float State => ref Projectile.ai[0];

        private ref float TargetIndex => ref Projectile.ai[1];

        /// <summary>轨道槽位（0~2，仅 State==0 时有意义）</summary>
        private ref float Slot => ref Projectile.ai[2];

        /// <summary>错拍延迟帧（受令后 ai[2] 改义，仅 State==1 时有意义，跨端同步）</summary>
        private ref float Stagger => ref Projectile.ai[2];

        private ref float Life => ref Projectile.localAI[1];

        /// <summary>突刺已进行帧数</summary>
        private ref float DashTime => ref Projectile.localAI[2];

        private float Seed => Projectile.identity * 0.5417f % 2.97f;

        private float VisualFade => MathHelper.Clamp(Life / 10f, 0f, 1f);

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Type] = 5;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 90;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
        }

        /// <summary>轨道悬浮态不判定，出鞘才伤人</summary>
        public override bool? CanDamage() => State == 1f && Stagger <= 0f;

        public override void AI() {
            Life++;
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            if (State == 0f) {
                //方案切走则灵剑失锻，自然熄灭（击杀只在佩戴者端裁定）
                if (owner.GetModPlayer<GodSmithArmorPlayer>().ActiveScheme is not GsMythrilArmor) {
                    if (Projectile.owner == Main.myPlayer) {
                        Projectile.Kill();
                    }
                    return;
                }
                Projectile.timeLeft = 60;
                //绕肩缓旋 + 呼吸浮沉，剑尖朝外
                float ang = Life * 0.028f + Slot * MathHelper.TwoPi / 3f + Seed;
                Vector2 anchor = owner.Center + new Vector2(0f, -6f);
                Vector2 offset = ang.ToRotationVector2() * new Vector2(46f, 30f);
                offset.Y += MathF.Sin(Life * 0.07f + Slot * 2.1f) * 5f;
                Projectile.Center = Vector2.Lerp(Projectile.Center, anchor + offset, 0.25f);
                Projectile.rotation = ang;
                Projectile.velocity = Vector2.Zero;
                Lighting.AddLight(Projectile.Center, GsMythrilArmor.MythrilMain.ToVector3() * 0.16f);
                return;
            }

            //受令：错拍延迟内压剑蓄势
            if (Stagger > 0f) {
                Stagger--;
                NPC aim = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                if (aim != null && aim.active) {
                    Projectile.rotation = (aim.Center - Projectile.Center).ToRotation();
                    //蓄势后坐：向反方向微退
                    Projectile.Center -= Projectile.rotation.ToRotationVector2() * 1.6f;
                }
                return;
            }

            //突刺：首帧佩戴者端定向并同步，全程微加速
            DashTime++;
            if (DashTime == 1f) {
                if (Projectile.owner == Main.myPlayer) {
                    NPC aim = TargetIndex >= 0 && TargetIndex < Main.maxNPCs ? Main.npc[(int)TargetIndex] : null;
                    Vector2 aimPos = aim != null && aim.active ? aim.Center : Projectile.Center + Projectile.rotation.ToRotationVector2() * 300f;
                    Projectile.velocity = (aimPos - Projectile.Center).SafeNormalize(Vector2.UnitX) * 17f;
                    Projectile.netUpdate = true;
                }
                Projectile.timeLeft = 40;
                if (!Main.dedServ) {
                    SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.5f, Pitch = 0.7f, MaxInstances = 3 }, Projectile.Center);
                }
            }
            Projectile.velocity *= 1.045f;
            if (Projectile.velocity.Length() > 30f) {
                Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.UnitX) * 30f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (!Main.dedServ && Life % 2 == 0) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center - Projectile.velocity * 0.4f,
                    Projectile.velocity * 0.06f, GsMythrilArmor.MythrilMain,
                    Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(6, 11));
            }
            Lighting.AddLight(Projectile.Center, GsMythrilArmor.MythrilBright.ToVector3() * 0.3f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.dedServ) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item37 with { Volume = 0.45f, Pitch = 0.6f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 5; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(2f, 5f),
                    Main.rand.NextBool() ? GsMythrilArmor.MythrilBright : GsMythrilArmor.MythrilMain,
                    Main.rand.NextFloat(0.3f, 0.5f))?.Configure(true, Main.rand.Next(12, 22));
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            //剑碎为淬锋星屑
            PRTLoader.NewParticle<PRT_Light>(Projectile.Center, Vector2.Zero,
                GsMythrilArmor.MythrilBright, 0.13f)?.Configure(8, 0.7f);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(Projectile.Center,
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1f, 3.5f),
                    GsMythrilArmor.MythrilMain, Main.rand.NextFloat(0.24f, 0.4f))
                    ?.Configure(true, Main.rand.Next(10, 18));
            }
        }

        //==================== 绘制：细刃长剑 + 柄部星辉 + 突刺残影 ====================

        private void DrawBlade(Vector2 pos, float rotation, float alpha, float lengthScale) {
            Texture2D shot = CWRAsset.LightShot?.Value;
            Texture2D star = CWRAsset.StarGlow01?.Value;
            if (shot == null || star == null) {
                return;
            }
            Vector2 origin = shot.Size() * 0.5f;
            //剑脊冷辉
            Main.EntitySpriteDraw(shot, pos, null,
                (GsMythrilArmor.MythrilDeep with { A = 0 }) * (0.9f * alpha), rotation, origin,
                new Vector2(0.42f * lengthScale, 0.13f), SpriteEffects.None, 0);
            //剑身主刃
            Main.EntitySpriteDraw(shot, pos, null,
                (GsMythrilArmor.MythrilMain with { A = 0 }) * alpha, rotation, origin,
                new Vector2(0.34f * lengthScale, 0.085f), SpriteEffects.None, 0);
            //亮刃线
            Main.EntitySpriteDraw(shot, pos, null,
                (GsMythrilArmor.MythrilBright with { A = 0 }) * (0.85f * alpha), rotation, origin,
                new Vector2(0.26f * lengthScale, 0.04f), SpriteEffects.None, 0);
            //柄部星辉（剑尾端）
            Vector2 hilt = pos - rotation.ToRotationVector2() * 26f * lengthScale;
            Main.EntitySpriteDraw(star, hilt, null,
                (GsMythrilArmor.MythrilBright with { A = 0 }) * (0.8f * alpha), 0f, star.Size() * 0.5f,
                0.28f, SpriteEffects.None, 0);
        }

        public override bool PreDraw(ref Color lightColor) {
            float fade = VisualFade;
            //悬浮态呼吸微光；突刺态速度拉伸
            float lengthScale = State == 1f && Stagger <= 0f
                ? 1f + MathHelper.Clamp(Projectile.velocity.Length() * 0.02f, 0f, 0.5f)
                : 1f + MathF.Sin(Life * 0.09f + Seed * 3f) * 0.05f;

            //突刺残影
            if (State == 1f && Stagger <= 0f) {
                for (int i = Projectile.oldPos.Length - 1; i >= 1; i--) {
                    if (Projectile.oldPos[i] == Vector2.Zero) {
                        continue;
                    }
                    float ghost = (1f - i / (float)Projectile.oldPos.Length) * 0.3f * fade;
                    DrawBlade(Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition,
                        Projectile.rotation, ghost, lengthScale * (1f - i * 0.05f));
                }
            }
            DrawBlade(Projectile.Center - Main.screenPosition, Projectile.rotation, fade, lengthScale);
            return false;
        }
    }
}
