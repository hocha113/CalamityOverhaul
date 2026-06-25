using CalamityOverhaul.Common;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace CalamityOverhaul.Content.Items.Tools
{
    internal class EncyclopediaofOceanicKnowledge : ModItem
    {
        public override string Texture => CWRConstant.Item + "Tools/EncyclopediaofOceanicKnowledge";
        public static LocalizedText Text1;
        public static LocalizedText Text2;
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
            Text1 = this.GetLocalization(nameof(Text1), () => "你已经掌握了所有知识");
            Text2 = this.GetLocalization(nameof(Text2), () => "习得了[NUM]种鱼类知识！");
        }

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useAnimation = 60;
            Item.useTime = 60;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.maxStack = 1;
            Item.rare = CWRID.Rarity_BurnishedAuric;
            Item.value = Item.sellPrice(platinum: 10);
            Item.UseSound = SoundID.Item29;
        }

        public override bool CanUseItem(Player player) {
            if (!player.TryGetModPlayer<HalibutSave>(out var save)) {
                return false;
            }
            //检查是否已经解锁所有技能
            int totalSkills = FishSkill.UnlockFishs.Count;
            if (save.unlocked.Count >= totalSkills) {
                SoundEngine.PlaySound(SoundID.MenuClose);
                string text = Text1.Value;
                CombatText.NewText(player.Hitbox, new Color(100, 200, 255), text, true);
                return false;//已经全部解锁
            }
            return true;
        }

        public override bool? UseItem(Player player) {
            if (!player.TryGetModPlayer<HalibutSave>(out var save)) {
                return false;
            }

            //生成特效弹幕
            if (Main.myPlayer == player.whoAmI) {
                Projectile.NewProjectile(
                    player.GetSource_ItemUse(Item),
                    player.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<EncyclopediaEffect>(),
                    0,
                    0,
                    player.whoAmI
                );
            }

            //延迟解锁走特效Proj
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                //所有可解锁鱼类
                .AddIngredient(ItemID.Goldfish)
                .AddIngredient(ItemID.Bass)
                .AddIngredient(ItemID.Trout)
                .AddIngredient(ItemID.AtlanticCod)
                .AddIngredient(ItemID.Tuna)
                .AddIngredient(ItemID.RedSnapper)
                .AddIngredient(ItemID.NeonTetra)
                .AddIngredient(ItemID.ArmoredCavefish)
                .AddIngredient(ItemID.Damselfish)
                .AddIngredient(ItemID.CrimsonTigerfish)
                .AddIngredient(ItemID.Hemopiranha)
                .AddIngredient(ItemID.Rockfish)
                .AddIngredient(ItemID.Stinkfish)
                .AddIngredient(ItemID.Honeyfin)
                .AddIngredient(ItemID.ChaosFish)
                .AddIngredient(ItemID.Ebonkoi)
                .AddIngredient(ItemID.Prismite)
                .AddIngredient(ItemID.VariegatedLardfish)
                .AddIngredient(ItemID.Flounder)
                .AddIngredient(ItemID.DoubleCod)
                .AddIngredient(ItemID.FrostMinnow)
                .AddIngredient(ItemID.PrincessFish)
                .AddIngredient(ItemID.GoldenCarp)
                .AddIngredient(ItemID.SpecularFish)
                .AddIngredient(ItemID.Cursedfish)
                .AddIngredient(ItemID.Ichorfish)
                .AddIngredient(ItemID.Obsidifish)
                .AddIngredient(ItemID.BlueJellyfish)
                .AddIngredient(ItemID.GreenJellyfish)
                .AddIngredient(ItemID.PinkJellyfish)
                .AddIngredient(ItemID.Shrimp)
                .AddIngredient(ItemID.ChaosFish)
                .AddIngredient(ItemID.Jewelfish)
                .AddIngredient(ItemID.Bonefish)
                .AddIngredient(ItemID.Cloudfish)
                .AddIngredient(ItemID.Wyverntail)
                .AddIngredient(ItemID.Bladetongue)
                .AddIngredient(ItemID.CrystalSerpent)
                .AddIngredient(ItemID.Toxikarp)
                .AddIngredient(ItemID.ScalyTruffle)
                .AddIngredient(ItemID.Batfish)
                .AddIngredient(ItemID.ZephyrFish)
                .AddIngredient(ItemID.LunarBar, 5)
                .AddTile(TileID.LunarCraftingStation)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            foreach (var i in tooltips) {
                if (i.Name == "ItemName") {
                    continue;
                }
                i.OverrideColor = new Color(100, 200, 255);
            }
        }
    }

    /// 海洋百科使用特效弹幕
    internal class EncyclopediaEffect : ModProjectile
    {
        public override string Texture => CWRConstant.Placeholder;

        private enum EffectPhase
        {
            Gather,//汇聚阶段
            Absorb,//吸收阶段
            Complete//完成阶段
        }

        private EffectPhase Phase {
            get => (EffectPhase)Projectile.ai[0];
            set => Projectile.ai[0] = (float)value;
        }

        private ref float Timer => ref Projectile.ai[1];
        private const int GatherDuration = 120;//汇聚持续时间
        private const int AbsorbDuration = 60;//吸收持续时间
        private const int CompleteDuration = 60;//完成持续时间

        //技能图标飞行实体列表
        private List<FlyingSkillIcon> flyingIcons = new List<FlyingSkillIcon>();

        //柔光粒子系统
        private List<OceanParticle> particles = new List<OceanParticle>();

        //仪式场状态（驱动着色器）
        private float fieldRadius;         //当前符文环半径（像素）
        private float coreIntensity;       //知识核心强度 0~1
        private float shockProgress = -1f; //完成冲击波进度 0~1，<0 表示未激活
        private float globalFade = 1f;     //整体淡出 0~1

        //已解锁的技能列表
        private List<FishSkill> unlockedSkills = new List<FishSkill>();

        private const float MinRingRadius = 70f;  //收束后的最小环半径
        private const float MaxRingRadius = 420f; //展开后的最大环半径
        private const float QuadRadius = 560f;    //着色器绘制区半径（含神光/冲击波余量）

        public override void SetStaticDefaults() {
            //仪式场以玩家为中心向外延伸，放宽绘制裁剪避免边缘被剔除
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1400;
        }

        public override void SetDefaults() {
            Projectile.width = 1;
            Projectile.height = 1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = GatherDuration + AbsorbDuration + CompleteDuration;
            Projectile.alpha = 255;
        }

        public override void AI() {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = owner.Center;
            Timer++;

            switch (Phase) {
                case EffectPhase.Gather:
                    GatherPhaseAI(owner);
                    break;
                case EffectPhase.Absorb:
                    AbsorbPhaseAI(owner);
                    break;
                case EffectPhase.Complete:
                    CompletePhaseAI(owner);
                    break;
            }

            //更新飞行图标
            for (int i = flyingIcons.Count - 1; i >= 0; i--) {
                flyingIcons[i].Update(owner.Center);
                if (flyingIcons[i].ShouldRemove) {
                    flyingIcons.RemoveAt(i);
                }
            }

            //更新粒子
            for (int i = particles.Count - 1; i >= 0; i--) {
                particles[i].Update();
                if (particles[i].ShouldRemove) {
                    particles.RemoveAt(i);
                }
            }
        }

        /// 汇聚：符文环展开，鱼类知识图标自四周汇聚
        private void GatherPhaseAI(Player owner) {
            //初始化飞行图标
            if (Timer == 1) {
                InitializeFlyingIcons(owner);
                PlayGatherSound(owner);
            }

            float progress = Timer / GatherDuration;
            //仪式环由内向外展开
            fieldRadius = MathHelper.Lerp(MinRingRadius, MaxRingRadius, VaultUtils.EaseOutCubic(progress));
            //核心微微亮起
            coreIntensity = MathHelper.Lerp(0f, 0.22f, progress);
            globalFade = 1f;

            //生成汇聚柔光粒子
            if (Timer % 2 == 0) {
                SpawnGatherParticles(owner.Center);
            }

            //音效
            if (Timer % 30 == 0) {
                SoundEngine.PlaySound(SoundID.Item29 with {
                    Volume = 0.3f,
                    Pitch = -0.5f + progress * 0.5f
                }, owner.Center);
            }

            //转入吸收阶段
            if (Timer >= GatherDuration) {
                Phase = EffectPhase.Absorb;
                Timer = 0;
                PlayAbsorbSound(owner);
            }
        }

        /// 吸收：符文环收束，知识汇入核心
        private void AbsorbPhaseAI(Player owner) {
            float progress = Timer / AbsorbDuration;

            //符文环向内收束
            fieldRadius = MathHelper.Lerp(MaxRingRadius, MinRingRadius, VaultUtils.EaseInCubic(progress));
            //核心急剧增亮
            coreIntensity = MathHelper.Lerp(0.22f, 1f, VaultUtils.EaseOutCubic(progress));
            globalFade = 1f;

            //强化的向心吸收粒子
            SpawnAbsorbParticles(owner.Center, progress);

            //脉冲音效
            if (Timer % 10 == 0) {
                SoundEngine.PlaySound(SoundID.MaxMana with {
                    Volume = 0.4f,
                    Pitch = progress * 0.6f
                }, owner.Center);
            }

            //转入完成阶段
            if (Timer >= AbsorbDuration) {
                Phase = EffectPhase.Complete;
                Timer = 0;
                shockProgress = 0f;//激活冲击波
                UnlockAllSkills(owner);
                PlayCompleteSound(owner);
            }
        }

        /// 完成：核心闪爆 + 冲击波向外扩散
        private void CompletePhaseAI(Player owner) {
            float progress = Timer / CompleteDuration;

            //爆发柔光粒子
            if (Timer == 1) {
                for (int i = 0; i < 60; i++) {
                    SpawnBurstParticle(owner.Center);
                }
            }

            //核心闪爆后回落
            coreIntensity = MathHelper.Lerp(1f, 0f, progress * progress);
            //冲击波向外扩散
            shockProgress = VaultUtils.EaseOutCubic(progress);
            //尾段整体淡出
            globalFade = MathHelper.Clamp(1f - (progress - 0.55f) / 0.45f, 0f, 1f);

            if (Timer >= CompleteDuration) {
                Projectile.Kill();
            }
        }

        /// 初始化飞行图标
        private void InitializeFlyingIcons(Player owner) {
            if (!owner.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            List<FishSkill> allSkills = FishSkill.UnlockFishs.Values.ToList();
            int index = 0;

            foreach (var skill in allSkills) {
                //跳过已解锁的技能
                if (save.IsUnlocked(skill)) {
                    continue;
                }

                //计算起始位置（螺旋分布）
                float angle = (index / (float)allSkills.Count) * MathHelper.TwoPi * 3f;
                float radius = 300f + (index % 3) * 100f;
                Vector2 startPos = owner.Center + angle.ToRotationVector2() * radius;

                flyingIcons.Add(new FlyingSkillIcon(skill, startPos, index));
                unlockedSkills.Add(skill);
                index++;
            }
        }

        /// 解锁全部技能
        private void UnlockAllSkills(Player owner) {
            if (!owner.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            foreach (var skill in unlockedSkills) {
                save.UnlockSkill(skill);
            }

            //触发复苏系统提升
            if (owner.TryGetOverride<HalibutPlayer>(out var halibutPlayer)) {
                float increase = unlockedSkills.Count * 15f;
                halibutPlayer.ResurrectionSystem.MaxValue += increase;
                halibutPlayer.ResurrectionSystem.Reset();
            }

            //播放消息
            if (Main.netMode != NetmodeID.Server) {
                string text = EncyclopediaofOceanicKnowledge.Text2.Value.Replace("[NUM]", unlockedSkills.Count.ToString());
                CombatText.NewText(owner.Hitbox, new Color(100, 200, 255), text, true);
            }
        }

        //粒子生成方法
        private void SpawnGatherParticles(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float radius = Main.rand.NextFloat(50f, Math.Max(60f, fieldRadius));
            Vector2 pos = center + angle.ToRotationVector2() * radius;
            Vector2 velocity = (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 5f);

            particles.Add(new OceanParticle(pos, velocity, OceanParticle.ParticleType.Gather));
        }

        private void SpawnAbsorbParticles(Vector2 center, float intensity) {
            for (int i = 0; i < 3; i++) {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(80f, 200f);
                Vector2 pos = center + angle.ToRotationVector2() * radius;
                Vector2 velocity = (center - pos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(4f, 8f) * (1f + intensity);

                particles.Add(new OceanParticle(pos, velocity, OceanParticle.ParticleType.Absorb));
            }
        }

        private void SpawnBurstParticle(Vector2 center) {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 15f);

            particles.Add(new OceanParticle(center, velocity, OceanParticle.ParticleType.Burst));
        }

        //音效方法
        private static void PlayGatherSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item84 with {
                Volume = 0.8f,
                Pitch = -0.3f
            }, owner.Center);
        }

        private static void PlayAbsorbSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item105 with {
                Volume = 0.8f,
                Pitch = 0.2f
            }, owner.Center);
        }

        private static void PlayCompleteSound(Player owner) {
            SoundEngine.PlaySound(SoundID.Item4 with {
                Volume = 0.8f,
                Pitch = 0.5f
            }, owner.Center);
            SoundEngine.PlaySound(SoundID.ResearchComplete, owner.Center);
        }

        public override bool PreDraw(ref Color lightColor) {
            Player owner = Main.player[Projectile.owner];
            Vector2 center = owner.Center;
            SpriteBatch sb = Main.spriteBatch;

            //着色器仪式场（焦散水盘 + 符文环 + 知识核心 + 神光 + 冲击波）
            //自带 Immediate/Additive 批次，结束时恢复 Deferred/AlphaBlend
            DrawRitualField(center);

            //柔光层：向心粒子 + 图标光晕与拖尾（Additive）
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (var particle in particles) {
                particle.Draw(sb);
            }
            foreach (var icon in flyingIcons) {
                icon.DrawGlow(sb);
            }
            sb.End();

            //主体层：技能图标本体（AlphaBlend），并恢复默认批次状态
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            foreach (var icon in flyingIcons) {
                icon.DrawBody(sb);
            }

            return false;
        }

        /// 用着色器单次绘制整个仪式场
        private void DrawRitualField(Vector2 center) {
            Effect shader = EffectLoader.EncyclopediaKnowledge?.Value;
            if (shader == null) {
                return;
            }

            Texture2D canvas = CWRAsset.Placeholder_White?.Value;
            Texture2D noise = CWRAsset.Extra_193?.Value;
            if (canvas == null || noise == null) {
                return;
            }

            float drawDiameter = QuadRadius * 2f;
            Vector2 drawPos = center - Main.screenPosition;

            shader.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            shader.Parameters["uRingScale"]?.SetValue(MathHelper.Clamp(fieldRadius / QuadRadius, 0f, 1f));
            shader.Parameters["uCoreIntensity"]?.SetValue(MathHelper.Clamp(coreIntensity, 0f, 1f));
            shader.Parameters["uShock"]?.SetValue(shockProgress < 0f ? 0f : MathHelper.Clamp(shockProgress, 0f, 1f));
            shader.Parameters["uFade"]?.SetValue(MathHelper.Clamp(globalFade, 0f, 1f));
            shader.Parameters["deepColor"]?.SetValue(new Vector3(0.03f, 0.10f, 0.20f));
            shader.Parameters["glowColor"]?.SetValue(new Vector3(0.30f, 0.78f, 0.98f));
            shader.Parameters["causticColor"]?.SetValue(new Vector3(0.78f, 0.94f, 1.0f));
            shader.Parameters["runeColor"]?.SetValue(new Vector3(0.62f, 0.90f, 1.0f));
            shader.Parameters["uNoiseTex"]?.SetValue(noise);

            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.CurrentTechnique.Passes[0].Apply();
            sb.Draw(canvas, drawPos, null, Color.White, 0f, canvas.Size() * 0.5f,
                new Vector2(drawDiameter, drawDiameter), SpriteEffects.None, 0f);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }

    /// 飞行技能图标实体：自外缘沿贝塞尔曲线汇入核心，带水流柔光拖尾
    internal class FlyingSkillIcon
    {
        public FishSkill Skill;
        public Vector2 Position;
        public Vector2 StartPosition;
        public float Progress;
        public float Speed;
        public float Rotation;
        public float Scale;
        public int Index;

        private readonly Vector2 ctrlOffset;
        private readonly List<Vector2> trail = new List<Vector2>();
        private const int MaxTrail = 10;

        public bool ShouldRemove => Progress >= 1f;

        public FlyingSkillIcon(FishSkill skill, Vector2 startPos, int index) {
            Skill = skill;
            StartPosition = startPos;
            Position = startPos;
            Index = index;
            Progress = 0f;
            Speed = 0.012f + (index % 10) * 0.0012f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Scale = 0.8f;
            //控制点偏移固定在生成时，避免逐帧抖动
            ctrlOffset = new Vector2(Main.rand.NextFloat(-110f, 110f), Main.rand.NextFloat(-340f, -180f));
        }

        public void Update(Vector2 targetPos) {
            Progress = Math.Clamp(Progress + Speed, 0f, 1f);

            //贝塞尔曲线飞行
            Vector2 mid = (StartPosition + targetPos) * 0.5f;
            Vector2 control1 = StartPosition + new Vector2(0f, -200f);
            Vector2 control2 = mid + ctrlOffset;
            Position = VaultUtils.CubicBezier(Progress, StartPosition, control1, control2, targetPos);

            Rotation += 0.05f;

            //先放大后收束，汇入核心时变小
            Scale = Progress < 0.5f
                ? MathHelper.Lerp(0.8f, 1.2f, Progress * 2f)
                : MathHelper.Lerp(1.2f, 0.3f, (Progress - 0.5f) * 2f);

            trail.Insert(0, Position);
            if (trail.Count > MaxTrail) {
                trail.RemoveAt(trail.Count - 1);
            }
        }

        /// 柔光层：水流拖尾 + 图标光晕（Additive）
        public void DrawGlow(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Vector2 glowOrigin = glow.Size() * 0.5f;
            float life = 1f - Progress;

            for (int i = 0; i < trail.Count; i++) {
                float t = i / (float)Math.Max(1, trail.Count - 1);
                float tAlpha = (1f - t) * life * 0.45f;
                if (tAlpha <= 0.01f) {
                    continue;
                }
                Color c = new Color(90, 190, 255, 0) * tAlpha;
                float s = Scale * 0.5f * (1f - t * 0.6f);
                sb.Draw(glow, trail[i] - Main.screenPosition, null, c, 0f, glowOrigin, s, SpriteEffects.None, 0f);
            }

            //图标背后的光晕
            Color halo = new Color(120, 210, 255, 0) * (0.25f + life * 0.45f);
            sb.Draw(glow, Position - Main.screenPosition, null, halo, 0f, glowOrigin, Scale * 1.15f, SpriteEffects.None, 0f);
        }

        /// 主体层：技能图标本体（AlphaBlend）
        public void DrawBody(SpriteBatch sb) {
            if (Skill?.Icon == null) {
                return;
            }
            Vector2 origin = Skill.Icon.Size() * 0.5f;
            Color drawColor = Color.Lerp(new Color(190, 232, 255), Color.White, 0.5f) * (1f - Progress * 0.35f);
            sb.Draw(Skill.Icon, Position - Main.screenPosition, null, drawColor, Rotation, origin, Scale, SpriteEffects.None, 0f);
        }
    }

    /// 海洋粒子
    internal class OceanParticle
    {
        public enum ParticleType
        {
            Gather,
            Absorb,
            Burst
        }

        public Vector2 Position;
        public Vector2 Velocity;
        public ParticleType Type;
        public float Scale;
        public float Rotation;
        public float Alpha;
        public int Life;
        public int MaxLife;

        public bool ShouldRemove => Life >= MaxLife;

        public OceanParticle(Vector2 pos, Vector2 vel, ParticleType type) {
            Position = pos;
            Velocity = vel;
            Type = type;
            Scale = Main.rand.NextFloat(0.8f, 1.5f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Alpha = 1f;
            Life = 0;
            MaxLife = type switch {
                ParticleType.Gather => Main.rand.Next(40, 80),
                ParticleType.Absorb => Main.rand.Next(30, 60),
                ParticleType.Burst => Main.rand.Next(50, 90),
                _ => 60
            };
        }

        public void Update() {
            Life++;
            Position += Velocity;

            if (Type == ParticleType.Gather || Type == ParticleType.Absorb) {
                Velocity *= 0.98f;
            }
            else {
                Velocity *= 0.95f;
            }

            Rotation += 0.05f;
            Alpha = 1f - (Life / (float)MaxLife);
        }

        public void Draw(SpriteBatch sb) {
            Texture2D glow = CWRAsset.SoftGlow?.Value;
            if (glow == null) {
                return;
            }
            Color color = (Type switch {
                ParticleType.Gather => new Color(90, 190, 255, 0),
                ParticleType.Absorb => new Color(150, 225, 255, 0),
                ParticleType.Burst => new Color(200, 245, 255, 0),
                _ => new Color(255, 255, 255, 0)
            }) * (Alpha * 0.85f);

            float s = Scale * 0.4f * (0.6f + Alpha * 0.4f);
            sb.Draw(glow, Position - Main.screenPosition, null, color,
                Rotation, glow.Size() * 0.5f, s, SpriteEffects.None, 0f);
        }
    }
}
