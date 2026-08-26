// ============================================================================
//BRelicIrisMark.fx 血雾之瞳·伏击裂瞳印记
//材质=血中虹膜：竖瞳睁开→虹膜湿光亮起→裂纹带冲击环炸开→噪声蚀散
//极角只用整数倍角(14/7/5)保证跨±π连续；噪声全走笛卡尔坐标
//直线算术+纯tex2D无动态分支；预乘输出进 AlphaBlend 批
// ============================================================================

sampler uImage0 : register(s0);   //批次主贴图(magic pixel，不采样)

// 噪声固定 s1：C# 侧须在 pass.Apply 前显式 Textures[1]=PerlinNoise + SamplerStates[1]=LinearWrap
sampler2D noiseTex : register(s1);

float uTime;
float uProgress;   //0 出生 → 1 消亡
float uIntensity;

//克眼血色板(与 EocMotion 同源)
static const float3 VenousDark = float3(0.239, 0.024, 0.043);
static const float3 Arterial   = float3(0.557, 0.059, 0.102);
static const float3 Bright     = float3(0.831, 0.129, 0.180);
static const float3 IrisRed    = float3(1.0, 0.235, 0.188);

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 p = (coords - 0.5) * 2.0;   //-1~1，quad 为正方形
    float r = length(p);
    float theta = atan2(p.y, p.x);

    float t = saturate(uProgress);
    //相位包络：睁瞳→亮虹膜→裂纹→蚀散
    float open  = smoothstep(0.0, 0.20, t);
    float glare = smoothstep(0.05, 0.32, t);
    float crack = smoothstep(0.28, 0.60, t);
    float fade  = 1.0 - smoothstep(0.60, 1.0, t);

    //笛卡尔滚动噪声，两个频段
    float n1 = tex2D(noiseTex, p * 0.9 + float2(uTime * 0.05, -uTime * 0.03)).r;
    float n2 = tex2D(noiseTex, p * 2.3 + float2(0.37, uTime * 0.06)).r;

    //----------------------------------------------------------------
    //虹膜盘：湿撕边缘 + 放射纤维 + 环带湿光
    //----------------------------------------------------------------
    float discR = 0.55 + (n1 - 0.5) * 0.1;
    float disc = 1.0 - smoothstep(discR - 0.1, discR, r);
    //纤维：整数倍角 14，径向相位扭入
    float fiber = sin(theta * 14.0 + r * 9.0 - t * 3.0) * 0.5 + 0.5;
    fiber = fiber * 0.35 + (n2 - 0.5) * 0.3;
    float3 irisCol = lerp(Arterial, VenousDark, saturate(r / max(discR, 1e-3)));
    irisCol += Bright * saturate(fiber) * glare * 0.5;
    //湿光泽带：血是湿的，不是发光的
    float sheen = smoothstep(0.28, 0.42, r) * (1.0 - smoothstep(0.42, 0.54, r));
    irisCol += IrisRed * sheen * glare * 0.35;

    //----------------------------------------------------------------
    //竖裂瞳：透镜形暗芯，open 驱动睁开
    //----------------------------------------------------------------
    float slitH = 0.5;
    float yNorm = saturate(1.0 - (p.y * p.y) / (slitH * slitH));
    float slitW = 0.13 * open * yNorm;
    float slit = 1.0 - smoothstep(slitW, slitW + 0.05, abs(p.x));
    slit *= 1.0 - smoothstep(slitH - 0.06, slitH + 0.03, abs(p.y));

    //----------------------------------------------------------------
    //裂纹：7+5 支整数倍角辐条，随 crack 向外生长冲出盘缘
    //----------------------------------------------------------------
    float spokesA = pow(max(sin(theta * 7.0 + 1.7), 0.0), 18.0);
    float spokesB = pow(max(sin(theta * 5.0 - 0.8), 0.0), 22.0);
    float crackLen = 0.3 + crack * 0.85;
    float inCrack = smoothstep(0.14, 0.3, r)
        * (1.0 - smoothstep(crackLen - 0.12, crackLen, r + (n2 - 0.5) * 0.24));
    float crackMask = saturate(spokesA + spokesB * 0.8) * inCrack * crack;

    //----------------------------------------------------------------
    //炸开冲击环：薄锐波前，湿缘噪声抖动，走远变淡
    //----------------------------------------------------------------
    float ringR = 0.3 + crack * 0.78;
    float ring = 1.0 - smoothstep(0.0, 0.1, abs(r + (n1 - 0.5) * 0.08 - ringR));
    ring *= crack * (1.0 - smoothstep(0.72, 1.05, ringR));

    //----------------------------------------------------------------
    //合成(预乘)：瞳孔压暗盘体，裂纹与环提亮
    //----------------------------------------------------------------
    float3 col = irisCol * disc;
    col = lerp(col, VenousDark * 0.25, slit * disc);
    col += Bright * crackMask * 0.9 + IrisRed * ring * 0.5;

    float alpha = disc * (0.5 + glare * 0.38);
    alpha = max(alpha, crackMask * 0.9);
    alpha = max(alpha, ring * 0.5);

    //蚀散：噪声先蚀，不做均匀淡出
    float erode = saturate(fade * (0.65 + n1 * 0.7)) * saturate(uIntensity);
    col *= erode;
    alpha = saturate(alpha) * erode;

    return float4(col, alpha);
}

technique Technique1
{
    pass BRelicIrisMarkPass
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
