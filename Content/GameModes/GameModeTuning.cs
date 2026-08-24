namespace CalamityOverhaul.Content.GameModes
{
    /// <summary>
    /// 档位数值表：通用增强与修罗强化的全部可调常量收拢在此，机制文件不散写数字。
    /// 档位口径见 <see cref="GameModeSystem.EffectiveTier"/>：1 残酷 / 2 修罗 / 3 毁灭（天顶世界的修罗）
    /// </summary>
    internal static class GameModeTuning
    {
        //——全体敌对 NPC：血量与伤害倍率（SetDefaults 时绑定，含无重制的 Boss）——
        private const float BrutalStatMult = 1.5f;
        private const float AsuraStatMult = 2.0f;
        private const float AnnihilationStatMult = 3.0f;

        //——仅非 Boss：提速（PostAI 位置推进系数）——
        private const float BrutalSpeedBonus = 0.45f;
        private const float AsuraSpeedBonus = 0.60f;
        private const float AnnihilationSpeedBonus = 0.8f;

        //——仅非 Boss 常态狂暴：击退抗性衰减（乘在 knockBackResist 上，越小越推不动）——
        private const float BrutalKnockbackMult = 0.6f;
        private const float AsuraKnockbackMult = 0.35f;
        private const float AnnihilationKnockbackMult = 0.1f;

        //——仅非 Boss 常态狂暴：接触伤害追加（在全局属性增幅之上）——
        private const float BrutalContactMult = 1.10f;
        private const float AsuraContactMult = 1.20f;
        private const float AnnihilationContactMult = 1.35f;

        //——毁灭下的修罗机制强化——

        /// <summary>毁灭下敌怪每次同类命中积累的免疫层数（其余档位 1 层）</summary>
        public const float AnnihilationAdaptStacksPerHit = 2f;
        /// <summary>毁灭下伤害下限相对最近命中的倍率（其余档位 1 倍）</summary>
        public const float AnnihilationFloorMult = 2f;

        /// <summary>档位血量伤害倍率</summary>
        public static float StatMult(int tier) => tier switch {
            1 => BrutalStatMult,
            2 => AsuraStatMult,
            3 => AnnihilationStatMult,
            _ => 1f,
        };

        /// <summary>档位提速系数（仅非 Boss）</summary>
        public static float SpeedBonus(int tier) => tier switch {
            1 => BrutalSpeedBonus,
            2 => AsuraSpeedBonus,
            3 => AnnihilationSpeedBonus,
            _ => 0f,
        };

        /// <summary>档位击退抗性衰减（仅非 Boss）</summary>
        public static float KnockbackMult(int tier) => tier switch {
            1 => BrutalKnockbackMult,
            2 => AsuraKnockbackMult,
            3 => AnnihilationKnockbackMult,
            _ => 1f,
        };

        /// <summary>档位接触伤害追加（仅非 Boss）</summary>
        public static float ContactMult(int tier) => tier switch {
            1 => BrutalContactMult,
            2 => AsuraContactMult,
            3 => AnnihilationContactMult,
            _ => 1f,
        };
    }
}
