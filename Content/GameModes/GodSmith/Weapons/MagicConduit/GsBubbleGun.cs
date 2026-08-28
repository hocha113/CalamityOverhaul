using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 泡泡枪重铸：泡压。材质身份：海沫水膜（Duke 血统的碧涛泡流，Sea 色板已预留）。<br/>
    /// ①热量=泡压：连喷积压；白热「沸泡」泡弹增大、命中裂两枚小泡；<br/>
    /// ②过载「爆管」：枪管堵塞进锁 90t，泡沫喷涌演出；<br/>
    /// ③泄压「泡爆潮」：前方扇形七枚大泡错速漂出，依距离连环爆；④施法有泡压后坐与起手泡沫
    /// </summary>
    internal class GsBubbleGun : GsHeatScheme
    {
        public override int TargetItemID => ItemID.BubbleGun;

        protected override string GsDescFallback =>
            "Reforged: nonstop spray builds bubble pressure; at a boil the bubbles swell and burst into twin beads on impact" +
            "\nCap the gauge and the barrel jams shut in a gush of foam" +
            "\nRight click to vent everything as a fan of seven great bubbles that pop in a rolling chain";

        internal override float HeatPerShot => 3f;
        internal override float CoolRatePerTick => 0.7f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Lock;
        internal override int OverloadLockTicks => 90;
        internal override Color MuzzleTheme => GsConduitVFX.SeaMain;

        /// <summary>原版泡弹类型</summary>
        private static int BubbleType => ContentSamples.ItemsByType[ItemID.BubbleGun].shoot;

        //==================== 动画法：泡压后坐 + 起手泡沫 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //泡压后坐：出手瞬间水平后坐 2px 带轻抖，随动画进度回坐（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction * 2f, MathF.Sin(player.itemAnimation * 1.7f) * 0.8f) * progress;
            player.itemRotation -= player.direction * 0.05f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手泡沫：喷口一撮海沫小泡
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 20f, -2f);
            PRTLoader.NewParticle<PRT_CampfireBubble>(tip + Main.rand.NextVector2Circular(4f, 4f),
                new Vector2(player.direction * Main.rand.NextFloat(0.6f, 1.4f), -Main.rand.NextFloat(0.3f, 0.8f)),
                GsConduitVFX.SeaBright, Main.rand.NextFloat(0.25f, 0.4f));
        }

        //==================== 沸泡：白热出生升格 ====================

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            base.GsProjOnSpawnMarked(proj, router);
            //白热出生的泡弹：增大（沸泡标随生成包过线，远端同拍渲染）
            if (proj.owner == Main.myPlayer && proj.type == BubbleType && router.MarkData >= 1f) {
                proj.scale *= 1.3f;
                proj.netUpdate = true;
            }
        }

        //==================== 飞行相：海沫微光 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BubbleType || VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsConduitVFX.SeaMain.ToVector3() * 0.14f);
            if (proj.timeLeft % 8 == 0 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(proj.Center + Main.rand.NextVector2Circular(5f, 5f),
                    -proj.velocity * 0.04f - Vector2.UnitY * 0.2f, GsConduitVFX.SeaBright,
                    Main.rand.NextFloat(0.04f, 0.07f))?.Configure(Main.rand.Next(10, 16), 0.65f);
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != BubbleType) {
                return;
            }
            //泡膜辉底：白热沸泡加亮圈（A=0 加色，identity 定相呼吸）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.82f + 0.18f * MathF.Sin(Main.GlobalTimeWrappedHourly * 8f + proj.identity * 0.87f);
            bool boil = router.MarkData >= 1f;
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null,
                GsConduitVFX.SeaMain with { A = 0 } * ((boil ? 0.55f : 0.35f) * pulse), 0f,
                glow.Size() / 2f, 0.3f * proj.scale * pulse, SpriteEffects.None, 0);
            if (boil) {
                Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null,
                    Color.White with { A = 0 } * (0.3f * pulse), 0f,
                    glow.Size() / 2f, 0.14f * proj.scale, SpriteEffects.None, 0);
            }
        }

        //==================== 命中：沸泡裂珠 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != BubbleType) {
                return;
            }
            if (!VaultUtils.isServer) {
                //命中反馈：水膜破碎
                SoundEngine.PlaySound(SoundID.Item54 with { Volume = 0.4f, Pitch = 0.3f, MaxInstances = 5 }, target.Center);
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_CampfireBubble>(target.Center + Main.rand.NextVector2Circular(7f, 7f),
                        Main.rand.NextVector2Circular(1.6f, 1.2f) - new Vector2(0f, 0.6f),
                        GsConduitVFX.SeaBright, Main.rand.NextFloat(0.3f, 0.5f));
                }
            }
            //沸泡裂珠：白热泡命中裂两枚小泡（源 Misc 不承签，防递归裂泡）
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData < 1f) {
                return;
            }
            int beadDamage = Math.Max(1, (int)(proj.damage * 0.3f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < 2; i++) {
                Vector2 vel = dir.RotatedBy(i == 0 ? 0.6f : -0.6f) * 4.5f;
                int idx = Projectile.NewProjectile(Main.player[proj.owner].GetSource_Misc("GsConduitBoil"),
                    target.Center, vel, BubbleType, beadDamage, proj.knockBack * 0.3f, proj.owner);
                if (idx >= 0 && idx < Main.maxProjectiles) {
                    Main.projectile[idx].scale *= 0.6f;
                    Main.projectile[idx].timeLeft = Math.Min(Main.projectile[idx].timeLeft, 40);
                    Main.projectile[idx].netUpdate = true;
                }
            }
        }

        //==================== 过载：爆管 ====================

        internal override void OnOverload(Player player, GsHeatPlayer hp) {
            base.OnOverload(player, hp);
            if (VaultUtils.isServer) {
                return;
            }
            //爆管演出：枪口泡沫喷涌
            SoundEngine.PlaySound(SoundID.Item85 with { Volume = 0.9f, Pitch = -0.4f }, player.Center);
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 20f, -2f);
            for (int i = 0; i < 10; i++) {
                PRTLoader.NewParticle<PRT_CampfireBubble>(tip + Main.rand.NextVector2Circular(6f, 6f),
                    new Vector2(player.direction * Main.rand.NextFloat(0.5f, 2.5f), -Main.rand.NextFloat(0.5f, 2.2f)),
                    GsConduitVFX.SeaMain, Main.rand.NextFloat(0.35f, 0.7f));
            }
        }

        //==================== 泄压：泡爆潮 ====================

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //扇形七枚大泡：错速漂出（近慢远快），威力随泡压；泡体是族内已有的水膜大泡
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.5f + 1.1f * frac)));
            Vector2 aim = GsAimUnit(player);
            for (int i = 0; i < 7; i++) {
                float off = MathHelper.Lerp(-0.55f, 0.55f, i / 6f);
                float speed = 3.2f + 1.8f * ((i * 3) % 7) / 6f;
                Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"),
                    player.MountedCenter + aim * 26f, aim.RotatedBy(off) * speed,
                    ModContent.ProjectileType<GsBubbleVentProj>(), damage, 6f, player.whoAmI);
            }
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item85 with { Volume = 1f, Pitch = 0.1f }, player.Center);
            }
        }
    }
}
