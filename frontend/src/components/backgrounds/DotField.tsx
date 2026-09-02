/**
 * Adapted from React Bits / DotField (TS-CSS variant).
 * React Bits copyright (c) 2026 David Haz.
 * See frontend/THIRD_PARTY_NOTICES.md for the upstream license and source revision.
 */
import { memo, useEffect, useRef, type HTMLAttributes } from "react";

interface Dot {
  anchorX: number;
  anchorY: number;
  x: number;
  y: number;
  phase: number;
  alpha: number;
  radiusScale: number;
}

type DotFieldProps = HTMLAttributes<HTMLDivElement> & {
  dotRadius?: number;
  dotSpacing?: number;
  topSpacingMultiplier?: number;
  cursorRadius?: number;
  bulgeStrength?: number;
  idleAmplitude?: number;
  idleSpeed?: number;
  gradientFrom?: string;
  gradientTo?: string;
  glowColor?: string;
};

export const DotField = memo(function DotField({
  dotRadius = 1.45,
  dotSpacing = 18,
  topSpacingMultiplier = 2.7,
  cursorRadius = 300,
  bulgeStrength = 22,
  idleAmplitude = 1.5,
  idleSpeed = 0.4,
  gradientFrom = "rgba(140, 120, 255, 0.82)",
  gradientTo = "rgba(74, 150, 255, 0.60)",
  glowColor = "rgba(118, 99, 255, 0.22)",
  className = "",
  ...rest
}: DotFieldProps) {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const parent = canvas?.parentElement;
    if (!canvas || !parent) return;

    const context = canvas.getContext("2d", { alpha: true });
    if (!context) return;

    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    const coarsePointer = window.matchMedia("(pointer: coarse)").matches;
    const interactive = !reducedMotion && !coarsePointer;
    const animateIdle = !reducedMotion && idleAmplitude > 0;
    const dots: Dot[] = [];
    const mouse = { x: -9999, y: -9999 };
    const frameInterval = 1000 / 24;
    let width = 0;
    let height = 0;
    let raf = 0;
    let lastFrame = 0;
    let settleUntil = 0;
    let resizeTimer = 0;
    let visible = !document.hidden;

    const draw = (now: number) => {
      context.clearRect(0, 0, width, height);
      const gradient = context.createLinearGradient(0, height, width, 0);
      gradient.addColorStop(0, gradientFrom);
      gradient.addColorStop(1, gradientTo);
      context.fillStyle = gradient;

      const time = now * 0.001 * idleSpeed;
      const radiusSquared = cursorRadius * cursorRadius;
      let stillMoving = false;

      for (const dot of dots) {
        const idleX = animateIdle ? Math.sin(time * 1.15 + dot.phase) * idleAmplitude : 0;
        const idleY = animateIdle ? Math.cos(time * 0.86 + dot.phase * 1.13) * idleAmplitude : 0;
        let targetX = dot.anchorX + idleX;
        let targetY = dot.anchorY + idleY;
        const dx = mouse.x - dot.anchorX;
        const dy = mouse.y - dot.anchorY;
        const distanceSquared = dx * dx + dy * dy;

        if (interactive && distanceSquared < radiusSquared) {
          const distance = Math.max(Math.sqrt(distanceSquared), 0.001);
          const influence = 1 - distance / cursorRadius;
          const displacement = influence * influence * bulgeStrength;
          targetX -= (dx / distance) * displacement;
          targetY -= (dy / distance) * displacement;
        }

        dot.x += (targetX - dot.x) * 0.18;
        dot.y += (targetY - dot.y) * 0.18;
        if (Math.abs(targetX - dot.x) > 0.03 || Math.abs(targetY - dot.y) > 0.03) stillMoving = true;

        const pulse = animateIdle ? 1 + Math.sin(time * 1.6 + dot.phase * 0.81) * 0.13 : 1;
        const radius = dotRadius * dot.radiusScale * pulse;
        context.globalAlpha = dot.alpha;
        context.beginPath();
        context.arc(dot.x, dot.y, radius, 0, Math.PI * 2);
        context.fill();
      }
      context.globalAlpha = 1;

      if (interactive && mouse.x > -1000) {
        const glow = context.createRadialGradient(mouse.x, mouse.y, 0, mouse.x, mouse.y, cursorRadius * 0.78);
        glow.addColorStop(0, glowColor);
        glow.addColorStop(0.42, "rgba(84, 115, 255, 0.07)");
        glow.addColorStop(1, "rgba(0, 0, 0, 0)");
        context.fillStyle = glow;
        context.fillRect(0, 0, width, height);
      }

      return stillMoving;
    };

    const tick = (now: number) => {
      raf = 0;
      if (!visible) return;
      if (now - lastFrame < frameInterval) {
        raf = requestAnimationFrame(tick);
        return;
      }
      lastFrame = now;
      const stillMoving = draw(now);
      if (animateIdle || stillMoving || now < settleUntil) raf = requestAnimationFrame(tick);
    };

    const requestDraw = (duration = 0) => {
      settleUntil = Math.max(settleUntil, performance.now() + duration);
      if (!raf && visible) raf = requestAnimationFrame(tick);
    };

    const rebuild = () => {
      const rect = parent.getBoundingClientRect();
      width = rect.width;
      height = rect.height;
      const dpr = Math.min(window.devicePixelRatio || 1, 1.25);
      canvas.width = Math.max(1, Math.floor(width * dpr));
      canvas.height = Math.max(1, Math.floor(height * dpr));
      canvas.style.width = `${width}px`;
      canvas.style.height = `${height}px`;
      context.setTransform(dpr, 0, 0, dpr, 0, 0);

      dots.length = 0;
      const bottomStep = Math.max(dotRadius * 2 + dotSpacing, 12);
      let y = height - bottomStep * 0.42;
      let row = 0;

      while (y > -bottomStep) {
        const upward = Math.min(1, Math.max(0, 1 - y / Math.max(height, 1)));
        const spacingScale = 1 + Math.pow(upward, 1.15) * (topSpacingMultiplier - 1);
        const rowStep = bottomStep * (0.92 + upward * 0.82);
        const columnStep = bottomStep * spacingScale;
        const offsetX = row % 2 === 0 ? 0 : columnStep * 0.46;
        const alpha = 1 - upward * 0.54;
        const radiusScale = 1 - upward * 0.34;

        for (let x = -columnStep + offsetX; x <= width + columnStep; x += columnStep) {
          const phase = row * 0.53 + (x / Math.max(columnStep, 1)) * 0.37;
          dots.push({ anchorX: x, anchorY: y, x, y, phase, alpha, radiusScale });
        }

        y -= rowStep;
        row++;
      }
      requestDraw(300);
    };

    const handleResize = () => {
      window.clearTimeout(resizeTimer);
      resizeTimer = window.setTimeout(rebuild, 100);
    };

    const resizeObserver = typeof ResizeObserver === "undefined" ? null : new ResizeObserver(handleResize);

    const handleMouseMove = (event: MouseEvent) => {
      if (!interactive) return;
      const rect = parent.getBoundingClientRect();
      mouse.x = event.clientX - rect.left;
      mouse.y = event.clientY - rect.top;
      requestDraw(650);
    };

    const handleMouseLeave = () => {
      mouse.x = -9999;
      mouse.y = -9999;
      requestDraw(650);
    };

    const handleVisibility = () => {
      visible = !document.hidden;
      if (visible) requestDraw(300);
      else if (raf) {
        cancelAnimationFrame(raf);
        raf = 0;
      }
    };

    rebuild();
    resizeObserver?.observe(parent);
    window.addEventListener("resize", handleResize);
    if (interactive) {
      window.addEventListener("mousemove", handleMouseMove, { passive: true });
      document.documentElement.addEventListener("mouseleave", handleMouseLeave);
    }
    document.addEventListener("visibilitychange", handleVisibility);

    return () => {
      if (raf) cancelAnimationFrame(raf);
      window.clearTimeout(resizeTimer);
      resizeObserver?.disconnect();
      window.removeEventListener("resize", handleResize);
      window.removeEventListener("mousemove", handleMouseMove);
      document.documentElement.removeEventListener("mouseleave", handleMouseLeave);
      document.removeEventListener("visibilitychange", handleVisibility);
    };
  }, [bulgeStrength, cursorRadius, dotRadius, dotSpacing, glowColor, gradientFrom, gradientTo, idleAmplitude, idleSpeed, topSpacingMultiplier]);

  return (
    <div className={`oj-dot-field ${className}`.trim()} {...rest}>
      <canvas ref={canvasRef} />
    </div>
  );
});
