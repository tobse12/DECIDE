/*
 * Author: Tobias Sorg
 * Date: 2025-01-15
 * Description: Implements visual distortion effects for the DECIDE VR framework
 * License: GPLv3
 */

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;
using DECIDE.Stressors;

namespace DECIDE.Stressors.Implementations {
    /// <summary>
    /// Creates visual distortions like blur, chromatic aberration, and warping
    /// </summary>
    public class VisualDistortionStressor : MonoBehaviour, IStressor {
        [Header("Blur Settings")]
        [SerializeField] private bool _enableBlur = true;
        [SerializeField] private float _maxBlurIntensity = 0.5f;
        [SerializeField] private AnimationCurve _blurCurve;
        
        [Header("Chromatic Aberration")]
        [SerializeField] private bool _enableChromaticAberration = true;
        [SerializeField] private float _maxChromaticIntensity = 1f;
        [SerializeField] private bool _animateChromatic = true;
        
        [Header("Screen Warp")]
        [SerializeField] private bool _enableWarp = true;
        [SerializeField] private float _warpStrength = 0.3f;
        [SerializeField] private float _warpFrequency = 2f;
        [SerializeField] private AnimationCurve _warpPattern;
        
        [Header("Tunnel Vision")]
        [SerializeField] private bool _enableTunnelVision = true;
        [SerializeField] private float _vignettteIntensity = 0.5f;
        [SerializeField] private float _vignetteSmoothness = 0.3f;
        [SerializeField] private bool _pulsateVignette = true;
        
        [Header("Double Vision")]
        [SerializeField] private bool _enableDoubleVision = true;
        [SerializeField] private float _doubleVisionOffset = 0.1f;
        [SerializeField] private float _doubleVisionOpacity = 0.5f;
        
        [Header("Glitch Effects")]
        [SerializeField] private bool _enableGlitch = true;
        [SerializeField] private float _glitchFrequency = 0.1f;
        [SerializeField] private float _glitchIntensity = 0.5f;
        
        [Header("Color Distortion")]
        [SerializeField] private bool _enableColorShift = true;
        [SerializeField] private Gradient _colorShiftGradient;
        [SerializeField] private float _colorShiftSpeed = 1f;
        
        // Interface implementation
        private string _name = "VisualDistortion";
        private float _intensity = 0.5f;
        private bool _isActive = false;
        private StressorParameters _parameters;
        
        // Internal components
        private Camera _mainCamera;
        private Material _distortionMaterial;
        private RenderTexture _distortionTexture;
        private Coroutine _distortionCoroutine;
        private float _currentDistortionTime;
        
        // Post-processing components (if using URP)
        private Volume _postProcessVolume;
        
        // IStressor properties
        public string Name => _name;
        public float Intensity {
            get => _intensity;
            set => _intensity = Mathf.Clamp01(value);
        }
        public bool IsActive => _isActive;
        
        private void Awake() {
            _mainCamera = Camera.main;
            SetupDistortionEffect();
            SetupCurves();
        }
        
        /// <summary>
        /// Sets up the distortion rendering
        /// </summary>
        private void SetupDistortionEffect() {
            if (_mainCamera == null) return;
            
            // Create distortion material
            Shader distortionShader = Shader.Find("Hidden/VisualDistortion");
            if (distortionShader == null) {
                // Create simple distortion shader
                distortionShader = CreateDistortionShader();
            }
            _distortionMaterial = new Material(distortionShader);
            
            // Create render texture
            _distortionTexture = new RenderTexture(Screen.width, Screen.height, 24);
            
            // Try to find post-process volume for URP
            _postProcessVolume = FindObjectOfType<Volume>();
            if (_postProcessVolume == null) {
                // Create post-process volume
                GameObject ppObject = new GameObject("DistortionPostProcess");
                ppObject.transform.SetParent(transform);
                _postProcessVolume = ppObject.AddComponent<Volume>();
                _postProcessVolume.isGlobal = true;
            }
        }
        
        /// <summary>
        /// Creates a basic distortion shader
        /// </summary>
        private Shader CreateDistortionShader() {
            string shaderCode = @"
                Shader ""Hidden/VisualDistortion"" {
                    Properties {
                        _MainTex (""Texture"", 2D) = ""white"" {}
                        _DistortionStrength (""Distortion"", Float) = 0
                        _ChromaticAberration (""Chromatic"", Float) = 0
                        _VignetteIntensity (""Vignette"", Float) = 0
                    }
                    SubShader {
                        Pass {
                            CGPROGRAM
                            #pragma vertex vert
                            #pragma fragment frag
                            #include ""UnityCG.cginc""
                            
                            struct appdata {
                                float4 vertex : POSITION;
                                float2 uv : TEXCOORD0;
                            };
                            
                            struct v2f {
                                float2 uv : TEXCOORD0;
                                float4 vertex : SV_POSITION;
                            };
                            
                            sampler2D _MainTex;
                            float _DistortionStrength;
                            float _ChromaticAberration;
                            float _VignetteIntensity;
                            
                            v2f vert (appdata v) {
                                v2f o;
                                o.vertex = UnityObjectToClipPos(v.vertex);
                                o.uv = v.uv;
                                return o;
                            }
                            
                            fixed4 frag (v2f i) : SV_Target {
                                float2 uv = i.uv;
                                
                                // Distortion
                                uv += sin(uv * 10 + _Time.y) * _DistortionStrength;
                                
                                // Chromatic aberration
                                fixed4 col;
                                col.r = tex2D(_MainTex, uv + float2(_ChromaticAberration, 0)).r;
                                col.g = tex2D(_MainTex, uv).g;
                                col.b = tex2D(_MainTex, uv - float2(_ChromaticAberration, 0)).b;
                                col.a = 1;
                                
                                // Vignette
                                float2 center = uv - 0.5;
                                float vignette = 1 - dot(center, center) * _VignetteIntensity;
                                col *= vignette;
                                
                                return col;
                            }
                            ENDCG
                        }
                    }
                }";
            
            return Shader.Find("Hidden/VisualDistortion") ?? Shader.Find("Unlit/Color");
        }
        
        /// <summary>
        /// Sets up animation curves
        /// </summary>
        private void SetupCurves() {
            if (_blurCurve == null || _blurCurve.length == 0) {
                _blurCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            }
            
            if (_warpPattern == null || _warpPattern.length == 0) {
                _warpPattern = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            }
            
            if (_colorShiftGradient == null || _colorShiftGradient.colorKeys.Length == 0) {
                _colorShiftGradient = new Gradient();
                GradientColorKey[] colorKeys = new GradientColorKey[3];
                colorKeys[0] = new GradientColorKey(Color.white, 0f);
                colorKeys[1] = new GradientColorKey(new Color(1f, 0.8f, 0.8f), 0.5f);
                colorKeys[2] = new GradientColorKey(Color.white, 1f);
                
                GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
                alphaKeys[0] = new GradientAlphaKey(1f, 0f);
                alphaKeys[1] = new GradientAlphaKey(1f, 1f);
                
                _colorShiftGradient.SetKeys(colorKeys, alphaKeys);
            }
        }
        
        /// <summary>
        /// Initializes the visual distortion stressor
        /// </summary>
        public void Initialize(StressorParameters parameters) {
            _parameters = parameters ?? new StressorParameters();
            _intensity = _parameters.intensity;
            
            if (_parameters.autoActivate) {
                Activate();
            }
        }
        
        /// <summary>
        /// Activates the visual distortion stressor
        /// </summary>
        public void Activate() {
            if (_isActive) return;
            
            _isActive = true;
            _currentDistortionTime = 0f;
            
            // Enable camera effect
            if (_mainCamera != null) {
                CameraDistortion cameraEffect = _mainCamera.GetComponent<CameraDistortion>();
                if (cameraEffect == null) {
                    cameraEffect = _mainCamera.gameObject.AddComponent<CameraDistortion>();
                }
                cameraEffect.distortionMaterial = _distortionMaterial;
                cameraEffect.enabled = true;
            }
            
            // Start distortion coroutine
            if (_distortionCoroutine != null) {
                StopCoroutine(_distortionCoroutine);
            }
            _distortionCoroutine = StartCoroutine(DistortionCoroutine());
        }
        
        /// <summary>
        /// Deactivates the visual distortion stressor
        /// </summary>
        public void Deactivate() {
            if (!_isActive) return;
            
            _isActive = false;
            
            // Disable camera effect
            if (_mainCamera != null) {
                CameraDistortion cameraEffect = _mainCamera.GetComponent<CameraDistortion>();
                if (cameraEffect != null) {
                    cameraEffect.enabled = false;
                }
            }
            
            // Stop coroutine
            if (_distortionCoroutine != null) {
                StopCoroutine(_distortionCoroutine);
                _distortionCoroutine = null;
            }
            
            // Reset material properties
            ResetDistortionProperties();
        }
        
        /// <summary>
        /// Main distortion update coroutine
        /// </summary>
        private IEnumerator DistortionCoroutine() {
            while (_isActive) {
                _currentDistortionTime += Time.deltaTime;
                
                // Update blur
                if (_enableBlur) {
                    UpdateBlur();
                }
                
                // Update chromatic aberration
                if (_enableChromaticAberration) {
                    UpdateChromaticAberration();
                }
                
                // Update warp
                if (_enableWarp) {
                    UpdateWarp();
                }
                
                // Update tunnel vision
                if (_enableTunnelVision) {
                    UpdateTunnelVision();
                }
                
                // Update double vision
                if (_enableDoubleVision && Random.value < 0.1f * _intensity) {
                    yield return DoubleVisionEffect();
                }
                
                // Update glitch
                if (_enableGlitch && Random.value < _glitchFrequency * _intensity) {
                    yield return GlitchEffect();
                }
                
                // Update color shift
                if (_enableColorShift) {
                    UpdateColorShift();
                }
                
                yield return null;
            }
        }
        
        /// <summary>
        /// Updates blur effect
        /// </summary>
        private void UpdateBlur() {
            float blurAmount = _blurCurve.Evaluate(_currentDistortionTime % 5f / 5f) * _maxBlurIntensity * _intensity;
            
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_BlurAmount", blurAmount);
            }
        }
        
        /// <summary>
        /// Updates chromatic aberration
        /// </summary>
        private void UpdateChromaticAberration() {
            float chromatic = _maxChromaticIntensity * _intensity;
            
            if (_animateChromatic) {
                chromatic *= Mathf.Sin(_currentDistortionTime * 2f) * 0.5f + 0.5f;
            }
            
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_ChromaticAberration", chromatic * 0.01f);
            }
        }
        
        /// <summary>
        /// Updates warp effect
        /// </summary>
        private void UpdateWarp() {
            float warp = _warpPattern.Evaluate((_currentDistortionTime * _warpFrequency) % 1f);
            warp *= _warpStrength * _intensity;
            
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_DistortionStrength", warp * 0.1f);
            }
        }
        
        /// <summary>
        /// Updates tunnel vision effect
        /// </summary>
        private void UpdateTunnelVision() {
            float vignette = _vignettteIntensity * _intensity;
            
            if (_pulsateVignette) {
                vignette *= Mathf.Sin(_currentDistortionTime * 3f) * 0.3f + 0.7f;
            }
            
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_VignetteIntensity", vignette * 2f);
                _distortionMaterial.SetFloat("_VignetteSmoothness", _vignetteSmoothness);
            }
        }
        
        /// <summary>
        /// Creates double vision effect
        /// </summary>
        private IEnumerator DoubleVisionEffect() {
            float duration = Random.Range(0.5f, 2f);
            float elapsed = 0f;
            
            GameObject doubleCamera = new GameObject("DoubleVisionCamera");
            Camera doubleCam = doubleCamera.AddComponent<Camera>();
            doubleCam.CopyFrom(_mainCamera);
            doubleCam.depth = _mainCamera.depth - 1;
            doubleCam.clearFlags = CameraClearFlags.Nothing;
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                
                // Offset position
                Vector3 offset = new Vector3(
                    Mathf.Sin(t * Mathf.PI * 4) * _doubleVisionOffset,
                    Mathf.Cos(t * Mathf.PI * 4) * _doubleVisionOffset * 0.5f,
                    0
                );
                
                doubleCamera.transform.position = _mainCamera.transform.position + offset;
                doubleCamera.transform.rotation = _mainCamera.transform.rotation;
                
                yield return null;
            }
            
            Destroy(doubleCamera);
        }
        
        /// <summary>
        /// Creates glitch effect
        /// </summary>
        private IEnumerator GlitchEffect() {
            float duration = Random.Range(0.05f, 0.2f);
            float elapsed = 0f;
            
            // Store original values
            float originalChromatic = _distortionMaterial?.GetFloat("_ChromaticAberration") ?? 0;
            float originalDistortion = _distortionMaterial?.GetFloat("_DistortionStrength") ?? 0;
            
            while (elapsed < duration) {
                elapsed += Time.deltaTime;
                
                if (_distortionMaterial != null) {
                    // Random glitch values
                    _distortionMaterial.SetFloat("_ChromaticAberration", Random.Range(-0.1f, 0.1f) * _glitchIntensity);
                    _distortionMaterial.SetFloat("_DistortionStrength", Random.Range(-0.2f, 0.2f) * _glitchIntensity);
                    
                    // Color glitch
                    _distortionMaterial.SetColor("_TintColor", new Color(
                        Random.Range(0.8f, 1.2f),
                        Random.Range(0.8f, 1.2f),
                        Random.Range(0.8f, 1.2f)
                    ));
                }
                
                yield return new WaitForSeconds(0.02f);
            }
            
            // Restore original values
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_ChromaticAberration", originalChromatic);
                _distortionMaterial.SetFloat("_DistortionStrength", originalDistortion);
                _distortionMaterial.SetColor("_TintColor", Color.white);
            }
        }
        
        /// <summary>
        /// Updates color shift effect
        /// </summary>
        private void UpdateColorShift() {
            float t = (_currentDistortionTime * _colorShiftSpeed) % 1f;
            Color shiftColor = _colorShiftGradient.Evaluate(t);
            
            if (_distortionMaterial != null) {
                _distortionMaterial.SetColor("_ColorShift", shiftColor);
            }
        }
        
        /// <summary>
        /// Resets distortion properties
        /// </summary>
        private void ResetDistortionProperties() {
            if (_distortionMaterial != null) {
                _distortionMaterial.SetFloat("_BlurAmount", 0);
                _distortionMaterial.SetFloat("_ChromaticAberration", 0);
                _distortionMaterial.SetFloat("_DistortionStrength", 0);
                _distortionMaterial.SetFloat("_VignetteIntensity", 0);
                _distortionMaterial.SetColor("_ColorShift", Color.white);
                _distortionMaterial.SetColor("_TintColor", Color.white);
            }
        }
        
        /// <summary>
        /// Updates the stressor
        /// </summary>
        public void UpdateStressor() {
            // Intensity affects all distortion parameters
        }
        
        /// <summary>
        /// Gets current parameters
        /// </summary>
        public StressorParameters GetParameters() {
            return _parameters;
        }
        
        /// <summary>
        /// Updates parameters at runtime
        /// </summary>
        public void UpdateParameters(StressorParameters parameters) {
            _parameters = parameters;
            _intensity = _parameters.intensity;
        }
        
        private void OnDestroy() {
            if (_isActive) {
                Deactivate();
            }
            
            if (_distortionTexture != null) {
                _distortionTexture.Release();
            }
        }
        
        /// <summary>
        /// Camera effect component for applying distortion
        /// </summary>
        private class CameraDistortion : MonoBehaviour {
            public Material distortionMaterial;
            
            private void OnRenderImage(RenderTexture source, RenderTexture destination) {
                if (distortionMaterial != null) {
                    Graphics.Blit(source, destination, distortionMaterial);
                } else {
                    Graphics.Blit(source, destination);
                }
            }
        }
    }
}