using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.SummonSentries.Schemes
{
    /// <summary>
    /// 寒霜九头蛇法杖「九头之怒」：<br/>
    /// 充能分段 9：每 3 层长出一颗幻影副头（至多 2 颗，owner 端可见），
    /// 副头随主头开火以 0.5× 齐射 ±8°；超频 300 帧「极寒吐息」= 每 8 帧追加一发锥形冰息；
    /// 链内哨兵命中附霜（族基类统一实现）。副头只读塔朝向帧，不写其 ai
    /// </summary>
    internal class GsFrostHydraStaff : GsSentryScheme
    {
        public override int TargetItemID => ItemID.StaffoftheFrostHydra;

        protected override int FamilyIdx => GsSentryFamilyIdx.FrostHydra;

        protected override string GsDescFallback =>
            "Deploy doctrine: every 3 charge grows a phantom head (up to 2) that echoes each shot\n" +
            "Right-click when full to overdrive into a freezing breath; linked sentries inflict frostburn";

        private static readonly Color FrostTint = new(150, 215, 250);

        protected override SentryKit BuildKit() => new() {
            TowerTypes = [ProjectileID.FrostHydra],
            BoltTypes = [ProjectileID.FrostBlastFriendly],
            ChargeMax = [9],
            OverdriveDuration = 300,
        };

        public override void GsModifyWeaponDamage(Item item, Player player, ref StatModifier damage)
            => damage *= 1.10f;

        /// <summary>副头数：每 3 层充能一颗，至多 2（充能是 owner 本地量）</summary>
        private static int PhantomHeads(Projectile tower)
            => Math.Min(SentryGrid.StateOf(tower).Charge / 3, 2);

        /// <summary>主头开火时副头齐射：owner 端补发 0.5× 冰弹 ±8°</summary>
        protected override void OnBoltFirstFrame(Projectile bolt, Projectile tower, GsSentryLocal st) {
            if (bolt.type != ProjectileID.FrostBlastFriendly || tower == null
                || !bolt.IsOwnedByLocalPlayer()) {
                return;
            }
            int heads = PhantomHeads(tower);
            for (int i = 0; i < heads; i++) {
                float sway = (i == 0 ? 1f : -1f) * 0.14f;
                SpawnBoltHandled(tower, bolt.Center, bolt.velocity.RotatedBy(sway),
                    bolt.type, bolt.damage / 2, bolt.knockBack, st.OverdriveShot);
            }
        }

        /// <summary>极寒吐息：每 8 帧向最近敌喷一发锥形冰息（owner 端，附加层不改原射击）</summary>
        internal override void OverdrivePulse(Projectile tower, Projectile odProj, int age) {
            if (age % 8 != 0) {
                return;
            }
            NPC target = null;
            float bestDist = 700f;
            foreach (NPC npc in Main.ActiveNPCs) {
                if (!npc.CanBeChasedBy(odProj)) {
                    continue;
                }
                float dist = npc.Center.Distance(tower.Center);
                if (dist < bestDist) {
                    bestDist = dist;
                    target = npc;
                }
            }
            Vector2 dir = target != null
                ? (target.Center - tower.Center).SafeNormalize(Vector2.UnitX * tower.spriteDirection)
                : new Vector2(tower.spriteDirection, -0.1f).SafeNormalize(Vector2.UnitX);
            Vector2 vel = dir.RotatedBy(Main.rand.NextFloat(-0.16f, 0.16f)) * 9f;
            Projectile.NewProjectile(SentrySource(tower), tower.Center + dir * 18f - new Vector2(0f, 8f), vel,
                ModContent.ProjectileType<GsSentryBoltProj>(),
                (int)(tower.damage * 0.4f), 1f, tower.owner, GsSentryBoltProj.StyleFrostBreath);
        }

        /// <summary>幻影副头：owner 端按充能分段绘制塔体重影（远端只看真弹幕齐射）</summary>
        protected override void DrawTowerExtra(Projectile tower, SentryKit kit, GsSentryLocal st, Color lightColor) {
            if (tower.owner != Main.myPlayer) {
                return;
            }
            int heads = PhantomHeads(tower);
            if (heads <= 0) {
                return;
            }
            Main.instance.LoadProjectile(tower.type);
            var tex = Terraria.GameContent.TextureAssets.Projectile[tower.type].Value;
            Rectangle frame = tex.Frame(1, Main.projFrames[tower.type], 0, tower.frame);
            SpriteEffects fx = tower.spriteDirection == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            for (int i = 0; i < heads; i++) {
                float side = i == 0 ? 1f : -1f;
                //identity 相位微浮动，读作悬于主头侧后的冰魄
                float bob = MathF.Sin(Main.GlobalTimeWrappedHourly * 2.2f + tower.identity * 0.7f + i * 2.1f) * 3f;
                Vector2 offset = new(side * 24f, -14f + bob);
                Color ghost = FrostTint * 0.38f;
                ghost.A = 0;
                Main.EntitySpriteDraw(tex, tower.Center + offset - Main.screenPosition, frame, ghost,
                    tower.rotation, frame.Size() * 0.5f, tower.scale * 0.72f, fx, 0);
            }
        }

        /// <summary>副头就位时的塔口寒雾（owner 端轻量提示）</summary>
        protected override void TowerPostAI(Projectile tower, SentryKit kit, GsSentryLocal st) {
            if (VaultUtils.isServer || tower.owner != Main.myPlayer
                || PhantomHeads(tower) <= 0 || Main.GameUpdateCount % 30 != 0) {
                return;
            }
            PRTLoader.NewParticle<PRT_DefFrostGlint>(
                tower.Center + Main.rand.NextVector2Circular(20f, 12f),
                new Vector2(0f, -0.4f), FrostTint, Main.rand.NextFloat(0.3f, 0.5f))?.Configure(Main.rand.Next(12, 20));
        }
    }
}
