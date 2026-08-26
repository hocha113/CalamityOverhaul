// ============================================================================
//ShieldDome.fx 护盾发生器能量膜穹顶
//画布为归一化圆盘 quad(placeholder2)，预乘输出，配 AlphaBlend 使用
//s1=PerlinNoise(LinearWrap) 缘起伏/蜂窝行波；G通道实测值域 0.227~0.776，阈值一律过 nrm()
//
//极角纪律：角向噪声只走 LinearWrap 采样且倍角一律整数(3/5/7)；
//缘上掠光用 frac 圆环距离，跨 0/1 连续；蜂窝格 SDF 全笛卡尔双 lattice 无分支；
//涟漪为笛卡尔欧氏距离场。全程直线算术+朴素 tex2D，无动态分支。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);

float uTime;        //秒级时间(实例相位已由 C# 侧混入)
float ringProgress; //膜半径(归一化 quad 空间)
float uQuadHalf;    //quad 半宽像素数，归一化单位→像素换算
float intensity;    //总体强度包络 0~1
float expandGlow;   //半径变化强调 0~1，扩张/塌缩时缘加厚加亮
float uStress;      //电力紧张 0~1：缘分段熄灭+蜂窝逐格闪烁
float seed;         //实例随机种子
float4 uImpact0;    //受击涟漪槽：xy=冲击点(归一化quad空间) z=寿命进度0~1 w=强度
float4 uImpact1;
float4 uImpact2;
float4 uImpact3;

//PerlinNoise 实测值域 0.227~0.776 归一
float nrm(float x)
{
    return saturate((x - 0.227) / 0.549);
}

float hash11(float p)
{
    p = frac(p * 0.1031);
    p *= p + 33.33;
    p *= p + p;
    return frac(p);
}

float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//六边形蜂窝：双 lattice 最近心(无分支)，xy=到格心向量 zw=格id
float4 hexCell(float2 p)
{
    float2 r = float2(1.0, 1.7320508);
    float2 h = r * 0.5;
    float2 a = frac(p / r) * r - h;
    float2 b = frac((p - h) / r) * r - h;
    float useB = step(dot(b, b), dot(a, a));
    float2 g = lerp(a, b, useB);
    return float4(g, p - g);
}

//单槽涟漪：x=行进波带(主波+尾波) y=冲击点闪
float2 rippleWave(float2 pos, float4 imp, float pxU, float domeR)
{
    float live = step(0.001, imp.w) * step(imp.z, 0.999);
    float d = length(pos - imp.xy);
    float age = saturate(imp.z);
    float waveR = (0.06 + age * 1.05) * domeR;
    float w = (11.0 + age * 30.0) * pxU;
    float band = exp2(-abs(d - waveR) / w * 3.0);
    float band2 = exp2(-abs(d - waveR * 0.62) / (w * 1.6) * 3.0) * 0.4;
    float fade = pow(saturate(1.0 - age), 1.6) * imp.w * live;
    float flash = exp2(-d / (22.0 * pxU) * 3.0) * pow(saturate(1.0 - age * 2.4), 2.0) * imp.w * live;
    return float2((band + band2) * fade, flash);
}

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float normAngle = (atan2(centered.y, centered.x) + 3.14159265) / 6.28318531;
    float pxU = 1.0 / uQuadHalf;

    //---- 缘低频起伏：膜不是死圆 ----
    float n1 = tex2D(noiseTex, float2(normAngle * 3.0 + uTime * 0.055, 0.31 + seed)).g;
    float n2 = tex2D(noiseTex, float2(normAngle * 7.0 - uTime * 0.042, 0.67 + seed)).g;
    float disp = (nrm(n1) * 0.6 + nrm(n2) * 0.4 - 0.5) * 8.0 * pxU;

    float signedOut = dist + disp - ringProgress;
    float sideOut = step(0.0, signedOut);

    //---- 电力紧张：缘分段熄灭 + 全局压暗闪 ----
    float jFrame = floor(uTime * 30.0);
    float gutterN = tex2D(noiseTex, float2(normAngle * 5.0 + hash11(jFrame * 0.31 + seed) * 0.11, 0.13 + seed)).g;
    float gutter = smoothstep(0.30, 0.72, nrm(gutterN));
    float rimLive = 1.0 - uStress * 0.78 * gutter;
    float flick = 1.0 - uStress * 0.32 * step(hash11(jFrame * 0.417 + seed), 0.28 + uStress * 0.30);

    //---- 受击涟漪(4 槽手动展开) ----
    float2 rip = rippleWave(centered, uImpact0, pxU, ringProgress);
    rip += rippleWave(centered, uImpact1, pxU, ringProgress);
    rip += rippleWave(centered, uImpact2, pxU, ringProgress);
    rip += rippleWave(centered, uImpact3, pxU, ringProgress);
    //涟漪只活在膜内(缘上略溢出)
    float rippleMask = 1.0 - smoothstep(ringProgress, ringProgress + 10.0 * pxU, dist);
    float rippleBand = rip.x * rippleMask;
    float rippleFlash = rip.y * rippleMask;

    //---- 菲涅尔缘：白紫细芯 + 内长外短的膜辉 ----
    float coreW = 2.4 * pxU * (1.0 + expandGlow * 0.8);
    float core = 1.0 - smoothstep(0.0, coreW * 2.0, abs(signedOut));
    core = pow(saturate(core), 1.5) * rimLive;
    //涟漪撞缘时缘局部增亮
    core *= 1.0 + rippleBand * 1.6;

    float haloW = lerp(58.0, 22.0, sideOut) * pxU;
    float halo = exp2(-abs(signedOut) / haloW * 3.0) * (0.55 + expandGlow * 0.55) * (0.50 + 0.50 * rimLive);

    //---- 缘上掠光：两段弧光反向巡缘(frac 圆距跨缝连续) ----
    float sw1 = abs(frac(normAngle - frac(uTime * 0.034 + seed) + 0.5) - 0.5);
    float sw2 = abs(frac(normAngle + frac(uTime * 0.019 + seed * 1.7) + 0.5) - 0.5);
    float sweep = exp2(-sw1 / 0.045 * 3.0) * 0.85 + exp2(-sw2 / 0.075 * 3.0) * 0.45;
    sweep *= 1.0 - smoothstep(0.0, 15.0 * pxU, abs(signedOut));
    sweep *= rimLive;

    //---- 蜂窝薄膜：格线+格内微光，菲涅尔权重向缘增强 ----
    float hexScale = 62.0 * pxU; //一格约62px
    float4 hc = hexCell(centered / hexScale);
    //邻心方位在 0/60/120度 → 侧面朝邻心的尖顶六边形度量,格界=0.5
    float hd = max(abs(hc.x), abs(hc.x) * 0.5 + abs(hc.y) * 0.8660254);
    //格线：贴格界的窄带
    float hexLine = exp2(-abs(hd - 0.46) / 0.045 * 3.0);
    //格内亮度：静态 hash + 慢行波
    float cellHash = hash21(hc.zw + seed * 19.0);
    float waveN = tex2D(noiseTex, hc.zw * 0.043 + float2(uTime * 0.015, uTime * 0.010) + seed).g;
    float cellLit = 0.22 + 0.78 * smoothstep(0.22, 0.86, nrm(waveN) * 0.62 + cellHash * 0.38);
    //紧张时逐格熄灭
    float cellFlick = 1.0 - uStress * 0.85 * step(hash21(hc.zw + floor(uTime * 9.0) * 0.71), uStress * 0.45);
    //菲涅尔：中心近透、向缘增强；膜只存在于穹顶内
    float fresnel = smoothstep(0.28, 0.99, dist / max(ringProgress, 0.001));
    float insideDome = 1.0 - smoothstep(ringProgress - 2.0 * pxU, ringProgress + 3.0 * pxU, dist);
    float membrane = (hexLine * 0.8 + 0.16) * cellLit * cellFlick * fresnel * insideDome;
    //涟漪行经处蜂窝点亮
    membrane *= 1.0 + rippleBand * 2.4;

    //---- 扩张/塌缩残辉：前沿身后的内侧余光 ----
    float afterglow = exp2(signedOut / (56.0 * pxU) * 3.0) * (1.0 - sideOut) * expandGlow;

    //---- 合成(青紫系，同 ShieldGenerator.Tint 170,160,255) ----
    float3 colCore = float3(1.28, 1.24, 1.72);
    float3 colViolet = float3(0.50, 0.44, 1.06);
    float3 colDeep = float3(0.13, 0.11, 0.40);

    float3 col = colCore * (core * (1.22 + expandGlow * 0.55));
    col += colViolet * (halo * 0.85);
    col += colCore * (sweep * 0.50);
    col += colViolet * (membrane * 0.62);
    col += colDeep * (membrane * 0.55);
    col += colCore * (rippleBand * 0.80);
    col += colCore * (rippleFlash * 1.35);
    col += colViolet * (afterglow * 0.55);

    //画布边缘保险：一切分量在 quad 边界前归零
    float guard = 1.0 - smoothstep(0.90, 0.985, dist);
    col *= guard * intensity * flick;

    float alpha = saturate(core * 0.34 + membrane * 0.06 + rippleBand * 0.08 + rippleFlash * 0.12)
        * guard * intensity;

    return float4(col, alpha) * vertexColor;
}

technique Tech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
