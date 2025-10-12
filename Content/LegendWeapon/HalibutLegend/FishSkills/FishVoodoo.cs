using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.Skills;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>
    /// 替死娃娃鱼技能：被动玩家受到伤害时，若技能冷却完成，
    /// 取消这次伤害并把最终伤害转移到附近随机敌人
    /// </summary>
    internal class FishVoodoo : FishSkill
    {
        public override int UnlockFishID => ItemID.GuideVoodooFish;
        public override int DefaultCooldown => 60 * (60 - HalibutData.GetDomainLayer() * 3); // 60 - 3 * 领域等级 秒
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) => halibutPlayer.HasHalibut;//未装备暂停冷却
    }

    /// <summary>
    /// 监听玩家受伤并触发替死效果
    /// </summary>
    internal class FishVoodooPlayer : ModPlayer
    {
        private bool triggerThisHit;
        private const int UnlimitedLayersThreshold = 9; // >=9 层领域时无限替死

        public override void ModifyHurt(ref Player.HurtModifiers modifiers) {
            // 不在此处改写伤害（避免和其它 Mod 计算冲突），仅预判是否要触发
            triggerThisHit = false;

            if (!Player.active || Player.dead)
                return;

            if (!TryGetSkill(out FishVoodoo skill, out HalibutPlayer hPlayer))
                return;

            // 不检查冷却（在 OnHurt 决定），标记可尝试
            triggerThisHit = true;
        }

        public override void OnHurt(Player.HurtInfo info) {
            if (!triggerThisHit)
                return;

            if (!TryGetSkill(out FishVoodoo skill, out HalibutPlayer hPlayer))
                return;

            bool unlimited = hPlayer.SeaDomainActive && hPlayer.SeaDomainLayers >= UnlimitedLayersThreshold;
            if (skill.Cooldown > 0 && !unlimited) {
                triggerThisHit = false;
                return; // 冷却中且不是无限模式
            }


            int damageTaken = info.Damage; // 已经过防御后的真实损失
            if (damageTaken <= 0)
                return;

            List<NPC> targets = null;
            if (hPlayer.SeaDomainActive) {
                targets = GetSeaDomainTargets(Player, out float domainRadius, out Vector2 domainCenter);
                if (targets.Count == 0) {
                    // 领域内没有敌人时降级为随机
                    NPC lone = PickRedirectTarget(Player.Center, 800f);
                    if (lone != null) targets.Add(lone);
                }
            }
            else {
                NPC target = PickRedirectTarget(Player.Center, 800f);
                if (target != null) {
                    targets = new List<NPC> { target };
                }
            }

            if (targets == null || targets.Count == 0)
                return;

            // 回血（抵消 + 奖励气血） 目前设计为 3 倍恢复
            Player.statLife += damageTaken * 3;
            if (Player.statLife > Player.statLifeMax2)
                Player.statLife = Player.statLifeMax2;
            Player.HealEffect(damageTaken, true);
            Player.immune = true;
            Player.immuneTime = Math.Max(Player.immuneTime, 30);

            int dir = Player.direction;
            foreach (var npc in targets) {
                if (!npc.active) continue;
                NPC.HitInfo hit = new NPC.HitInfo {
                    Damage = damageTaken,
                    Knockback = 0f,
                    HitDirection = dir,
                    Crit = false
                };
                npc.StrikeNPC(hit);
            }

            // 冷却（九层及以上无限替死不进入冷却）
            if (!unlimited) {
                skill.SetCooldown();
            }
            else {
                skill.Cooldown = 0; // 保证保持 0
            }

            // 演出：玩家中心聚合 -> 每个目标分裂
            foreach (var npc in targets) {
                PlayAbsorbEffects(Player, npc, damageTaken / targets.Count);
                SpawnLinkDust(Player.Center, npc.Center);
                SpawnTargetImpact(npc);
                SpawnMarkProjectile(npc);
            }
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (triggerThisHit) {
                return false; //触发时免死
            }
            return base.PreKill(damage, hitDirection, pvp, ref playSound, ref genDust, ref damageSource);
        }

        private static List<NPC> GetSeaDomainTargets(Player player, out float radius, out Vector2 center) {
            radius = 0f; center = player.Center;
            List<NPC> list = new();
            // 寻找玩家的 SeaDomainProj
            int projType = ModContent.ProjectileType<SeaDomainProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile pr = Main.projectile[i];
                if (!pr.active || pr.owner != player.whoAmI || pr.type != projType) continue;
                center = pr.Center;
                if (pr.ModProjectile is SeaDomainProj sea) {
                    radius = Math.Max(radius, sea.GetMaxRadius());
                }
            }
            if (radius <= 0f) radius = 800f; // 兜底半径
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.lifeMax <= 5 || npc.dontTakeDamage) continue;
                if (!npc.CanBeChasedBy(player)) continue;
                if (Vector2.Distance(center, npc.Center) <= radius) {
                    list.Add(npc);
                }
            }
            return list;
        }

        private bool TryGetSkill(out FishVoodoo skill, out HalibutPlayer hPlayer) {
            skill = null;
            hPlayer = Player.GetOverride<HalibutPlayer>();
            if (hPlayer == null || !hPlayer.HasHalibut)
                return false;
            if (!FishSkill.UnlockFishs.TryGetValue(ItemID.GuideVoodooFish, out FishSkill fs))
                return false;
            skill = fs as FishVoodoo;
            if (skill == null)
                return false;
            if (!HalibutPlayer.UnlockedSkills.Contains(skill))
                return false;
            return true;
        }

        private NPC PickRedirectTarget(Vector2 center, float maxDistance) {
            List<int> candidates = new();
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.lifeMax <= 5 || npc.dontTakeDamage)
                    continue;
                if (!npc.CanBeChasedBy(Player))
                    continue;
                if (Vector2.Distance(center, npc.Center) > maxDistance)
                    continue;
                candidates.Add(i);
            }
            if (candidates.Count == 0)
                return null;
            int pick = Main.rand.Next(candidates.Count);
            return Main.npc[candidates[pick]];
        }

        private void PlayAbsorbEffects(Player player, NPC target, int dmgShare) {
            // 玩家周围暗影散裂
            for (int i = 0; i < 24; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(5f, 5f);
                Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(32f, 32f);
                int dustId = Dust.NewDust(pos, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 120, default, Main.rand.NextFloat(0.9f, 1.4f));
                Main.dust[dustId].noGravity = true;
            }
            for (int i = 0; i < 12; i++) {
                Vector2 dir = (target.Center - player.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.6f);
                Vector2 vel = dir * Main.rand.NextFloat(4f, 9f);
                int dustId = Dust.NewDust(player.Center, 0, 0, DustID.PurpleTorch, vel.X, vel.Y, 80, default, Main.rand.NextFloat(0.8f, 1.2f));
                Main.dust[dustId].noGravity = true;
            }
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.35f, Pitch = 0.2f }, target.Center);
        }

        private void SpawnLinkDust(Vector2 from, Vector2 to) {
            int steps = 18;
            for (int i = 0; i <= steps; i++) {
                float t = i / (float)steps;
                Vector2 pos = Vector2.Lerp(from, to, t) + Main.rand.NextVector2Circular(5f, 5f);
                Vector2 vel = (to - from).SafeNormalize(Vector2.Zero).RotatedByRandom(0.4f) * Main.rand.NextFloat(0.5f, 2f);
                int dustId = Dust.NewDust(pos, 0, 0, DustID.Clentaminator_Purple, vel.X, vel.Y, 150, default, Main.rand.NextFloat(0.7f, 1.3f));
                Main.dust[dustId].noGravity = true;
            }
        }

        private void SpawnTargetImpact(NPC target) {
            for (int i = 0; i < 30; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                int dustId = Dust.NewDust(target.Center, 0, 0, DustID.Clentaminator_Purple, vel.X, vel.Y, 60, default, Main.rand.NextFloat(1.1f, 1.6f));
                Main.dust[dustId].noGravity = true;
            }
            for (int i = 0; i < 18; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                int dustId = Dust.NewDust(target.Center, 0, 0, DustID.MagicMirror, vel.X, vel.Y, 120, default, Main.rand.NextFloat(0.8f, 1.3f));
                Main.dust[dustId].noGravity = true;
            }
        }

        private void SpawnMarkProjectile(NPC target) {
            if (Main.myPlayer != Player.whoAmI)
                return;
            Projectile.NewProjectile(Player.GetSource_Misc("FishVoodoo"), target.Center, Vector2.Zero
                , ModContent.ProjectileType<FishVoodooMark>(), 0, 0f, Player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>
    /// 被标记的敌人头顶显示一个巫毒娃娃鱼视觉效果
    /// </summary>
    internal class FishVoodooMark : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder; // 实际绘制时改用鱼的贴图

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.timeLeft = 60;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.friendly = false;
            Projectile.hostile = false;
        }

        public override void AI() {
            int npcId = (int)Projectile.ai[0];
            if (npcId < 0 || npcId >= Main.maxNPCs) { Projectile.Kill(); return; }
            NPC npc = Main.npc[npcId];
            if (!npc.active) { Projectile.Kill(); return; }

            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI) * 6f;
            Projectile.Center = npc.Top + new Vector2(0, -30 + bob);
            Projectile.rotation += 0.08f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Main.instance.LoadItem(ItemID.GuideVoodooFish);
            Texture2D tex = TextureAssets.Item[ItemID.GuideVoodooFish].Value;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Rectangle rect = tex.Frame();
            Vector2 origin = rect.Size() / 2f;
            float scale = 1.1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 8f + Projectile.whoAmI) * 0.15f;

            // 发光脉冲圈
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color auraColor = Color.Lerp(Color.MediumPurple, Color.HotPink, (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.5f + 0.5f);
            for (int i = 0; i < 6; i++) {
                float rot = i / 6f * MathHelper.TwoPi + Main.GlobalTimeWrappedHourly * 2f;
                Vector2 off = rot.ToRotationVector2() * 6f;
                Main.spriteBatch.Draw(tex, pos + off, rect, auraColor * 0.25f, Projectile.rotation, origin, scale * 1.15f, SpriteEffects.None, 0f);
            }

            // 主体
            Main.spriteBatch.Draw(tex, pos, rect, Color.White, Projectile.rotation * 0.5f, origin, scale, SpriteEffects.None, 0f);
            // 高亮层
            Main.spriteBatch.Draw(tex, pos, rect, new Color(255, 200, 255, 0) * 0.6f, -Projectile.rotation * 0.7f, origin, scale * 1.05f, SpriteEffects.None, 0f);

            // 下方诅咒光束向下坠落的细线（营造能量）
            for (int i = 0; i < 3; i++) {
                float lineRot = (i / 3f * MathHelper.TwoPi) + Main.GlobalTimeWrappedHourly * 3f;
                Vector2 lineStart = pos + lineRot.ToRotationVector2() * 8f;
                Rectangle lineRect = new Rectangle(0, 0, 1, 1);
                Vector2 lineScale = new Vector2(2f, 24f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + i) * 6f);
                Main.spriteBatch.Draw(pixel, lineStart, lineRect, auraColor * 0.35f, MathHelper.PiOver2, Vector2.Zero, lineScale, SpriteEffects.None, 0f);
            }
            return false;
        }
    }
}
