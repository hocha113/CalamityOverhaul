using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.GunsHard.Specials
{
    /// <summary>
    /// 食人鱼枪重铸（L2 弹幕增强，经典不毁）：原版弹幕不换、免弹药保留。<br/>
    /// [咬附] 完全原版；[鱼群巡域] 鱼群绕光标约 120px 盘旋撕咬 6 秒后回巢，
    /// 巡域 AI 经 <see cref="GodSmithProjRouter"/> 打标接管（GsProjPreAI 压掉原版 aiStyle），
    /// 锚点随光标实时更新（ai[0]/ai[1] 承载 + netUpdate 节流过线）
    /// </summary>
    internal class GsPiranhaGun : GodSmithScheme
    {
        public override int TargetItemID => ItemID.PiranhaGun;

        public override string GsFamily => "GunsSpecial";

        protected override string GsDescFallback =>
            "Reforged: two hunting modes. Latch is the classic bite-and-hold; School Patrol sends the fish circling your cursor for 6s, gnawing anything in the ring, then they swim home"
            + "\nRight click to switch modes. Patrol fish deal 85% damage per bite; at most 9 patrol fish at once";

        /// <summary>模式名（[0]=咬附 [1]=鱼群巡域）</summary>
        internal static LocalizedText[] ModeNames;

        /// <summary>巡域时长（tick）</summary>
        private const int PatrolDuration = 360;
        /// <summary>入环过渡时长</summary>
        private const int JoinDuration = 20;
        /// <summary>巡域鱼在场上限</summary>
        private const int PatrolCap = 9;

        //以下瞬时字段只在本地玩家路径消费（方案单例的 owner 契约）
        private int mode;
        private int switchCd;
        private float pendingMode;
        private Vector2 pendingAnchor;

        public override void GsSetStaticDefaults() {
            ModeNames = [
                this.GetLocalization("Mode0", () => "Latch"),
                this.GetLocalization("Mode1", () => "School Patrol"),
            ];
        }

        public override bool? GsAltFunctionUse(Item item, Player player) => true;

        public override bool? GsCanUseItem(Item item, Player player) {
            if (player.altFunctionUse == 2) {
                if (player.whoAmI == Main.myPlayer && switchCd <= 0) {
                    switchCd = 12;
                    mode = mode == 0 ? 1 : 0;
                    GsGunPose.ModeSwitchFeedback(player, ModeNames[mode].Value);
                }
                return false;
            }
            //巡域鱼有在场上限，满编时这一枪打不出去
            if (player.whoAmI == Main.myPlayer && mode == 1
                && player.ownedProjectileCounts[ProjectileID.Piranha] >= PatrolCap) {
                return false;
            }
            return null;
        }

        public override void GsHoldItem(Item item, Player player) {
            if (player.whoAmI != Main.myPlayer) {
                return;
            }
            if (switchCd > 0) {
                switchCd--;
            }
        }

        public override bool? GsShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            //本次 use 的三条鱼共用同一份模式与锚点；下一次 GsShoot 覆写，不做消费清零
            pendingMode = mode;
            pendingAnchor = Main.MouseWorld;
            return null;//原版继续生成鱼，交给路由打标
        }

        public override void GsProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router) {
            router.MarkData = pendingMode;
            if (pendingMode >= 1f) {
                //巡域模式接管后 ai[] 归方案使用：ai0/ai1=锚点，先于生成包写入
                proj.ai[0] = pendingAnchor.X;
                proj.ai[1] = pendingAnchor.Y;
                proj.ai[2] = 0f;
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            if (proj.type != ProjectileID.Piranha || router.MarkData < 1f) {
                return true;//咬附档完全原版
            }

            //出生 alpha 兜底：原版 AI 被压后没人递减，防隐形鱼
            proj.alpha = 0;
            proj.tileCollide = false;
            proj.ai[2]++;
            float t = proj.ai[2];

            //owner 端把锚点跟到光标上，位移超阈值才发包（节流）
            if (proj.IsOwnedByLocalPlayer()) {
                Vector2 want = Main.MouseWorld;
                if (Vector2.DistanceSquared(want, new Vector2(proj.ai[0], proj.ai[1])) > 32f * 32f) {
                    proj.ai[0] = want.X;
                    proj.ai[1] = want.Y;
                    proj.netUpdate = true;
                }
            }
            Vector2 anchor = new(proj.ai[0], proj.ai[1]);

            Player owner = Main.player[proj.owner];
            if (t > PatrolDuration) {
                //回巢：加速游回玩家，贴身消失
                Vector2 home = owner.Center - proj.Center;
                float dist = home.Length();
                if (dist < 40f) {
                    if (proj.IsOwnedByLocalPlayer()) {
                        proj.Kill();
                    }
                    return false;
                }
                Vector2 desired = home.SafeNormalize(Vector2.UnitX) * 19f;
                proj.velocity = Vector2.Lerp(proj.velocity, desired, 0.12f);
            }
            else {
                //盘旋：identity 定相绕锚点游弋，半径呼吸让环带有厚度
                float phase = proj.identity * 2.399f + t * 0.11f;
                float radius = 120f + MathF.Sin(proj.identity * 1.7f + t * 0.045f) * 26f;
                Vector2 ringPos = anchor + phase.ToRotationVector2() * radius;
                float approach = t < JoinDuration ? 0.10f : 0.16f;
                Vector2 desired = (ringPos - proj.Center) * approach;
                if (desired.Length() > 17f) {
                    desired = desired.SafeNormalize(Vector2.UnitX) * 17f;
                }
                proj.velocity = Vector2.Lerp(proj.velocity, desired, 0.35f);
            }

            //鱼身姿态与帧动画自管（原版 AI 被压）
            if (proj.velocity.X < 0f) {
                proj.rotation = proj.velocity.ToRotation() + MathHelper.Pi;
                proj.spriteDirection = -1;
            }
            else {
                proj.rotation = proj.velocity.ToRotation();
                proj.spriteDirection = 1;
            }
            if (++proj.frameCounter >= 4) {
                proj.frameCounter = 0;
                if (++proj.frame >= Main.projFrames[proj.type]) {
                    proj.frame = 0;
                }
            }
            return false;
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            if (router.MarkData >= 1f) {
                modifiers.FinalDamage *= 0.85f;//巡域是区域持续压制，单口撕咬折价
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //撕咬迸血（个人反馈层，预算 3 粒）
            SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.25f, Pitch = 0.4f, MaxInstances = 3 }, target.Center);
            for (int i = 0; i < 3; i++) {
                PRTLoader.NewParticle<PRT_HeartcarverDroplet>(target.Center,
                    Main.rand.NextVector2Circular(3f, 3f) - Vector2.UnitY * 1.2f,
                    new Color(170, 30, 34), Main.rand.NextFloat(0.6f, 1f))?.Configure(Main.rand.Next(14, 24), 0.28f);
            }
        }

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (router.MarkData < 1f || VaultUtils.isServer) {
                return;
            }
            //回巢入水花：鱼隐没的余痕
            for (int i = 0; i < 3; i++) {
                Dust d = Dust.NewDustPerfect(proj.Center, DustID.Water,
                    Main.rand.NextVector2Circular(2f, 2f) - Vector2.UnitY * 1.5f, 80, default, 1.1f);
                d.noGravity = Main.rand.NextBool();
            }
        }

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;//基线补偿，综合 DPS 落在原版 108%~112%
    }
}
