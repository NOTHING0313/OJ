import { useCallback, useLayoutEffect, useRef } from "react";

export function useRankMovementAnimation(keys: string[]) {
  const nodesRef = useRef(new Map<string, HTMLElement>());
  const previousTopsRef = useRef(new Map<string, number>());
  const keySignature = keys.join("|");

  const capturePositions = useCallback(() => {
    const positions = new Map<string, number>();
    nodesRef.current.forEach((node, key) => positions.set(key, node.getBoundingClientRect().top));
    previousTopsRef.current = positions;
  }, []);

  const setRowNode = useCallback((key: string, node: HTMLElement | null) => {
    if (node) {
      nodesRef.current.set(key, node);
    } else {
      nodesRef.current.delete(key);
    }
  }, []);

  useLayoutEffect(() => {
    const previous = previousTopsRef.current;
    const current = new Map<string, number>();
    nodesRef.current.forEach((node, key) => current.set(key, node.getBoundingClientRect().top));

    if (previous.size > 0) {
      nodesRef.current.forEach((node, key) => {
        if (typeof node.animate !== "function") {
          return;
        }

        const oldTop = previous.get(key);
        const newTop = current.get(key);
        if (newTop === undefined) {
          return;
        }

        if (oldTop === undefined) {
          node.animate(
            [
              { opacity: 0, transform: "translateY(12px)" },
              { opacity: 1, transform: "translateY(0)" }
            ],
            { duration: 360, easing: "cubic-bezier(0.22, 1, 0.36, 1)" }
          );
          return;
        }

        const deltaY = oldTop - newTop;
        if (Math.abs(deltaY) < 1) {
          return;
        }

        node.animate(
          [
            { transform: `translateY(${deltaY}px)` },
            { transform: "translateY(0)" }
          ],
          { duration: 560, easing: "cubic-bezier(0.22, 1, 0.36, 1)" }
        );
      });
    }

    previousTopsRef.current = current;
  }, [keySignature]);

  return { capturePositions, setRowNode };
}
