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
    /// 地狱叉重铸：燎原。材质身份：炉底狱火（地牢炉渣里舀出来的一叉火）。<br/>
    /// ①左键 rider：火球拖螺旋火屑，狱火场垫呼吸焰辉，命中挂狱火并积「积炎」；<br/>
    /// ②满量右键「燎原」：光标处贴地起火线，三波火浪沿地横扫，波间火雨，
    /// 余韵焚野烬滩；③施法有掷叉后坐与起手炉焰
    /// </summary>
    internal class GsInfernoFork : GsCataclysmScheme
    {
        public override int TargetItemID => ItemID.InfernoFork;

        protected override string GsDescFallback =>
            "Reforged: fireballs corkscrew with furnace slag and every hit banks Cinder" +
            "\nWhen the gauge is full, right click to set the Wildfire at your cursor: three waves of flame sweep the ground, fire rains between them, and an ember shoal smolders after";

        protected override float PassiveDamageBonus => 0.08f;
        protected override int DirectorType => ModContent.ProjectileType<GsInfernoForkWildfireDirector>();
        protected override Color AccentColor => GsInfernoForkWildfireDirector.FireMain;
        protected override SoundStyle TriggerSound => SoundID.Item74;

        /// <summary>原版狱火弹 / 狱火场弹类型</summary>
        private static int BoltType => ContentSamples.ItemsByType[ItemID.InfernoFork].shoot;
        private const int FieldType = ProjectileID.InfernoFriendlyBlast;

        //==================== 动画法：掷叉后坐 + 起手炉焰 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //掷叉后坐：出手瞬间叉身后坐 4px 上踢，随动画进度回坐（确定性输入，各端一致）
            float progress = player.itemAnimation / (float)player.itemAnimationMax;
            player.itemLocation -= new Vector2(player.direction * 4f, 1f) * progress;
            player.itemRotation -= player.direction * 0.1f * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //起手炉焰：叉尖腾起炉渣火
            Vector2 tip = player.MountedCenter + new Vector2(player.direction * 18f, -6f);
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_HellFlame>(tip + Main.rand.NextVector2Circular(5f, 4f),
                    new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), -Main.rand.NextFloat(0.9f, 1.9f)),
                    GsInfernoForkWildfireDirector.FireMain, Main.rand.NextFloat(0.32f, 0.5f));
            }
            Lighting.AddLight(tip, GsInfernoForkWildfireDirector.FireMain.ToVector3() * 0.35f);
        }

        //==================== 左键 rider：螺旋火屑 + 焰辉垫场 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (VaultUtils.isServer) {
                return;
            }
            if (proj.type == BoltType) {
                Lighting.AddLight(proj.Center, GsInfernoForkWildfireDirector.FireMain.ToVector3() * 0.3f);
                //螺旋火屑：绕弹道盘旋（identity 定相）
                if (proj.timeLeft % 3 == 0) {
                    float phase = proj.timeLeft * 0.55f + proj.identity * 1.2f;
                    Vector2 side = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2)
                        * MathF.Sin(phase) * 9f;
                    PRTLoader.NewParticle<PRT_Spark>(proj.Center + side - proj.velocity * 0.3f,
                        -proj.velocity * 0.06f, MathF.Cos(phase) > 0f
                            ? GsInfernoForkWildfireDirector.FireBright : GsInfernoForkWildfireDirector.FireMain,
                        Main.rand.NextFloat(0.2f, 0.32f))?.Configure(false, Main.rand.Next(8, 14));
                }
                return;
            }
            if (proj.type == FieldType) {
                Lighting.AddLight(proj.Center, GsInfernoForkWildfireDirector.FireMain.ToVector3() * 0.45f);
            }
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != FieldType) {
                return;
            }
            //狱火场焰辉垫：原版火场之下的呼吸辉底（A=0 加色，identity 定相）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            float pulse = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5.5f + proj.identity * 0.79f);
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null,
                GsInfernoForkWildfireDirector.FireDeep with { A = 0 } * (0.5f * pulse), 0f,
                glow.Size() / 2f, 1.1f * pulse * proj.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(glow, proj.Center - Main.screenPosition, null,
                GsInfernoForkWildfireDirector.FireMain with { A = 0 } * (0.35f * pulse), 0f,
                glow.Size() / 2f, 0.6f * proj.scale, SpriteEffects.None, 0);
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //基类积炎（计量是攻击方本地量）
            base.GsProjOnHitNPC(proj, target, hit, damageDone, router);
            if (proj.type != BoltType && proj.type != FieldType) {
                return;
            }
            if (!VaultUtils.isServer) {
                for (int i = 0; i < 3; i++) {
                    PRTLoader.NewParticle<PRT_HellFlame>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                        Main.rand.NextVector2Circular(1.8f, 1.8f) - new Vector2(0f, 1.1f),
                        i % 2 == 0 ? GsInfernoForkWildfireDirector.FireMain : GsInfernoForkWildfireDirector.FireBright,
                        Main.rand.NextFloat(0.35f, 0.55f));
                }
            }
            if (proj.IsOwnedByLocalPlayer()) {
                target.AddBuff(BuffID.OnFire3, proj.type == BoltType ? 180 : 120);
            }
        }
    }
}
