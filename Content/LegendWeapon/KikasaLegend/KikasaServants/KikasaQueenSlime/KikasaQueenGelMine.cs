using CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaDomains;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.PRT;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.KikasaLegend.KikasaServants.KikasaQueenSlime
{
    /// <summary>
    /// 鬼奴史莱姆皇后的空中晶格雷：一团凝胶被甩向阵位点，飞行中减速、
    /// 内里晶种渐长，到位一拍脆响原地结晶成悬浮血晶棱块
    /// 滞留期微光脉动、缓慢自旋、晶面偶闪；到时或敌人贴近即碎裂成一小扇晶片弹。
    /// 阵位点由 ai[0/1] 随 spawn 包带全；ai[2]=1 为溶解令（皇后遣返/湖塌时下达），
    /// 走"先失光泽再化水"的谢幕而非碎裂。碎裂/收场裁决只在 owner 端，
    /// 远端按本地同规则推进演出、以宽限计时器兜底自杀
    /// </summary>
    internal class KikasaQueenGelMine : ModProjectile
    {
        public override string Texture => CWRConstant.VaultPlaceholder;

        //==================== 时序 ====================

        /// <summary>飞行段上限，到时未至阵位也原地结晶</summary>
        private const int FlyMax = 44;
        /// <summary>结晶落位读数（缩放过冲 + 闪光）</summary>
        private const int SnapFrames = 9;
        /// <summary>晶格滞留时长（滞留数秒）</summary>
        internal const int CrystalHold = 165;
        /// <summary>敌人贴近引信半径（矩形半宽）</summary>
        private const float FuseHalf = 54f;
        private const int MeltLusterFrames = 18;
        private const int MeltDripFrames = 24;

        private const int PhaseFly = 0;
        private const int PhaseCrystal = 1;
        private const int PhaseMelt = 2;

        //==================== 同步量 ====================

        /// <summary>阵位点（spawn 包自带，全程不变）</summary>
        private Vector2 AnchorPoint => new(Projectile.ai[0], Projectile.ai[1]);

        /// <summary>溶解令：1=失泽化水收场（owner 盖章下达）</summary>
        private ref float MeltOrder => ref Projectile.ai[2];

        //==================== 本地状态（各端按同规则推进，owner 裁决生死）====================

        private int phase;
        private int phaseTimer;
        private bool snapFxDone;
        /// <summary>进入化水时晶体已长到的尺寸：飞行中被令化水不许弹回满尺寸</summary>
        private float meltGrow = 1f;
        /// <summary>owner 端主动碎裂过（OnKill 选震屏强度用）</summary>
        private bool burstFired;

        private Player Owner => Main.player[Projectile.owner];

        private float Seed => Projectile.identity * 0.7391f % 5.19f;

        private bool Authority => Main.myPlayer == Projectile.owner;

        public override void SetDefaults() {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.friendly = false;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 460;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.netImportant = true;
        }

        public override bool? CanDamage() => false;

        public override bool? CanCutTiles() => false;

        //==================== 推进 ====================

        public override void AI() {
            Player owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            //溶解令：任意相位可达，幂等；带走当下的晶体成长度
            if ((int)MeltOrder == 1 && phase != PhaseMelt) {
                meltGrow = phase == PhaseFly
                    ? 0.3f + 0.25f * MathHelper.Clamp(phaseTimer / (float)FlyMax, 0f, 1f)
                    : 1f;
                phase = PhaseMelt;
                phaseTimer = 0;
            }

            //湖塌/主人死亡：晶体失去湖的供养，只有 owner 裁决（服务器无领域状态）
            if (Authority && phase != PhaseMelt && phaseTimer % 10 == 0 && !LakeHealthy(owner)) {
                OrderMelt();
            }

            phaseTimer++;
            switch (phase) {
                case PhaseFly: UpdateFly(); break;
                case PhaseCrystal: UpdateCrystal(owner); break;
                case PhaseMelt: UpdateMelt(); break;
            }

            //晶心脉动微光
            float pulse = phase == PhaseCrystal
                ? 0.42f + 0.18f * MathF.Sin(phaseTimer * 0.11f + Seed)
                : 0.3f;
            if (phase == PhaseMelt) {
                pulse *= MathF.Max(0f, 1f - phaseTimer / (float)MeltLusterFrames);
            }
            Lighting.AddLight(Projectile.Center, 0.5f * pulse, 0.18f * pulse, 0.26f * pulse);
        }

        private static bool LakeHealthy(Player owner)
            => !owner.dead
            && owner.TryGetModPlayer(out KikasaDomainPlayer domain)
            && domain.AnyActive
            && domain.Phase != KikasaDomainPhase.Closing
            && domain.RiseT >= 0.9f;

        /// <summary>溶解令下达：owner 盖章同步，远端靠 ai[2] 跟进</summary>
        internal void OrderMelt() {
            if (!Authority || (int)MeltOrder == 1) {
                return;
            }
            MeltOrder = 1;
            phase = PhaseMelt;
            phaseTimer = 0;
            Projectile.netUpdate = true;
        }

        private void UpdateFly() {
            Vector2 to = AnchorPoint - Projectile.Center;
            float dist = to.Length();

            //到位或超时：原地结晶
            if (dist < 10f || phaseTimer >= FlyMax) {
                Projectile.Center = dist < 60f ? AnchorPoint : Projectile.Center;
                Projectile.velocity = Vector2.Zero;
                phase = PhaseCrystal;
                phaseTimer = 0;
                return;
            }

            //趋近减速：比例导引 + 粘滞，读出"飞着飞着慢下来"
            Vector2 desired = to * 0.16f;
            float maxSpeed = MathHelper.Clamp(dist * 0.12f, 2.2f, 15f);
            if (desired.Length() > maxSpeed) {
                desired = desired.SafeNormalize(Vector2.Zero) * maxSpeed;
            }
            Projectile.velocity = Vector2.Lerp(Projectile.velocity, desired, 0.16f);
            Projectile.rotation = Projectile.velocity.X * 0.03f;

            //凝胶失稳甩珠
            if (!Main.dedServ && phaseTimer % 4 == 1) {
                PRTLoader.NewParticle<PRT_GhostRainDrop>(
                    Projectile.Center - Projectile.velocity * 0.5f + Main.rand.NextVector2Circular(6f, 6f),
                    -Projectile.velocity * 0.12f + new Vector2(0f, Main.rand.NextFloat(0.5f, 1.4f)),
                    KikasaQueenSlimeServant.GelBlood * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                    ?.Configure(Main.rand.Next(12, 20), 0f);
            }
        }

        private void UpdateCrystal(Player owner) {
            Projectile.velocity = Vector2.Zero;

            if (!snapFxDone) {
                //结晶拍：脆响落位 + 晶面闪光 + 微环
                snapFxDone = true;
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.5f, Pitch = 0.15f, MaxInstances = 3 }, Projectile.Center);
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.3f, Pitch = 0.3f, MaxInstances = 3 }, Projectile.Center);
                if (!Main.dedServ) {
                    PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Vector2.Zero,
                        KikasaQueenSlimeServant.CrystalGlint, 0.62f)
                        ?.Configure(KikasaQueenSlimeServant.CrystalGlint * 0.6f, 14, 0.05f, 1f);
                    PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                        KikasaQueenSlimeServant.CrystalGlint, 0.04f)
                        ?.Configure(new Vector2(1f, 1f), Seed, 0.14f, 8);
                    for (int i = 0; i < 4; i++) {
                        PRTLoader.NewParticle<PRT_GhostRainDrop>(
                            Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                            new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(1f, 2.4f)),
                            KikasaQueenSlimeServant.GelBlood * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                            ?.Configure(Main.rand.Next(12, 22), 0f);
                    }
                }
            }

            //悬浮：本体定桩，起伏走绘制层；晶面偶闪的碎晶光
            if (!Main.dedServ && phaseTimer % 9 == 4 && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Sparkle>(
                    Projectile.Center + Main.rand.NextVector2Circular(12f, 14f),
                    new Vector2(0f, -Main.rand.NextFloat(0.1f, 0.35f)),
                    KikasaQueenSlimeServant.CrystalGlint * 0.5f, Main.rand.NextFloat(0.18f, 0.3f))
                    ?.Configure(KikasaQueenSlimeServant.CrystalGlint * 0.4f, 12, 0f, 0.6f);
            }

            //碎裂裁决只在 owner：到时 or 敌人贴近
            if (Authority) {
                bool fuse = false;
                if (phaseTimer % 2 == 0) {
                    Rectangle fuseRect = Utils.CenteredRectangle(Projectile.Center, new Vector2(FuseHalf * 2f));
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (npc?.active == true && npc.CanBeChasedBy(Projectile)
                            && npc.Hitbox.Intersects(fuseRect)) {
                            fuse = true;
                            break;
                        }
                    }
                }
                if (fuse || phaseTimer >= CrystalHold) {
                    Burst(owner);
                    return;
                }
            }
            else if (phaseTimer >= CrystalHold + 25) {
                //远端兜底：kill 包丢失也不留永久悬晶
                Projectile.Kill();
            }
        }

        private void UpdateMelt() {
            //失泽段：光熄、自旋停、飞行余速被粘滞收走；化水段：晶体下沉软塌、淌血珠
            Projectile.velocity *= 0.86f;
            if (phaseTimer == 1) {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.3f, Pitch = -0.6f, MaxInstances = 3 }, Projectile.Center);
            }
            if (phaseTimer > MeltLusterFrames) {
                Projectile.velocity.Y = MathF.Min(Projectile.velocity.Y + 0.05f, 1.4f);
                if (!Main.dedServ && phaseTimer % 3 == 0) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(9f, 11f),
                        new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(1.4f, 2.8f)),
                        KikasaQueenSlimeServant.GelBlood * 0.55f, Main.rand.NextFloat(0.32f, 0.55f))
                        ?.Configure(Main.rand.Next(14, 24), 0f);
                }
            }
            int total = MeltLusterFrames + MeltDripFrames;
            if (Authority && phaseTimer >= total) {
                Projectile.Kill();
            }
            else if (!Authority && phaseTimer >= total + 10) {
                Projectile.Kill();
            }
        }

        /// <summary>碎裂：朝最近猎物张开一小扇晶片弹；无猎物则朝正下。只在 owner 端执行</summary>
        private void Burst(Player owner) {
            burstFired = true;
            //扇向：晶格雷自寻最近敌人
            Vector2 aim = Vector2.UnitY;
            float best = 900f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc?.active != true || !npc.CanBeChasedBy(Projectile)) {
                    continue;
                }
                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < best) {
                    best = dist;
                    aim = (npc.Center + npc.velocity * 6f - Projectile.Center).SafeNormalize(Vector2.UnitY);
                }
            }

            int damage = (int)owner.GetTotalDamage(DamageClass.Summon)
                .ApplyTo(KikasaQueenSlimeServant.ShardDamage);
            const int shardCount = 5;
            const float fanHalf = 0.31f;
            for (int i = 0; i < shardCount; i++) {
                float off = -fanHalf + 2f * fanHalf * i / (shardCount - 1);
                Vector2 vel = aim.RotatedBy(off) * 14.5f;
                Projectile.NewProjectile(Projectile.GetSource_FromAI(), Projectile.Center, vel,
                    ModContent.ProjectileType<KikasaQueenCrystalShard>(), damage, 2f, Projectile.owner);
            }
            Projectile.Kill();
        }

        //==================== 谢幕 ====================

        public override void OnKill(int timeLeft) {
            if (Main.dedServ) {
                return;
            }
            if ((int)MeltOrder == 1) {
                //化水收场：一小摊血珠散落，无碎裂
                for (int i = 0; i < 5; i++) {
                    PRTLoader.NewParticle<PRT_GhostRainDrop>(
                        Projectile.Center + Main.rand.NextVector2Circular(10f, 10f),
                        new Vector2(Main.rand.NextFloat(-0.8f, 0.8f), Main.rand.NextFloat(1f, 2.6f)),
                        KikasaQueenSlimeServant.GelBlood * 0.5f, Main.rand.NextFloat(0.3f, 0.5f))
                        ?.Configure(Main.rand.Next(14, 24), 0f);
                }
                SoundEngine.PlaySound(SoundID.Drip with { Volume = 0.45f, Pitch = -0.3f, MaxInstances = 3 }, Projectile.Center);
                return;
            }

            //碎裂收场：晶屑半球 + 闪光 + 玻璃/水晶层叠脆响（各端都跑，队友可见）
            for (int i = 0; i < 11; i++) {
                Vector2 vel = Main.rand.NextVector2Unit() * Main.rand.NextFloat(1.6f, 6.2f);
                vel.Y -= 1.1f;
                PRTLoader.NewParticle<PRT_KikasaQueenSlimeFacet>(
                    Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), vel,
                    Main.rand.NextBool(3) ? KikasaQueenSlimeServant.CrystalDeep : KikasaQueenSlimeServant.GelBlood,
                    Main.rand.NextFloat(0.4f, 0.75f))
                    ?.Configure(Main.rand.Next(22, 36), 0.24f, Main.rand.NextFloat(-0.16f, 0.16f));
            }
            PRTLoader.NewParticle<PRT_DWave>(Projectile.Center, Vector2.Zero,
                KikasaQueenSlimeServant.CrystalGlint, 0.07f)
                ?.Configure(new Vector2(1f, 1f), Seed, 0.26f, 9);
            PRTLoader.NewParticle<PRT_Sparkle>(Projectile.Center, Vector2.Zero,
                KikasaQueenSlimeServant.CrystalGlint, 0.8f)
                ?.Configure(KikasaQueenSlimeServant.CrystalGlint * 0.7f, 12, 0.12f, 1.1f);

            SoundEngine.PlaySound(SoundID.Shatter with { Volume = 0.5f, Pitch = 0.08f, MaxInstances = 3 }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.55f, Pitch = -0.05f, MaxInstances = 3 }, Projectile.Center);
            if (KikasaDomain.Viewed != null && KikasaDomain.Viewed.Player.whoAmI == Projectile.owner
                && Vector2.Distance(Main.LocalPlayer.Center, Projectile.Center) < 900f) {
                Main.LocalPlayer?.CWR()?.GetScreenShake(burstFired ? 1.2f : 0.9f);
            }
        }

        //==================== 绘制 ====================

        public override bool PreDraw(ref Color lightColor) {
            Texture2D glob = CWRAsset.Extra_98?.Value;
            //Extra 贴图在资产初始化时已全量加载，直接取用
            Texture2D crystal = TextureAssets.Extra[186]?.Value;
            if (glob == null || crystal == null) {
                return false;
            }
            SpriteBatch sb = Main.spriteBatch;
            Vector2 pos = Projectile.Center - Main.screenPosition;
            Vector2 globOrigin = glob.Size() * 0.5f;
            Vector2 crysOrigin = crystal.Size() * 0.5f;

            //滞留期悬浮起伏走绘制层，判定桩不动
            float bob = phase == PhaseCrystal ? MathF.Sin(Main.GlobalTimeWrappedHourly * 2.3f + Seed) * 3f : 0f;
            Vector2 drawPos = pos + new Vector2(0f, bob);

            //晶体成长度：飞行期是凝胶里的晶种，结晶拍长满；化水期停在被打断时的尺寸
            float crysGrow = phase switch {
                PhaseFly => 0.3f + 0.25f * MathHelper.Clamp(phaseTimer / (float)FlyMax, 0f, 1f),
                PhaseMelt => meltGrow,
                _ => 1f,
            };
            //结晶落位过冲：1.24 → 1
            if (phase == PhaseCrystal && phaseTimer < SnapFrames) {
                float k = 1f - phaseTimer / (float)SnapFrames;
                crysGrow = 1f + 0.24f * k * k;
            }

            //光泽：失泽段熄灭
            float luster = phase == PhaseMelt
                ? MathF.Max(0f, 1f - phaseTimer / (float)MeltLusterFrames)
                : 1f;
            //化水段：整体软塌下沉
            float meltSag = phase == PhaseMelt
                ? MathHelper.Clamp((phaseTimer - MeltLusterFrames) / (float)MeltDripFrames, 0f, 1f)
                : 0f;
            float alpha = 1f - meltSag * 0.95f;

            //凝胶壳：飞行期饱满、结晶后化作薄薄一层湿膜、化水期回涨
            float gelA = phase == PhaseFly ? 0.9f : 0.28f + meltSag * 0.5f;
            float stretch = MathHelper.Clamp(Projectile.velocity.Length() * 0.04f, 0f, 0.5f);
            float wob = MathF.Sin(phaseTimer * 0.5f + Seed * 6f) * 0.1f;
            Vector2 jiggle = new(1f + wob, 1f - wob * 0.8f);
            sb.Draw(glob, drawPos, null, KikasaQueenSlimeServant.CrystalDeep * (0.7f * gelA * alpha),
                Projectile.rotation, globOrigin, new Vector2(0.4f, 0.42f + stretch * 0.6f) * jiggle, SpriteEffects.None, 0f);
            sb.Draw(glob, drawPos, null, KikasaQueenSlimeServant.GelBlood * (0.85f * gelA * alpha),
                Projectile.rotation, globOrigin, new Vector2(0.3f, 0.34f + stretch * 0.5f) * jiggle, SpriteEffects.None, 0f);

            //血晶棱块：缓慢自旋；化水期纵向软塌
            float spin = phase == PhaseCrystal ? Seed + phaseTimer * 0.016f
                : phase == PhaseMelt ? Seed + CrystalHold * 0.016f
                : Projectile.rotation;
            Vector2 crysScale = new Vector2(0.72f, 0.72f * (1f - meltSag * 0.35f)) * crysGrow;
            Color crysBody = Color.Lerp(KikasaQueenSlimeServant.GelBlood,
                new Color(60, 18, 30), 0.25f + meltSag * 0.5f) * alpha;
            sb.Draw(crystal, drawPos, null, crysBody, spin, crysOrigin, crysScale, SpriteEffects.None, 0f);

            //晶面高光层（A=0 加色）：脉动内芯 + 偶发折面锐光，失泽即熄
            if (luster > 0.03f) {
                Texture2D soft = CWRAsset.SoftGlow?.Value;
                float pulse = 0.35f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 3.4f + Seed * 2f);
                if (soft != null) {
                    sb.Draw(soft, drawPos, null, KikasaQueenSlimeServant.CrystalCore * (pulse * luster * crysGrow * alpha),
                        0f, soft.Size() * 0.5f, new Vector2(46f * crysGrow / soft.Width * 2f), SpriteEffects.None, 0f);
                }
                sb.Draw(crystal, drawPos, null, (KikasaQueenSlimeServant.CrystalGlint with { A = 0 }) * (0.4f * pulse * luster * alpha),
                    spin, crysOrigin, crysScale, SpriteEffects.None, 0f);

                float tw = MathF.Sin(Main.GlobalTimeWrappedHourly * 7.5f + Seed * 3f);
                float flash = MathF.Max(0f, tw);
                flash = flash * flash * flash * flash;
                Texture2D star = CWRAsset.StarGlow01?.Value;
                if (star != null && flash > 0.15f && phase == PhaseCrystal) {
                    Vector2 facet = drawPos + (Seed + spin).ToRotationVector2() * 9f * crysGrow;
                    sb.Draw(star, facet, null, KikasaQueenSlimeServant.CrystalCore * (flash * 0.85f * luster),
                        spin, star.Size() * 0.5f, 0.24f, SpriteEffects.None, 0f);
                }
            }
            return false;
        }
    }
}
