using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.DomainSkills;
using CalamityOverhaul.Content.TimeFreezes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    /// <summary>替死娃娃鱼，受伤时冷却内将伤害转给附近敌人</summary>
    internal class FishVoodoo : FishSkill
    {
        public override int UnlockFishID => ItemID.GuideVoodooFish;
        public override int DefaultCooldown => 80 * (60 - HalibutData.GetDomainLayer() * 3); //80 - 3 * 领域等级 秒
        public override int ResearchDuration => 60 * 12;
        //未装备暂停冷却;冷却走完那一帧娃娃重织(纯视觉,宣告就绪)
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (Cooldown == 1 && halibutPlayer.HeldHalibut && player.whoAmI == Main.myPlayer && Active(player)) {
                Projectile.NewProjectile(player.GetSource_Misc("FishVoodooReweave"), player.Center, Vector2.Zero
                    , ModContent.ProjectileType<FishVoodooRitual>(), 0, 0f, player.whoAmI, 1f);
            }
            return halibutPlayer.HeldHalibut;
        }
    }

    /// <summary>替死受伤监听</summary>
    internal class FishVoodooPlayer : ModPlayer
    {
        private const int UnlimitedLayersThreshold = 10; //>=10 层领域时无限替死
        private const int MaxThreads = 12; //缝线演出封顶,超出的目标立即定帧+标记

        private bool OnSet(int damageTaken) {
            if (!TryGetSkill(out FishVoodoo skill, out HalibutPlayer hPlayer)) {
                return false;
            }

            bool unlimited = hPlayer.SeaDomainActive && hPlayer.SeaDomainLayers >= UnlimitedLayersThreshold;
            if (skill.Cooldown > 0 && !unlimited) {
                return false; //冷却中且不是无限模式
            }

            if (damageTaken <= 0) {
                return false;
            }

            List<NPC> targets = null;
            if (hPlayer.SeaDomainActive) {
                targets = GetSeaDomainTargets(Player, out float domainRadius, out Vector2 domainCenter);
                if (targets.Count == 0) {
                    //领域内没有敌人时降级为随机
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

            if (targets == null || targets.Count == 0) {
                return false;
            }

            //回血（抵消 + 奖励气血） 目前设计为 3 倍恢复
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

            //冷却（九层及以上无限替死不进入冷却）
            if (!unlimited) {
                skill.SetCooldown();
            }
            else {
                skill.Cooldown = 0; //保证保持 0
            }

            //演出:凝滞开场 -> 娃娃绕线显形 -> 灵魂缝线针步缝向各目标 -> 落点针刺定帧 -> 娃娃自燃成灰
            //标记随缝线到达生成,伤害结算仍在上方即时完成
            PlayTriggerEffects(Player);
            int threadBudget = MaxThreads;
            foreach (var npc in targets) {
                if (!npc.active) continue;
                if (threadBudget > 0) {
                    threadBudget--;
                    if (Main.myPlayer == Player.whoAmI) {
                        Vector2 dollPos = Player.Center + new Vector2(-Player.direction * 16f, -54f);
                        Projectile.NewProjectile(Player.GetSource_Misc("FishVoodoo"), dollPos, Vector2.Zero
                            , ModContent.ProjectileType<FishVoodooThread>(), 0, 0f, Player.whoAmI, npc.whoAmI, Main.rand.Next(4));
                    }
                }
                else {
                    TimeFreezeSystem.RefreshNPC<FishVoodoo>(npc, 4);
                    SpawnMarkProjectile(npc);
                }
            }
            SpawnRitual();

            Player.GivePlayerImmuneState(60);//给个短暂的无敌防止被秒

            return true;
        }

        public override bool PreKill(double damage, int hitDirection, bool pvp
            , ref bool playSound, ref bool genDust, ref PlayerDeathReason damageSource) {
            if (OnSet((int)damage)) {
                return false;
            }
            return true;
        }

        private static List<NPC> GetSeaDomainTargets(Player player, out float radius, out Vector2 center) {
            radius = 0f; center = player.Center;
            List<NPC> list = new();
            //寻找玩家的 SeaDomainProj
            int projType = ModContent.ProjectileType<SeaDomainProj>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile pr = Main.projectile[i];
                if (!pr.active || pr.owner != player.whoAmI || pr.type != projType) continue;
                center = pr.Center;
                if (pr.ModProjectile is SeaDomainProj sea) {
                    radius = Math.Max(radius, sea.GetMaxRadius());
                }
            }
            if (radius <= 0f) radius = 800f; //兜底半径
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
            if (hPlayer == null) {
                return false;
            }
            if (!FishSkill.UnlockFishs.TryGetValue(ItemID.GuideVoodooFish, out FishSkill fs)) {
                return false;
            }
            if (fs is not FishVoodoo fv) {
                return false;
            }
            if (!fs.Active(Player)) {
                return false;
            }
            skill = fv;
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

        /// <summary>触发一拍:双层音 + 相机 punch + 少量暗影布尘底噪(英雄时刻交给仪式弹幕与缝线)</summary>
        private static void PlayTriggerEffects(Player player) {
            SoundEngine.PlaySound(SoundID.Item74 with { Volume = 0.55f, Pitch = -0.3f }, player.Center);
            SoundEngine.PlaySound(SoundID.NPCDeath52 with { Volume = 0.4f, Pitch = -0.1f }, player.Center);
            if (!VaultUtils.isServer) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(player.Center
                    , Main.rand.NextVector2Unit(), 3f, 6f, 10, 800f, "FishVoodoo"));
            }
            for (int i = 0; i < 10; i++) {
                Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f);
                Vector2 pos = player.Center + Main.rand.NextVector2CircularEdge(26f, 30f);
                int dustId = Dust.NewDust(pos, 0, 0, DustID.Shadowflame, vel.X, vel.Y, 160, default, Main.rand.NextFloat(0.7f, 1.05f));
                Main.dust[dustId].noGravity = true;
            }
        }

        /// <summary>仪式娃娃(mode0),重复触发时旧仪式让位</summary>
        private void SpawnRitual() {
            if (Main.myPlayer != Player.whoAmI) {
                return;
            }
            int type = ModContent.ProjectileType<FishVoodooRitual>();
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.type == type && p.ai[0] == 0f) {
                    p.Kill();
                }
            }
            Projectile.NewProjectile(Player.GetSource_Misc("FishVoodoo"), Player.Center, Vector2.Zero
                , type, 0, 0f, Player.whoAmI, 0f);
        }

        private void SpawnMarkProjectile(NPC target) {
            if (Main.myPlayer != Player.whoAmI)
                return;
            Projectile.NewProjectile(Player.GetSource_Misc("FishVoodoo"), target.Center, Vector2.Zero
                , ModContent.ProjectileType<FishVoodooMark>(), 0, 0f, Player.whoAmI, target.whoAmI);
        }
    }

    /// <summary>
    /// 被缝中的敌人头顶吊起一具小布偶:吊线阻尼摆随宿主移动晃荡,
    /// 针尾余烬按节拍明灭,到期散作灰烬与断纤(伤害转移的滞留证据)
    /// </summary>
    internal class FishVoodooMark : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        private float sway;
        private float swayVel;

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

            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4.2f + Projectile.whoAmI) * 3f;
            Projectile.Center = npc.Top + new Vector2(0, -32 + bob);
            //吊坠阻尼摆:宿主横向速度激励
            swayVel += -npc.velocity.X * 0.014f - sway * 0.11f;
            swayVel *= 0.88f;
            sway = MathHelper.Clamp(sway + swayVel, -0.65f, 0.65f);
            Projectile.rotation = sway;
        }

        public override void OnKill(int timeLeft) {
            for (int i = 0; i < 2; i++) {
                PRTLoader.NewParticle<PRT_FishVoodooAsh>(Projectile.Center + Main.rand.NextVector2Circular(5f, 7f)
                    , new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.7f, -0.3f))
                    , Color.White, 0.7f)?.Configure(Main.rand.Next(36, 52), 0.5f);
            }
            PRTLoader.NewParticle<PRT_FishVoodooFiber>(Projectile.Center
                , new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), 0.2f), Color.White, 0.8f)?.Configure(28);
        }

        public override bool PreDraw(ref Color lightColor) {
            float age = 60f - Projectile.timeLeft;
            float alpha = MathHelper.Clamp(age / 5f, 0f, 1f) * MathHelper.Clamp(Projectile.timeLeft / 10f, 0f, 1f);
            if (alpha <= 0.02f) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 dollC = Projectile.Center;
            Color light = Lighting.GetColor(dollC.ToTileCoordinates());
            float lightMul = 0.5f + 0.5f * (light.R + light.G + light.B) / 765f;

            //吊线:上锚点到布偶顶,3 段虚线随摆倾斜微垂
            Vector2 hang = dollC + new Vector2(-sway * 14f, -22f);
            Vector2 dollTop = dollC + (Projectile.rotation - MathHelper.PiOver2).ToRotationVector2() * 12f;
            for (int i = 0; i < 3; i++) {
                float t0 = i / 3f;
                Vector2 a = Vector2.Lerp(hang, dollTop, t0);
                Vector2 b = Vector2.Lerp(hang, dollTop, t0 + 0.24f);
                float sag = (float)Math.Sin(t0 * MathHelper.Pi) * 1.6f * sway;
                a.X += sag;
                b.X += sag;
                FishVoodooArt.DrawLine(sb, a, b, FishVoodooArt.ThreadDark * (0.85f * alpha), 2f);
                FishVoodooArt.DrawLine(sb, a, b, FishVoodooArt.ThreadCrimson * (0.7f * alpha), 1f);
            }

            FishVoodooArt.DrawEffigy(sb, dollC, 0.55f, Projectile.rotation, alpha, lightMul);

            //针尾余烬:每 9 帧亮 2 帧,唯一加色点
            if ((int)age % 9 < 2) {
                Texture2D glow = FishVoodooAssets.Glow?.Value;
                if (glow != null) {
                    Vector2 tipPos = dollC + new Vector2(7f, 3f).RotatedBy(Projectile.rotation) * 0.55f;
                    sb.Draw(glow, tipPos - Main.screenPosition, null, new Color(220, 110, 60, 0) * (0.7f * alpha)
                        , 0f, glow.Size() / 2f, 0.06f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
