using CalamityOverhaul.Content.GameModes.GodSmith.Framework;
using CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing.Projectiles;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.GameModes.GodSmith.Weapons.Throwing
{
    /// <summary>手里剑:连投 ≥5 层每次掷出双星(副星免费),贴墙钉入可捡回,暴击直接返还</summary>
    internal class GsShuriken : GsThrowScheme
    {
        public override int TargetItemID => ItemID.Shuriken;
        protected override string GsDescFallback =>
            "Reforged: 15% chance not to consume; crits refund one; misses stuck in walls can be picked back up\nAt 5 combo stacks every throw splits off a free twin star";

        protected override float NoConsumeChance => 0.15f;
        protected override float RecoverOnTileChance => 0.35f;
        protected override float RecoverOnFadeChance => 0.15f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.10f;

        /// <summary>正在生成副星(OnSpawn 扩展点消费,myPlayer 契约)</summary>
        private bool pendingEcho;

        protected override bool? GsThrowShoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source,
            Vector2 position, Vector2 velocity, int type, int damage, float knockback) {
            if (player.GetModPlayer<GsThrowPlayer>().ComboFor(item.type) >= 5) {
                //双星:副星免费,0.6 倍伤,±7 度
                pendingEcho = true;
                Vector2 v = velocity.RotatedBy(MathHelper.ToRadians(Main.rand.NextBool() ? 7f : -7f));
                Projectile.NewProjectile(source, position, v, type,
                    (int)(damage * 0.6f), knockback * 0.5f, player.whoAmI);
                pendingEcho = false;
            }
            return null;
        }

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            if (pendingEcho) {
                //副星不参与任何回收通道,远端按 MarkData2 画半透明
                st.IsPrimary = false;
                router.MarkData2 = 1f;
            }
        }

        public override bool? GsProjPreDraw(Projectile proj, ref Color lightColor, GodSmithProjRouter router) {
            if (router.MarkData2 != 1f) {
                return null;
            }
            //副星:六成透明的星影
            Main.instance.LoadProjectile(proj.type);
            Texture2D tex = TextureAssets.Projectile[proj.type].Value;
            Main.EntitySpriteDraw(tex, proj.Center - Main.screenPosition, null, lightColor * 0.6f,
                proj.rotation, tex.Size() / 2f, proj.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    /// <summary>投刀:命中后短窗内下一刀更疼(势头,叠 3),贴墙高回收,暴击返还</summary>
    internal class GsThrowingKnife : GsThrowScheme
    {
        public override int TargetItemID => ItemID.ThrowingKnife;
        protected override string GsDescFallback =>
            "Reforged: crits refund one; knives stuck in walls often survive to be reclaimed\nEach hit sharpens the next knife within 0.8s, +12% up to 3 stacks";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.40f;
        protected override float RecoverOnFadeChance => 0.15f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.08f;

        //势头:myPlayer 契约字段(写在命中钩子,读在射击链,均为 owner 端)
        private int momentumStacks;
        private uint momentumUntil;

        protected override void GsThrowModifyShoot(Item item, Player player, ref Vector2 position,
            ref Vector2 velocity, ref int type, ref int damage, ref float knockback) {
            if (momentumStacks > 0 && Main.GameUpdateCount <= momentumUntil) {
                damage = (int)(damage * (1f + 0.12f * momentumStacks));
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary) {
                return;
            }
            momentumStacks = Main.GameUpdateCount <= momentumUntil ? System.Math.Min(3, momentumStacks + 1) : 1;
            momentumUntil = Main.GameUpdateCount + 48;
        }
    }

    /// <summary>毒刀:命中叠毒延时,6 层触发腐蚀(本玩家对其 +5 穿甲 8s),贴墙高回收</summary>
    internal class GsPoisonedKnife : GsThrowScheme
    {
        public override int TargetItemID => ItemID.PoisonedKnife;
        protected override string GsDescFallback =>
            "Reforged: crits refund one; wall-stuck knives can be reclaimed\nHits stack venom, extending poison; at 6 stacks the target corrodes, taking your knives 5 armor deeper for 8s";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.40f;
        protected override float RecoverOnFadeChance => 0.15f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.08f;

        protected override void GsThrowModifyHit(Projectile proj, NPC target, ref NPC.HitModifiers modifiers, GodSmithProjRouter router) {
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (Main.GameUpdateCount <= gn.CorrodeUntil) {
                //腐蚀:攻击方本地量,本玩家的刀吃穿甲
                modifiers.ArmorPenetration += 5f;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly) {
                return;
            }
            GsThrowGlobalNPC gn = target.GetGlobalNPC<GsThrowGlobalNPC>();
            if (Main.GameUpdateCount > gn.PoisonWindowUntil) {
                gn.PoisonStacks = 0;
            }
            gn.PoisonStacks = System.Math.Min(6, gn.PoisonStacks + 1);
            gn.PoisonWindowUntil = Main.GameUpdateCount + 300;
            //叠毒:每层把中毒续得更长(AddBuff 自动同步)
            target.AddBuff(BuffID.Poisoned, 120 + 120 * gn.PoisonStacks);
            if (gn.PoisonStacks >= 6) {
                gn.PoisonStacks = 0;
                gn.CorrodeUntil = Main.GameUpdateCount + 480;
                if (!VaultUtils.isServer) {
                    SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = 0.6f }, target.Center);
                    for (int i = 0; i < 6; i++) {
                        PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(10f, 10f),
                            Main.rand.NextVector2Circular(1.6f, 1.6f) - Vector2.UnitY,
                            new Color(150, 220, 60), Main.rand.NextFloat(0.25f, 0.42f))?.Configure(false, 18);
                    }
                }
            }
        }
    }

    /// <summary>骨投刀:同一目标嵌满 4 支全部炸裂并必掉一枚回收体,暴击返还</summary>
    internal class GsBoneDagger : GsThrowScheme
    {
        public override int TargetItemID => ItemID.BoneDagger;
        protected override string GsDescFallback =>
            "Reforged: crits refund one; strays can be reclaimed\nEmbed 4 daggers in the same foe and they all shatter for 60% each, always dropping one recovery pickup";

        protected override float NoConsumeChance => 0.10f;
        protected override float RecoverOnTileChance => 0.30f;
        protected override float RecoverOnFadeChance => 0.25f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.06f;

        protected override float RecoverChanceOnKill(Projectile proj, int timeLeft, GsThrowProjState st, bool diedOnHit)
            //嵌在敌身上的刀不走常规回收(炸裂路径已补必掉)
            => IsStuck(proj) ? 0f : base.RecoverChanceOnKill(proj, timeLeft, st, diedOnHit);

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || target.friendly || !target.active) {
                return;
            }
            //嵌入计数:纯几何,原版嵌入弹幕 ai[0]==1 / ai[1]=目标编号;本支若尚未挂上也算一支
            int count = CountStuckOn(target, proj.owner, proj.type) + (IsStuck(proj) ? 0 : 1);
            if (count < 4) {
                return;
            }
            int burstType = ModContent.ProjectileType<GsBurstProj>();
            int burstDamage = (int)(proj.damage * 0.6f);
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == proj.type && p.owner == proj.owner
                    && p.ai[0] == 1f && (int)p.ai[1] == target.whoAmI) {
                    Projectile.NewProjectile(proj.GetSource_FromThis(), p.Center, Vector2.Zero,
                        burstType, burstDamage, 3f, proj.owner, 60f, GsBurstProj.FxNone);
                    p.Kill();
                }
            }
            Projectile.NewProjectile(proj.GetSource_FromThis(), target.Center, Vector2.Zero,
                burstType, burstDamage, 3f, proj.owner, 60f, GsBurstProj.FxNone);
            //炸裂必掉一枚回收体
            SpawnRecoveryAt(proj.GetSource_FromThis(), target.Center, proj.owner);
            if (!VaultUtils.isServer) {
                SoundEngine.PlaySound(SoundID.Item51 with { Volume = 0.8f, Pitch = -0.2f }, target.Center);
                for (int i = 0; i < 8; i++) {
                    //骨屑迸散
                    PRTLoader.NewParticle<PRT_Spark>(target.Center + Main.rand.NextVector2Circular(12f, 12f),
                        Main.rand.NextVector2Circular(3.5f, 3.5f),
                        new Color(236, 230, 210), Main.rand.NextFloat(0.28f, 0.46f))?.Configure(true, 20);
                }
            }
            proj.Kill();
        }
    }

    /// <summary>八角茴香:敌间弹射两次(每跳 -15%),暴击必返还</summary>
    internal class GsStarAnise : GsThrowScheme
    {
        public override int TargetItemID => ItemID.StarAnise;
        protected override string GsDescFallback =>
            "Reforged: crits always refund one; strays can be reclaimed\nRicochets to a nearby foe up to twice, losing 15% per hop";

        protected override float NoConsumeChance => 0.15f;
        protected override float RecoverOnTileChance => 0.35f;
        protected override float RecoverOnFadeChance => 0.15f;
        protected override bool CritRefund => true;
        protected override float DamageMul => 1.12f;

        protected override void GsThrowOnSpawn(Projectile proj, GodSmithProjRouter router, GsThrowProjState st) {
            //弹射需要额外穿透;>0 守卫防 -1 无限穿被写坏
            if (proj.penetrate > 0) {
                proj.penetrate += 2;
            }
        }

        protected override void GsThrowOnHit(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone,
            GodSmithProjRouter router, GsThrowProjState st) {
            if (proj.owner != Main.myPlayer || !st.IsPrimary || st.Custom >= 2 || !proj.active) {
                return;
            }
            //找 260px 内最近的另一个可追敌
            NPC next = null;
            float best = 260f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.whoAmI == target.whoAmI || !npc.CanBeChasedBy()) {
                    continue;
                }
                float d = proj.Distance(npc.Center);
                if (d < best) {
                    best = d;
                    next = npc;
                }
            }
            if (next == null) {
                return;
            }
            st.Custom++;
            proj.velocity = (next.Center - proj.Center).SafeNormalize(Vector2.UnitX) * proj.velocity.Length();
            proj.damage = (int)(proj.damage * 0.85f);
            proj.netUpdate = true;
            if (!VaultUtils.isServer) {
                PRTLoader.NewParticle<PRT_Sparkle>(proj.Center, Vector2.Zero,
                    new Color(255, 200, 90), 0.4f)?.Configure(new Color(255, 200, 90), 12, 0.06f, 0.6f);
            }
        }
    }
}
