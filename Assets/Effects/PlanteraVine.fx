// ============================================================================
//PlanteraVine.fx 世纪之花活体藤蔓
//UV.x 0根(本体)→1梢(钩爪) UV.y 横截面；预乘输出+AlphaBlend
//材质=活体藤蔓：纤维沿纹+圆柱明暗+毛边撕裂+生物荧光脉络行波+张力应变
//全笛卡尔域无极角，无分支，噪声全走绑定贴图
//ps_3_0 / vs_3_0
// ============================================================================

float4x4 transformMatrix;
float uTime;      //滚动时间
float uFade;      //整体透明度 0~1
float uTaut;      //张力 0松弛~1绷紧(变窄变直变亮)
float uPulse;     //蓄力脉冲强度 0~1(脉络行波增速增亮)
float uPulseDir;  //行波方向 +1根→梢 -1梢→根
float uGrow;      //生长进度 0~1，1=完整
float uPhase2;    //0一阶段绿 1二阶段品红
float seed;       //实例种子

// 噪声固定 s1：sampler_state 自动分配会落 s0，图元路径今日侥幸、批次路径必坏；
// C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler noiseSamp : register(s1);

struct VSInput
{
    float4 Position : POSITION0;
    float2 TexCoords : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float2 TexCoords : TEXCOORD0;
};

PSInput VertexShaderFunction(VSInput v)
{
    PSInput o;
    o.Position = mul(v.Position, transformMatrix);
    o.Color = v.Color;
    o.TexCoords = v.TexCoords;
    return o;
}

float4 PixelShaderFunction(PSInput input) : COLOR0
{
    float2 uv = input.TexCoords;
    float along = uv.x;
    float cross_ = (uv.y - 0.5) * 2.0; //-1~1 横截面

    //=========================================================
    //主轴蜿蜒：藤不是直线，绷紧时拉直
    //=========================================================
    float wob = tex2D(noiseSamp, float2(along * 2.2 + seed, seed * 3.0 + uTime * 0.04)).r - 0.5;
    float axis = wob * 0.34 * (1.0 - uTaut);
    float d = abs(cross_ - axis);

    //=========================================================
    //宽度轮廓：根粗梢细，绷紧收窄；边缘噪声毛口
    //=========================================================
    float taper = lerp(1.0, 0.58, along) * lerp(1.0, 0.66, uTaut);
    float edgeN = tex2D(noiseSamp, float2(along * 8.5 - uTime * 0.12, seed + cross_ * 0.3)).r;
    float bodyEdge = taper * (0.68 + 0.30 * edgeN);
    float body = smoothstep(bodyEdge, bodyEdge - 0.20, d);

    //=========================================================
    //生长啃噬：along>uGrow 被吃掉，前沿噪声撕裂
    //=========================================================
    float growN = tex2D(noiseSamp, float2(along * 6.0 + seed * 7.0, cross_ * 0.8 + seed)).r - 0.5;
    float grow = smoothstep(uGrow + 0.05, uGrow - 0.03, along + growN * 0.09);

    //=========================================================
    //圆柱明暗+纤维沿纹：它是实体不是光带
    //=========================================================
    float lam = 1.0 - saturate(d / max(bodyEdge, 0.001));
    float shade = 0.40 + 0.60 * pow(lam, 0.72);
    float fiber = tex2D(noiseSamp, float2(along * 13.0 + seed * 5.0, cross_ * 1.8 + seed * 2.0)).r;
    shade *= 0.84 + 0.32 * fiber;

    //=========================================================
    //生物荧光脉络：蜿蜒亮脉+行波(常态慢呼吸，蓄力涌流)
    //=========================================================
    float veinPath = (tex2D(noiseSamp, float2(along * 3.1 + seed * 2.0, 0.37 + seed)).r - 0.5) * 0.5 * taper;
    float vd = abs(cross_ - axis - veinPath);
    float vein = exp(-vd * vd * 110.0);
    float wavePhase = frac(along * 1.7 - uTime * (0.30 + uPulse * 1.9) * uPulseDir + seed * 3.0);
    float wave = exp(-pow((wavePhase - 0.5) * 3.2, 2.0));
    float veinGlow = vein * (0.20 + 0.62 * wave) * (0.50 + uPulse * 1.55);

    //生长前沿嫩芽亮头(uGrow=1 时消失)
    float frontier = exp(-pow((along - uGrow) * 9.0, 2.0)) * saturate((1.0 - uGrow) * 4.0);
    //张力应变高频微闪
    float strain = uTaut * (0.5 + 0.5 * sin(uTime * 26.0 + along * 34.0 + seed * 11.0)) * 0.14;

    //=========================================================
    //调色板：一阶段丛林绿/查特绿荧光，二阶段暗肉紫/品红荧光
    //=========================================================
    float3 cBark = lerp(float3(0.085, 0.135, 0.050), float3(0.120, 0.055, 0.095), uPhase2);
    float3 cFlesh = lerp(float3(0.230, 0.400, 0.120), float3(0.360, 0.130, 0.240), uPhase2);
    float3 cVein = lerp(float3(0.600, 1.000, 0.330), float3(1.000, 0.420, 0.720), uPhase2);

    float3 color = lerp(cBark, cFlesh, shade);
    color += cVein * (veinGlow + frontier * 0.9 + strain);

    float alpha = saturate(body * grow) * uFade * input.Color.a;
    //世界光照乘染(vertex RGB 由 CPU 采样)
    color *= lerp(float3(1.0, 1.0, 1.0), input.Color.rgb, 0.72);
    //荧光脉络突破环境暗度(自发光份额)
    color += cVein * veinGlow * 0.35;

    return float4(color * alpha, alpha);
}

technique Technique1
{
    pass PlanteraVinePass
    {
        VertexShader = compile vs_3_0 VertexShaderFunction();
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
