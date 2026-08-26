using System.Collections.Generic;
using Terraria.ID;

namespace CalamityOverhaul.Content.GameModes.BrutalMobs.EliteMove
{
    /// <summary>困难精英的行为家族。None = 不在本组类型表内</summary>
    internal enum EliteFamily : byte
    {
        None,
        /// <summary>格挡反击：亮起架势窗口，期间被打则反击突进，正解是停手</summary>
        Parry,
        /// <summary>二段跃击：小前扑接落点锁定大跳，锁定后不再重瞄</summary>
        Leap,
        /// <summary>相位隐现：淡出隐没，重现处凝形预告足帧后才恢复杀伤</summary>
        Phase,
        /// <summary>残影惑真：放出无伤残影分身，真体更亮</summary>
        Decoy,
        /// <summary>具名缺口散射：预告扇面固定跳过一条安全巷</summary>
        Scatter,
    }

    /// <summary>类型级风味参数：家族共用一套机制代码，此表只调强度与皮肤</summary>
    internal struct EliteProfile
    {
        public EliteFamily Family;
        /// <summary>基础触发冷却（帧），档位只按系数缩短</summary>
        public int Cooldown;
        /// <summary>家族主参（名义值，未含模式提速）：突进/跃扑速度或散射弹速</summary>
        public float Power;
        /// <summary>触发距离上限（像素）</summary>
        public float Range;
        /// <summary>家族副参：格挡=武装帧数；相位=隐没帧数</summary>
        public int Aux;
        /// <summary>攻击窗口内接触命中附加的原版减益（0=无）</summary>
        public int HitBuff;
        public int HitBuffTime;
        /// <summary>仅湿身可触发（琵琶鱼）</summary>
        public bool NeedsWet;
        /// <summary>家族内样式：散射=0箭/1激光/2毒镖；残影=0翻转/1随速旋转</summary>
        public byte Style;
        /// <summary>该类型的机制主题色（预告与辉光用）</summary>
        public Color Tint;
    }

    /// <summary>F组类型表：困难模式常见精英 → 行为档案</summary>
    internal static class EliteMoveSets
    {
        internal static readonly Dictionary<int, EliteProfile> Profiles = new() {
            //——格挡反击（读招：亮姿态=停手）——
            [NPCID.PossessedArmor] = new EliteProfile {
                Family = EliteFamily.Parry, Cooldown = 330, Power = 8.2f, Range = 700f,
                Aux = 66, Tint = new Color(250, 140, 255),
            },
            [NPCID.ArmoredSkeleton] = new EliteProfile {
                Family = EliteFamily.Parry, Cooldown = 360, Power = 7.6f, Range = 700f,
                Aux = 60, Tint = new Color(255, 205, 95),
            },
            [NPCID.Mimic] = new EliteProfile {
                Family = EliteFamily.Parry, Cooldown = 390, Power = 9.2f, Range = 620f,
                Aux = 78, Tint = new Color(235, 190, 80),
            },

            //——二段跃击（读招：落点印记锁定后位移必得回报）——
            [NPCID.Werewolf] = new EliteProfile {
                Family = EliteFamily.Leap, Cooldown = 300, Power = 8f, Range = 720f,
                HitBuff = BuffID.Bleeding, HitBuffTime = 180, Tint = new Color(255, 120, 60),
            },
            [NPCID.Unicorn] = new EliteProfile {
                Family = EliteFamily.Leap, Cooldown = 270, Power = 9.5f, Range = 780f,
                Tint = new Color(255, 130, 210),
            },
            [NPCID.BlackRecluse] = new EliteProfile {
                Family = EliteFamily.Leap, Cooldown = 330, Power = 6.5f, Range = 620f,
                HitBuff = BuffID.Poisoned, HitBuffTime = 300, Tint = new Color(150, 230, 90),
            },

            //——相位隐现（读招：凝形足帧才恢复杀伤）——
            [NPCID.Wraith] = new EliteProfile {
                Family = EliteFamily.Phase, Cooldown = 480, Power = 8.6f, Range = 950f,
                Aux = 66, Tint = new Color(170, 140, 255),
            },
            [NPCID.GiantBat] = new EliteProfile {
                Family = EliteFamily.Phase, Cooldown = 420, Power = 7.4f, Range = 820f,
                Aux = 50, Tint = new Color(170, 120, 190),
            },
            [NPCID.IlluminantBat] = new EliteProfile {
                Family = EliteFamily.Phase, Cooldown = 390, Power = 6.6f, Range = 800f,
                Aux = 46, Tint = new Color(255, 120, 255),
            },
            [NPCID.AnglerFish] = new EliteProfile {
                Family = EliteFamily.Phase, Cooldown = 450, Power = 9.4f, Range = 700f,
                Aux = 58, NeedsWet = true, HitBuff = BuffID.Bleeding, HitBuffTime = 120,
                Tint = new Color(120, 220, 255),
            },

            //——残影惑真（读招：真体更亮，弹丸穿残影而过）——
            [NPCID.ChaosElemental] = new EliteProfile {
                Family = EliteFamily.Decoy, Cooldown = 540, Power = 8.4f, Range = 1000f,
                Style = 0, Tint = new Color(230, 140, 255),
            },
            [NPCID.WanderingEye] = new EliteProfile {
                Family = EliteFamily.Decoy, Cooldown = 480, Power = 7f, Range = 900f,
                Style = 1, Tint = new Color(200, 80, 120),
            },

            //——具名缺口散射（读招：预告扇面缺的那条巷就是安全巷）——
            [NPCID.SkeletonArcher] = new EliteProfile {
                Family = EliteFamily.Scatter, Cooldown = 330, Power = 10.5f, Range = 1050f,
                Style = 0, Tint = new Color(220, 200, 160),
            },
            [NPCID.Gastropod] = new EliteProfile {
                Family = EliteFamily.Scatter, Cooldown = 390, Power = 12.5f, Range = 900f,
                Style = 1, Tint = new Color(255, 110, 230),
            },
            [NPCID.BlackRecluseWall] = new EliteProfile {
                Family = EliteFamily.Scatter, Cooldown = 300, Power = 9f, Range = 680f,
                Style = 2, Tint = new Color(150, 230, 90),
            },
        };

        internal static EliteFamily FamilyOf(int npcType)
            => Profiles.TryGetValue(npcType, out EliteProfile p) ? p.Family : EliteFamily.None;
    }
}
