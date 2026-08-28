import { useEffect, useRef } from "react";

const LogoAssetPath = "/brand/unrealstudio-logo.png";
const LogoParticleCap = 620;
const MobileLogoParticleCap = 300;
const FreeParticleCap = 110;
const MobileFreeParticleCap = 36;
const MaxShards = 18;
const MaxShardFragments = 12;
const MobileMaxShards = 4;
const MobileMaxShardFragments = 3;
const RepulsionRadius = 128;
const MaxDpr = 1.5;

interface LogoTarget {
  x: number;
  y: number;
  claimed: boolean;
}

interface LogoParticle {
  state: "target" | "incoming" | "detached";
  targetIndex: number;
  x: number;
  y: number;
  vx: number;
  vy: number;
  startX: number;
  startY: number;
  controlX: number;
  controlY: number;
  age: number;
  duration: number;
  radius: number;
  opacity: number;
  scale: number;
  seed: number;
}

interface CrystalShard {
  x: number;
  y: number;
  vx: number;
  vy: number;
  size: number;
  scale: number;
  opacity: number;
  rotation: number;
  angularVelocity: number;
  age: number;
  duration: number;
  fragment: boolean;
  split: boolean;
  tone: number;
}

interface SafeZone {
  left: number;
  top: number;
  right: number;
  bottom: number;
}

export function AuthParticleField() {
  const canvasRef = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = canvasRef.current;
    const host = canvas?.parentElement;
    if (!canvas || !host) return;

    const context = canvas.getContext("2d", { alpha: true });
    if (!context) return;

    const reducedMotionQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const coarsePointerQuery = window.matchMedia("(pointer: coarse)");
    const mobileQuery = window.matchMedia("(max-width: 767px)");
    const logoImage = new Image();

    const targets: LogoTarget[] = [];
    const particles: LogoParticle[] = [];
    const shards: CrystalShard[] = [];
    const pointer = { x: -10_000, y: -10_000, active: false };
    let width = 0;
    let height = 0;
    let frame = 0;
    let resizeTimer = 0;
    let lastTime = 0;
    let incomingAccumulator = 0;
    let shardAccumulator = 0;
    let visible = !document.hidden;
    let safeZone: SafeZone | null = null;

    const isMobile = () => mobileQuery.matches;
    const isReducedMotion = () => reducedMotionQuery.matches;
    const canRepel = () => !isReducedMotion() && !isMobile() && !coarsePointerQuery.matches;
    const logoCap = () => isMobile() ? MobileLogoParticleCap : LogoParticleCap;
    const freeCap = () => isMobile() ? MobileFreeParticleCap : FreeParticleCap;
    const shardCap = () => isMobile() ? MobileMaxShards : MaxShards;
    const fragmentCap = () => isMobile() ? MobileMaxShardFragments : MaxShardFragments;

    const updateSafeZone = (canvasRect: DOMRect) => {
      const card = host.querySelector<HTMLElement>(".auth-studio-card");
      const cardRect = card?.getBoundingClientRect();
      safeZone = cardRect
        ? {
            left: cardRect.left - canvasRect.left - 60,
            top: cardRect.top - canvasRect.top - 60,
            right: cardRect.right - canvasRect.left + 60,
            bottom: cardRect.bottom - canvasRect.top + 60
          }
        : null;
    };

    const sampleLogoTargets = () => {
      targets.length = 0;
      const visualSize = isMobile()
        ? Math.min(width * 0.64, 290)
        : Math.min(Math.max(width * 0.29, 320), 420);
      const sampleCanvas = document.createElement("canvas");
      const sampleSize = Math.max(1, Math.round(visualSize));
      sampleCanvas.width = sampleSize;
      sampleCanvas.height = sampleSize;
      const sampleContext = sampleCanvas.getContext("2d", { willReadFrequently: true });
      if (!sampleContext) return;

      sampleContext.drawImage(logoImage, 0, 0, sampleSize, sampleSize);
      const pixels = sampleContext.getImageData(0, 0, sampleSize, sampleSize).data;
      const gap = isMobile() ? 7 : 6;
      const candidates: Array<{ x: number; y: number }> = [];

      for (let y = gap / 2; y < sampleSize; y += gap) {
        for (let x = gap / 2; x < sampleSize; x += gap) {
          const alpha = pixels[(Math.floor(y) * sampleSize + Math.floor(x)) * 4 + 3];
          if (alpha > 72) candidates.push({ x, y });
        }
      }

      const cap = logoCap();
      const count = Math.min(cap, candidates.length);
      const centerX = isMobile() ? width * 0.5 : width * 0.255;
      const centerY = isMobile() ? Math.min(185, height * 0.23) : height * 0.53;
      const originX = centerX - sampleSize / 2;
      const originY = centerY - sampleSize / 2;
      const stride = candidates.length / Math.max(count, 1);

      for (let index = 0; index < count; index++) {
        const candidate = candidates[Math.floor(index * stride)];
        targets.push({ x: originX + candidate.x, y: originY + candidate.y, claimed: false });
      }
    };

    const createTargetParticle = (targetIndex: number, delayed = false): LogoParticle => {
      const target = targets[targetIndex];
      target.claimed = true;
      return {
        state: "target",
        targetIndex,
        x: target.x + randomBetween(-8, 8),
        y: target.y + randomBetween(-8, 8),
        vx: 0,
        vy: 0,
        startX: target.x,
        startY: target.y,
        controlX: target.x,
        controlY: target.y,
        age: delayed ? randomBetween(-1.2, 2.2) : randomBetween(0.5, 3),
        duration: 1.8,
        radius: randomBetween(1.2, 2.15),
        opacity: randomBetween(0.56, 0.9),
        scale: 1,
        seed: Math.random() * Math.PI * 2
      };
    };

    const createIncomingParticle = (targetIndex: number): LogoParticle => {
      const target = targets[targetIndex];
      target.claimed = true;
      const angle = Math.random() * Math.PI * 2;
      const distance = randomBetween(100, isMobile() ? 190 : 350);
      const startX = target.x + Math.cos(angle) * distance;
      const startY = target.y + Math.sin(angle) * distance;
      const curve = randomBetween(-75, 75);
      return {
        state: "incoming",
        targetIndex,
        x: startX,
        y: startY,
        vx: 0,
        vy: 0,
        startX,
        startY,
        controlX: (startX + target.x) / 2 - Math.sin(angle) * curve,
        controlY: (startY + target.y) / 2 + Math.cos(angle) * curve,
        age: 0,
        duration: randomBetween(2.5, 7),
        radius: randomBetween(1.15, 1.95),
        opacity: randomBetween(0.48, 0.78),
        scale: randomBetween(0.15, 0.4),
        seed: Math.random() * Math.PI * 2
      };
    };

    const rebuildParticles = () => {
      particles.length = 0;
      const initialIncoming = isReducedMotion() ? 0 : Math.min(freeCap(), Math.floor(targets.length * 0.18));
      const incomingIndexes = new Set<number>();
      while (incomingIndexes.size < initialIncoming) incomingIndexes.add(Math.floor(Math.random() * targets.length));

      targets.forEach((target, index) => {
        target.claimed = false;
        particles.push(incomingIndexes.has(index)
          ? createIncomingParticle(index)
          : createTargetParticle(index, !isReducedMotion()));
      });
    };

    const createShard = (fragment = false, parent?: CrystalShard): CrystalShard => {
      if (parent) {
        return {
          x: parent.x + randomBetween(-8, 8),
          y: parent.y + randomBetween(-8, 8),
          vx: parent.vx + randomBetween(-22, 22),
          vy: parent.vy + randomBetween(-14, 8),
          size: parent.size * randomBetween(0.24, 0.42),
          scale: 1,
          opacity: parent.opacity * 0.8,
          rotation: parent.rotation + randomBetween(-0.6, 0.6),
          angularVelocity: randomBetween(-0.11, 0.11),
          age: 0,
          duration: randomBetween(3.5, 6.5),
          fragment: true,
          split: true,
          tone: parent.tone
        };
      }

      return {
        x: randomBetween(-40, width * 0.56),
        y: randomBetween(height * 0.62, height * 1.08),
        vx: randomBetween(7, 27),
        vy: randomBetween(-40, -17),
        size: randomBetween(fragment ? 18 : 38, fragment ? 42 : 118),
        scale: randomBetween(0.7, 1.4),
        opacity: randomBetween(0.22, 0.48),
        rotation: randomBetween(-0.8, 0.8),
        angularVelocity: randomBetween(-0.055, 0.055),
        age: 0,
        duration: randomBetween(12, 24),
        fragment,
        split: fragment,
        tone: Math.random()
      };
    };

    const rebuildShards = () => {
      shards.length = 0;
      const count = isReducedMotion() ? Math.min(5, shardCap()) : Math.max(4, shardCap() - 3);
      for (let index = 0; index < count; index++) {
        const shard = createShard();
        shard.age = isReducedMotion() ? shard.duration * randomBetween(0.2, 0.58) : shard.duration * Math.random();
        shard.x += shard.vx * shard.age;
        shard.y += shard.vy * shard.age;
        shard.rotation += shard.angularVelocity * shard.age;
        shards.push(shard);
      }
    };

    const rebuild = () => {
      if (!logoImage.complete || !logoImage.naturalWidth) return;
      const rect = host.getBoundingClientRect();
      width = rect.width;
      height = rect.height;
      const dpr = Math.min(window.devicePixelRatio || 1, MaxDpr);
      canvas.width = Math.max(1, Math.floor(width * dpr));
      canvas.height = Math.max(1, Math.floor(height * dpr));
      canvas.style.width = `${width}px`;
      canvas.style.height = `${height}px`;
      context.setTransform(dpr, 0, 0, dpr, 0, 0);
      updateSafeZone(rect);
      sampleLogoTargets();
      rebuildParticles();
      rebuildShards();
      draw();
      if (!isReducedMotion()) requestFrame();
    };

    const applyRepulsion = (particle: LogoParticle, delta: number) => {
      if (!pointer.active || !canRepel()) return;
      const dx = particle.x - pointer.x;
      const dy = particle.y - pointer.y;
      const distance = Math.hypot(dx, dy);
      if (distance <= 0.01 || distance >= RepulsionRadius) return;
      const force = Math.pow(1 - distance / RepulsionRadius, 2) * 0.34 * delta * 60;
      particle.vx += (dx / distance) * force;
      particle.vy += (dy / distance) * force;
    };

    const updateParticles = (delta: number, now: number) => {
      let freeCount = particles.reduce((count, particle) => count + (particle.state === "target" ? 0 : 1), 0);
      for (let index = particles.length - 1; index >= 0; index--) {
        const particle = particles[index];
        particle.age += delta;

        if (particle.state === "incoming") {
          const target = targets[particle.targetIndex];
          const progress = Math.min(1, particle.age / particle.duration);
          const eased = progress * progress * (3 - 2 * progress);
          const inverse = 1 - eased;
          particle.x = inverse * inverse * particle.startX + 2 * inverse * eased * particle.controlX + eased * eased * target.x;
          particle.y = inverse * inverse * particle.startY + 2 * inverse * eased * particle.controlY + eased * eased * target.y;
          particle.x += Math.sin(now * 0.0011 + particle.seed) * (1 - eased) * 3;
          particle.y += Math.cos(now * 0.0009 + particle.seed) * (1 - eased) * 3;
          particle.scale = 0.2 + eased * 0.8;
          applyRepulsion(particle, delta);
          particle.x += particle.vx;
          particle.y += particle.vy;
          particle.vx *= 0.84;
          particle.vy *= 0.84;
          if (progress >= 1) {
            particle.state = "target";
            particle.age = 1;
            particle.x = target.x;
            particle.y = target.y;
            particle.vx = 0;
            particle.vy = 0;
            particle.scale = 1;
          }
          continue;
        }

        if (particle.state === "detached") {
          particle.x += particle.vx * delta;
          particle.y += particle.vy * delta;
          particle.vx *= Math.pow(0.985, delta * 60);
          particle.vy *= Math.pow(0.985, delta * 60);
          particle.scale = Math.max(0, 1 - particle.age / particle.duration);
          if (particle.age >= particle.duration) {
            particles.splice(index, 1);
            freeCount--;
          }
          continue;
        }

        const target = targets[particle.targetIndex];
        if (!target) continue;
        const frameScale = delta * 60;
        particle.vx += (target.x - particle.x) * 0.045 * frameScale;
        particle.vy += (target.y - particle.y) * 0.045 * frameScale;
        applyRepulsion(particle, delta);
        const damping = Math.pow(0.86, frameScale);
        particle.vx *= damping;
        particle.vy *= damping;
        particle.x += particle.vx * frameScale;
        particle.y += particle.vy * frameScale;

        if (particle.age > 3 && freeCount < freeCap() && Math.random() < delta * 0.008) {
          target.claimed = false;
          particle.state = "detached";
          particle.age = 0;
          particle.duration = randomBetween(3.5, 6);
          const angle = Math.atan2(particle.y - height * 0.52, particle.x - width * 0.255) + randomBetween(-0.45, 0.45);
          const speed = randomBetween(7, 18);
          particle.vx = Math.cos(angle) * speed;
          particle.vy = Math.sin(angle) * speed;
          freeCount++;
        }
      }

      incomingAccumulator += delta;
      if (incomingAccumulator >= 0.18 && freeCount < freeCap()) {
        incomingAccumulator = 0;
        const targetIndex = targets.findIndex((target) => !target.claimed);
        if (targetIndex >= 0) particles.push(createIncomingParticle(targetIndex));
      }
    };

    const updateShards = (delta: number) => {
      let fragments = shards.filter((shard) => shard.fragment).length;
      for (let index = shards.length - 1; index >= 0; index--) {
        const shard = shards[index];
        shard.age += delta;
        shard.x += shard.vx * delta;
        shard.y += shard.vy * delta;
        shard.rotation += shard.angularVelocity * delta;
        const progress = shard.age / shard.duration;

        if (!shard.fragment && !shard.split && progress > randomBetween(0.67, 0.76) && fragments < fragmentCap()) {
          shard.split = true;
          const count = Math.min(randomBetween(2, 3.99) | 0, fragmentCap() - fragments);
          for (let part = 0; part < count; part++) shards.push(createShard(true, shard));
          fragments += count;
          shard.duration = shard.age + 0.7;
        }

        if (progress >= 1 || shard.y < -180 || shard.x > width + 180) shards.splice(index, 1);
      }

      shardAccumulator += delta;
      if (shardAccumulator > 1.15 && shards.filter((shard) => !shard.fragment).length < shardCap()) {
        shardAccumulator = 0;
        shards.push(createShard());
      }
    };

    const drawParticle = (particle: LogoParticle) => {
      const intro = particle.state === "target"
        ? Math.min(1, Math.max(0, particle.age / 1.8))
        : particle.state === "incoming"
          ? Math.min(1, particle.age / Math.max(particle.duration * 0.18, 0.01))
          : 1;
      const fade = particle.state === "detached" ? Math.max(0, 1 - particle.age / particle.duration) : 1;
      const inSafeZone = safeZone
        && particle.x > safeZone.left && particle.x < safeZone.right
        && particle.y > safeZone.top && particle.y < safeZone.bottom;
      const alpha = particle.opacity * intro * fade * (inSafeZone ? 0.16 : 1);
      if (alpha <= 0.01) return;
      context.globalAlpha = alpha;
      context.fillStyle = particle.seed > 6.05 ? "#8584d8" : particle.seed > 3.9 ? "#aab5ca" : "#77859f";
      context.beginPath();
      context.arc(particle.x, particle.y, particle.radius * particle.scale, 0, Math.PI * 2);
      context.fill();
    };

    const drawShard = (shard: CrystalShard) => {
      const progress = Math.min(1, shard.age / shard.duration);
      const fadeIn = Math.min(1, progress / 0.14);
      const fadeOut = Math.min(1, (1 - progress) / 0.34);
      const lifecycleScale = progress < 0.2
        ? 0.72 + progress * 1.4
        : 1 - Math.pow((progress - 0.2) / 0.8, 1.15) * 0.8;
      let safeMultiplier = 1;
      if (safeZone && shard.x > safeZone.left && shard.x < safeZone.right && shard.y > safeZone.top && shard.y < safeZone.bottom) {
        safeMultiplier = 0.16;
      }

      context.save();
      context.translate(shard.x, shard.y);
      context.rotate(shard.rotation);
      context.scale(shard.scale * lifecycleScale, shard.scale * lifecycleScale);
      context.globalAlpha = shard.opacity * fadeIn * fadeOut * safeMultiplier;
      const gradient = context.createLinearGradient(-shard.size * 0.4, shard.size, shard.size * 0.3, -shard.size);
      gradient.addColorStop(0, shard.tone > 0.82 ? "#222b46" : "#111726");
      gradient.addColorStop(0.55, "#182139");
      gradient.addColorStop(1, "#34415f");
      context.fillStyle = gradient;
      context.beginPath();
      context.moveTo(0, -shard.size);
      context.lineTo(shard.size * 0.48, shard.size * 0.72);
      context.lineTo(shard.size * 0.04, shard.size * 0.45);
      context.lineTo(-shard.size * 0.42, shard.size * 0.76);
      context.closePath();
      context.fill();
      context.globalAlpha *= 0.68;
      context.fillStyle = "#5d6c8d";
      context.beginPath();
      context.moveTo(0, -shard.size);
      context.lineTo(shard.size * 0.04, shard.size * 0.45);
      context.lineTo(-shard.size * 0.42, shard.size * 0.76);
      context.closePath();
      context.fill();
      context.restore();
    };

    const draw = () => {
      context.clearRect(0, 0, width, height);
      shards.forEach(drawShard);
      particles.forEach(drawParticle);
      context.globalAlpha = 1;
    };

    const tick = (now: number) => {
      frame = 0;
      if (!visible || isReducedMotion()) return;
      const delta = Math.min((now - lastTime) / 1000 || 0.016, 0.04);
      lastTime = now;
      updateParticles(delta, now);
      updateShards(delta);
      draw();
      requestFrame();
    };

    const requestFrame = () => {
      if (!frame && visible && !isReducedMotion()) frame = window.requestAnimationFrame(tick);
    };

    const handlePointerMove = (event: PointerEvent) => {
      if (!canRepel() || event.pointerType === "touch") return;
      const rect = canvas.getBoundingClientRect();
      pointer.x = event.clientX - rect.left;
      pointer.y = event.clientY - rect.top;
      pointer.active = true;
    };

    const handlePointerLeave = () => {
      pointer.active = false;
      pointer.x = -10_000;
      pointer.y = -10_000;
    };

    const handleVisibility = () => {
      visible = !document.hidden;
      if (!visible && frame) {
        window.cancelAnimationFrame(frame);
        frame = 0;
      } else if (visible) {
        lastTime = performance.now();
        requestFrame();
      }
    };

    const handleMotionChange = () => {
      if (frame) window.cancelAnimationFrame(frame);
      frame = 0;
      pointer.active = false;
      rebuild();
    };

    const resizeObserver = new ResizeObserver(() => {
      window.clearTimeout(resizeTimer);
      resizeTimer = window.setTimeout(rebuild, 120);
    });

    logoImage.addEventListener("load", rebuild, { once: true });
    logoImage.src = LogoAssetPath;
    if (logoImage.complete && logoImage.naturalWidth) rebuild();
    host.addEventListener("pointermove", handlePointerMove, { passive: true });
    host.addEventListener("pointerleave", handlePointerLeave);
    document.addEventListener("visibilitychange", handleVisibility);
    reducedMotionQuery.addEventListener("change", handleMotionChange);
    mobileQuery.addEventListener("change", handleMotionChange);
    resizeObserver.observe(host);

    return () => {
      if (frame) window.cancelAnimationFrame(frame);
      window.clearTimeout(resizeTimer);
      logoImage.removeEventListener("load", rebuild);
      host.removeEventListener("pointermove", handlePointerMove);
      host.removeEventListener("pointerleave", handlePointerLeave);
      document.removeEventListener("visibilitychange", handleVisibility);
      reducedMotionQuery.removeEventListener("change", handleMotionChange);
      mobileQuery.removeEventListener("change", handleMotionChange);
      resizeObserver.disconnect();
    };
  }, []);

  return <canvas ref={canvasRef} className="auth-studio-particle-field" aria-hidden="true" />;
}

function randomBetween(min: number, max: number) {
  return min + Math.random() * (max - min);
}
