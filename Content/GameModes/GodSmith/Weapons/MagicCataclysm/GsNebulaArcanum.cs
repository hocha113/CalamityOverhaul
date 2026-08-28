using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicCataclysm
{
    /// <summary>
    /// 星云奥秘重铸（P13 抬档 B→A，终局件）。材质身份：星云凝浆（漩臂里舀出来的一捧活星雾）。<br/>
    /// ①左键 rider：奥秘巨球双臂星雾对旋、辉体随飞行渐胀，300px 内非 Boss 小敌被缓缓拽向球心，
    /// 呼应大招漩臂的拖曳；分裂子星曳星屑彗尾②巨球消散处残留星雾余晖（余痕相）
    /// ③咏唱层级可见：计量每过四分之一，杖尖星雾升一档密度④施法有推杖蓄势与起手星雾
    /// ⑤满量右键「星云漩臂」照旧。拖曳是控场收益不计伤害，底伤加成保持 5%
    /// </summary>
    internal class GsNebulaArcanum : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.NebulaArcanum;

        protected override string GsDescFallback =>
            "Reforged: hits build Arcanum; at full charge, right click to spin up the Nebula Spiral\n" +
            "Three nebula arms orbit you, grinding foes and dragging the lesser ones inward\n" +
            "The arcanum orb itself now swells in flight and slowly drags lesser foes toward its heart\n" +
            "Nebula mist rises from the staff as the gauge fills";

        public override int ChargePerHit => 3;

        public override int CataclysmManaCost => 55;

        protected override float PassiveDamageBonus => 0.05f;

        protected override int DirectorType => ModContent.ProjectileType<GsNebulaSpiralDirector>();

        protected override Color AccentColor => new(160, 90, 240);

        protected override bool AnchorAtCursor => false;

        protected override SoundStyle TriggerSound => SoundID.Item84;

        /// <summary>原版奥秘巨球弹类型</summary>
        private static int OrbType => ContentSamples.ItemsByType[ItemID.NebulaArcanum].shoot;

        /// <summary>巨球本地状态：各端各持（辉体渐胀与双臂相位）</summary>
        private class ArcanumState
        {
            public int T;
        }

        //==================== 动画法：推杖蓄势 + 起手星雾 + 咏唱层级读数 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //推杖蓄势：杖身先前送 4px 微抬再指数回坐，读作把凝浆推出去（确定性输入，各端一致）
            float elapsed = 1f - player.itemAnimation / (float)player.itemAnimationMax;
            float kick = MathF.Exp(-4.5f * elapsed);
            Vector2 aimDir = player.itemRotation.ToRotationVector2() * player.direction;
            player.itemLocation += aimDir * (4f * kick);
            player.itemLocation.Y -= 1.2f * kick * player.gravDir;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手星雾：杖尖腾起两粒星云雾
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 18f, -8f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_Light>(tip + Main.rand.NextVector2Circular(6f, 5f),
                    new Vector2(player.direction * 0.3f, -Main.rand.NextFloat(0.5f, 1.1f)),
                    Main.rand.NextBool() ? GsNebulaSpiralDirector.NebulaPink : GsNebulaSpiralDirector.NebulaViolet,
                    Main.rand.NextFloat(0.09f, 0.14f))?.Configure(16, 0.8f);
            }
            Lighting.AddLight(tip, GsNebulaSpiralDirector.NebulaViolet.ToVector3() * 0.3f);
        }

        /// <summary>咏唱层级读数：计量每过四分之一，杖尖星雾升一档密度（满档交给基类金辉）</summary>
        protected override void GsCataclysmHoldItem(Item item, Player player) {
            if (VaultUtils.isServer || player.whoAmI != Main.myPlayer) {
                return;
            }
            GsCataclysmPlayer state = player.GetModPlayer<GsCataclysmPlayer>();
            if (state.BoundItemType != TargetItemID || state.Charge >= ChargeMax) {
                return;
            }
            int tier = Math.Min(3, state.Charge * 4 / ChargeMax);
            if (tier <= 0 || Main.GameUpdateCount % (12 - tier * 3) != 0) {
                return;
            }
            Vector2 tip = player.itemLocation + new Vector2(player.direction * 12f, -6f)
                + Main.rand.NextVector2Circular(6f, 6f);
            PRTLoader.NewParticle<PRT_Light>(tip, new Vector2(0f, -Main.rand.NextFloat(0.3f, 0.7f)),
                tier >= 2 ? GsNebulaSpiralDirector.NebulaPink : GsNebulaSpiralDirector.NebulaViolet,
                0.06f + tier * 0.02f)?.Configure(14, 0.8f);
        }

        //==================== 左键 rider：双臂星雾 + 微引力 + 子星彗尾 ====================

        public override void GsProjOnSpawnInherited(Projectile proj, GodSmithProjRouter router,
            Projectile parent, GodSmithProjRouter parentRouter) {
            //巨球分裂的子星打角色标（生成端执行，随生成包过线）
            router.MarkData = 1f;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer && proj.type != OrbType) {
                return;
            }
            if (proj.type == OrbType) {
                ArcanumState st = router.GetOrCreateState<ArcanumState>();
                st.T++;
                //微引力：300px 内非 Boss、吃击退的小敌被缓缓拽向球心。
                //NPC 位移权威在服务器（单人即本机），联机客机不写速度防漂移
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    foreach (NPC npc in Main.ActiveNPCs) {
                        if (!npc.CanBeChasedBy() || npc.boss || npc.knockBackResist <= 0f) {
                            continue;
                        }
                        if (npc.realLife >= 0 && Main.npc[npc.realLife].boss) {
                            continue;
                        }
                        float dist = Vector2.Distance(npc.Center, proj.Center);
                        if (dist > 300f || dist < 24f) {
                            continue;
                        }
                        Vector2 pull = (proj.Center - npc.Center).SafeNormalize(Vector2.Zero)
                            * 0.22f * npc.knockBackResist * (1f - dist / 300f);
                        npc.velocity += pull;
                    }
                }
                if (VaultUtils.isServer) {
                    return;
                }
                Lighting.AddLight(proj.Center, GsNebulaSpiralDirector.NebulaViolet.ToVector3() * 0.42f);
                //双臂星雾对旋：两条雾臂绕球心公转（identity 定相；大招三臂，左键两臂，主从有别）
                if (st.T % 3 == 0) {
                    for (int arm = 0; arm < 2; arm++) {
                        float ang = st.T * 0.2f + MathHelper.Pi * arm + proj.identity * 0.61f;
                        Vector2 at = proj.Center + ang.ToRotationVector2() * 26f;
                        PRTLoader.NewParticle<PRT_Light>(at, ang.ToRotationVector2().RotatedBy(MathHelper.PiOver2) * 0.8f,
                            arm == 0 ? GsNebulaSpiralDirector.NebulaPink : GsNebulaSpiralDirector.NebulaViolet,
                            0.09f)?.Configure(12, 0.8f);
                    }
                }
                return;
            }
            //分裂子星：星屑彗尾
            if (router.MarkData >= 1f && proj.timeLeft % 4 == 0) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center - proj.velocity * 0.4f,
                    -proj.velocity * 0.06f,
                    Main.rand.NextBool() ? GsNebulaSpiralDirector.NebulaPink : GsNebulaSpiralDirector.NebulaViolet,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(GsNebulaSpiralDirector.NebulaViolet, 16, 0.06f, 0.8f);
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (proj.type != OrbType) {
                if (router.MarkData >= 1f) {
                    GsCataclysmRiderLib.DrawSpeedGhost(proj, GsNebulaSpiralDirector.NebulaPink, 0.34f);
                }
                return null;
            }
            //巨球辉体渐胀：三层呼吸辉底随飞龄放大（A=0 加色，identity 定相，本体照常盖在其上）
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return null;
            }
            ArcanumState st = router.GetOrCreateState<ArcanumState>();
            float swell = MathHelper.Clamp(st.T / 90f, 0f, 1f);
            float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f + proj.identity * 0.79f);
            Vector2 pos = proj.Center - Main.screenPosition;
            Main.EntitySpriteDraw(glow, pos, null,
                GsNebulaSpiralDirector.NebulaDeep with { A = 0 } * (0.55f * pulse),
                0f, glow.Size() * 0.5f, (0.55f + 0.5f * swell) * pulse, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null,
                GsNebulaSpiralDirector.NebulaViolet with { A = 0 } * (0.42f * pulse),
                0f, glow.Size() * 0.5f, 0.34f + 0.3f * swell, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, pos, null,
                (Color.White with { A = 0 }) * (0.2f * pulse),
                0f, glow.Size() * 0.5f, 0.16f + 0.1f * swell, SpriteEffects.None, 0);
            return null;
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积奥秘
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (VaultUtils.isServer) {
                return;
            }
            //命中反馈：星云绽花（巨球更盛）
            bool orb = proj.type == OrbType;
            if (orb) {
                SoundEngine.PlaySound(SoundID.Item103 with { Volume = 0.35f, Pitch = -0.2f, MaxInstances = 3 }, target.Center);
            }
            for (int i = 0; i < (orb ? 4 : 2); i++) {
                PRTLoader.NewParticle<PRT_Sparkle>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                    Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.2f, 3f),
                    Main.rand.NextBool() ? GsNebulaSpiralDirector.NebulaPink : GsNebulaSpiralDirector.NebulaViolet,
                    Main.rand.NextFloat(0.28f, 0.46f))?.Configure(GsNebulaSpiralDirector.NebulaViolet, 22, 0.05f, 0.9f);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (proj.type != OrbType || VaultUtils.isServer) {
                return;
            }
            //余痕相：巨球消散处残留星雾余晖，活得比球体久
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(20f, 20f),
                    Main.rand.NextVector2Circular(0.5f, 0.5f) - new Vector2(0f, 0.2f),
                    Main.rand.NextBool() ? GsNebulaSpiralDirector.NebulaViolet : GsNebulaSpiralDirector.NebulaDeep,
                    Main.rand.NextFloat(0.14f, 0.2f))?.Configure(Main.rand.Next(30, 46), 0.7f);
            }
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeavenfallStar>(proj.Center + Main.rand.NextVector2Circular(14f, 14f),
                    new Vector2(Main.rand.NextFloat(-0.6f, 0.6f), -Main.rand.NextFloat(0.4f, 1f)),
                    GsNebulaSpiralDirector.NebulaPink, Main.rand.NextFloat(0.28f, 0.42f))?.Configure(false, 24);
            }
        }
    }
}
