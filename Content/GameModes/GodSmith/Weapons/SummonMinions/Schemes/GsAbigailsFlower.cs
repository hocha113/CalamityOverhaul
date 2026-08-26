using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Projectiles;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonMinions.Schemes
{
    /// <summary>
    /// 阿比盖尔之花「同伴之魂」：独子编制不入阵；
    /// 协同「灵慰领域」= 她的命中点留下灵光圈（圈内全族仆从 +10%，每 90 帧至多一圈）；
    /// 突击 = 对焦点目标 +12%（等价替换原计划的攻击间隔缩短，不碰她的 counter 等级系统）；
    /// 指挥官光环在场时给她灵辉描边。只注册 AbigailMinion，严禁写 AbigailCounter
    /// </summary>
    internal class GsAbigailsFlower : GsMinionScheme
    {
        public override int TargetItemID => ItemID.AbigailsFlower;

        public override string GsFamily => "SummonMinionsA";

        protected override string GsDescFallback =>
            "Companion Soul: Abigail's strikes leave a soothing soul-light ring that empowers your minions inside it; under the assault order she focuses her wrath on the marked foe";

        private static readonly Color SoulTeal = new(120, 226, 210);

        private static readonly GsMinionKit kit = new() {
            Formation = GsFormationKind.Solo,
        };

        protected override GsMinionKit Kit => kit;

        protected override int[] MinionProjTypes => [ProjectileID.AbigailMinion];

        /// <summary>灵慰领域生成冷却（owner 命中路径独占消费）</summary>
        private uint solaceReadyTick;

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        protected override void GsMinionModifyHit(Projectile proj, NPC target,
            ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            //突击技：对焦点目标 +12%
            if (MinionDoctrine.TryGetAssaultTarget(proj.owner, out NPC focus)
                && focus.whoAmI == target.whoAmI) {
                modifiers.FinalDamage *= 1.12f;
            }
        }

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit,
            int damageDone, GodSmithProjRouter router) {
            if (Main.GameUpdateCount < solaceReadyTick) {
                return;
            }
            solaceReadyTick = Main.GameUpdateCount + 90;
            Projectile.NewProjectile(proj.GetSource_FromAI(), target.Center, Vector2.Zero,
                ModContent.ProjectileType<GsSoulSolaceProj>(), 0, 0f, proj.owner);
        }

        public override void GsProjPostDraw(Projectile proj, Color lightColor, GodSmithProjRouter router) {
            //指挥官光环在场：灵辉描边（放大重影表达存在感，不改判定）
            Player owner = Main.player[proj.owner];
            if (!owner.active || !MinionDoctrine.CommanderAuraActive(owner)) {
                return;
            }
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            int frames = Math.Max(1, Main.projFrames[proj.type]);
            Rectangle frame = tex.Frame(1, frames, 0, proj.frame % frames);
            float pulse = 0.55f + 0.2f * (float)Math.Sin(
                Main.GlobalTimeWrappedHourly * 4.2f + proj.identity * 0.83f);
            Color halo = SoulTeal with { A = 0 };
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, frame,
                halo * pulse, proj.rotation, frame.Size() / 2f, proj.scale * 1.15f,
                proj.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0);
        }
    }
}
