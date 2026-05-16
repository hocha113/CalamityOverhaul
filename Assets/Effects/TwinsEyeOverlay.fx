//=============================================================================
// TwinsEyeOverlay.fx — 双子魔眼程序化着色器
// 在 placeholder_white 画布上程序化生成一颗 360° 旋转的双子魔眼
// 双 Technique：RetinazerEye(激光眼/青紫) 与 SpazmatismEye(魔焰眼/橙红)
// 共用 PS — 通过 uEyeMode 切换瞳孔形态、虹膜花纹和血丝走向
// ps_3_0
//=============================================================================

float uTime;          //全局时间，用于动画
float uIntensity;     //整体亮度乘数(0..2)
float uProgress;      //生命周期进度(0..1)，用于淡入淡出
float uEyeMode;       //0=激光眼，1=魔焰眼
float uPupilDilation; //瞳孔放大系数(0..1)，越大瞳孔越细
float uBloodshot;     //血丝/电弧强度(0..1)
float2 uPupilOffset;  //瞳孔偏移(-0.15..0.15)，造成"四处张望"效果
float uRotation;      //眼睛整体旋转角

//双子颜色配置
float3 uIrisColor;    //虹膜主色
float3 uPupilGlow;    //瞳孔深处的反光色
float3 uScleraColor;  //眼白底色(带机械金属感)

#define PI  3.14159265
#define TAU 6.28318530

//==============================
// 工具函数
//==============================

//伪随机
float hash21(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

//值噪声
float valueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = hash21(i);
    float b = hash21(i + float2(1.0, 0.0));
    float c = hash21(i + float2(0.0, 1.0));
    float d = hash21(i + float2(1.0, 1.0));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

//分形噪声
float fbm(float2 p)
{
    float v = 0.0;
    float a = 0.5;
    for (int i = 0; i < 4; i++)
    {
        v += valueNoise(p) * a;
        p = p * 2.13 + 17.3;
        a *= 0.5;
    }
    return v;
}

//SDF：圆
float sdCircle(float2 p, float r)
{
    return length(p) - r;
}

//SDF：垂直瞳孔(椭圆/裂缝)
float sdSlitPupil(float2 p, float h, float w)
{
    p.x /= w;
    p.y /= h;
    return length(p) - 1.0;
}

//围绕中心旋转
float2 rotate(float2 p, float a)
{
    float c = cos(a);
    float s = sin(a);
    return float2(p.x * c - p.y * s, p.x * s + p.y * c);
}

//平滑阶梯
float smoothmask(float d, float w)
{
    return smoothstep(w, -w, d);
}

//==============================
// 主像素着色器
//==============================
float4 TwinsEyePS(float2 uv : TEXCOORD0) : COLOR0
{
    float2 centered = uv - 0.5;
    centered = rotate(centered, uRotation);
    float dist = length(centered);
    float angle = atan2(centered.y, centered.x);

    //超出眼眶范围直接丢弃
    if (dist > 0.49)
        return float4(0, 0, 0, 0);

    float3 col = 0;
    float alpha = 0;

    //=========================================
    //第一层：外层光晕 (机械护甲外壳)
    //=========================================
    {
        float shellInner = 0.42;
        float shellOuter = 0.49;
        float shellMask = smoothstep(shellOuter, shellInner, dist)
                        * smoothstep(shellInner - 0.06, shellInner, dist);
        //旋转的机械装甲纹理
        float armorPattern = abs(sin(angle * 12.0 + uTime * 0.6));
        armorPattern = smoothstep(0.55, 0.95, armorPattern);
        float3 shellCol = lerp(uScleraColor * 0.45, uIrisColor * 0.7, armorPattern);
        col += shellCol * shellMask * 1.2;
        alpha += shellMask * 0.85;
    }

    //=========================================
    //第二层：眼白 (sclera) 带机械纹理
    //=========================================
    {
        float scleraR = 0.42;
        float scleraMask = smoothstep(scleraR, scleraR - 0.04, dist);

        //径向条纹(虹膜外圈机械隔栏)
        float radial = abs(sin(angle * 24.0));
        radial = smoothstep(0.7, 0.95, radial) * 0.4;

        //微弱噪声(金属磨损感)
        float wear = fbm(centered * 8.0 + uTime * 0.05);
        float3 scl = uScleraColor + radial * 0.15 + (wear - 0.5) * 0.15;

        //血丝(从眼眶向虹膜延伸的红色线条)
        float veins = 0.0;
        for (int v = 0; v < 6; v++)
        {
            float vAng = float(v) * 1.047 + uTime * 0.1;
            float2 vDir = float2(cos(vAng), sin(vAng));
            float vDot = dot(normalize(centered + 1e-5), vDir);
            float vLine = pow(saturate(vDot), 60.0);
            //血丝从距中心0.20~0.40范围内显示
            vLine *= smoothstep(0.20, 0.28, dist) * smoothstep(0.40, 0.34, dist);
            veins += vLine;
        }
        veins = saturate(veins) * uBloodshot;
        scl = lerp(scl, lerp(float3(0.9, 0.1, 0.1), uIrisColor, uEyeMode), veins * 0.85);

        col += scl * scleraMask;
        alpha += scleraMask;
    }

    //=========================================
    //第三层：虹膜外环 (机械齿轮/能量环)
    //=========================================
    {
        float irisOuter = 0.30;
        float irisInner = 0.27;
        float ringMask = smoothstep(irisOuter, irisInner, dist)
                       * smoothstep(irisInner - 0.02, irisInner, dist);

        //旋转的能量符文带
        float runePhase = angle * 8.0 + uTime * (1.5 + uIntensity * 0.5);
        float runes = abs(sin(runePhase));
        runes = smoothstep(0.4, 0.95, runes);
        //叠加反向旋转的次级符文
        float subRunes = abs(sin(angle * 16.0 - uTime * 2.0));
        subRunes = smoothstep(0.7, 1.0, subRunes) * 0.5;

        float3 ringCol = uIrisColor * (0.8 + (runes + subRunes) * 1.2);
        col += ringCol * ringMask * 1.5;
        alpha += ringMask * 0.9;
    }

    //=========================================
    //第四层：虹膜主体 (彩色等离子体)
    //=========================================
    {
        float irisR = 0.27;
        float pupilR = 0.10;
        float irisMask = smoothstep(irisR, irisR - 0.04, dist)
                       * smoothstep(pupilR - 0.02, pupilR, dist);

        //归一化虹膜半径
        float normR = saturate((dist - pupilR) / (irisR - pupilR));

        //旋转的虹膜花纹(细密放射纹)
        float irisAng = angle * 36.0 + uTime * 0.8;
        float fiber = abs(sin(irisAng));
        fiber = pow(fiber, 4.0);

        //径向湍流(等离子体)
        float turb = fbm(float2(angle * 5.0 / TAU, normR * 8.0) + uTime * 0.4);

        //同心圆纹理
        float rings = abs(sin(normR * 25.0 - uTime * 3.0));
        rings = smoothstep(0.3, 0.9, rings) * 0.6;

        //温度色彩梯度：靠近瞳孔最亮(白热)，向外变成虹膜色
        float3 hot = uPupilGlow;
        float3 cold = uIrisColor;
        float3 irisCol = lerp(hot, cold, normR);
        irisCol *= 0.6 + fiber * 0.8 + turb * 0.5 + rings;

        //双色注入(根据眼睛模式)
        //魔焰眼:橙红向中心加深;激光眼:紫蓝向中心加亮
        float depthFactor = 1.0 - normR;
        if (uEyeMode > 0.5)
        {
            //魔焰眼:加入跳动火焰心
            float flame = fbm(centered * 12.0 + float2(uTime * 1.5, -uTime * 2.0));
            flame = smoothstep(0.35, 0.9, flame);
            irisCol += float3(1.0, 0.5, 0.1) * flame * depthFactor * 0.8;
        }
        else
        {
            //激光眼:加入扫描线
            float scan = sin(centered.y * 60.0 + uTime * 8.0);
            scan = smoothstep(0.92, 1.0, scan) * 0.6;
            irisCol += float3(0.3, 0.7, 1.0) * scan;
        }

        col += irisCol * irisMask * uIntensity;
        alpha += irisMask;
    }

    //=========================================
    //第五层：瞳孔 (随眼睛模式不同形态)
    //=========================================
    {
        float2 pupilP = centered - uPupilOffset;
        float pupilDist;
        //魔焰眼:垂直裂缝瞳孔(野兽态)；激光眼:圆形机械瞳孔
        if (uEyeMode > 0.5)
        {
            float slitWidth = 0.035 + (1.0 - uPupilDilation) * 0.045;
            pupilDist = sdSlitPupil(pupilP, 0.105, slitWidth);
        }
        else
        {
            float r = 0.07 + uPupilDilation * 0.03;
            pupilDist = sdCircle(pupilP, r);
        }

        float pupilMask = smoothmask(pupilDist, 0.008);

        //瞳孔本体几乎纯黑，带一圈高光
        float3 pupilCol = float3(0.02, 0.02, 0.03);

        //瞳孔深处的反光(中心高光)
        float coreGlow = exp(-length(pupilP) * 40.0) * 0.9;
        pupilCol += uPupilGlow * coreGlow;

        //追加瞳孔环高亮(虹膜与瞳孔交界处)
        float pupilEdge = smoothstep(0.015, 0.0, abs(pupilDist + 0.012));
        pupilCol += uPupilGlow * pupilEdge * 1.8;

        //叠加上层(乘法削减下层颜色)
        col = lerp(col, pupilCol, pupilMask);
        alpha = max(alpha, pupilMask);
    }

    //=========================================
    //第六层：充能进度环 (从下往上点亮)
    //=========================================
    {
        float chargeAngle = (angle + PI) / TAU; // 0..1, 起点在左侧
        float chargeMask = smoothstep(0.305, 0.295, abs(dist - 0.30));
        float chargeFill = step(chargeAngle, uProgress);

        float3 chargeCol = lerp(uPupilGlow, uIrisColor, uProgress);
        col += chargeCol * chargeMask * chargeFill * 1.4;
        alpha = max(alpha, chargeMask * chargeFill * 0.95);
    }

    //=========================================
    //第七层：电弧/闪电(高强度模式下显现)
    //=========================================
    if (uBloodshot > 0.05)
    {
        float arcAng = angle * 3.0 + uTime * 5.0;
        float arc = abs(sin(arcAng + fbm(centered * 20.0 + uTime) * 4.0));
        arc = pow(arc, 8.0);
        float arcRing = smoothstep(0.40, 0.36, dist) * smoothstep(0.32, 0.36, dist);
        float3 arcCol = lerp(float3(0.4, 0.9, 1.0), float3(1.0, 0.5, 0.1), uEyeMode);
        col += arcCol * arc * arcRing * uBloodshot * 2.0;
        alpha += arc * arcRing * uBloodshot * 0.5;
    }

    //最终包络衰减:边缘外圈半透明
    float edgeFade = smoothstep(0.49, 0.40, dist);
    alpha = saturate(alpha * edgeFade);

    //生命周期淡入淡出
    float fade = saturate(uProgress * 4.0) * saturate((1.0 - uProgress) * 4.0 + 0.05);
    //出生与死亡阶段过渡更平滑
    fade = smoothstep(0.0, 0.15, uProgress) * smoothstep(1.0, 0.85, uProgress);

    return float4(col * uIntensity, alpha * fade);
}

//==============================
// Technique
//==============================
technique TwinsEye
{
    pass P0
    {
        PixelShader = compile ps_3_0 TwinsEyePS();
    }
}
