using CalamityOverhaul.Common;
using CalamityOverhaul.Content.PRTTypes;
using InnoVault.GameContent.BaseEntity;
using InnoVault.PRT;
using InnoVault.Trails;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.LegendWeapon.HalibutLegend.FishSkills
{
    internal class FishRock : FishSkill
    {
        public override int UnlockFishID => ItemID.Rockfish;
        public override int DefaultCooldown => 180 - HalibutData.GetDomainLayer() * 9;
        public override int ResearchDuration => 60 * 16;
        public override bool UpdateCooldown(HalibutPlayer halibutPlayer, Player player) {
            if (!Active(player)) {
                return false;
            }

            if (Cooldown <= 0) {
                NPC target = player.Center.FindClosestNPC(800f);
                ShootState shootState = player.GetShootState();

                if (target != null) {
                    SetCooldown();

                    //从玩家头顶生成，向目标俯冲砸落
                    int hammerProj = Projectile.NewProjectile(
                        shootState.Source,
                        player.Center + new Vector2(0, -150),
                        Vector2.Zero,
                        ModContent.ProjectileType<RockHammerFish>(),
                        (int)(shootState.WeaponDamage * (3.6f + HalibutData.GetDomainLayer() * 1.2f)),
                        shootState.WeaponKnockback * 3f,
                        player.whoAmI,
                        ai0: target.whoAmI
                    );

                    if (hammerProj >= 0) {
                        SpawnSummonEffect(player.Center + new Vector2(0, -150));
                        SoundEngine.PlaySound(SoundID.Item70 with { Volume = 0.7f, Pitch = -0.3f }, player.Center);
                    }
                }
            }
            return base.UpdateCooldown(halibutPlayer, player);
        }

        private static void SpawnSummonEffect(Vector2 position) {
            if (VaultUtils.isServer) {
                return;
            }
            //汇聚的余烬环：暗示"凝实"而非凭空出现
            for (int i = 0; i < 18; i++) {
                float angle = MathHelper.TwoPi * i / 18f;
                Vector2 from = position + angle.ToRotationVector2() * Main.rand.NextFloat(60f, 90f);
                Vector2 vel = (position - from) * 0.06f;
                PRTLoader.NewParticle<PRT_Light>(from, vel
                    , Color.Lerp(new Color(255, 170, 80), new Color(150, 110, 80), Main.rand.NextFloat()), 0.5f)
                    .Configure(26, hueShift: -0.004f);
            }
            for (int i = 0; i < 6; i++) {
                PRTLoader.NewParticle<PRT_Smoke>(position + Main.rand.NextVector2Circular(24f, 24f)
                    , Main.rand.NextVector2Circular(2f, 2f), new Color(90, 75, 60), Main.rand.NextFloat(0.7f, 1.1f))
                    .Configure(28, 0.7f, 0.03f);
            }
        }
    }

    /// <summary>
    /// 岩鱼锤：以"长蓄力 + 瞬时强击 + 阻尼回弹"的演出节奏俯冲砸落，
    /// 着色器拖尾条带 + 顶点冲击波环 + PRT 碎屑共同强调重量感。
    /// </summary>
    internal class RockHammerFish : BaseHeldProj
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Rockfish;

        private enum Phase
        {
            Gather,     //凝实 + 上提（反向蓄势）
            Approach,   //快速移动到目标头顶（行进式而非瞬移）
            WindUp,     //顶点蓄力，向后翻起，末段静止
            Slam,       //瞬时强击俯冲
            Recoil,     //阻尼回弹（次级运动）
            Vanish      //崩解消散
        }

        private Phase State {
            get => (Phase)Projectile.ai[1];
            set => Projectile.ai[1] = (float)value;
        }
        private ref float TargetNPCID => ref Projectile.ai[0];
        private ref float Timer => ref Projectile.ai[2];

        //运动锚点
        private Vector2 spawnPos;
        private Vector2 apexPos;
        private Vector2 slamStart;
        private Vector2 impactPos;
        private Vector2 predictedTarget;

        //姿态
        private float hammerRot;
        private float spin;
        private float squashX = 1f;
        private float squashY = 1f;
        private float glow;
        private float telegraph;      //地面预警环强度 0-1
        private float trailOpacity;

        //回弹弹簧
        private Vector2 recoilOffset;
        private Vector2 recoilVel;

        private bool hasStruck;
        private Trail slamTrail;
        private readonly List<FishSkillVFX.ShockRing> rings = new();

        //配色：陨铁化的炽热岩石
        private static readonly Color StoneColor = new(125, 100, 78);
        private static readonly Color MoltenColor = new(255, 165, 70);
        private static readonly Color EmberColor = new(255, 110, 45);

        private const int GatherTime = 24;
        private const int ApproachTime = 18;
        private const int WindUpTime = 17;
        private const int SilenceTime = 4;   //WindUp 末段静止帧（"风暴前的寂静"）
        private const int SlamTime = 8;
        private const int RecoilTime = 22;
        private const int VanishTime = 18;
        private const float HitRadius = 175f;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 16;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 54;
            Projectile.height = 54;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10086;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanDamage() => false;//伤害走砸落瞬间的范围判定，避免拖尾误伤与重复结算

        public override void AI() {
            if (!Owner.active || Owner.dead) {
                Projectile.Kill();
                return;
            }

            if (Timer == 0 && State == Phase.Gather) {
                spawnPos = Projectile.Center;
                spin = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            if (!FishSkill.GetT<FishRock>().Active(Owner) && State != Phase.Vanish && State != Phase.Recoil) {
                EnterVanish();
            }

            Timer++;
            UpdatePrediction();

            switch (State) {
                case Phase.Gather:
                    GatherAI();
                    break;
                case Phase.Approach:
                    ApproachAI();
                    break;
                case Phase.WindUp:
                    WindUpAI();
                    break;
                case Phase.Slam:
                    SlamAI();
                    break;
                case Phase.Recoil:
                    RecoilAI();
                    break;
                case Phase.Vanish:
                    VanishAI();
                    break;
            }

            //回弹弹簧积分（次级运动：本体到位后仍有惯性摆动）
            recoilVel += -recoilOffset * 0.32f;
            recoilVel *= 0.78f;
            recoilOffset += recoilVel;

            for (int i = rings.Count - 1; i >= 0; i--) {
                rings[i].Update();
                if (rings[i].Dead) {
                    rings.RemoveAt(i);
                }
            }

            Lighting.AddLight(Projectile.Center, MoltenColor.ToVector3() * glow * 0.9f);
        }

        private void UpdatePrediction() {
            if (IsTargetValid()) {
                NPC target = Main.npc[(int)TargetNPCID];
                predictedTarget = target.Center + target.velocity * 8f;
            }
        }

        private void GatherAI() {
            float p = Timer / GatherTime;
            float ease = CWRUtils.EaseOutElastic(MathHelper.Clamp(p, 0f, 1f));

            //向上"提锤"——蓄势的反向运动
            Projectile.Center = spawnPos + new Vector2(0, -36f * ease);
            spin += MathHelper.Lerp(0.05f, 0.34f, p);
            hammerRot = spin;
            squashX = squashY = MathHelper.Lerp(0.3f, 1f, ease);
            glow = MathHelper.Lerp(0f, 1f, p);

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                Vector2 from = Projectile.Center + Main.rand.NextVector2Circular(46f, 46f);
                PRTLoader.NewParticle<PRT_Light>(from, (Projectile.Center - from) * 0.08f
                    , Color.Lerp(MoltenColor, StoneColor, Main.rand.NextFloat()), 0.45f).Configure(20, hueShift: -0.005f);
            }

            if (Timer >= GatherTime) {
                EnterApproach();
            }
        }

        private void EnterApproach() {
            State = Phase.Approach;
            Timer = 0;
            slamStart = Projectile.Center;
            apexPos = (IsTargetValid() ? predictedTarget : Projectile.Center) + new Vector2(0, -markHeight());
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.6f, Pitch = -0.1f }, Projectile.Center);
        }

        private float markHeight() => 250f;

        private void ApproachAI() {
            float p = MathHelper.Clamp(Timer / ApproachTime, 0f, 1f);
            float ease = VaultUtils.EaseInOutCubic(p);

            //持续修正到（移动中的）目标头顶
            apexPos = (IsTargetValid() ? predictedTarget : apexPos) + new Vector2(0, -markHeight());
            Vector2 newPos = Vector2.Lerp(slamStart, apexPos, ease);
            Vector2 move = newPos - Projectile.Center;
            Projectile.Center = newPos;

            if (move.LengthSquared() > 0.5f) {
                hammerRot = MathHelper.WrapAngle(MathHelper.Lerp(hammerRot, move.ToRotation() + MathHelper.PiOver2, 0.4f));
            }
            squashX = MathHelper.Lerp(squashX, 1.12f, 0.3f);
            squashY = MathHelper.Lerp(squashY, 0.9f, 0.3f);
            glow = 1f;

            if (Timer >= ApproachTime) {
                EnterWindUp();
            }
        }

        private void EnterWindUp() {
            State = Phase.WindUp;
            Timer = 0;
            apexPos = Projectile.Center;
        }

        private void WindUpAI() {
            float p = MathHelper.Clamp(Timer / WindUpTime, 0f, 1f);
            bool silence = Timer >= WindUpTime - SilenceTime;

            //锁定砸落落点，向后/向上翻起蓄力
            impactPos = IsTargetValid() ? predictedTarget : new Vector2(apexPos.X, apexPos.Y + markHeight());
            Vector2 windDir = (apexPos - impactPos).SafeNormalize(-Vector2.UnitY);
            float windBack = (silence ? 1f : VaultUtils.EaseOutCubic(p)) * 30f;
            Projectile.Center = apexPos + windDir * windBack;

            hammerRot = MathHelper.WrapAngle(MathHelper.Lerp(hammerRot, (impactPos - Projectile.Center).ToRotation() + MathHelper.PiOver2 - 0.5f, 0.2f));
            squashX = MathHelper.Lerp(squashX, 1.18f, 0.2f);
            squashY = MathHelper.Lerp(squashY, 0.86f, 0.2f);

            telegraph = VaultUtils.EaseOutCubic(p);

            if (silence) {
                //寂静段：切掉粒子与抖动，蓄满的光收紧——强击前的"塌缩"
                glow = MathHelper.Lerp(glow, 1.9f, 0.4f);
            }
            else {
                glow = 1f + p * 0.5f;
                if (!Main.dedServ && Main.rand.NextBool(2)) {
                    Vector2 from = Projectile.Center + Main.rand.NextVector2Circular(52f, 52f);
                    PRTLoader.NewParticle<PRT_Light>(from, (Projectile.Center - from) * 0.12f, MoltenColor, 0.55f)
                        .Configure(18, hueShift: -0.006f);
                }
                if (Timer % 5 == 0) {
                    SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 0.35f, Pitch = -0.4f + p }, Projectile.Center);
                }
            }

            if (Timer >= WindUpTime) {
                EnterSlam();
            }
        }

        private void EnterSlam() {
            State = Phase.Slam;
            Timer = 0;
            slamStart = Projectile.Center;
            impactPos = IsTargetValid() ? predictedTarget : new Vector2(slamStart.X, slamStart.Y + markHeight());
            trailOpacity = 1f;
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffSwing with { Volume = 1.2f, Pitch = -0.6f }, Projectile.Center);
            SoundEngine.PlaySound(SoundID.Item71 with { Volume = 0.7f, Pitch = 0.2f }, Projectile.Center);
        }

        private void SlamAI() {
            float p = MathHelper.Clamp(Timer / SlamTime, 0f, 1f);
            //极陡的 EaseIn：前段几乎不动，末段瞬时抵达——速度靠对比而非匀速
            float ease = p * p * p;
            //轻微过冲，砸进落点下方一点
            Vector2 overshoot = (impactPos - slamStart).SafeNormalize(Vector2.UnitY) * 26f;
            Projectile.Center = Vector2.Lerp(slamStart, impactPos + overshoot, ease);

            Vector2 dir = (impactPos - slamStart).SafeNormalize(Vector2.UnitY);
            hammerRot = dir.ToRotation() + MathHelper.PiOver2;
            squashX = MathHelper.Lerp(0.78f, 1.25f, ease);//俯冲拉伸
            squashY = MathHelper.Lerp(1.35f, 0.8f, ease);
            glow = 2.2f;

            if (!hasStruck && p >= 0.78f) {
                DoImpact();
            }

            if (Timer >= SlamTime) {
                if (!hasStruck) {
                    DoImpact();
                }
                EnterRecoil();
            }
        }

        private void DoImpact() {
            hasStruck = true;
            impactPos = Projectile.Center;

            ApplyImpactDamage();

            FishSkillVFX.Punch(Owner, 15f);
            recoilOffset = new Vector2(0, -40f);//触地反冲，喂给弹簧产生回弹
            recoilVel = new Vector2(Main.rand.NextFloat(-3f, 3f), -7f);
            squashX = 1.5f;
            squashY = 0.62f;

            SoundEngine.PlaySound(SoundID.Item70 with { Volume = 1.3f, Pitch = -0.5f }, impactPos);
            SoundEngine.PlaySound(SoundID.DD2_MonkStaffGroundImpact with { Volume = 1.2f, Pitch = -0.2f }, impactPos);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.35f }, impactPos);

            if (Main.dedServ) {
                return;
            }

            //顶点冲击波环：两道椭圆冲击 + 一道高速细环
            rings.Add(new FishSkillVFX.ShockRing(impactPos, 230f, 22f, new Color(255, 190, 110), 0.42f, 26));
            rings.Add(new FishSkillVFX.ShockRing(impactPos, 150f, 14f, new Color(255, 130, 60), 0.42f, 20));
            rings.Add(new FishSkillVFX.ShockRing(impactPos, 320f, 7f, new Color(255, 230, 170), 0.42f, 16));

            //碎石飞溅（受重力）
            for (int i = 0; i < 26; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-11f, 11f), -Main.rand.NextFloat(4f, 14f));
                PRTLoader.NewParticle<PRT_Spark>(impactPos, vel
                    , Color.Lerp(EmberColor, StoneColor, Main.rand.NextFloat()), Main.rand.NextFloat(0.8f, 1.5f))
                    .Configure(true, Main.rand.Next(26, 40));
            }
            //横向冲击粉尘
            for (int i = 0; i < 14; i++) {
                float a = MathHelper.Lerp(-0.5f, MathHelper.Pi + 0.5f, i / 13f);
                PRTLoader.NewParticle<PRT_Smoke>(impactPos, (-a).ToRotationVector2() * Main.rand.NextFloat(3f, 8f)
                    , new Color(95, 78, 62), Main.rand.NextFloat(1.1f, 1.7f)).Configure(34, 0.8f, 0.04f);
            }
            //地面炽光余烬
            for (int i = 0; i < 16; i++) {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-7f, 7f), -Main.rand.NextFloat(1f, 5f));
                PRTLoader.NewParticle<PRT_Light>(impactPos, vel, MoltenColor, Main.rand.NextFloat(0.5f, 0.9f))
                    .Configure(22, hueShift: -0.01f);
            }
        }

        private void ApplyImpactDamage() {
            if (!Projectile.IsOwnedByLocalPlayer()) {
                return;
            }
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || !npc.CanBeChasedBy()) {
                    continue;
                }
                if (Vector2.Distance(npc.Center, impactPos) > HitRadius + npc.width * 0.5f) {
                    continue;
                }

                float dmg = Projectile.damage;
                if (npc.boss) {
                    dmg *= 1.5f;
                }
                if (npc.IsWormBody()) {
                    dmg *= 0.7f;
                }
                int dir = Math.Sign(npc.Center.X - impactPos.X);
                if (dir == 0) {
                    dir = 1;
                }
                npc.SimpleStrikeNPC((int)dmg, dir, false, Projectile.knockBack * 1.5f, null, false, 0f, true);
            }
        }

        private void EnterRecoil() {
            State = Phase.Recoil;
            Timer = 0;
        }

        private void RecoilAI() {
            float p = MathHelper.Clamp(Timer / RecoilTime, 0f, 1f);
            //本体停在落点，靠弹簧 recoilOffset 表现回弹颤动
            squashX = MathHelper.Lerp(squashX, 1f, 0.18f);
            squashY = MathHelper.Lerp(squashY, 1f, 0.18f);
            glow = MathHelper.Lerp(2.2f, 0.6f, p);
            telegraph = MathHelper.Lerp(telegraph, 0f, 0.2f);
            trailOpacity *= 0.82f;
            hammerRot = MathHelper.WrapAngle(hammerRot + recoilVel.X * 0.01f);

            if (Timer >= RecoilTime) {
                EnterVanish();
            }
        }

        private void EnterVanish() {
            if (State == Phase.Vanish) {
                return;
            }
            State = Phase.Vanish;
            Timer = 0;
        }

        private void VanishAI() {
            float p = MathHelper.Clamp(Timer / VanishTime, 0f, 1f);
            Projectile.Center += new Vector2(0, -0.7f);//轻微上浮再化散
            squashX = MathHelper.Lerp(squashX, 0.5f, 0.1f);
            squashY = MathHelper.Lerp(squashY, 0.5f, 0.1f);
            Projectile.alpha = (int)(255 * VaultUtils.EaseInCubic(p));
            glow = MathHelper.Lerp(glow, 0f, 0.18f);
            trailOpacity *= 0.8f;

            if (!Main.dedServ && Main.rand.NextBool(2)) {
                PRTLoader.NewParticle<PRT_Light>(Projectile.Center + Main.rand.NextVector2Circular(18f, 18f)
                    , new Vector2(0, -Main.rand.NextFloat(0.5f, 1.6f)), Color.Lerp(MoltenColor, StoneColor, p), 0.5f)
                    .Configure(20, hueShift: -0.01f);
            }

            if (Timer >= VanishTime) {
                Projectile.Kill();
            }
        }

        private bool IsTargetValid() {
            int id = (int)TargetNPCID;
            if (id < 0 || id >= Main.maxNPCs) {
                return false;
            }
            NPC target = Main.npc[id];
            return target.active && target.CanBeChasedBy();
        }

        private bool TrailVisible => (State == Phase.Slam || (State == Phase.Recoil && trailOpacity > 0.05f)) && trailOpacity > 0.05f;

        public float TrailWidth(float c) => MathHelper.Lerp(58f, 6f, c) * trailOpacity;

        public Color TrailColor(Vector2 _) => Color.White * trailOpacity;

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Item[ItemID.Rockfish].Value;
            Rectangle src = tex.Frame();
            Vector2 origin = src.Size() / 2f;
            Vector2 drawPos = Projectile.Center + recoilOffset - Main.screenPosition;
            float fade = (255f - Projectile.alpha) / 255f;
            Vector2 scaleVec = new Vector2(squashX, squashY) * Projectile.scale * 1.05f;

            //地面预警 / 冲击焦痕（压扁椭圆）
            if (telegraph > 0.01f && !Main.dedServ) {
                Texture2D ring = CWRAsset.DiffusionCircle.Value;
                Vector2 groundPos = impactPos - Main.screenPosition;
                float warn = telegraph * fade;
                Main.spriteBatch.Draw(ring, groundPos, null, new Color(255, 120, 50, 0) * (warn * 0.6f)
                    , 0f, ring.Size() / 2f, new Vector2(0.9f, 0.32f) * (1.1f + warn * 0.3f), SpriteEffects.None, 0f);
            }

            //本体背光（A=0 加色）
            Color glowColor = Color.Lerp(MoltenColor, Color.White, MathHelper.Clamp(glow - 1f, 0f, 1f)) with { A = 0 };
            float glowA = MathHelper.Clamp(glow, 0f, 2.4f) * fade;
            for (int i = 0; i < 3; i++) {
                Main.spriteBatch.Draw(tex, drawPos, src, glowColor * (glowA * (0.32f - i * 0.08f)), hammerRot, origin
                    , scaleVec * (1.15f + i * 0.12f), SpriteEffects.None, 0f);
            }

            //本体
            Main.spriteBatch.Draw(tex, drawPos, src, lightColor * fade, hammerRot, origin, scaleVec, SpriteEffects.None, 0f);

            //炽热边光
            Main.spriteBatch.Draw(tex, drawPos, src, new Color(255, 200, 120, 0) * (MathHelper.Clamp(glow, 0f, 2f) * 0.4f * fade)
                , hammerRot, origin, scaleVec * 0.96f, SpriteEffects.None, 0f);

            //着色器拖尾条带 + 顶点冲击波（脱离默认批次后绘制）
            bool drawTrail = TrailVisible && BuildSlamTrail();
            bool drawRings = rings.Count > 0;
            if (drawTrail || drawRings) {
                Main.spriteBatch.End();

                if (drawTrail) {
                    Effect gradient = EffectLoader.GradientTrail?.Value;
                    if (gradient != null) {
                        FishSkillVFX.ApplyGradientTrail(gradient, CWRAsset.DragonRage_Bar.Value, CWRAsset.LightShot.Value, 0.12f);
                        Main.graphics.GraphicsDevice.BlendState = BlendState.Additive;
                        slamTrail.DrawTrail(gradient);
                        Main.graphics.GraphicsDevice.BlendState = BlendState.AlphaBlend;
                    }
                }

                if (drawRings) {
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.AnisotropicClamp
                        , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
                    Texture2D ringTex = CWRAsset.Placeholder_White.Value;
                    foreach (FishSkillVFX.ShockRing r in rings) {
                        r.Draw(ringTex);
                    }
                    Main.spriteBatch.End();
                }

                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState
                    , DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            }

            return false;
        }

        private bool BuildSlamTrail() {
            if (Main.dedServ || Projectile.oldPos == null || Projectile.oldPos.Length < 4) {
                return false;
            }
            Vector2[] positions = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < positions.Length; i++) {
                Vector2 old = Projectile.oldPos[i];
                positions[i] = (old == Vector2.Zero ? Projectile.Center : old + Projectile.Size * 0.5f);
            }
            slamTrail ??= new Trail(positions, TrailWidth, TrailColor);
            slamTrail.TrailPositions = positions;
            return true;
        }
    }
}
