/**
 * Native WebGL adaptation inspired by React Bits / DarkVeil.
 * It intentionally avoids the upstream OGL runtime dependency so the OJ keeps its existing dependency surface.
 * See frontend/THIRD_PARTY_NOTICES.md for attribution and the upstream license.
 */
import { useEffect, useRef } from "react";

interface DarkVeilProps {
  speed?: number;
  intensity?: number;
  resolutionScale?: number;
}

const vertexShaderSource = `
attribute vec2 aPosition;
void main() {
  gl_Position = vec4(aPosition, 0.0, 1.0);
}
`;

const fragmentShaderSource = `
precision mediump float;
uniform vec2 uResolution;
uniform float uTime;
uniform float uIntensity;

float hash(vec2 p) {
  p = fract(p * vec2(123.34, 456.21));
  p += dot(p, p + 45.32);
  return fract(p.x * p.y);
}

float noise(vec2 p) {
  vec2 i = floor(p);
  vec2 f = fract(p);
  f = f * f * (3.0 - 2.0 * f);
  float a = hash(i);
  float b = hash(i + vec2(1.0, 0.0));
  float c = hash(i + vec2(0.0, 1.0));
  float d = hash(i + vec2(1.0, 1.0));
  return mix(mix(a, b, f.x), mix(c, d, f.x), f.y);
}

float fbm(vec2 p) {
  float value = 0.0;
  float amplitude = 0.52;
  for (int i = 0; i < 5; i++) {
    value += amplitude * noise(p);
    p = mat2(1.62, 1.13, -1.13, 1.62) * p + 0.17;
    amplitude *= 0.48;
  }
  return value;
}

void main() {
  vec2 resolution = max(uResolution, vec2(1.0));
  vec2 uv = (gl_FragCoord.xy * 2.0 - resolution.xy) / min(resolution.x, resolution.y);
  float time = uTime;

  vec2 flow = uv;
  flow.x += sin(uv.y * 1.35 + time * 0.52) * 0.16;
  flow.y += cos(uv.x * 1.15 - time * 0.39) * 0.12;

  float fieldA = fbm(flow * 1.05 + vec2(time * 0.10, -time * 0.07));
  float fieldB = fbm(flow * 1.72 + vec2(-time * 0.055, time * 0.09));
  float ribbon = smoothstep(0.30, 0.86, fieldA * 0.72 + fieldB * 0.48);
  float haze = smoothstep(0.10, 0.90, fbm(flow * 0.62 - time * 0.035));

  vec3 deep = vec3(0.010, 0.014, 0.028);
  vec3 violet = vec3(0.215, 0.165, 0.72);
  vec3 blue = vec3(0.10, 0.31, 0.74);
  vec3 color = deep;
  color += violet * ribbon * 0.38;
  color += blue * haze * 0.19;

  float radial = 1.0 - smoothstep(0.18, 1.62, length(uv * vec2(0.72, 0.88)));
  color *= 0.58 + radial * 0.62;
  color *= uIntensity;

  gl_FragColor = vec4(color, 1.0);
}
`;

export function DarkVeil({ speed = 0.24, intensity = 0.7, resolutionScale = 0.7 }: DarkVeilProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const parent = canvas?.parentElement;
    if (!canvas || !parent) return;

    const gl = canvas.getContext("webgl", {
      alpha: false,
      antialias: false,
      depth: false,
      powerPreference: "low-power",
      preserveDrawingBuffer: false
    });
    if (!gl) return;

    const vertexShader = createShader(gl, gl.VERTEX_SHADER, vertexShaderSource);
    const fragmentShader = createShader(gl, gl.FRAGMENT_SHADER, fragmentShaderSource);
    if (!vertexShader || !fragmentShader) return;

    const program = createProgram(gl, vertexShader, fragmentShader);
    gl.deleteShader(vertexShader);
    gl.deleteShader(fragmentShader);
    if (!program) return;

    const positionLocation = gl.getAttribLocation(program, "aPosition");
    const resolutionLocation = gl.getUniformLocation(program, "uResolution");
    const timeLocation = gl.getUniformLocation(program, "uTime");
    const intensityLocation = gl.getUniformLocation(program, "uIntensity");
    const buffer = gl.createBuffer();
    if (!buffer || positionLocation < 0 || !resolutionLocation || !timeLocation || !intensityLocation) {
      gl.deleteProgram(program);
      return;
    }

    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
    gl.useProgram(program);
    gl.enableVertexAttribArray(positionLocation);
    gl.vertexAttribPointer(positionLocation, 2, gl.FLOAT, false, 0, 0);
    gl.uniform1f(intensityLocation, intensity);

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const frameInterval = 1000 / 30;
    const start = performance.now();
    let width = 0;
    let height = 0;
    let raf = 0;
    let lastFrame = 0;
    let visible = !document.hidden;
    let resizeTimer = 0;

    const resize = () => {
      const rect = parent.getBoundingClientRect();
      const dpr = Math.min(window.devicePixelRatio || 1, 1.35);
      width = Math.max(1, Math.floor(rect.width * dpr * resolutionScale));
      height = Math.max(1, Math.floor(rect.height * dpr * resolutionScale));
      if (canvas.width !== width || canvas.height !== height) {
        canvas.width = width;
        canvas.height = height;
      }
      gl.viewport(0, 0, width, height);
      gl.uniform2f(resolutionLocation, width, height);
    };

    const render = (now: number) => {
      raf = 0;
      if (!visible) return;
      if (!reducedMotion && now - lastFrame < frameInterval) {
        raf = requestAnimationFrame(render);
        return;
      }
      lastFrame = now;
      gl.uniform1f(timeLocation, ((now - start) / 1000) * speed);
      gl.uniform1f(intensityLocation, intensity);
      gl.drawArrays(gl.TRIANGLES, 0, 3);
      if (!reducedMotion) raf = requestAnimationFrame(render);
    };

    const requestRender = () => {
      if (!raf && visible) raf = requestAnimationFrame(render);
    };

    const handleResize = () => {
      window.clearTimeout(resizeTimer);
      resizeTimer = window.setTimeout(() => {
        resize();
        requestRender();
      }, 100);
    };

    const handleVisibility = () => {
      visible = !document.hidden;
      if (visible) requestRender();
      else if (raf) {
        cancelAnimationFrame(raf);
        raf = 0;
      }
    };

    resize();
    requestRender();
    window.addEventListener("resize", handleResize);
    document.addEventListener("visibilitychange", handleVisibility);

    return () => {
      if (raf) cancelAnimationFrame(raf);
      window.clearTimeout(resizeTimer);
      window.removeEventListener("resize", handleResize);
      document.removeEventListener("visibilitychange", handleVisibility);
      gl.deleteBuffer(buffer);
      gl.deleteProgram(program);
    };
  }, [intensity, resolutionScale, speed]);

  return <canvas ref={canvasRef} className="oj-darkveil-canvas" />;
}

function createShader(gl: WebGLRenderingContext, type: number, source: string) {
  const shader = gl.createShader(type);
  if (!shader) return null;
  gl.shaderSource(shader, source);
  gl.compileShader(shader);
  if (gl.getShaderParameter(shader, gl.COMPILE_STATUS)) return shader;
  console.warn(`DarkVeil Shader Compile Warning: ${gl.getShaderInfoLog(shader) ?? "Unknown Error"}`);
  gl.deleteShader(shader);
  return null;
}

function createProgram(gl: WebGLRenderingContext, vertexShader: WebGLShader, fragmentShader: WebGLShader) {
  const program = gl.createProgram();
  if (!program) return null;
  gl.attachShader(program, vertexShader);
  gl.attachShader(program, fragmentShader);
  gl.linkProgram(program);
  if (gl.getProgramParameter(program, gl.LINK_STATUS)) return program;
  console.warn(`DarkVeil Program Link Warning: ${gl.getProgramInfoLog(program) ?? "Unknown Error"}`);
  gl.deleteProgram(program);
  return null;
}
