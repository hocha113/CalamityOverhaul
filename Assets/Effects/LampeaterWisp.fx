// ============================================================================
//LampeaterWisp.fx 噬灯魂本体（L3 档案馆精英，食灯与无光）
//材质身份：吞灯的烛烟——墨烟壳裹粒状烬芯。三个签名行为：
//  ①烟体沿运动方向拉伸、静止时头上尾下悬停，尾部流苏被上涌烟纹撕散（uStretch / 噪声上升流）
//  ②烬芯是粒状余烬簇不是光球，吃一盏灯多一颗绕芯的灯魂珠（uFeed 分级）
//  ③扑食前整团向心倒吸（uInhale：壳收缩、流向反转、收拢环）
//TechSmokeBody: 墨烟壳（预乘输出带实 alpha，能遮挡背景——本体唯一的暗层，
//  画在原版精灵图之下）；轮廓=宽度剖面+双频撕裂噪声，鼻端干净尾部撕散；
//  烬芯从烟内透光，环境光 uEnvLight 喂烟体受光度（烟受光，烬自发光）
//TechEmberFlame: 烬芯亮层（画在精灵图之上）：粒状余烬+紧凑底辉（底辉只做
//  под层≤30%视觉质量）+进食火舌（根实尖碎）+灯魂珠×3（可数进食数）+倒吸收拢环
//坐标全笛卡尔（无 atan2）；灯魂珠轨道用 cos/sin(uTime) 连续无接缝
//绑定噪声 PerlinNoise 实测值域 0.227~0.776，阈值一律过 nrm() 归一
//消费入口 Content/Scenarios/Dungeonworld/NPCs/Elites/LampeaterWisp.cs（PreDraw）
// ============================================================================

sampler uImage0 : register(s0);   //批主纹理：白像素 quad，不采样
sampler uNoiseTex : register(s1); //PerlinNoise，LinearWrap，消费端上 s1

float uTime;      //秒
float uSeed;      //个体相位
float uFeed;      //0~1 进食度（口数/3）
float uEmber;     //0~1.05 烬芯亮度（含 telegraph 增亮与呼吸下限，C# 端算好）
float uInhale;    //0~1 倒吸进度（telegraph 内推进，其余 0）
float uStretch;   //1~1.9 沿体轴速度拉伸（quad 已按此拉长，shader 补偿噪声频率）
float uEnvLight;  //0~1 环境光亮度（烟体受光）
float uFade;      //0~1 整体不透明度（NPC.Opacity）
float uSwell;     //0~1 进食后胀亮脉冲（吃到一口后衰减）

//====== 色板：烛橙烬芯 + 墨褐烟体（L3 纸墨语汇，比灾厄金鬼火更红更暗）======
static const float3 INK_SMOKE  = float3(0.078, 0.062, 0.050); //墨烟基色
static const float3 EMBER_BODY = float3(1.000, 0.700, 0.330); //烬体橙
static const float3 EMBER_HOT  = float3(1.000, 0.900, 0.700); //暖白热粒（非纯白）
static const float3 TIP_AMBER  = float3(0.830, 0.420, 0.130); //舌尖琥珀
static const float3 INHALE_PALE= float3(0.880, 0.860, 0.800); //倒吸收拢环的苍光

//绑定噪声实测值域归一（0.227~0.776）
float nrm(float n) {
    return saturate((n - 0.227) * 1.821);
}

//体轴归一坐标：头在 v=0（运动方向），核心 (0.5, 0.58)，x 半宽 0.30 uv、y 半高 0.26 uv
float2 BodyCoord(float2 coords) {
    return float2((coords.x - 0.5) / 0.30, (coords.y - 0.58) / 0.26);
}

//====== TechSmokeBody：墨烟壳（唯一暗层，预乘实 alpha）======
float4 PSSmokeBody(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    float2 p = BodyCoord(coords);
    //倒吸：整壳收缩（坐标放大即收缩）
    float shrink = 1.0 - 0.20 * uInhale;
    p /= shrink;
    float xN = p.x;
    float yN = p.y;

    //速度拉伸补偿：quad 拉长后纵向噪声频率同除，烟纹不被抻糊
    float yFlow = yN / max(uStretch, 1.0);
    //流向：常态尾向（+y 卷出），倒吸反转向芯
    float flowSign = 1.0 - 2.0 * smoothstep(0.05, 0.45, uInhale);

    //双频撕裂噪声（慢，鬼火不是篝火）
    float n1 = nrm(tex2D(uNoiseTex, float2(xN * 0.21 + uSeed, yFlow * 0.16 - uTime * 0.16 * flowSign)).r);
    float n2 = nrm(tex2D(uNoiseTex, float2(xN * 0.55 - uSeed * 1.3, yFlow * 0.42 - uTime * 0.34 * flowSign)).r);

    //宽度剖面：鼻端收尖 → 核心最宽 → 尾部收窄
    float r = lerp(0.26, 1.0, smoothstep(-1.75, -0.10, yN))
            * lerp(1.0, 0.20, smoothstep(0.15, 1.45, yN));
    //撕裂加权：鼻端干净、尾部撕成流苏
    float tailW = 0.30 + 0.85 * smoothstep(0.0, 1.35, yN);
    float headW = smoothstep(-1.90, -1.20, yN);
    float tear = ((n1 - 0.5) * 0.62 + (n2 - 0.5) * 0.40) * tailW * headW;

    float e = abs(xN) - r + tear;
    float envY = smoothstep(-2.05, -1.50, yN) * (1.0 - smoothstep(1.05, 1.55, yN));
    float dens = smoothstep(0.10, -0.30, e) * envY;
    float core = smoothstep(0.0, -0.62, e);

    //体内明暗涡（烟不是平涂）
    float stria = 0.70 + 0.45 * (n2 - 0.5);

    //烬芯从烟内透光：烟薄处光弱，芯亮时烟壳内侧被烘暖
    float2 de = float2(xN, (yN - 0.12) * 1.15);
    float dEmb = dot(de, de);
    float innerGlow = exp2(-dEmb * 3.2) * (0.20 + 0.80 * uEmber);

    //倒吸绷紧的壳缘冷光（吸气时轮廓短暂可辨——预告的一部分）
    float rim = smoothstep(0.10, -0.04, e) * smoothstep(-0.30, -0.02, e)
              * smoothstep(0.04, 0.35, uInhale) * envY;

    //烟受光，烬自发光
    float3 ink = INK_SMOKE * (0.30 + 0.70 * uEnvLight) * stria;
    float3 col = ink + EMBER_BODY * innerGlow * 0.55;

    float a = dens * (0.52 + 0.22 * core) * uFade * vc.a;
    //预乘输出：壳体乘 a 遮挡，壳缘冷光作微加色不占 alpha
    return float4(col * a + INHALE_PALE * rim * 0.28 * uFade * vc.a, a);
}

//====== TechEmberFlame：烬芯亮层（粒状余烬+火舌+灯魂珠）======
float4 PSEmberFlame(float2 coords : TEXCOORD0, float4 vc : COLOR0) : COLOR0 {
    float2 p = BodyCoord(coords);
    //烬芯随壳轻微收紧
    p /= (1.0 - 0.10 * uInhale);
    float xN = p.x;
    float yN = p.y;

    float2 de = float2(xN, (yN - 0.12) * 1.12);
    float d = length(de);

    //粒状余烬簇：高频噪声阈值取粒，簇半径随进食长大
    float coreR = 0.20 + 0.34 * uFeed + 0.14 * uSwell;
    float g = nrm(tex2D(uNoiseTex, float2(xN * 0.85 + uSeed * 2.1, yN * 0.85 - uTime * 0.10)).r);
    float flick = nrm(tex2D(uNoiseTex, float2(uTime * 0.83 + uSeed, d * 0.5 + uSeed * 3.1)).r);
    float speckThr = 0.72 - 0.10 * flick - 0.06 * uSwell;
    float specks = saturate((g - speckThr) * 7.0) * smoothstep(coreR, coreR * 0.22, d);

    //紧凑底辉：под层，不是本体
    float glow = exp2(-d * d * (10.0 - 4.5 * uFeed)) * 0.55;

    //进食火舌：根实尖碎，自烬芯向头端舔；倒吸时反向内卷
    float h = 0.12 - yN;
    float tG = smoothstep(0.28, 0.85, uFeed);
    float flowSign = 1.0 - 2.0 * smoothstep(0.05, 0.45, uInhale);
    float sway = (g - 0.5) * 0.22;
    float f1 = nrm(tex2D(uNoiseTex, float2(xN * 0.62 + sway + uSeed, h * 0.40 - uTime * 0.55 * flowSign)).r);
    float f2 = nrm(tex2D(uNoiseTex, float2(xN * 1.50 - sway - uSeed, h * 0.95 - uTime * 1.00 * flowSign)).r);
    float fN = f1 * 0.62 + f2 * 0.38;
    float hMax = 0.55 + 0.75 * uFeed;
    float q = saturate(h / hMax);
    float thr = 0.30 + q * 0.62;
    float lat = smoothstep(0.62, 0.18, abs(xN));
    float tongues = saturate((fN - thr) * 4.2) * lat
                  * smoothstep(-0.06, 0.10, h) * (1.0 - smoothstep(0.85, 1.05, q)) * tG;

    //灯魂珠：每吞一盏灯多一颗，绕芯椭圆轨道（cos/sin 时间参数，连续无接缝）
    float3 moteAcc = float3(0.0, 0.0, 0.0);
    float2 pp = float2(xN, yN);
    {
        float gate0 = saturate((uFeed * 3.0 - 0.55) * 4.0);
        float a0 = uTime * 0.85 + uSeed * 6.28;
        float2 mp0 = float2(cos(a0) * 0.42, 0.12 + sin(a0) * 0.24);
        float md0 = dot(pp - mp0, pp - mp0);
        moteAcc += lerp(EMBER_BODY, EMBER_HOT, 0.75) * exp2(-md0 * 220.0) * gate0;
    }
    {
        float gate1 = saturate((uFeed * 3.0 - 1.55) * 4.0);
        float a1 = -uTime * 0.99 + 2.09 + uSeed * 6.28;
        float2 mp1 = float2(cos(a1) * 0.36, 0.12 + sin(a1) * 0.28);
        float md1 = dot(pp - mp1, pp - mp1);
        moteAcc += lerp(EMBER_BODY, EMBER_HOT, 0.75) * exp2(-md1 * 220.0) * gate1;
    }
    {
        float gate2 = saturate((uFeed * 3.0 - 2.55) * 4.0);
        float a2 = uTime * 1.13 + 4.19 + uSeed * 6.28;
        float2 mp2 = float2(cos(a2) * 0.46, 0.12 + sin(a2) * 0.20);
        float md2 = dot(pp - mp2, pp - mp2);
        moteAcc += lerp(EMBER_BODY, EMBER_HOT, 0.75) * exp2(-md2 * 220.0) * gate2;
    }

    //倒吸收拢环：从外圈收向烬芯的一圈苍光（负空间预告的可见沿）
    //噪声啃缘：纯高斯环读作塑料圈，借粒噪声撕出参差
    float ringR = lerp(1.30, 0.24, uInhale);
    float ring = exp2(-(d - ringR) * (d - ringR) * 55.0)
               * smoothstep(0.03, 0.18, uInhale) * 0.50
               * (0.55 + 0.90 * g);

    //饱食暖白热核（受限幅度，非常驻纯白）
    float hot = exp2(-d * d * 30.0) * uFeed * uFeed * 0.45;

    float3 col = EMBER_BODY * glow
               + lerp(EMBER_BODY, EMBER_HOT, flick) * specks * 1.15
               + lerp(EMBER_HOT, TIP_AMBER, q) * tongues * (0.85 + 0.30 * flick)
               + moteAcc
               + INHALE_PALE * ring
               + float3(1.0, 0.93, 0.78) * hot;
    col *= uEmber * uFade * vc.a;

    //热体微遮挡：只有实粒与火舌根占一点 alpha，辉光纯加色
    float a = saturate(specks * 0.35 + tongues * 0.22) * uEmber * uFade * vc.a;
    return float4(col, a);
}

technique TechSmokeBody {
    pass P0 {
        PixelShader = compile ps_3_0 PSSmokeBody();
    }
}

technique TechEmberFlame {
    pass P0 {
        PixelShader = compile ps_3_0 PSEmberFlame();
    }
}
