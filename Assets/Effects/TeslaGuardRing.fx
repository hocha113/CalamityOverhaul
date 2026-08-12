// ============================================================================
//TeslaGuardRing.fx 特斯拉塔护卫力场边界环
//画布为归一化圆盘 quad(placeholder2)，预乘输出，配 AlphaBlend 使用
//s1=PerlinNoise(LinearWrap) 边缘扰动/丝流门控；s2=Extra_193 Voronoi(LinearWrap) 内域薄膜
//
//极角纪律：角向噪声只走 LinearWrap 采样且倍角一律整数(2/3/5/7/9/13)；
//逐帧毛刺的角向格数取整数(220)；电荷节点用 frac 圆环距离，跨 0/1 连续；
//薄膜噪声走刚体旋转笛卡尔坐标。全程直线算术+朴素 tex2D，无动态分支。
// ============================================================================

sampler uImage0 : register(s0);
sampler noiseTex : register(s1);
sampler voroTex : register(s2);

float uTime;        //秒级时间(实例相位已由 C# 侧混入)
float ringProgress; //环半径(归一化 quad 空间)
float uQuadHalf;    //quad 半宽像素数，归一化单位→像素换算
float intensity;    //总体强度包络 0~1
float expandGlow;   //半径变化强调 0~1，扩张/塌缩时前沿加厚加亮
float seed;         //实例随机种子

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

float4 PixelShaderFunction(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0
{
    float2 centered = coords * 2.0 - 1.0;
    float dist = length(centered);
    float normAngle = (atan2(centered.y, centered.x) + 3.14159265) / 6.28318531;

    float pxU = 1.0 / uQuadHalf; //一个像素对应的归一化长度

    //---- 边缘低频扰动：环不是死圆 ----
    float n1 = tex2D(noiseTex, float2(normAngle * 3.0 + uTime * 0.11, 0.23 + seed)).r;
    float n2 = tex2D(noiseTex, float2(normAngle * 7.0 - uTime * 0.07, 0.61 + seed)).r;
    float disp = (n1 * 0.62 + n2 * 0.38 - 0.5) * 9.0 * pxU;

    //---- 逐帧毛刺：电的瞬态，角向 220 格每帧重掷径向微位移 ----
    float jFrame = floor(uTime * 46.0);
    float jitter = (hash21(float2(floor(normAngle * 220.0), jFrame + seed * 7.1)) - 0.5) * 3.6 * pxU;

    float signedOut = dist + disp + jitter - ringProgress; //>0 在环外

    //---- 主环：白热细芯 + 电青 halo(内长外短的不对称衰减) ----
    float coreW = 2.2 * pxU * (1.0 + expandGlow * 0.9);
    float core = 1.0 - smoothstep(0.0, coreW * 2.0, abs(signedOut));
    core = pow(saturate(core), 1.6);

    float sideOut = step(0.0, signedOut);
    float haloW = lerp(54.0, 26.0, sideOut) * pxU;
    float halo = exp2(-abs(signedOut) / haloW * 3.0) * (0.62 + expandGlow * 0.55);

    //---- 爬行电弧丝：三股细丝绕环游走，角向门控成移动弧段 ----
    float fOff1 = (tex2D(noiseTex, float2(normAngle * 5.0 + uTime * 0.43, 0.08 + seed)).r - 0.5) * 24.0 * pxU;
    float fOff2 = (tex2D(noiseTex, float2(normAngle * 9.0 - uTime * 0.31, 0.44 + seed)).r - 0.5) * 16.0 * pxU;
    float fOff3 = (tex2D(noiseTex, float2(normAngle * 13.0 + uTime * 0.57, 0.86 + seed)).r - 0.5) * 10.0 * pxU;

    float gate1 = smoothstep(0.46, 0.74, tex2D(noiseTex, float2(normAngle * 2.0 + uTime * 0.16, 0.31 + seed)).r);
    float gate2 = smoothstep(0.48, 0.76, tex2D(noiseTex, float2(normAngle * 3.0 - uTime * 0.12, 0.69 + seed)).r);
    float gate3 = smoothstep(0.50, 0.78, tex2D(noiseTex, float2(normAngle * 2.0 + uTime * 0.21, 0.95 + seed)).r);

    //随机熄灭帧：每股独立掷骰，暗而不灭
    float dim1 = 0.30 + 0.70 * step(0.22, hash11(jFrame * 0.711 + seed + 1.7));
    float dim2 = 0.30 + 0.70 * step(0.22, hash11(jFrame * 0.531 + seed + 4.2));
    float dim3 = 0.30 + 0.70 * step(0.22, hash11(jFrame * 0.377 + seed + 8.9));

    float filW = 1.6 * pxU;
    float f1 = (1.0 - smoothstep(0.0, filW * 2.0, abs(signedOut - fOff1))) * gate1 * dim1;
    float f2 = (1.0 - smoothstep(0.0, filW * 2.0, abs(signedOut - fOff2))) * gate2 * dim2;
    float f3 = (1.0 - smoothstep(0.0, filW * 2.0, abs(signedOut - fOff3))) * gate3 * dim3;
    float filament = saturate(f1 + f2 + f3);
    //丝周围的弱辉光
    float filGlow = (exp2(-abs(signedOut - fOff1) / (9.0 * pxU) * 3.0) * gate1 * dim1
                   + exp2(-abs(signedOut - fOff2) / (8.0 * pxU) * 3.0) * gate2 * dim2
                   + exp2(-abs(signedOut - fOff3) / (7.0 * pxU) * 3.0) * gate3 * dim3) * 0.34;

    //---- 电荷节点：4 个亮点沿环滑行，frac 圆距跨缝连续 ----
    float circU = 6.28318531 * ringProgress; //角1.0对应的弧长(归一化单位)
    float rdN = signedOut - disp;            //节点贴基准圆，不吃低频扰动

    float ad1 = (abs(frac(normAngle - frac(uTime * 0.061 + seed) + 0.5) - 0.5)) * circU;
    float ad2 = (abs(frac(normAngle + frac(uTime * 0.047 + seed * 2.0) + 0.5) - 0.5)) * circU;
    float ad3 = (abs(frac(normAngle - frac(uTime * 0.083 + seed * 3.0) + 0.5) - 0.5)) * circU;
    float ad4 = (abs(frac(normAngle + frac(uTime * 0.029 + seed * 4.0) + 0.5) - 0.5)) * circU;

    float nodeD1 = length(float2(ad1, rdN));
    float nodeD2 = length(float2(ad2, rdN));
    float nodeD3 = length(float2(ad3, rdN));
    float nodeD4 = length(float2(ad4, rdN));

    float pulse1 = 0.72 + 0.28 * sin(uTime * 6.3 + 0.0);
    float pulse2 = 0.72 + 0.28 * sin(uTime * 7.1 + 2.1);
    float pulse3 = 0.72 + 0.28 * sin(uTime * 5.7 + 4.2);
    float pulse4 = 0.72 + 0.28 * sin(uTime * 6.7 + 1.1);

    float nodeCore = exp2(-nodeD1 / (5.0 * pxU) * 3.0) * pulse1
                   + exp2(-nodeD2 / (5.0 * pxU) * 3.0) * pulse2
                   + exp2(-nodeD3 / (5.0 * pxU) * 3.0) * pulse3
                   + exp2(-nodeD4 / (5.0 * pxU) * 3.0) * pulse4;
    float nodeGlow = exp2(-nodeD1 / (16.0 * pxU) * 3.0) * pulse1
                   + exp2(-nodeD2 / (16.0 * pxU) * 3.0) * pulse2
                   + exp2(-nodeD3 / (16.0 * pxU) * 3.0) * pulse3
                   + exp2(-nodeD4 / (16.0 * pxU) * 3.0) * pulse4;

    //---- 内域力场薄膜：贴环内侧的 Voronoi 衰减带，向心归零 ----
    float filmW = 132.0 * pxU;
    float filmZone = saturate(1.0 + signedOut / filmW) * step(signedOut, 0.0);
    filmZone *= filmZone;
    //刚体旋转笛卡尔采样，无极角参与
    float ca = cos(uTime * 0.05);
    float sa = sin(uTime * 0.05);
    float2 rp = float2(centered.x * ca - centered.y * sa, centered.x * sa + centered.y * ca);
    float2 vuv = rp * (uQuadHalf / 380.0);
    float vor = tex2D(voroTex, vuv).r;
    float vor2 = tex2D(voroTex, vuv * 1.7 + float2(uTime * 0.013, -uTime * 0.009)).r;
    float film = pow(saturate(vor), 2.4) * (0.45 + 0.75 * vor2) * filmZone;

    //---- 扩张/塌缩残辉：前沿身后的内侧余光 ----
    float afterglow = exp2(signedOut / (62.0 * pxU) * 3.0) * (1.0 - sideOut) * expandGlow;

    //---- 全局电闪 ----
    float flick = 0.90 + 0.10 * (hash11(jFrame * 0.253 + seed) * 2.0 - 1.0);

    //---- 合成(电青系) ----
    float3 colCore = float3(1.30, 1.62, 1.68);
    float3 colCyan = float3(0.20, 0.86, 1.02);
    float3 colDeep = float3(0.035, 0.30, 0.40);

    float3 col = colCore * (core * (1.30 + expandGlow * 0.6));
    col += colCyan * (halo * 0.85);
    col += colCore * (filament * 0.85);
    col += colCyan * filGlow;
    col += colCore * (nodeCore * 0.95);
    col += colCyan * (nodeGlow * 0.40);
    col += colDeep * (film * (0.85 + expandGlow * 0.45));
    col += colCyan * (afterglow * 0.55);

    //画布边缘保险：一切分量在 quad 边界前归零
    float guard = 1.0 - smoothstep(0.90, 0.985, dist);
    col *= guard * intensity * flick;

    float alpha = saturate(core * 0.40 + nodeCore * 0.18) * guard * intensity;

    return float4(col, alpha) * vertexColor;
}

technique Tech
{
    pass P0
    {
        PixelShader = compile ps_3_0 PixelShaderFunction();
    }
}
