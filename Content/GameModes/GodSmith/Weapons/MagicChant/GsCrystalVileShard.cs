using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicConduit;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.MagicChant
{
    /// <summary>
    /// 晶邪碎片重铸：嵌晶引爆。材质身份：粉紫邪晶（脆而锋利的水晶尖矛）。<br/>
    /// ①「嵌晶」：正拍晶矛命中在敌人体内嵌下晶籽；晶矛消亡瞬间所有嵌晶之敌
    /// 炸成十字四向晶芒（0.3 倍短程光线）；<br/>
    /// ②满层强化「晶狱丛生」：以自身为心六向晶矛齐出；③施法有前刺推压体感
    /// </summary>
    internal class GsCrystalVileShard : GsChantScheme
    {
        public override int TargetItemID => ItemID.CrystalVileShard;

        protected override string GsDescFallback =>
            "Reforged: on-beat spears embed crystal seeds; when the spear shatters, every seeded foe erupts in a cross of shard rays\nAt full resonance the next cast raises six crystal spears around you";
        protected override float BaseDamageMult => 1.08f;

        /// <summary>私有形态：嵌晶引爆的十字晶芒</summary>
        private const float FormCrossRay = 10f;

        /// <summary>本弹是否晶矛族延展段（矛干递归延展 / 矛尖收尾）</summary>
        private static bool IsSpearSegment(int projType)
            => projType == ProjectileID.CrystalVileShardShaft || projType == ProjectileID.CrystalVileShardHead;

        //==================== 动画法：前刺推压 ====================

        public override void GsUseStyle(Item item, Player player, Rectangle heldItemFrame) {
            if (player.itemAnimationMax <= 0) {
                return;
            }
            //前刺推压：出手瞬间杖身向瞄准向前压 4px 并下沉，随动画进度回收（绝对剖面 −0.06·p 下压，差分施加防累积漂移）
            float n = player.itemAnimationMax;
            float progress = player.itemAnimation / n;
            player.itemLocation += new Vector2(player.direction, 0.4f) * (4f * progress);
            GsMagicKickMath.ApplyKickDiff(player, -0.06f * progress, -0.06f * ((player.itemAnimation + 1) / n));
        }

        //==================== 强化咏唱：晶狱丛生 ====================

        protected override bool? ChantEmpowerShoot(Item item, Player player, GsChantPlayer chant,
            EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity,
            int type, int damage, float knockback) {
            //六向晶矛齐出（各 0.5 倍）；返回 null 让原版主矛照常刺出（主矛带强化标，照常嵌晶）
            SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.8f, Pitch = -0.15f }, player.Center);
            int spearDamage = Math.Max(1, (int)(damage * 0.5f));
            float speed = velocity.Length();
            float aimRot = velocity.ToRotation();
            for (int i = 0; i < 6; i++) {
                Vector2 dir = (aimRot + MathHelper.TwoPi * i / 6f).ToRotationVector2();
                Projectile.NewProjectile(source, player.MountedCenter, dir * speed,
                    ProjectileID.CrystalVileShardShaft, spearDamage, knockback * 0.6f, player.whoAmI);
            }
            return null;
        }

        //==================== 命中：嵌晶 ====================

        public override void GsProjOnHitNPC(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone, GodSmithProjRouter router) {
            if (!IsSpearSegment(proj.type)) {
                return;
            }
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData == FormCrossRay) {
                return;
            }
            if (router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            //正拍嵌晶：晶籽留在敌人体内，等晶矛碎裂时引爆
            target.GetGlobalNPC<GsCrystalVileShardNPC>().AddSeed(240);
        }

        //==================== 消亡：晶矛碎裂引爆全部嵌晶 ====================

        public override void GsProjOnKill(Projectile proj, int timeLeft, GodSmithProjRouter router) {
            if (!IsSpearSegment(proj.type)) {
                return;
            }
            //嵌晶引爆：任一正拍延展段碎裂即引爆（首段引爆后清籽，后续段消亡找不到籽，天然去重）
            if (!proj.IsOwnedByLocalPlayer() || router.MarkData is not (FormOnBeat or FormEmpower)) {
                return;
            }
            uint now = Main.GameUpdateCount;
            foreach (NPC npc in Main.ActiveNPCs) {
                GsCrystalVileShardNPC seed = npc.GetGlobalNPC<GsCrystalVileShardNPC>();
                if (seed.SeedStacks <= 0 || now >= seed.SeedUntil
                    || Vector2.DistanceSquared(npc.Center, proj.Center) > 1200f * 1200f) {
                    continue;
                }
                seed.ClearSeeds();
                DetonateSeed(proj, npc);
            }
        }

        /// <summary>嵌晶引爆：十字四向晶芒（owner 端生成，全端可见）</summary>
        private void DetonateSeed(Projectile proj, NPC target) {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.75f, Pitch = 0.2f, MaxInstances = 3 }, target.Center);
            int rayDamage = Math.Max(1, (int)(proj.damage * 0.3f));
            for (int i = 0; i < 4; i++) {
                Vector2 dir = (MathHelper.PiOver2 * i).ToRotationVector2();
                QueueForm(Main.player[proj.owner], FormCrossRay);
                Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center + dir * 10f, dir * 0.5f,
                    ProjectileID.CrystalVileShardHead, rayDamage, proj.knockBack * 0.3f, proj.owner);
            }
        }
    }

    /// <summary>
    /// 嵌晶籽标记（攻击方本地量：命中钩子只在攻击方端执行，引爆裁决与可见结果经弹幕过线）
    /// </summary>
    internal class GsCrystalVileShardNPC : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        /// <summary>嵌晶籽层数（引爆时全数清空）</summary>
        internal int SeedStacks;

        /// <summary>晶籽失效时刻</summary>
        internal uint SeedUntil;

        internal void AddSeed(uint durationTicks) {
            if (SeedStacks > 0 && Main.GameUpdateCount >= SeedUntil) {
                SeedStacks = 0;
            }
            SeedStacks++;
            SeedUntil = Main.GameUpdateCount + durationTicks;
        }

        internal void ClearSeeds() {
            SeedStacks = 0;
            SeedUntil = 0;
        }
    }
}
