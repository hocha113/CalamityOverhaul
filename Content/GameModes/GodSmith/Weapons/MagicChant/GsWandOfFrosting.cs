using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 冰霜魔杖重铸：霜晶折射。正拍霜火命中散出斜向冰片（层数高时多散一枚），
    /// 满层强化「冰雾锥」：短程宽锥九枚冷雾霜弹并附霜噬。材质身份：冰晶。<br/>
    /// 与设计的偏差：冻缓 20% 需要跨端权威的 NPC 减速通道，联机纪律下降级为
    /// 霜噬（Frostbite）持续伤害，只烧不控
    /// </summary>
    internal class GsWandOfFrosting : GsChantScheme
    {
        public override int TargetItemID => ItemID.WandofFrosting;

        protected override string GsDescFallback =>
            "Reforged: on-beat frost bolts refract into ice shards on hit;\nat full resonance the next cast breathes a wide cone of freezing mist that inflicts frostbite";
        protected override float BaseDamageMult => 1.15f;

        /// <summary>形态：折射冰片</summary>
        private const float FormShard = 10f;
        /// <summary>形态：冰雾锥霜弹</summary>
        private const float FormMist = 11f;

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //冰雾锥：约 120px 短程 50 度宽锥，九枚慢速冷雾霜弹
            int mistDamage = Math.Max(1, (int)(damage * 0.5f));
            for (int i = 0; i < 9; i++) {
                float off = MathHelper.ToRadians(-25f + 50f * i / 8f);
                Vector2 vel = velocity.RotatedBy(off).SafeNormalize(Vector2.UnitX)
                    * Main.rand.NextFloat(3.2f, 5.2f);
                QueueForm(player, FormMist);
                Projectile.NewProjectile(source, position, vel, type, mistDamage, knockback * 0.4f, player.whoAmI);
            }
            return false;
        }

        protected override void ChantProjOnSpawnMarked(Projectile proj, GodSmithProjRouter router, GsChantPlayer chant) {
            if (router.MarkData == FormMist) {
                //冷雾短程：约 24 帧走完 120px
                proj.timeLeft = Math.Min(proj.timeLeft, 26);
                proj.scale *= 1.25f;
            }
        }

        public override void GsProjModifyHitNPC(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //冷雾霜弹附霜噬 1.5s（AddBuff 原生入同步）
            if (router.MarkData == FormMist) {
                target.AddBuff(BuffID.Frostburn2, 90);
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            //霜晶折射：正拍原生弹命中时斜向散冰片，层数 3 以上加散中路一枚
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            int shards = router.MarkData2 >= 3f ? 3 : 2;
            int shardDamage = Math.Max(1, (int)(proj.damage * 0.4f));
            Vector2 dir = proj.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = 0; i < shards; i++) {
                float off = shards == 3 && i == 2 ? 0f : i == 0 ? MathHelper.PiOver4 : -MathHelper.PiOver4;
                Vector2 vel = dir.RotatedBy(off) * 7f;
                QueueForm(Main.player[proj.owner], FormShard);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, vel,
                    proj.type, shardDamage, proj.knockBack * 0.4f, proj.owner);
            }
        }
    }
}
