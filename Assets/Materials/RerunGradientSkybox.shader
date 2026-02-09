Shader "Skybox/RerunGradient"
{
    Properties
    {
        _TopColor    ("Top Colour",    Color) = (0.12, 0.10, 0.22, 1)
        _MidColor    ("Mid Colour",    Color) = (0.97, 0.50, 0.30, 1)
        _BotColor    ("Bottom Colour", Color) = (1.0, 0.82, 0.60, 1)
        _MidPoint    ("Mid Point",     Range(0,1)) = 0.42
        _Exposure    ("Exposure",      Range(0,8)) = 1.3
        _HorizonSharpness ("Horizon Sharpness", Range(0.1, 10)) = 1.2

        [Header(Sun Disc)]
        _SunColor    ("Sun Color",     Color) = (1, 0.92, 0.7, 1)
        _SunDir      ("Sun Direction", Vector) = (0.15, 0.14, -0.98, 0)
        _SunRadius   ("Sun Radius",    Range(0.001, 0.15)) = 0.045
        _SunFalloff  ("Sun Falloff",   Range(1, 128)) = 48
        _SunIntensity("Sun Intensity", Range(0, 10)) = 3.5

        [Header(Horizon Haze)]
        _HazeColor   ("Haze Color",    Color) = (1, 0.78, 0.55, 1)
        _HazeIntensity("Haze Intensity", Range(0, 2)) = 0.6
        _HazeExponent("Haze Exponent", Range(1, 32)) = 8

        [Header(Stars)]
        _StarDensity ("Star Density",  Range(0, 500)) = 120
        _StarBrightness("Star Brightness", Range(0, 3)) = 0.7
        _StarThreshold("Star Threshold (y)", Range(0, 1)) = 0.55
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _TopColor;
                half4  _MidColor;
                half4  _BotColor;
                half   _MidPoint;
                half   _Exposure;
                half   _HorizonSharpness;

                half4  _SunColor;
                float4 _SunDir;
                half   _SunRadius;
                half   _SunFalloff;
                half   _SunIntensity;

                half4  _HazeColor;
                half   _HazeIntensity;
                half   _HazeExponent;

                half   _StarDensity;
                half   _StarBrightness;
                half   _StarThreshold;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir    : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.viewDir    = v.positionOS.xyz;
                return o;
            }

            // Simple hash for star noise
            float hash(float3 p)
            {
                p = frac(p * float3(443.897, 441.423, 437.195));
                p += dot(p, p.yzx + 19.19);
                return frac((p.x + p.y) * p.z);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.viewDir);

                // ── Gradient ──
                half t = saturate(dir.y * _HorizonSharpness * 0.5 + 0.5);

                half3 col;
                if (t < _MidPoint)
                    col = lerp(_BotColor.rgb, _MidColor.rgb, t / _MidPoint);
                else
                    col = lerp(_MidColor.rgb, _TopColor.rgb, (t - _MidPoint) / (1.0 - _MidPoint));

                // ── Horizon haze (additive glow around horizon line) ──
                half horizonFactor = 1.0 - abs(dir.y);
                half haze = pow(horizonFactor, _HazeExponent) * _HazeIntensity;
                col += _HazeColor.rgb * haze;

                // ── Sun disc ──
                float3 sunDirection = normalize(_SunDir.xyz);
                half sunDot = saturate(dot(dir, sunDirection));
                half sunDisc = pow(sunDot, _SunFalloff);
                // Soft edge: smoothstep from radius to 0
                half sunAngle = acos(sunDot);
                half sunMask = 1.0 - smoothstep(_SunRadius * 0.7, _SunRadius, sunAngle);
                half sun = max(sunDisc, sunMask) * _SunIntensity;
                col += _SunColor.rgb * sun;

                // ── Stars (only above threshold, fading in) ──
                half starMask = smoothstep(_StarThreshold, _StarThreshold + 0.2, t);
                if (starMask > 0.001)
                {
                    // Quantize direction into cells for pseudo-random stars
                    float3 cellDir = floor(dir * _StarDensity);
                    float star = hash(cellDir);
                    // Only show very bright points
                    star = step(0.985, star) * star;
                    // Twinkle based on time + position
                    star *= 0.5 + 0.5 * sin(_Time.y * 2.0 + hash(cellDir + 1.0) * 6.28);
                    col += star * _StarBrightness * starMask;
                }

                col *= _Exposure;
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
