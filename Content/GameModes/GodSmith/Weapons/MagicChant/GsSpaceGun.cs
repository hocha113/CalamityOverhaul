using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 太空枪重铸：充能节拍。共鸣层即弹计数：正拍 +1、失拍停滞不清；
    /// 每攒满 4 层自动清层，该发升格为「宽束脉冲」（1.8 倍、穿透 2、束宽加倍、
    /// 微微吸附小敌）。流星套 0 蓝语义原样保留，法力经济不动。材质身份：相干光（绿）
    /// </summary>
    internal class GsSpaceGun : GsChantScheme
    {
        public override int TargetItemID => ItemID.SpaceGun;

        protected override string GsDescFallback =>
            "Reforged: every on-beat shot charges the coil, the charge never decays on a miss;\nevery fourth charge discharges as a wide piercing pulse that drags in lesser foes";
        protected override float BaseDamageMult => 1.06f;

        protected override int MaxResonance => 4;

        //充能语义：攒满即放、失拍停滞、免蓝语义不动
        protected override bool EmpowerTriggersInstantly => true;
        protected override bool DecayEnabled => false;
        protected override float OnBeatManaRefund => 0f;

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //宽束脉冲：单发放大增压（形态即强化标，档位随生成包过线）
            int idx = Projectile.NewProjectile(source, position, velocity * 1.1f, type,
                Math.Max(1, (int)(damage * 1.8f)), knockback * 1.4f, player.whoAmI);
            if (idx >= 0 && idx < Main.maxProjectiles) {
                Projectile pulse = Main.projectile[idx];
                pulse.scale *= 2f;
                if (pulse.penetrate > 0) {
                    pulse.penetrate += 2;
                }
                pulse.netUpdate = true;
            }
            return false;
        }

        public override void GsProjPostAI(Projectile proj, GodSmithProjRouter router) {
            bool pulse = router.MarkData == FormEmpower;
            //宽束脉冲微吸附：30px 级近旁小敌被拉向束心（NPC 位移走服务器权威端）
            if (pulse && !VaultUtils.isClient) {
                foreach (NPC npc in Main.ActiveNPCs) {
                    if (npc.boss || !npc.CanBeChasedBy() || npc.knockBackResist <= 0f) {
                        continue;
                    }
                    float dist = Vector2.Distance(npc.Center, proj.Center);
                    if (dist > 46f || dist < 4f) {
                        continue;
                    }
                    Vector2 pull = (proj.Center - npc.Center).SafeNormalize(Vector2.Zero) * 0.7f * npc.knockBackResist;
                    npc.velocity += pull;
                }
            }
        }
    }
}
