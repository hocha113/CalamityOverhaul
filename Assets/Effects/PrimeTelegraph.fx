// ============================================================================
// PrimeTelegraph.fx —— 机械骷髅王通用预警着色器（线 / 扇形 / 圆环 三模式）
// 机械扫描线风格：暗红基底 + 琥珀亮纹 + 行进刻度，进度推满时整体增亮提示玩家即将开火。
// 四边形约定：
//   线  模式 uMode=0：origin 在左端中点，uv.x=0 根部 → 1 末端，uv.y 横向
//   扇  模式 uMode=1：origin 在左端中点（顶点），uFanAngle 为扇形半角
//   环  模式 uMode=2：origin 在中心，uv 全幅映射 -1~1
// 输出预乘 alpha，配合 BlendState.Additive 使用。
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uProgress;   //0~1 预警充能进度（由 timeLeft 推导，全端一致）
float uIntensity;  //总强度
float uMode;       //0=线 1=扇 2=环
float uFanAngle;   //扇形半角（弧度）

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float3 warnDeep = float3(1.00, 0.22, 0.06);
    float3 warnHot  = float3(1.00, 0.80, 0.28);

    //脉冲随进度加速：宣告"越来越近了"
    float pulse = 0.6 + 0.4 * sin(uTime * (5.0 + 8.0 * uProgress));
    float3 col = lerp(warnDeep, warnHot, 0.30 + 0.50 * pulse * uProgress);

    float a = 0.0;

    if (uMode < 0.5)
    {
        // ---- 线：中央亮芯 + 两侧细轨 + 行进虚线 + 进度填充 ----
        float lat = abs(coords.y - 0.5) * 2.0;
        float core = 1.0 - smoothstep(0.0, 0.18, lat);
        float rail = exp(-pow((lat - 0.58) * 9.0, 2.0)) * 0.45;
        //行进虚线段（向末端流动）
        float dash = step(frac(coords.x * 14.0 - uTime * 5.0), 0.55) * 0.14;
        //进度填充：0→uProgress 一段更亮，前端带一粒光点
        float fill = 1.0 - smoothstep(uProgress - 0.02, uProgress + 0.02, coords.x);
        float head = exp(-pow((coords.x - uProgress) * 26.0, 2.0)) * 1.3;
        //根部增强、末端微衰减
        float rootBoost = 1.0 + (1.0 - smoothstep(0.0, 0.2, coords.x)) * 0.5;
        a = (core * (0.20 + 0.55 * fill + dash) + rail * 0.4 + head * core) * rootBoost;
    }
    else if (uMode < 1.5)
    {
        // ---- 扇：角度边界亮线 + 内部弧形扫描波 + 径向渐隐 ----
        float2 p = float2(coords.x, (coords.y - 0.5) * 2.0);
        float r = length(p);
        float ang = atan2(p.y, max(p.x, 1e-4));
        float absAng = abs(ang);

        float inside = 1.0 - smoothstep(uFanAngle * 0.92, uFanAngle, absAng);
        float radial = smoothstep(0.02, 0.10, r) * (1.0 - smoothstep(0.82, 1.0, r));
        //两条角度边界的硬亮线
        float edge = exp(-pow((absAng - uFanAngle) * 24.0, 2.0)) * 1.2;
        //从顶点向外行进的弧形扫描波
        float arcs = step(frac(r * 6.0 - uTime * 2.4), 0.4) * 0.18;
        //径向进度填充（扫描半径从顶点推向外缘）
        float fillR = 1.0 - smoothstep(uProgress - 0.04, uProgress + 0.04, r);

        a = inside * radial * (0.10 + 0.28 * uProgress + arcs + 0.22 * fillR)
          + edge * radial * (0.35 + 0.65 * uProgress);
    }
    else
    {
        // ---- 环：收缩圆环 + 旋转刻度 + 中心十字准星 ----
        float2 c = (coords - 0.5) * 2.0 + 1e-5;
        float r = length(c);
        float ang = atan2(c.y, c.x);

        //环半径随进度微收缩——"包围圈正在合拢"
        float ringR = lerp(0.95, 0.78, uProgress);
        float ring = exp(-pow((r - ringR) * 20.0, 2.0));
        //旋转的刻度虚线
        float dash = lerp(0.35, 1.0, step(0.3, frac(ang / 6.28318 * 24.0 + uTime * 0.6)));
        //中心十字准星（短臂，靠近中心才显示）
        float axisDist = min(abs(c.x), abs(c.y));
        float cross = exp(-pow(axisDist * 26.0, 2.0)) * (1.0 - smoothstep(0.28, 0.5, r)) * 0.4;

        a = ring * dash * (0.5 + 0.5 * pulse) * (0.45 + 0.55 * uProgress) + cross * uProgress;
    }

    a *= uIntensity;
    a = saturate(a);
    return float4(col * a * vertexColor.rgb, a * vertexColor.a);
}

technique Technique1
{
    pass PrimeTelegraphPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
