// ============================================================================
//OniDomainDeco.fx 鬼域装饰件 SDF：樱瓣与纸灯笼，单 Apply 多 quad 批绘
//quad UV 0~1，中心 0.5；颜色/透明度走 vertexColor（预乘输出）
//旋转/摆动/明灭全在 C# 侧摆 quad，本着色器只负责形体
// ============================================================================

sampler uImage0 : register(s0);

float uTime;
float uPetalSoftness; //0.02~0.28 菜单花瓣柔边

//樱瓣：竖轴泪滴 + 顶端凹口 + 中脉
float4 PSPetal(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = (coords - 0.5) * 2.0;
    float aa = 0.08;

    //主体椭圆，下端收尖
    float taper = 1.0 - smoothstep(0.0, 1.0, -p.y) * 0.34;
    float body = (p.x * p.x) / (0.60 * 0.60 * taper * taper) + (p.y * p.y) / (0.92 * 0.92);
    float cov = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, body);

    //顶端凹口
    float2 dn = p - float2(0.0, 1.02);
    float notch = smoothstep(0.26, 0.34, length(dn));
    cov *= notch;

    if (cov <= 0.003) {
        return float4(0, 0, 0, 0);
    }

    //根部略深、瓣尖略亮 + 中脉
    float shade = 0.86 + 0.14 * smoothstep(-0.9, 0.9, p.y);
    float vein = 1.0 - 0.10 * (1.0 - smoothstep(0.02, 0.07, abs(p.x))) * step(abs(p.y), 0.75);

    float a = cov * vertexColor.a;
    float3 col = vertexColor.rgb * shade * vein;
    return float4(col * a, a);
}

//沿用原瓣面 SDF，柔边由景深层级控制
float4 PSMenuPetal(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = (coords - 0.5) * 2.0;
    float aa = max(uPetalSoftness, 0.001);

    float taper = 1.0 - smoothstep(0.0, 1.0, -p.y) * 0.34;
    float body = (p.x * p.x) / (0.60 * 0.60 * taper * taper) + (p.y * p.y) / (0.92 * 0.92);
    float cov = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, body);

    float2 dn = p - float2(0.0, 1.02);
    float notchHalfWidth = aa * 0.5;
    float notch = smoothstep(0.30 - notchHalfWidth, 0.30 + notchHalfWidth, length(dn));
    cov *= notch;

    float shade = 0.86 + 0.14 * smoothstep(-0.9, 0.9, p.y);
    float vein = 1.0 - 0.10 * (1.0 - smoothstep(0.02, 0.07, abs(p.x))) * step(abs(p.y), 0.75);

    float a = cov * vertexColor.a;
    float3 col = vertexColor.rgb * shade * vein;
    return float4(col * a, a);
}

//纸灯笼：椭圆纸罩 + 横竹圈 + 上下口盖 + 内芯暖光
float4 PSLantern(float2 coords : TEXCOORD0, float4 vertexColor : COLOR0) : COLOR0 {
    float2 p = (coords - 0.5) * 2.0;
    float aa = 0.05;

    //纸罩
    float body = (p.x * p.x) / (0.62 * 0.62) + (p.y * p.y) / (0.80 * 0.80);
    float cov = 1.0 - smoothstep(1.0 - aa, 1.0 + aa, body);

    //上下口盖：扁矩形
    float capT = (1.0 - smoothstep(0.10, 0.16, abs(p.y - 0.84))) * step(abs(p.x), 0.24);
    float capB = (1.0 - smoothstep(0.10, 0.16, abs(p.y + 0.84))) * step(abs(p.x), 0.24);
    float caps = max(capT, capB);

    float total = max(cov, caps);
    if (total <= 0.003) {
        return float4(0, 0, 0, 0);
    }

    //横竹圈：纸罩上的暗环
    float ribs = 0.90 + 0.10 * sin(p.y * 17.0);
    //球面明暗
    float sphere = 1.0 - body * 0.35;

    //内芯暖光：中心偏下，随 uTime 微微呼吸
    float2 dc = p - float2(0.0, -0.06);
    float core = exp(-dot(dc, dc) * 2.6);
    float breathe = 0.92 + 0.08 * sin(uTime * 2.3 + vertexColor.a * 12.0);
    float3 warm = float3(1.0, 0.52, 0.16) * core * breathe * 1.15;

    float3 paperCol = vertexColor.rgb * ribs * sphere;
    float3 col = paperCol + warm * cov;
    //口盖压成深木色
    col = lerp(col, float3(0.06, 0.035, 0.02), caps * (1.0 - cov * 0.4));

    float a = total * vertexColor.a;
    return float4(col * a, a);
}

technique TechPetal {
    pass P0 {
        PixelShader = compile ps_3_0 PSPetal();
    }
}

technique TechMenuPetal {
    pass P0 {
        PixelShader = compile ps_3_0 PSMenuPetal();
    }
}

technique TechLantern {
    pass P0 {
        PixelShader = compile ps_3_0 PSLantern();
    }
}
