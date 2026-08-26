// ============================================================================
//BRelicStoneGuard.fx 日核拳骨·石卫姿态
//TechShell：预乘 AlphaBlend 批——环抱玩家的玄武岩护壳（暗石真遮挡，不是加色光泡），
//  裂纹用 GolemMagmaVein 同款脊线噪声语汇，熔岩脉络随蓄能自下而上逐层点亮，
//  uFlare 受击过曝脉冲，uAura 满层时外缘挂余烬
//TechAura：Additive 批满层灼热光环——锐外锋+缓内尾+上浮火舌，
//  强度写进 A（加色批源因子是 SrcAlpha，写 0 整层隐形）
//全笛卡尔无极角、无动态分支；噪声固定 s1，C# 侧 pass.Apply 前显式
//Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图（白像素画布，本 shader 不采样）
sampler noiseS : register(s1);    //PerlinNoise

float uTime;
float uForm;     //石壳成形 0~1（自脚底向上长，前沿噪声撕口）
float uCharge;   //蓄能可视化 0~1（=层数/满层，脉络自下而上点亮）
float uFlare;    //受击闪 0~1
float uAura;     //灼热光环强度 0~1
float uSeed;     //逐玩家去相关相位
float uAspect;   //quad 宽/高（TechShell 用）

//PerlinNoise 实测值域 0.23~0.776（阈值消费前归一，Noise-threshold rule）
float Nrm(float v)
{
    return saturate((v - 0.23) * 1.83);
}

float SdSegment(float2 p, float2 a, float2 b)
{
    float2 pa = p - a;
    float2 ba = b - a;
    float h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-5));
    return length(pa - ba * h);
}

//------------------------------------------------------------------
//石卫护壳：贴体石甲环带 + 裂纹脉络逐层点亮
//------------------------------------------------------------------
float4 ShellPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    //画布坐标：y∈[-1,1]，x 按宽高比展开
    float2 p = (coords - 0.5) * 2.0;
    p.x *= uAspect;

    //躯干竖向胶囊距离场（头上脚下，锚在玩家中心），壳底略入地读作扎根
    float d = SdSegment(p, float2(0.0, -0.18), float2(0.0, 0.10));

    //护壳环带：中心留空露出玩家本体
    float band = smoothstep(0.26, 0.36, d) * (1.0 - smoothstep(0.44, 0.58, d));

    //石板噪声（裂纹=脊线，语汇同 GolemMagmaVein）
    float n1 = Nrm(tex2D(noiseS, p * 0.62 + uSeed).r);
    float n2 = Nrm(tex2D(noiseS, p * 1.35 + float2(uSeed, 0.37) - float2(0.0, uTime * 0.006)).r);
    float ridge = abs(n1 - 0.5) * 1.6 + abs(n2 - 0.5) * 0.7;
    float crack = 1.0 - smoothstep(0.05, 0.16, ridge);

    //成形：自脚底(coords.y=1)向上长，前沿噪声撕口
    float formEdge = 1.0 - uForm * 1.25 + (n2 - 0.5) * 0.14;
    float grown = smoothstep(formEdge, formEdge + 0.10, coords.y);

    //石面：玄武岩暗面 + 顶光，裂纹刻痕压暗
    float topLight = saturate(0.62 - p.y * 0.42);
    float3 stone = lerp(float3(0.115, 0.105, 0.10), float3(0.30, 0.28, 0.255), n1 * 0.85 + n2 * 0.15);
    stone *= topLight;
    stone *= 1.0 - crack * 0.55;

    //脉络点亮：自下而上按蓄能推进，前沿噪声抖动
    float litEdge = 1.0 - uCharge * 1.12 + (n1 - 0.5) * 0.12;
    float lit = smoothstep(litEdge, litEdge + 0.16, coords.y);
    //缝内流动亮波（MagmaVein 同款相位滚动）
    float flow = 0.55 + 0.45 * (sin(n1 * 14.0 + n2 * 9.0 - uTime * 3.4) * 0.5 + 0.5);
    float heat = saturate(crack * lit * flow * (1.0 + uFlare * 1.6));

    float3 deepRed = float3(0.55, 0.08, 0.02);
    float3 orange = float3(1.00, 0.45, 0.08);
    float3 gold = float3(1.00, 0.85, 0.40);
    float3 vein = lerp(deepRed, orange, saturate(heat * 1.7));
    vein = lerp(vein, gold, saturate(heat * heat * 2.2));

    //满层外缘余烬
    float rim = smoothstep(0.38, 0.46, d) * (1.0 - smoothstep(0.46, 0.58, d));
    float3 ember = float3(1.0, 0.55, 0.14) * rim * uAura
        * (0.55 + 0.45 * sin(uTime * 4.1 + p.x * 3.0 + p.y * 2.0));

    //画布护栏：内容在边缘前自然归零
    float guard = 1.0 - smoothstep(0.86, 0.99, max(abs(p.x) / max(uAspect, 1e-3), abs(p.y)));

    float mask = band * grown * guard * vertexColor.a;
    float a = mask * 0.90;
    //预乘输出：石面吃 alpha，脉络/余烬作为溢出光加在 rgb 上
    float3 col = stone * a + (vein * heat * (0.9 + uFlare * 0.8) + ember) * mask;
    return float4(col * vertexColor.rgb, a);
}

//------------------------------------------------------------------
//灼热光环：锐外锋 + 缓内尾 + 上浮火舌 + 内域热霾
//------------------------------------------------------------------
float4 AuraPS(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;
    float r = length(p);

    //刚体旋转坐标采噪声（无极角接缝）
    float cs = cos(uTime * 0.22);
    float sn = sin(uTime * 0.22);
    float2 rc = float2(p.x * cs - p.y * sn, p.x * sn + p.y * cs);

    float n1 = Nrm(tex2D(noiseS, rc * 0.85 + uSeed).r);
    float n2 = Nrm(tex2D(noiseS, p * 1.6 + float2(0.0, uTime * 0.10) + uSeed).r);

    //火环：缘口被噪声撕开
    float ringR = 0.77 + (n1 - 0.5) * 0.10;
    float outerFront = exp(-max(r - ringR, 0.0) * 22.0);
    float innerTail = exp(-max(ringR - r, 0.0) * 6.5);
    float ring = max(outerFront, innerTail * 0.55);

    //火舌：环带附近的高阈值噪声团，上浮流动
    float tongue = smoothstep(0.55, 0.82, n2) * exp(-abs(r - ringR) * 7.0);

    //内域热霾微光
    float haze = exp(-r * 2.6) * 0.16 * (0.7 + 0.3 * n2);

    float breath = 0.82 + 0.18 * sin(uTime * 3.7 + uSeed * 6.0);
    float t = saturate((ring * 0.75 + tongue * 0.9 + haze) * breath * uAura);

    float3 edgeCol = float3(0.85, 0.28, 0.05);
    float3 midCol = float3(1.00, 0.60, 0.16);
    float3 hotCol = float3(1.00, 0.90, 0.62);
    float3 col = lerp(edgeCol, midCol, saturate(t * 1.8));
    col = lerp(col, hotCol, saturate((t - 0.55) * 1.6));

    float guard = 1.0 - smoothstep(0.86, 0.99, r);
    float a = t * guard;
    //Additive 批源因子是 SrcAlpha：强度必须写进 A
    return float4(col * vertexColor.rgb, a * vertexColor.a);
}

technique TechShell
{
    pass ShellPass
    {
        PixelShader = compile ps_3_0 ShellPS();
    }
}

technique TechAura
{
    pass AuraPass
    {
        PixelShader = compile ps_3_0 AuraPS();
    }
}
