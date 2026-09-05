using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 恶魔镰刀重铸：收割节拍。正拍镰刀滞空缩短约四成（更快出手），失拍反而
    /// 拖长三成；共鸣 3 层起镰刀有概率在飞远后回旋一程（回程 0.6 倍）；
    /// 满层强化「死神十字」：横竖双镰交叉钉向准星（各 0.9 倍）。材质身份：暗焰。<br/>
    /// 滞空干预走 vanilla ai[0] 计数的增速/减速（各端按同步的 MarkData 一致推演）
    /// </summary>
    internal class GsDemonScythe : GsChantScheme
    {
        public override int TargetItemID => ItemID.DemonScythe;

        protected override string GsDescFallback =>
            "Reforged: on-beat scythes wind up faster and may boomerang back at high resonance;\nat full resonance the next cast crosses two scythes over your cursor";
        protected override float BaseDamageMult => 1.04f;

        //滞空节拍原版已慢，正拍返蓝按计划压到 25%
        protected override float OnBeatManaRefund => 0.25f;

        /// <summary>形态：死神十字镰（MarkData2 = 0 横 / 1 竖）</summary>
        private const float FormCross = 10f;

        /// <summary>MarkData2 回旋标志位（层数 + 100）</summary>
        private const float ReturnFlag = 100f;

        /// <summary>回旋状态（端本地：回旋触发按本地飞行帧计，伤害只认 owner 端）</summary>
        private class ScytheState
        {
            public int Frames;
            public bool Returning;
        }

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //死神十字：横镰自侧方平扫、竖镰自上方直落，交叉于准星
            Vector2 aim = Main.MouseWorld;
            int crossDamage = Math.Max(1, (int)(damage * 0.9f));
            int side = Math.Sign(aim.X - player.Center.X);
            if (side == 0) {
                side = 1;
            }
            for (int i = 0; i < 2; i++) {
                bool vertical = i == 1;
                Vector2 from = vertical ? aim - Vector2.UnitY * 260f : aim - new Vector2(side * 260f, 0f);
                Vector2 vel = (aim - from).SafeNormalize(Vector2.UnitX) * 11f;
                QueueForm(player, FormCross, vertical ? 1f : 0f);
                Projectile.NewProjectile(source, from, vel, type, crossDamage, knockback, player.whoAmI);
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            //回旋裁决：3 层起 owner 掷一次（每高一层 +25%），结果编码进 MarkData2 过线
            if (router.MarkData is FormOnBeat or FormEmpower && router.MarkData2 >= 3f) {
                float chance = 0.25f * (router.MarkData2 - 2f);
                if (Main.rand.NextFloat() < chance) {
                    router.MarkData2 += ReturnFlag;
                    proj.netUpdate = true;
                }
            }
        }

        public override bool GsProjPreAI(Projectile proj, GodSmithProjRouter router) {
            //十字镰全接管：直线钉飞不滞空，自转读作锯刃
            if (router.MarkData == FormCross) {
                proj.rotation += 0.38f;
                proj.alpha = Math.Max(0, proj.alpha - 40);
                return false;
            }
            return true;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            float mark = router.MarkData;
            //收割节拍：正拍加速滞空计数（约 -40%），平拍拖慢（约 +30%）
            if (mark is FormOnBeat or FormEmpower && proj.ai[0] < 100f) {
                proj.ai[0] += 0.67f;
            }
            else if (mark == FormStraight && proj.ai[0] > 1f) {
                proj.ai[0] -= 0.23f;
            }
            //回旋：飞行 52 帧后掉头扑向持杖人，一去一回两段收割
            if (mark is FormOnBeat or FormEmpower && router.MarkData2 >= ReturnFlag) {
                ScytheState state = router.GetOrCreateState<ScytheState>();
                state.Frames++;
                if (!state.Returning && state.Frames > 52) {
                    state.Returning = true;
                    Player owner = Main.player[proj.owner];
                    proj.velocity = (owner.Center - proj.Center).SafeNormalize(Vector2.UnitX)
                        * Math.Max(6f, proj.velocity.Length() * 0.6f);
                }
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //回程收割衰减：伤害裁决在 owner 端，本地状态即权威
            if (router.MarkData2 >= ReturnFlag
                && router.LocalState is ScytheState { Returning: true }) {
                modifiers.FinalDamage *= 0.6f;
            }
        }
    }
}
