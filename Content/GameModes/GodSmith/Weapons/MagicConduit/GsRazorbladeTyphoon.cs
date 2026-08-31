using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit
{
    /// <summary>
    /// 刀刃台风重铸（A 档）。材质身份：海沫风暴刃（Duke 血统的碧涛回旋刃，Sea 色板已预留）。<br/>
    /// ①热量=风暴势能：慢射高积，顶格维持不锁；<br/>
    /// ②「涡心归位」：白热出生的风刃飞远后折返归位，绕行再猎（不失刃）；<br/>
    /// ③泄压「台风眼」：光标处驻场向心风暴 2 秒，在场风刃全部被卷向涡眼合猎，
    /// 涡散时环爆收场；④A 档四相：出手浪沫喷薄/飞行涡线残影/命中碧涛迸溅/涡眼余韵
    /// </summary>
    internal class GsRazorbladeTyphoon : GsHeatScheme
    {
        public override int TargetItemID => ItemID.RazorbladeTyphoon;

        protected override string GsDescFallback =>
            "Reforged: each cast banks storm charge; blades born at white heat never get lost, they wheel back to your side and hunt again" +
            "\nRight click to open the Eye of the Typhoon at your cursor: every blade in the air is drawn into the vortex, and the eye bursts as it closes";

        internal override float HeatPerShot => 22f;
        internal override float CoolRatePerTick => 0.5f;
        internal override float WhiteHotDamageMult => 1.1f;
        internal override GsOverloadPolicy OverloadPolicy => GsOverloadPolicy.Sustain;
        internal override float VentMinHeat => 40f;
        internal override Color MuzzleTheme => GsConduitVFX.SeaMain;

        /// <summary>原版台风刃弹类型</summary>
        private static int BladeType => ContentSamples.ItemsByType[ItemID.RazorbladeTyphoon].shoot;

        //==================== 动画法：甩盘后坐 + 起手浪沫 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //甩盘后坐：出手瞬间整臂角度踢起，随动画进度扫回（绝对剖面 0.3·p²，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            float prev = (player.itemAnimation + 1) / n;
            GsMagicKickMath.ApplyKickDiff(player, 0.3f * progress * progress, 0.3f * prev * prev);
            player.itemLocation -= new Vector2(player.direction * 3f, -2f) * progress;
        }

        public override void GsUseAnimation(Item item, Player player) {
            if (VaultUtils.isServer) {
                return;
            }
            //出手浪沫喷薄：腕间一蓬海沫甩出
            SoundEngine.PlaySound(SoundID.Item84 with { Volume = 0.5f, Pitch = 0.2f, MaxInstances = 3 }, player.Center);
            Vector2 tip = player.MountedCenter + GsAimUnit(player) * 22f;
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(tip + Main.rand.NextVector2Circular(6f, 6f),
                    GsAimUnit(player).RotatedBy(Main.rand.NextFloat(-0.7f, 0.7f)) * Main.rand.NextFloat(1.5f, 3.5f),
                    i % 2 == 0 ? GsConduitVFX.SeaMain : GsConduitVFX.SeaBright,
                    Main.rand.NextFloat(0.22f, 0.36f))?.Configure(true, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_CampfireBubble>(tip, -Vector2.UnitY * 0.6f,
                GsConduitVFX.SeaBright, Main.rand.NextFloat(0.25f, 0.4f));
        }

        //==================== 飞行相：涡线残影 + 涡心归位 + 涡眼合猎 ====================

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != BladeType) {
                return;
            }
            Player owner = Main.player[proj.owner];

            //涡眼合猎：owner 的台风眼在场时，风刃被卷向涡眼（原版 AI 先跑，这里汇聚覆写；
            //输入全是已同步实体位置，各端确定性一致）
            Projectile eye = FindOwnerEye(proj.owner);
            if (eye != null) {
                Vector2 pull = (eye.Center - proj.Center).SafeNormalize(Vector2.UnitX);
                float dist = Vector2.Distance(eye.Center, proj.Center);
                float grip = dist > GsTyphoonEyeProj.EyeRadius ? 0.16f : 0.05f;
                proj.velocity = Vector2.Lerp(proj.velocity.SafeNormalize(Vector2.UnitX), pull, grip)
                    .SafeNormalize(Vector2.UnitX) * proj.velocity.Length();
            }
            //涡心归位：白热刃飞离施法者过远即折返（同步位置驱动，各端一致），归程中不再散逸
            else if (router.MarkData >= 1f && owner.active && !owner.dead
                && Vector2.DistanceSquared(proj.Center, owner.Center) > 650f * 650f) {
                Vector2 home = (owner.Center - proj.Center).SafeNormalize(Vector2.UnitX);
                proj.velocity = Vector2.Lerp(proj.velocity.SafeNormalize(Vector2.UnitX), home, 0.1f)
                    .SafeNormalize(Vector2.UnitX) * proj.velocity.Length();
                proj.timeLeft = Math.Max(proj.timeLeft, 60);
            }

            if (VaultUtils.isServer) {
                return;
            }
            Lighting.AddLight(proj.Center, GsConduitVFX.SeaMain.ToVector3() * 0.22f);
            //涡线：刃缘两侧交替甩出的海沫涡尘（identity 定相）
            if (proj.timeLeft % 3 == 0) {
                float side = MathF.Sin(proj.timeLeft * 0.6f + proj.identity * 1.1f) * 10f;
                Vector2 lateral = proj.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2) * side;
                PRTLoader.NewParticle<PRT_Light>(proj.Center + lateral - proj.velocity * 0.3f,
                    -proj.velocity * 0.06f, router.MarkData >= 1f ? GsConduitVFX.SeaBright : GsConduitVFX.SeaMain,
                    Main.rand.NextFloat(0.05f, 0.09f))?.Configure(Main.rand.Next(10, 18), 0.65f);
            }
        }

        /// <summary>某玩家在场的台风眼（各端扫同一同步弹幕表，结果一致）</summary>
        private static Projectile FindOwnerEye(int owner) {
            int eyeType = ModContent.ProjectileType<GsTyphoonEyeProj>();
            foreach (Projectile p in Main.ActiveProjectiles) {
                if (p.type == eyeType && p.owner == owner) {
                    return p;
                }
            }
            return null;
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            if (proj.type != BladeType) {
                return;
            }
            //刃底旋涡辉：反向差速旋的双层涡光 + 白热刃追加白芯（A=0 加色，identity 定相）
            Texture2D glow = CWRAsset.SoftGlow.Value;
            Texture2D star = CWRAsset.StarTexture.Value;
            Vector2 pos = proj.Center - Main.screenPosition;
            float t = Main.GlobalTimeWrappedHourly;
            float seed = proj.identity * 0.53f;
            bool storm = router.MarkData >= 1f;
            Main.EntitySpriteDraw(glow, pos, null,
                GsConduitVFX.SeaDeep with { A = 0 } * 0.5f, 0f, glow.Size() / 2f, 0.42f * proj.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null,
                GsConduitVFX.SeaMain with { A = 0 } * 0.55f, t * 9f + seed, star.Size() / 2f,
                0.3f * proj.scale, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(star, pos, null,
                GsConduitVFX.SeaBright with { A = 0 } * 0.4f, -t * 6f + seed + 1.3f, star.Size() / 2f,
                0.2f * proj.scale, SpriteEffects.None, 0);
            if (storm) {
                float pulse = 0.8f + 0.2f * MathF.Sin(t * 8f + seed);
                Main.EntitySpriteDraw(glow, pos, null,
                    Color.White with { A = 0 } * (0.35f * pulse), 0f, glow.Size() / 2f,
                    0.16f * proj.scale, SpriteEffects.None, 0);
            }
        }

        //==================== 命中：碧涛迸溅 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (proj.type != BladeType || VaultUtils.isServer) {
                return;
            }
            SoundEngine.PlaySound(SoundID.Item86 with { Volume = 0.35f, Pitch = 0.35f, MaxInstances = 4 }, target.Center);
            for (int i = 0; i < 4; i++) {
                PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(8f, 8f),
                    Main.rand.NextVector2Circular(2.2f, 2.2f),
                    i % 2 == 0 ? GsConduitVFX.SeaMain : GsConduitVFX.SeaBright,
                    Main.rand.NextFloat(0.24f, 0.4f))?.Configure(true, Main.rand.Next(10, 16));
            }
            PRTLoader.NewParticle<PRT_CampfireBubble>(target.Center, -Vector2.UnitY * 0.8f,
                GsConduitVFX.SeaBright, Main.rand.NextFloat(0.25f, 0.4f));
        }

        //==================== 泄压：台风眼 ====================

        internal override void FireVent(Player player, GsHeatPlayer hp) {
            //光标处驻场涡眼（威力随势能）：环刃 tick 判定 + 卷刃合猎 + 涡散环爆由涡眼自体负责
            float frac = hp.Heat / GsHeatPlayer.HeatMax;
            int damage = Math.Max(1, (int)(player.GetWeaponDamage(player.HeldItem) * (0.4f + 0.8f * frac)));
            Projectile.NewProjectile(player.GetSource_Misc("GsConduitVent"), Main.MouseWorld, Vector2.Zero,
                ModContent.ProjectileType<GsTyphoonEyeProj>(), damage, 6f, player.whoAmI);
        }
    }

    /// <summary>
    /// 法器施法踢的差分数学（法器四族共用，镜像枪族 GsGunKickMath 的差分思路）。<br/>
    /// 原版事实（TML 源 Player.cs）：useStyle-5 的 itemRotation 只在射击帧被绝对赋值
    /// （L43594-L43604，每发一次、动画中途连发同样 snap），动画期 ItemCheck_ApplyUseStyle_Inner
    /// 只重算 itemLocation 不碰 itemRotation（L46736-L46781，3779/4715/4952 等每帧归零特例除外），
    /// 且方案钩子在原版之后每帧跑（L46313-L46317）——UseStyle 里直接 `itemRotation ±= k·包络`
    /// 是逐帧累减而非绝对偏移，慢杖漂移可达 48°~238° 再被下一发 snap 掰回。<br/>
    /// 修法：目标绝对剖面 offset(a) = want(a)，逐帧差分 Δ = want(本帧) − want(上帧)。
    /// 帧序确定（itemAnimation 逐帧递减、UseStyle 每帧一跑），差分无需记账字段：<br/>
    /// ·射击帧（itemTime==0；射击门 L39786-L39793 在 UseStyle 之后同帧）写了也被 snap 抹掉，跳写；<br/>
    /// ·射击后首帧（itemTime==itemTimeMax−1；SetItemTime L4971 同帧写双值）上帧施加量已被
    /// snap 清掉，按 0 计；<br/>
    /// ·远端不 snap 但同样走 ApplyItemTime（L43377-L43380），itemTime 节奏各端一致，残差 ≤want
    /// 峰值且被 owner 每发的 NetMessage 41 绝对覆盖自愈，故无需 myPlayer 守门，旁观者同样看到施法踢
    /// </summary>
    internal static class GsMagicKickMath
    {
        /// <summary>
        /// 差分施加一帧施法踢。want 正=杖头上挑、负=下压（镜像角约定同 GsGunKickMath：
        /// 上挑符号 = −direction·gravDir，倒挂自动翻转）。wantNow/wantPrev 为本帧与上帧
        /// （itemAnimation+1）的目标绝对偏移，须按确定性输入求值
        /// </summary>
        internal static void ApplyKickDiff(Player player, float wantNow, float wantPrev) {
            if (player.itemTime == 0) {
                //射击帧：UseStyle 之后同帧 snap 绝对覆盖 itemRotation，施加无意义
                return;
            }
            if (player.itemTime == player.itemTimeMax - 1) {
                //上帧是射击帧，其施加量已被 snap 抹掉，按已清账计
                wantPrev = 0f;
            }
            player.itemRotation -= (wantNow - wantPrev) * player.direction * player.gravDir;
        }
    }
}
