// ============================================================================
//BRelicIrisBlob.fx 血雾之瞳·液态血团（本体血核 / 拉丝珠段 / 甩出的血块 共用一支）
//SpriteBatch Immediate 单 quad：quad 沿运动方向拉长（+x 迎风，y 横向），coords→p∈[-1,1]²
//顶点色打包：R=噪声种子  G=半幅px/64  B=长宽比/8  A=不透明度
//组级 uniform：uTear 轮廓撕裂 / uBackTear 背风丝根 / uHeat 压密与湿光 / uFlow 表面回流速度
//
//材质=高速中的血：前钝后毛
//  1) 迎风端圆钝、略亮（血被压密），背风端被噪声撕成向后拖的丝根（高速液体拉丝的起点）；
//  2) 表面纹理顺 x 向后流，暗脉络/亮斑块随之滚动，团块每帧都在变；
//  3) 上前侧一条断续湿光（血是湿的、不发光），边缘 1.4px 暗沿（厚度读数），无白热；
//  4) 半幅 <3px 的小珠不画湿光（一颗珠只剩暗沿和体色，正是珠链该有的样子）
//
//噪声全走笛卡尔坐标；直线代码+纯 tex2D，剔除靠门控乘法；预乘输出进 AlphaBlend 批
//可见体半径 = 0.52 quad 半幅，C# 侧按 BlobQuadScale 折算 quad 尺寸
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图(magic pixel，不采样)
//噪声固定 s1：C# 侧在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

float uTime;      //秒
float uTear;      //轮廓撕裂 0..1
float uBackTear;  //背风丝根 0..1
float uHeat;      //压密亮度与湿光 0..1
float uFlow;      //表面回流速度倍率

//克眼血色板(与 EocMotion 同源)，无白热
static const float3 RimDark    = float3(0.10, 0.008, 0.02);
static const float3 DeepVein   = float3(0.165, 0.014, 0.03);
static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);
static const float3 Highlight  = float3(0.95, 0.36, 0.34);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0
{
    float2 p = coords * 2.0 - 1.0;              //x 迎风 +，y 横向
    float seed = vc.r * 7.31;
    float halfPx = max(vc.g * 64.0, 1.0);       //quad 半幅 px（y 向）
    float aspect = max(vc.b * 8.0, 0.5);        //长/宽
    float alphaIn = vc.a;

    //---- 噪声坐标：PerlinNoise 一格 512 纹素约 8 个特征，按 ~300px/格取样使特征 ≈40px、
    //     每屏幕像素 ≈1.7 纹素（更密就成椒盐）；x 压 0.45 让斑块顺速度拉长 ----
    float2 np = p * float2(aspect * 0.45, 1.0) * (halfPx / 300.0);
    float2 flowOff = float2(uTime * 0.35 * uFlow, 0.0);
    float n1 = tex2D(noiseTex, np + flowOff + float2(seed, seed * 0.37)).r;
    float n2 = tex2D(noiseTex, np * 2.6 + flowOff * 1.7 + float2(seed * 0.5 + 0.3, -seed)).r;

    float backness = saturate(0.5 - p.x * 0.5);   //1 背风端 → 0 迎风端

    //---- 轮廓：迎风小抖、背风大撕（Perlin 值域 0.22~0.78，先拉满幅） ----
    float n1c = saturate((n1 - 0.5) * 1.8 + 0.5);
    float n2c = saturate((n2 - 0.5) * 1.8 + 0.5);
    float disp = (n1c - 0.5) * 0.22 * uTear * (0.45 + 0.55 * backness)
               + (n2c - 0.5) * 0.10 * uTear * backness;

    //---- 背风丝根：沿 x 拉长、沿 y 压窄的条纹噪声，阈值挑出向后拖的细丝 ----
    float streak = tex2D(noiseTex, float2(p.x * 0.05 + uTime * 0.2 * uFlow + seed * 1.9, p.y * 0.35 * (halfPx / 22.0) + seed * 0.7)).r;
    float roots = pow(saturate((streak - 0.50) / 0.26), 1.5) * backness * backness * uBackTear;

    float r = length(p);
    float d = r - 0.52 - disp - roots * 0.36;    //<0 体内

    //---- 边缘：1.3px 抗锯齿 + 按尺寸缩放的暗沿 ----
    float aa = 1.3 / halfPx;
    float body = 1.0 - smoothstep(-aa, aa, d);
    float edgePx = -d * halfPx;
    float rimW = min(1.4, halfPx * 0.3);
    float rim = 1.0 - smoothstep(rimW * 0.6, rimW * 1.6, edgePx);

    //---- 体色：暗脉络 + 动脉斑块顺流滚动；迎风压密略亮 ----
    float tone = n1c * 0.6 + n2c * 0.4;
    float3 col = lerp(VenousDark, Arterial, smoothstep(0.36, 0.68, tone));
    col = lerp(col, DeepVein, smoothstep(0.42, 0.2, n2c) * 0.65);
    col = lerp(col, Bright, saturate(p.x * 0.9) * 0.4 * uHeat);

    //---- 湿光：上前侧、距边 2.3px、被噪声断成短划；小珠不画 ----
    float specGate = smoothstep(3.0, 6.0, halfPx) * uHeat;
    float spec = (1.0 - smoothstep(1.0, 3.2, abs(edgePx - 2.3)))
               * smoothstep(0.05, 0.5, -p.y) * smoothstep(-0.5, 0.2, p.x)
               * smoothstep(0.45, 0.62, n2c) * specGate;
    col = lerp(col, Highlight, spec * 0.9);

    col = lerp(col, RimDark, rim * 0.75);

    float alpha = body * alphaIn * 0.96;
    return float4(col * alpha, alpha) * step(0.003, alpha);
}

technique Technique1
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
