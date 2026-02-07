using CalamityOverhaul.Content.LegendWeapon.HalibutLegend;
using CalamityOverhaul.Content.LegendWeapon.HalibutLegend.UI;
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
            if (save.unlockSkills.Count >= totalSkills) {
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

            //延迟解锁技能由特效弹幕处理
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

    /// <summary>
    /// 海洋知识百科全书使用特效弹幕
    /// </summary>
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

        //粒子系统
        private List<OceanParticle> particles = new List<OceanParticle>();

        //光环效果
        private float auraRadius = 0f;
        private float auraIntensity = 0f;

        //已解锁的技能列表
        private List<FishSkill> unlockedSkills = new List<FishSkill>();

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

        ///<summary>
        ///汇聚阶段：所有技能图标从四周飞向玩家
        ///</summary>
        private void GatherPhaseAI(Player owner) {
            //初始化飞行图标
            if (Timer == 1) {
                InitializeFlyingIcons(owner);
                PlayGatherSound(owner);
            }

            //更新光环
            float progress = Timer / GatherDuration;
            auraRadius = MathHelper.Lerp(0f, 400f, CWRUtils.EaseOutCubic(progress));
            auraIntensity = MathHelper.Lerp(0f, 1f, progress);

            //生成环境粒子
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

        ///<summary>
        ///吸收阶段：技能被吸入玩家体内
        ///</summary>
        private void AbsorbPhaseAI(Player owner) {
            float progress = Timer / AbsorbDuration;

            //光环收缩
            auraRadius = MathHelper.Lerp(400f, 0f, CWRUtils.EaseInCubic(progress));
            auraIntensity = MathHelper.Lerp(1f, 0.3f, progress);

            //强化的吸收粒子
            if (Timer % 1 == 0) {
                SpawnAbsorbParticles(owner.Center, progress);
            }

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
                UnlockAllSkills(owner);
                PlayCompleteSound(owner);
            }
        }

        ///<summary>
        ///完成阶段：爆发式特效
        ///</summary>
        private void CompletePhaseAI(Player owner) {
            float progress = Timer / CompleteDuration;

            //爆发光环
            if (Timer == 1) {
                for (int i = 0; i < 50; i++) {
                    SpawnBurstParticle(owner.Center);
                }
            }

            //淡出
            auraIntensity = MathHelper.Lerp(0.3f, 0f, progress);

            if (Timer >= CompleteDuration) {
                Projectile.Kill();
            }
        }

        ///<summary>
        ///初始化飞行图标
        ///</summary>
        private void InitializeFlyingIcons(Player owner) {
            if (!owner.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            List<FishSkill> allSkills = FishSkill.UnlockFishs.Values.ToList();
            int index = 0;

            foreach (var skill in allSkills) {
                //跳过已解锁的技能
                if (save.unlockSkills.Contains(skill)) {
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

        ///<summary>
        ///解锁所有技能
        ///</summary>
        private void UnlockAllSkills(Player owner) {
            if (!owner.TryGetModPlayer<HalibutSave>(out var save)) {
                return;
            }

            foreach (var skill in unlockedSkills) {
                if (!save.unlockSkills.Contains(skill)) {
                    save.unlockSkills.Add(skill);
                    SkillSlot newSlot = HalibutUIPanel.AddSkillSlot(skill, 1f);
                    save.halibutUISkillSlots.Add(newSlot);
                }
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
            float radius = Main.rand.NextFloat(50f, auraRadius);
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
            SpriteBatch sb = Main.spriteBatch;
            Player owner = Main.player[Projectile.owner];

            //绘制光环
            DrawAura(sb, owner.Center);

            //绘制粒子
            foreach (var particle in particles) {
                particle.Draw(sb);
            }

            //绘制飞行图标
            foreach (var icon in flyingIcons) {
                icon.Draw(sb);
            }

            //绘制中心发光
            if (Phase == EffectPhase.Absorb || Phase == EffectPhase.Complete) {
                DrawCenterGlow(sb, owner.Center);
            }

            return false;
        }

        private void DrawAura(SpriteBatch sb, Vector2 center) {
            if (auraIntensity <= 0.01f) {
                return;
            }

            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color auraColor = new Color(100, 200, 255) * (auraIntensity * 0.3f);

            //绘制多层光环
            for (int i = 0; i < 3; i++) {
                float ringRadius = auraRadius * (0.8f + i * 0.1f);
                float ringThickness = 2f + i;
                int segments = 64;

                for (int j = 0; j < segments; j++) {
                    float angle1 = (j / (float)segments) * MathHelper.TwoPi;
                    float angle2 = ((j + 1) / (float)segments) * MathHelper.TwoPi;

                    Vector2 pos1 = center + angle1.ToRotationVector2() * ringRadius;
                    Vector2 pos2 = center + angle2.ToRotationVector2() * ringRadius;

                    Vector2 diff = pos2 - pos1;
                    float length = diff.Length();
                    float rotation = diff.ToRotation();

                    sb.Draw(pixel, pos1 - Main.screenPosition, null, auraColor,
                        rotation, Vector2.Zero, new Vector2(length, ringThickness), SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawCenterGlow(SpriteBatch sb, Vector2 center) {
            Texture2D pixel = VaultAsset.placeholder2.Value;
            float pulse = (float)Math.Sin(Timer * 0.3f) * 0.3f + 0.7f;
            Color glowColor = new Color(100, 200, 255) * (auraIntensity * pulse);

            for (int i = 0; i < 5; i++) {
                float scale = 30f * (1f + i * 0.3f);
                float alpha = (1f - i / 5f) * auraIntensity;
                sb.Draw(pixel, center - Main.screenPosition, null, glowColor * alpha,
                    0f, new Vector2(0.5f), new Vector2(scale), SpriteEffects.None, 0f);
            }
        }
    }

    /// <summary>
    /// 飞行的技能图标实体
    /// </summary>
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

        private const float FlyDuration = 120f;

        public bool ShouldRemove => Progress >= 1f;

        public FlyingSkillIcon(FishSkill skill, Vector2 startPos, int index) {
            Skill = skill;
            StartPosition = startPos;
            Position = startPos;
            Index = index;
            Progress = 0f;
            Speed = 0.01f + (index % 10) * 0.001f;
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            Scale = 0.8f;
        }

        public void Update(Vector2 targetPos) {
            Progress += Speed;
            Progress = Math.Clamp(Progress, 0f, 1f);

            //贝塞尔曲线飞行
            Vector2 mid = (StartPosition + targetPos) / 2;
            Vector2 control1 = StartPosition + new Vector2(0, -200f);
            Vector2 control2 = mid + new Vector2(Main.rand.NextFloat(-100f, 100f), -300f);

            Position = CWRUtils.CubicBezier(Progress, StartPosition, control1, control2, targetPos);

            //旋转
            Rotation += 0.05f;

            //缩放动画
            if (Progress < 0.5f) {
                Scale = MathHelper.Lerp(0.8f, 1.2f, Progress * 2f);
            }
            else {
                Scale = MathHelper.Lerp(1.2f, 0.3f, (Progress - 0.5f) * 2f);
            }
        }

        public void Draw(SpriteBatch sb) {
            if (Skill?.Icon == null) {
                return;
            }

            Color drawColor = Color.White * (1f - Progress * 0.5f);
            Vector2 origin = Skill.Icon.Size() / 2f;

            //绘制发光
            for (int i = 0; i < 3; i++) {
                Color glowColor = new Color(100, 200, 255) * ((1f - Progress) * 0.3f);
                sb.Draw(Skill.Icon, Position - Main.screenPosition, null, glowColor,
                    Rotation, origin, Scale * (1f + i * 0.1f), SpriteEffects.None, 0f);
            }

            //绘制主体
            sb.Draw(Skill.Icon, Position - Main.screenPosition, null, drawColor,
                Rotation, origin, Scale, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    /// 海洋粒子效果
    /// </summary>
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
            Texture2D pixel = VaultAsset.placeholder2.Value;
            Color color = Type switch {
                ParticleType.Gather => new Color(100, 200, 255),
                ParticleType.Absorb => new Color(150, 220, 255),
                ParticleType.Burst => new Color(200, 240, 255),
                _ => Color.White
            } * Alpha;

            sb.Draw(pixel, Position - Main.screenPosition, null, color,
                Rotation, new Vector2(0.5f), new Vector2(Scale * 4f), SpriteEffects.None, 0f);
        }
    }
}
